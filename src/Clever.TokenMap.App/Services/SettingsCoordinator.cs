using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Infrastructure.Logging;
using Clever.TokenMap.Infrastructure.Settings;
using Clever.TokenMap.Core.Enums;

namespace Clever.TokenMap.App.Services;

public sealed class SettingsCoordinator : ISettingsCoordinator
{
    private static readonly TimeSpan DefaultDebounceDelay = TimeSpan.FromMilliseconds(250);

    private readonly IAppSettingsStore _appSettingsStore;
    private readonly IThemeService _themeService;
    private readonly IAppLogger _logger;
    private readonly TimeSpan _debounceDelay;
    private readonly Lock _syncLock = new();

    private AppSettings _currentSettings = AppSettings.CreateDefault();
    private CancellationTokenSource? _saveDebounceCancellationTokenSource;
    private Task? _pendingSaveTask;
    private bool _isApplyingSettings;
    private long _settingsVersion;
    private long _lastSavedVersion;

    public SettingsCoordinator(
        IAppSettingsStore appSettingsStore,
        IThemeService themeService,
        AppSettings? initialSettings = null,
        IAppLogger? logger = null,
        TimeSpan? debounceDelay = null)
    {
        _appSettingsStore = appSettingsStore;
        _themeService = themeService;
        _logger = logger ?? NullAppLogger.Instance;
        _debounceDelay = debounceDelay ?? DefaultDebounceDelay;
        State = new SettingsState();
        State.PropertyChanged += StateOnPropertyChanged;
        State.RecentFolderPathsChanged += StateRecentFolderPathsOnCollectionChanged;

        LoadSettings(initialSettings ?? _appSettingsStore.Load());
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
                await pendingSaveTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }
        }

        SaveIfNeeded(versionToSave);
    }

    private void StateOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isApplyingSettings || !IsPersistedStateProperty(e.PropertyName))
        {
            return;
        }

        lock (_syncLock)
        {
            _currentSettings.Analysis.SelectedMetric = NormalizeAnalysisMetric(State.SelectedMetric);
            _currentSettings.Analysis.RespectGitIgnore = State.RespectGitIgnore;
            _currentSettings.Analysis.UseDefaultExcludes = State.UseDefaultExcludes;
            _currentSettings.Appearance.ThemePreference = State.SelectedThemePreference;
            _currentSettings.Appearance.TreemapPalette = NormalizeTreemapPalette(State.SelectedTreemapPalette);
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
            await Task.Delay(_debounceDelay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        SaveIfNeeded(versionToSave);
    }

    private void SaveIfNeeded(long versionToSave)
    {
        AppSettings? snapshot = null;

        lock (_syncLock)
        {
            if (versionToSave <= _lastSavedVersion || versionToSave != _settingsVersion)
            {
                return;
            }

            snapshot = _currentSettings.Clone();
            _lastSavedVersion = versionToSave;
        }

        _appSettingsStore.Save(snapshot);
        _logger.LogDebug("Persisted updated app settings to settings.json.");
    }

    private void LoadSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _currentSettings = settings.Clone();
        _currentSettings.Analysis.SelectedMetric = NormalizeAnalysisMetric(_currentSettings.Analysis.SelectedMetric);
        _currentSettings.Appearance.TreemapPalette = NormalizeTreemapPalette(_currentSettings.Appearance.TreemapPalette);

        _isApplyingSettings = true;
        try
        {
            State.SelectedMetric = _currentSettings.Analysis.SelectedMetric;
            State.RespectGitIgnore = _currentSettings.Analysis.RespectGitIgnore;
            State.UseDefaultExcludes = _currentSettings.Analysis.UseDefaultExcludes;
            State.SelectedThemePreference = _currentSettings.Appearance.ThemePreference;
            State.SelectedTreemapPalette = _currentSettings.Appearance.TreemapPalette;
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
        nameof(SettingsState.RespectGitIgnore) or
        nameof(SettingsState.UseDefaultExcludes) or
        nameof(SettingsState.SelectedThemePreference) or
        nameof(SettingsState.SelectedTreemapPalette);

    private static AnalysisMetric NormalizeAnalysisMetric(AnalysisMetric selectedMetric) =>
        selectedMetric == AnalysisMetric.NonEmptyLines
            ? AnalysisMetric.TotalLines
            : selectedMetric;

    private static TreemapPalette NormalizeTreemapPalette(TreemapPalette selectedPalette) =>
        Enum.IsDefined(selectedPalette)
            ? selectedPalette
            : TreemapPalette.Weighted;
}
