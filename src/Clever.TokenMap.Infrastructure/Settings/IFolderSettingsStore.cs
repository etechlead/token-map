namespace Clever.TokenMap.Infrastructure.Settings;

public interface IFolderSettingsStore
{
    FolderSettings Load(string rootPath);

    void Save(string rootPath, FolderSettings settings);
}
