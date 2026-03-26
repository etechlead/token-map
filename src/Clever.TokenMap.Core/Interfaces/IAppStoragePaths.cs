namespace Clever.TokenMap.Core.Interfaces;

public interface IAppStoragePaths
{
    string GetAppDataRootPath();

    string GetSettingsFilePath();

    string GetFolderSettingsRootPath();

    string GetFolderSettingsFilePath(string rootPath);

    string GetLogsDirectoryPath();
}
