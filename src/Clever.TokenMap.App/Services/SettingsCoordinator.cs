using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Logging;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Paths;
using Clever.TokenMap.Core.Settings;

namespace Clever.TokenMap.App.Services;

public sealed class SettingsCoordinator : ISettingsCoordinator
{
    private static readonly TimeSpan DefaultDebounceDelay = TimeSpan.FromMilliseconds(250);

    private readonly IAppSettingsStore _appSettingsStore;
    private readonly IFolderSettingsStore _folderSettingsStore;
    private readonly IThemeService _themeService;
    private readonly IAppLogger _logger;
    private readonly TimeSpan _debounceDelay;
    private readonly IPathNormalizer _pathNormalizer;
    private readonly Lock _syncLock = new();
    private readonly Lock _folderSyncLock = new();

    private AppSettings _currentSettings = AppSettings.CreateDefault();
    private FolderSettings _currentFolderSettings = FolderSettings.CreateDefault();
    private CancellationTokenSource? _saveDebounceCancellationTokenSource;
    private CancellationTokenSource? _folderSaveDebounceCancellationTokenSource;
    private Task? _pendingSaveTask;
    private Task? _pendingFolderSaveTask;
    private Task _appPersistenceTask = Task.CompletedTask;
    private Task _folderPersistenceTask = Task.CompletedTask;
    private bool _isApplyingSettings;
    private bool _isApplyingFolderSettings;
    private long _settingsVersion;
    private long _folderSettingsVersion;
    private long _lastSavedVersion;
    private long _lastSavedFolderSettingsVersion;

    public SettingsCoordinator(
        IAppSettingsStore appSettingsStore,
        IFolderSettingsStore folderSettingsStore,
        IThemeService themeService,
        AppSettings? initialSettings = null,
        IAppLogger? logger = null,
        TimeSpan? debounceDelay = null,
        IPathNormalizer? pathNormalizer = null)
    {
        _appSettingsStore = appSettingsStore;
        _folderSettingsStore = folderSettingsStore;
        _themeService = themeService;
        _logger = logger ?? NullAppLogger.Instance;
        _debounceDelay = debounceDelay ?? DefaultDebounceDelay;
        _pathNormalizer = pathNormalizer ?? new PathNormalizer();
        State = new SettingsState();
        CurrentFolderState = new CurrentFolderSettingsState();
        State.PropertyChanged += StateOnPropertyChanged;
        State.RecentFolderPathsChanged += StateRecentFolderPathsOnCollectionChanged;
        CurrentFolderState.PropertyChanged += CurrentFolderStateOnPropertyChanged;

        LoadSettings(initialSettings ?? _appSettingsStore.Load());
        CurrentFolderState.Reset();
    }

    public SettingsState State { get; }

    public CurrentFolderSettingsState CurrentFolderState { get; }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        Task? pendingAppSaveTask;
        Task? pendingFolderSaveTask;
        CancellationTokenSource? appSaveDebounceCancellationTokenSource;
        CancellationTokenSource? folderSaveDebounceCancellationTokenSource;
        long appVersionToSave;
        long folderVersionToSave;

        lock (_syncLock)
        {
            pendingAppSaveTask = _pendingSaveTask;
            _pendingSaveTask = null;
            appSaveDebounceCancellationTokenSource = _saveDebounceCancellationTokenSource;
            _saveDebounceCancellationTokenSource = null;
            appVersionToSave = _settingsVersion;
        }

        lock (_folderSyncLock)
        {
            pendingFolderSaveTask = _pendingFolderSaveTask;
            _pendingFolderSaveTask = null;
            folderSaveDebounceCancellationTokenSource = _folderSaveDebounceCancellationTokenSource;
            _folderSaveDebounceCancellationTokenSource = null;
            folderVersionToSave = _folderSettingsVersion;
        }

        appSaveDebounceCancellationTokenSource?.Cancel();
        appSaveDebounceCancellationTokenSource?.Dispose();
        folderSaveDebounceCancellationTokenSource?.Cancel();
        folderSaveDebounceCancellationTokenSource?.Dispose();

        if (pendingAppSaveTask is not null)
        {
            try
            {
                await pendingAppSaveTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }
        }

        if (pendingFolderSaveTask is not null)
        {
            try
            {
                await pendingFolderSaveTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }
        }

