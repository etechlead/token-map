namespace Clever.TokenMap.Core.Settings;

public interface IFolderSettingsStore
{
    FolderSettings Load(string rootPath);

    void Save(string rootPath, FolderSettings settings);
}
