namespace Clever.TokenMap.Core.Interfaces;

public interface IAppStoragePaths
{
    string GetSettingsFilePath();

    string GetFolderSettingsRootPath();

    string GetLogsDirectoryPath();
}
