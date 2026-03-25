using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Logging;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Paths;
using Clever.TokenMap.Core.Settings;

namespace Clever.TokenMap.App.Services;

internal sealed class FolderSettingsSession
{
    private readonly IFolderSettingsStore _folderSettingsStore;
    private readonly IAppLogger _logger;
    private readonly TimeSpan _debounceDelay;
    private readonly IPathNormalizer _pathNormalizer;
    private readonly Lock _syncLock = new();

    private FolderSettings _currentFolderSettings = FolderSettings.CreateDefault();
    private CancellationTokenSource? _saveDebounceCancellationTokenSource;
    private Task? _pendingSaveTask;
    private Task _persistenceTask = Task.CompletedTask;
    private bool _isApplyingSettings;
    private long _settingsVersion;
    private long _lastSavedVersion;

    public FolderSettingsSession(
        IFolderSettingsStore folderSettingsStore,
        IPathNormalizer pathNormalizer,
        IAppLogger logger,
        TimeSpan debounceDelay)
    {
        _folderSettingsStore = folderSettingsStore;
        _pathNormalizer = pathNormalizer;
        _logger = logger;
        _debounceDelay = debounceDelay;
        CurrentFolderState = new CurrentFolderSettingsState();
        CurrentFolderState.PropertyChanged += CurrentFolderStateOnPropertyChanged;
        CurrentFolderState.Reset();
    }

    public CurrentFolderSettingsState CurrentFolderState { get; }

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

        lock (_syncLock)
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
        long versionToSave;

        lock (_syncLock)
        {
            previousRootPath = string.IsNullOrWhiteSpace(_currentFolderSettings.RootPath)
                ? null
                : _currentFolderSettings.RootPath;
            versionToSave = _settingsVersion;
        }

        if (_pathNormalizer.PathComparer.Equals(previousRootPath, normalizedRootPath))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(previousRootPath) &&
            !_pathNormalizer.PathComparer.Equals(previousRootPath, normalizedRootPath))
        {
            CancelPendingSave();
            QueueImmediateSave(versionToSave);
        }

        if (string.IsNullOrWhiteSpace(normalizedRootPath))
        {
            LoadFolderSettings(null, FolderSettings.CreateDefault());
            return;
        }

        LoadFolderSettings(normalizedRootPath, _folderSettingsStore.Load(normalizedRootPath));
    }

    private void CurrentFolderStateOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isApplyingSettings || !IsPersistedFolderStateProperty(e.PropertyName))
        {
            return;
        }

        lock (_syncLock)
        {
            _currentFolderSettings.Scan.UseFolderExcludes = CurrentFolderState.UseFolderExcludes;
            _currentFolderSettings.Scan.FolderExcludes = [.. CurrentFolderState.FolderExcludes];
            _settingsVersion++;
        }

        ScheduleSave();
    }

    private void ScheduleSave()
    {
        if (!CurrentFolderState.HasActiveFolder)
        {
            return;
        }

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
        FolderSettings? snapshot = null;
        string? rootPath = null;
        Task queuedPersistenceTask;

        lock (_syncLock)
        {
            if (versionToSave <= _lastSavedVersion || versionToSave != _settingsVersion)
            {
                return Task.CompletedTask;
            }

            if (string.IsNullOrWhiteSpace(_currentFolderSettings.RootPath))
            {
                _lastSavedVersion = versionToSave;
                return Task.CompletedTask;
            }

            snapshot = _currentFolderSettings.Clone();
            rootPath = snapshot.RootPath;
            _lastSavedVersion = versionToSave;
            queuedPersistenceTask = SettingsPersistenceQueue.QueueAsync(
                _persistenceTask,
                () =>
                {
                    _folderSettingsStore.Save(rootPath, snapshot);
                    _logger.LogDebug($"Persisted updated folder settings for '{rootPath}'.");
                });
            _persistenceTask = queuedPersistenceTask;
        }

        return queuedPersistenceTask;
    }

    private void LoadFolderSettings(string? normalizedRootPath, FolderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalizedSettings = settings.Clone();
        normalizedSettings.RootPath = string.IsNullOrWhiteSpace(normalizedRootPath)
            ? string.Empty
            : _pathNormalizer.NormalizeRootPath(normalizedRootPath);
        normalizedSettings.Scan = FolderScanSettings.Normalize(normalizedSettings.Scan);

        lock (_syncLock)
        {
            _currentFolderSettings = normalizedSettings;
            _settingsVersion = 0;
            _lastSavedVersion = 0;
        }

        _isApplyingSettings = true;
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
            _isApplyingSettings = false;
        }
    }

    private static bool IsPersistedFolderStateProperty(string? propertyName) =>
        propertyName is nameof(CurrentFolderSettingsState.UseFolderExcludes) or
        nameof(CurrentFolderSettingsState.FolderExcludes);

    private void CancelPendingSave()
    {
        CancellationTokenSource? saveDebounceCancellationTokenSource;

        lock (_syncLock)
        {
            saveDebounceCancellationTokenSource = _saveDebounceCancellationTokenSource;
            _saveDebounceCancellationTokenSource = null;
            _pendingSaveTask = null;
        }

        saveDebounceCancellationTokenSource?.Cancel();
        saveDebounceCancellationTokenSource?.Dispose();
    }

    private void QueueImmediateSave(long versionToSave)
    {
        var saveTask = SaveIfNeededAsync(versionToSave);

        lock (_syncLock)
        {
            _pendingSaveTask = saveTask;
        }
    }
}
