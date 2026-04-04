using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Diagnostics;
using Clever.TokenMap.Core.Logging;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Core.Settings;

namespace Clever.TokenMap.App.Services;

internal sealed class AppSettingsSession
{
    private readonly IAppSettingsStore _appSettingsStore;
    private readonly IThemeService _themeService;
    private readonly IAppLogger _logger;
    private readonly TimeSpan _debounceDelay;
    private readonly Lock _syncLock = new();

    private AppSettings _currentSettings = AppSettings.CreateDefault();
    private CancellationTokenSource? _saveDebounceCancellationTokenSource;
    private Task? _pendingSaveTask;
    private Task _persistenceTask = Task.CompletedTask;
    private bool _isApplyingSettings;
    private long _settingsVersion;
    private long _lastSavedVersion;

    public AppSettingsSession(
        IAppSettingsStore appSettingsStore,
        IThemeService themeService,
        AppSettings? initialSettings,
        IAppLogger logger,
        TimeSpan debounceDelay)
    {
        _appSettingsStore = appSettingsStore;
        _themeService = themeService;
        _logger = logger;
        _debounceDelay = debounceDelay;
        State = new SettingsState();
        State.PropertyChanged += StateOnPropertyChanged;
        State.RecentFolderPathsChanged += StateRecentFolderPathsOnCollectionChanged;
        if (initialSettings is not null)
        {
            LoadSettings(initialSettings);
            return;
        }

        LoadSettings(_appSettingsStore.Load());
    }

    public SettingsState State { get; }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        Task? pendingSaveTask;
        CancellationTokenSource? saveDebounceCancellationTokenSource;
        long versionToSave;

        lock (_syncLock)
        {
            pendingSaveTask = _pendingSaveTask;
            _pendingSaveTask = null;
            saveDebounceCancellationTokenSource = _saveDebounceCancellationTokenSource;
            _saveDebounceCancellationTokenSource = null;
            versionToSave = _settingsVersion;
        }

        saveDebounceCancellationTokenSource?.Cancel();
        saveDebounceCancellationTokenSource?.Dispose();

        if (pendingSaveTask is not null)
        {
            try
            {
                await pendingSaveTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }
        }

        await SaveIfNeededAsync(versionToSave).ConfigureAwait(false);
    }

    private void StateOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isApplyingSettings || !IsPersistedStateProperty(e.PropertyName))
        {
            return;
        }

        lock (_syncLock)
        {
            _currentSettings.Analysis.SelectedMetric = DefaultMetricCatalog.NormalizeMetricId(State.SelectedMetric);
            _currentSettings.Analysis.VisibleMetricIds = [.. State.VisibleMetricIds];
            _currentSettings.Analysis.RespectGitIgnore = State.RespectGitIgnore;
            _currentSettings.Analysis.UseGlobalExcludes = State.UseGlobalExcludes;
            _currentSettings.Analysis.GlobalExcludes = [.. State.GlobalExcludes];
            _currentSettings.Appearance.ThemePreference = State.SelectedThemePreference;
            _currentSettings.Appearance.WorkspaceLayoutMode = State.WorkspaceLayoutMode;
            _currentSettings.Appearance.TreemapPalette = State.SelectedTreemapPalette;
            _currentSettings.Appearance.ShowTreemapMetricValues = State.ShowTreemapMetricValues;
            _settingsVersion++;
        }

        _themeService.ApplyThemePreference(State.SelectedThemePreference);
        ScheduleSave();
    }

    private void StateRecentFolderPathsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_isApplyingSettings)
        {
            return;
        }

        lock (_syncLock)
        {
            _currentSettings.RecentFolderPaths = State.RecentFolderPaths.ToList();
            _settingsVersion++;
        }

        ScheduleSave();
    }

    private void ScheduleSave()
    {
        CancellationTokenSource cancellationTokenSource;
        long versionToSave;

        lock (_syncLock)
        {
            _saveDebounceCancellationTokenSource?.Cancel();
            _saveDebounceCancellationTokenSource?.Dispose();

            cancellationTokenSource = new CancellationTokenSource();
            _saveDebounceCancellationTokenSource = cancellationTokenSource;
            versionToSave = _settingsVersion;
            _pendingSaveTask = SaveAfterDelayAsync(versionToSave, cancellationTokenSource.Token);
        }
    }

    private async Task SaveAfterDelayAsync(long versionToSave, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_debounceDelay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await SaveIfNeededAsync(versionToSave).ConfigureAwait(false);
    }

    private Task SaveIfNeededAsync(long versionToSave)
    {
        AppSettings? snapshot = null;
        Task queuedPersistenceTask;

        lock (_syncLock)
        {
            if (versionToSave <= _lastSavedVersion || versionToSave != _settingsVersion)
            {
                return Task.CompletedTask;
            }

            snapshot = _currentSettings.Clone();
            _lastSavedVersion = versionToSave;
            queuedPersistenceTask = SettingsPersistenceQueue.QueueAsync(
                _persistenceTask,
                () =>
                {
                    _appSettingsStore.Save(snapshot);
                    _logger.LogDebug(
                        "Persisted updated app settings.",
                        eventCode: "settings.app.persisted",
                        context: AppIssueContext.Create(("SettingsVersion", versionToSave)));
                });
            _persistenceTask = queuedPersistenceTask;
        }

        return queuedPersistenceTask;
    }

    private void LoadSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _currentSettings = AppSettingsCanonicalizer.Normalize(settings.Clone());

        _isApplyingSettings = true;
        try
        {
            State.ReplaceVisibleMetricIds(_currentSettings.Analysis.VisibleMetricIds);
            State.SelectedMetric = _currentSettings.Analysis.SelectedMetric;
            State.RespectGitIgnore = _currentSettings.Analysis.RespectGitIgnore;
            State.UseGlobalExcludes = _currentSettings.Analysis.UseGlobalExcludes;
            State.LoadGlobalExcludes(_currentSettings.Analysis.GlobalExcludes);
            State.SelectedThemePreference = _currentSettings.Appearance.ThemePreference;
            State.WorkspaceLayoutMode = _currentSettings.Appearance.WorkspaceLayoutMode;
            State.SelectedTreemapPalette = _currentSettings.Appearance.TreemapPalette;
            State.ShowTreemapMetricValues = _currentSettings.Appearance.ShowTreemapMetricValues;
            State.ReplaceRecentFolderPaths(_currentSettings.RecentFolderPaths);
            _themeService.ApplyThemePreference(State.SelectedThemePreference);
        }
        finally
        {
            _isApplyingSettings = false;
        }
    }

    private static bool IsPersistedStateProperty(string? propertyName) =>
        propertyName is nameof(SettingsState.SelectedMetric) or
        nameof(SettingsState.VisibleMetricIds) or
        nameof(SettingsState.RespectGitIgnore) or
        nameof(SettingsState.UseGlobalExcludes) or
        nameof(SettingsState.GlobalExcludes) or
        nameof(SettingsState.SelectedThemePreference) or
        nameof(SettingsState.WorkspaceLayoutMode) or
        nameof(SettingsState.SelectedTreemapPalette) or
        nameof(SettingsState.ShowTreemapMetricValues);

}
