using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Infrastructure.Logging;
using Clever.TokenMap.Infrastructure.Settings;

namespace Clever.TokenMap.App.Services;

public sealed class SettingsCoordinator : ISettingsCoordinator
{
    private static readonly TimeSpan DefaultDebounceDelay = TimeSpan.FromMilliseconds(250);

    private readonly IAppSettingsStore _appSettingsStore;
    private readonly IThemeService _themeService;
    private readonly IAppLogger _logger;
    private readonly TimeSpan _debounceDelay;
    private readonly Lock _syncLock = new();

    private ToolbarViewModel? _toolbar;
    private AppSettings _currentSettings = AppSettings.CreateDefault();
    private CancellationTokenSource? _saveDebounceCancellationTokenSource;
    private Task? _pendingSaveTask;
    private bool _isApplyingSettings;
    private long _settingsVersion;
    private long _lastSavedVersion;

    public SettingsCoordinator(
        IAppSettingsStore appSettingsStore,
        IThemeService themeService,
        IAppLogger? logger = null,
        TimeSpan? debounceDelay = null)
    {
        _appSettingsStore = appSettingsStore;
        _themeService = themeService;
        _logger = logger ?? NullAppLogger.Instance;
        _debounceDelay = debounceDelay ?? DefaultDebounceDelay;
    }

    public void Attach(ToolbarViewModel toolbar)
    {
        ArgumentNullException.ThrowIfNull(toolbar);

        if (_toolbar is not null)
        {
            _toolbar.PropertyChanged -= ToolbarOnPropertyChanged;
        }

        _toolbar = toolbar;
        _currentSettings = _appSettingsStore.Load();

        _isApplyingSettings = true;
        try
        {
            _toolbar.ApplyAnalysisSettings(_currentSettings.Analysis);
            _toolbar.ApplyAppearanceSettings(_currentSettings.Appearance);
            _themeService.ApplyThemePreference(_currentSettings.Appearance.ThemePreference);
        }
        finally
        {
            _isApplyingSettings = false;
        }

        _toolbar.PropertyChanged += ToolbarOnPropertyChanged;
    }

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

    private void ToolbarOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isApplyingSettings || _toolbar is null || !IsPersistedToolbarProperty(e.PropertyName))
        {
            return;
        }

        lock (_syncLock)
        {
            _currentSettings.Analysis = _toolbar.BuildAnalysisSettings();
            _currentSettings.Appearance = _toolbar.BuildAppearanceSettings();
            _settingsVersion++;
        }

        _themeService.ApplyThemePreference(_toolbar.SelectedThemePreference);
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

    private static bool IsPersistedToolbarProperty(string? propertyName) =>
        propertyName is nameof(ToolbarViewModel.SelectedMetric) or
        nameof(ToolbarViewModel.SelectedTokenProfile) or
        nameof(ToolbarViewModel.RespectGitIgnore) or
        nameof(ToolbarViewModel.RespectIgnore) or
        nameof(ToolbarViewModel.UseDefaultExcludes) or
        nameof(ToolbarViewModel.SelectedThemePreference);
}
