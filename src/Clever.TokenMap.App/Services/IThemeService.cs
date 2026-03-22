using Clever.TokenMap.Infrastructure.Settings;

namespace Clever.TokenMap.App.Services;

public interface IThemeService
{
    void ApplyThemePreference(ThemePreference themePreference);
}
