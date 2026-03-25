using Clever.TokenMap.Core.Paths;

namespace Clever.TokenMap.Infrastructure.Settings;

public static class TokenMapAppDataPaths
{
    public static string GetAppDataRootPath()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            localApplicationData = AppContext.BaseDirectory;
        }

        return Path.Combine(localApplicationData, "Clever", "TokenMap");
    }

    public static string GetSettingsFilePath() =>
        Path.Combine(GetAppDataRootPath(), "settings.json");

    public static string GetFolderSettingsRootPath() =>
        Path.Combine(GetAppDataRootPath(), "folders");

    public static string GetFolderSettingsFilePath(
        string rootPath,
        string? folderSettingsRootPath = null,
        PathNormalizer? pathNormalizer = null)
    {
        var normalizer = pathNormalizer ?? new PathNormalizer();
        var normalizedRootPath = normalizer.NormalizeRootPath(rootPath);
        var directoryName = FolderSettingsStorageKey.Build(normalizedRootPath, normalizer);
        var folderRoot = string.IsNullOrWhiteSpace(folderSettingsRootPath)
            ? GetFolderSettingsRootPath()
            : folderSettingsRootPath;

        return Path.Combine(folderRoot, directoryName, "settings.json");
    }

    public static string GetLogsDirectoryPath() =>
        Path.Combine(GetAppDataRootPath(), "logs");
}
