namespace Clever.TokenMap.App.Services;

public interface IThemeService
{
    string CurrentSystemTheme { get; }

    void ApplyThemePreference(string themePreference);
}
