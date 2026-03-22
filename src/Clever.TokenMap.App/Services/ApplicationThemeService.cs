using System;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Styling;
using Clever.TokenMap.Infrastructure.Settings;

namespace Clever.TokenMap.App.Services;

public sealed class ApplicationThemeService(Application application) : IThemeService
{
    private readonly Application _application = application;

    public string CurrentSystemTheme => GetCurrentSystemTheme();

    public void ApplyThemePreference(string themePreference)
    {
        _application.RequestedThemeVariant = NormalizeThemePreference(themePreference) switch
        {
            ThemePreferences.Light => ThemeVariant.Light,
            ThemePreferences.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }

    private static string NormalizeThemePreference(string value)
    {
        if (string.Equals(value, ThemePreferences.Light, StringComparison.OrdinalIgnoreCase))
        {
            return ThemePreferences.Light;
        }

        if (string.Equals(value, ThemePreferences.Dark, StringComparison.OrdinalIgnoreCase))
        {
            return ThemePreferences.Dark;
        }

        return ThemePreferences.System;
    }

    private string GetCurrentSystemTheme()
    {
        var platformSettings = _application.PlatformSettings;
        if (platformSettings is null)
        {
            return ThemePreferences.Light;
        }

        var colorValues = platformSettings.GetColorValues();
        return colorValues.ThemeVariant == PlatformThemeVariant.Dark
            ? ThemePreferences.Dark
            : ThemePreferences.Light;
    }
}
