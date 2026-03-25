namespace Clever.TokenMap.Core.Settings;

public interface IAppSettingsStore
{
    AppSettings Load();

    void Save(AppSettings settings);
}
