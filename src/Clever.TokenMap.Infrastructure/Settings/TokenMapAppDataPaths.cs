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

    public static string GetLogsDirectoryPath() =>
        Path.Combine(GetAppDataRootPath(), "logs");
}
