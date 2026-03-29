using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Paths;

namespace Clever.TokenMap.Infrastructure.Settings;

public sealed class TokenMapAppDataPaths : IAppStoragePaths
{
    private readonly string? _appDataRootPath;
    private readonly PathNormalizer _pathNormalizer;

    public TokenMapAppDataPaths(string? appDataRootPath = null, PathNormalizer? pathNormalizer = null)
    {
        _appDataRootPath = appDataRootPath;
        _pathNormalizer = pathNormalizer ?? new PathNormalizer();
    }

    private string GetAppDataRootPath()
    {
        if (!string.IsNullOrWhiteSpace(_appDataRootPath))
        {
            return _appDataRootPath;
        }

        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            localApplicationData = AppContext.BaseDirectory;
        }

        return Path.Combine(localApplicationData, "Clever", "TokenMap");
    }

    public string GetSettingsFilePath() =>
        Path.Combine(GetAppDataRootPath(), "settings.json");

    public string GetFolderSettingsRootPath() =>
        Path.Combine(GetAppDataRootPath(), "folders");

    public string GetLogsDirectoryPath() =>
        Path.Combine(GetAppDataRootPath(), "logs");
}
