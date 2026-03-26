using System;
using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Logging;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Paths;
using Clever.TokenMap.Core.Settings;

namespace Clever.TokenMap.App.Services;

public sealed class SettingsCoordinator : ISettingsCoordinator
{
    private static readonly TimeSpan DefaultDebounceDelay = TimeSpan.FromMilliseconds(250);

    private readonly AppSettingsSession _appSettingsSession;
    private readonly FolderSettingsSession _folderSettingsSession;

    public SettingsCoordinator(
        IAppSettingsStore appSettingsStore,
        IFolderSettingsStore folderSettingsStore,
        IThemeService themeService,
        AppSettings? initialSettings = null,
        IAppLogger? logger = null,
        TimeSpan? debounceDelay = null,
        PathNormalizer? pathNormalizer = null)
    {
        var effectiveLogger = logger ?? NullAppLogger.Instance;
        var effectiveDebounceDelay = debounceDelay ?? DefaultDebounceDelay;
        var effectivePathNormalizer = pathNormalizer ?? new PathNormalizer();

        _appSettingsSession = new AppSettingsSession(
            appSettingsStore,
            themeService,
            initialSettings,
            effectiveLogger,
            effectiveDebounceDelay);
        _folderSettingsSession = new FolderSettingsSession(
            folderSettingsStore,
            effectivePathNormalizer,
            effectiveLogger,
            effectiveDebounceDelay);
    }

    public SettingsState State => _appSettingsSession.State;

    public CurrentFolderSettingsState CurrentFolderState => _folderSettingsSession.CurrentFolderState;

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        await _appSettingsSession.FlushAsync(cancellationToken).ConfigureAwait(false);
        await _folderSettingsSession.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public ScanOptions Resolve(string? rootPath, ScanOptions baseOptions) =>
        _folderSettingsSession.Resolve(rootPath, baseOptions);

    public void SwitchActiveFolder(string? rootPath) =>
        _folderSettingsSession.SwitchActiveFolder(rootPath);
}