        await SaveAppSettingsIfNeededAsync(appVersionToSave).ConfigureAwait(false);
        await SaveFolderSettingsIfNeededAsync(folderVersionToSave).ConfigureAwait(false);
    }

    public ScanOptions Resolve(string? rootPath, ScanOptions baseOptions)
    {
        ArgumentNullException.ThrowIfNull(baseOptions);

        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return new ScanOptions
            {
                RespectGitIgnore = baseOptions.RespectGitIgnore,
                UseGlobalExcludes = baseOptions.UseGlobalExcludes,
                GlobalExcludes = [.. baseOptions.GlobalExcludes],
                UseFolderExcludes = false,
                FolderExcludes = [],
            };
        }

        FolderSettings? folderSettings = null;
        var normalizedRootPath = _pathNormalizer.NormalizeRootPath(rootPath);

        lock (_folderSyncLock)
        {
            if (CurrentFolderState.HasActiveFolder &&
                CurrentFolderState.ActiveRootPath is { } activeRootPath &&
                _pathNormalizer.PathComparer.Equals(activeRootPath, normalizedRootPath))
            {
                folderSettings = _currentFolderSettings.Clone();
            }
        }

        folderSettings ??= _folderSettingsStore.Load(normalizedRootPath);

        return new ScanOptions
        {
            RespectGitIgnore = baseOptions.RespectGitIgnore,
            UseGlobalExcludes = baseOptions.UseGlobalExcludes,
            GlobalExcludes = [.. baseOptions.GlobalExcludes],
            UseFolderExcludes = folderSettings.Scan.UseFolderExcludes,
            FolderExcludes = [.. folderSettings.Scan.FolderExcludes],
        };
    }

    public void SwitchActiveFolder(string? rootPath)
    {
        var normalizedRootPath = string.IsNullOrWhiteSpace(rootPath)
            ? null
            : _pathNormalizer.NormalizeRootPath(rootPath);

        string? previousRootPath;
        long folderVersionToSave;

        lock (_folderSyncLock)
        {
            previousRootPath = string.IsNullOrWhiteSpace(_currentFolderSettings.RootPath)
                ? null
                : _currentFolderSettings.RootPath;
            folderVersionToSave = _folderSettingsVersion;
        }

        if (_pathNormalizer.PathComparer.Equals(previousRootPath, normalizedRootPath))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(previousRootPath) &&
            !_pathNormalizer.PathComparer.Equals(previousRootPath, normalizedRootPath))
        {
            CancelPendingFolderSave();
            QueueImmediateFolderSave(folderVersionToSave);
        }

        if (string.IsNullOrWhiteSpace(normalizedRootPath))
        {
            LoadFolderSettings(null, FolderSettings.CreateDefault());
            return;
        }

        LoadFolderSettings(normalizedRootPath, _folderSettingsStore.Load(normalizedRootPath));
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
            _currentSettings.Analysis.UseGlobalExcludes = State.UseGlobalExcludes;
            _currentSettings.Analysis.GlobalExcludes = [.. State.GlobalExcludes];
            _currentSettings.Appearance.ThemePreference = State.SelectedThemePreference;
            _currentSettings.Appearance.TreemapPalette = NormalizeTreemapPalette(State.SelectedTreemapPalette);
            _settingsVersion++;
        }

        _themeService.ApplyThemePreference(State.SelectedThemePreference);
        ScheduleSave();
    }

    private void CurrentFolderStateOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isApplyingFolderSettings || !IsPersistedFolderStateProperty(e.PropertyName))
        {
            return;
        }

        lock (_folderSyncLock)
        {
            _currentFolderSettings.Scan.UseFolderExcludes = CurrentFolderState.UseFolderExcludes;
            _currentFolderSettings.Scan.FolderExcludes = [.. CurrentFolderState.FolderExcludes];
            _folderSettingsVersion++;
        }

        ScheduleFolderSave();
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

    private void ScheduleFolderSave()
    {
        if (!CurrentFolderState.HasActiveFolder)
        {
            return;
        }

        CancellationTokenSource cancellationTokenSource;
        long versionToSave;

        lock (_folderSyncLock)
        {
            _folderSaveDebounceCancellationTokenSource?.Cancel();
            _folderSaveDebounceCancellationTokenSource?.Dispose();

            cancellationTokenSource = new CancellationTokenSource();
            _folderSaveDebounceCancellationTokenSource = cancellationTokenSource;
            versionToSave = _folderSettingsVersion;
            _pendingFolderSaveTask = SaveFolderAfterDelayAsync(versionToSave, cancellationTokenSource.Token);
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

        await SaveAppSettingsIfNeededAsync(versionToSave).ConfigureAwait(false);
    }

    private async Task SaveFolderAfterDelayAsync(long versionToSave, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_debounceDelay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await SaveFolderSettingsIfNeededAsync(versionToSave).ConfigureAwait(false);
    }

    private Task SaveAppSettingsIfNeededAsync(long versionToSave)
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
            queuedPersistenceTask = QueuePersistenceTask(
                _appPersistenceTask,
                () =>
                {
                    _appSettingsStore.Save(snapshot);
                    _logger.LogDebug("Persisted updated app settings to settings.json.");
                });
            _appPersistenceTask = queuedPersistenceTask;
        }

        return queuedPersistenceTask;
    }

    private Task SaveFolderSettingsIfNeededAsync(long versionToSave)
    {
        FolderSettings? snapshot = null;
        string? rootPath = null;
        Task queuedPersistenceTask;

        lock (_folderSyncLock)
        {
            if (versionToSave <= _lastSavedFolderSettingsVersion || versionToSave != _folderSettingsVersion)
            {
                return Task.CompletedTask;
            }

            if (string.IsNullOrWhiteSpace(_currentFolderSettings.RootPath))
            {
                _lastSavedFolderSettingsVersion = versionToSave;
                return Task.CompletedTask;
            }

            snapshot = _currentFolderSettings.Clone();
            rootPath = snapshot.RootPath;
            _lastSavedFolderSettingsVersion = versionToSave;
            queuedPersistenceTask = QueuePersistenceTask(
                _folderPersistenceTask,
                () =>
                {
                    _folderSettingsStore.Save(rootPath, snapshot);
                    _logger.LogDebug($"Persisted updated folder settings for '{rootPath}'.");
                });
            _folderPersistenceTask = queuedPersistenceTask;
        }

        return queuedPersistenceTask;
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
            State.UseGlobalExcludes = _currentSettings.Analysis.UseGlobalExcludes;
            State.LoadGlobalExcludes(_currentSettings.Analysis.GlobalExcludes);
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

    private void LoadFolderSettings(string? normalizedRootPath, FolderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalizedSettings = settings.Clone();
        normalizedSettings.RootPath = string.IsNullOrWhiteSpace(normalizedRootPath)
            ? string.Empty
            : _pathNormalizer.NormalizeRootPath(normalizedRootPath);
        normalizedSettings.Scan = FolderScanSettings.Normalize(normalizedSettings.Scan);

        lock (_folderSyncLock)
        {
            _currentFolderSettings = normalizedSettings;
            _folderSettingsVersion = 0;
            _lastSavedFolderSettingsVersion = 0;
        }

        _isApplyingFolderSettings = true;
        try
        {
            if (string.IsNullOrWhiteSpace(normalizedSettings.RootPath))
            {
                CurrentFolderState.Reset();
            }
            else
            {
                CurrentFolderState.Load(
                    normalizedSettings.RootPath,
                    normalizedSettings.Scan.UseFolderExcludes,
                    normalizedSettings.Scan.FolderExcludes);
            }
        }
        finally
        {
            _isApplyingFolderSettings = false;
        }
    }

    private static bool IsPersistedStateProperty(string? propertyName) =>
        propertyName is nameof(SettingsState.SelectedMetric) or
        nameof(SettingsState.RespectGitIgnore) or
        nameof(SettingsState.UseGlobalExcludes) or
        nameof(SettingsState.GlobalExcludes) or
        nameof(SettingsState.SelectedThemePreference) or
        nameof(SettingsState.SelectedTreemapPalette);

    private static bool IsPersistedFolderStateProperty(string? propertyName) =>
        propertyName is nameof(CurrentFolderSettingsState.UseFolderExcludes) or
        nameof(CurrentFolderSettingsState.FolderExcludes);

    private static AnalysisMetric NormalizeAnalysisMetric(AnalysisMetric selectedMetric) =>
        selectedMetric == AnalysisMetric.NonEmptyLines
            ? AnalysisMetric.TotalLines
            : selectedMetric;

    private static TreemapPalette NormalizeTreemapPalette(TreemapPalette selectedPalette) =>
        Enum.IsDefined(selectedPalette)
            ? selectedPalette
            : TreemapPalette.Weighted;

    private void CancelPendingFolderSave()
    {
        CancellationTokenSource? saveDebounceCancellationTokenSource;

        lock (_folderSyncLock)
        {
            saveDebounceCancellationTokenSource = _folderSaveDebounceCancellationTokenSource;
            _folderSaveDebounceCancellationTokenSource = null;
            _pendingFolderSaveTask = null;
        }

        saveDebounceCancellationTokenSource?.Cancel();
        saveDebounceCancellationTokenSource?.Dispose();
    }

    private void QueueImmediateFolderSave(long versionToSave)
    {
        var saveTask = SaveFolderSettingsIfNeededAsync(versionToSave);

        lock (_folderSyncLock)
        {
            _pendingFolderSaveTask = saveTask;
        }
    }

    private static async Task QueuePersistenceTask(Task previousTask, Action persistenceAction)
    {
        ArgumentNullException.ThrowIfNull(previousTask);
        ArgumentNullException.ThrowIfNull(persistenceAction);

        try
        {
            await previousTask.ConfigureAwait(false);
        }
        catch
        {
        }

        try
        {
            await Task.Run(persistenceAction).ConfigureAwait(false);
        }
        catch
        {
            throw;
        }
    }
}
