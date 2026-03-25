using Clever.TokenMap.Core.Settings;

namespace Clever.TokenMap.Infrastructure.Settings;

public interface IAppSettingsStore
{
    AppSettings Load();

    void Save(AppSettings settings);
}
