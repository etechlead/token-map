using Clever.TokenMap.Infrastructure.Settings;

namespace Clever.TokenMap.App.Services;

public interface IThemeService
{
    ThemePreference CurrentSystemTheme { get; }

    void ApplyThemePreference(ThemePreference themePreference);
}
