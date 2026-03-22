using System;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Styling;
using Clever.TokenMap.Core.Enums;

namespace Clever.TokenMap.App.Services;

public sealed class ApplicationThemeService(Application application) : IThemeService
{
    private readonly Application _application = application;

    internal ThemePreference CurrentSystemTheme => GetCurrentSystemTheme();

    public void ApplyThemePreference(ThemePreference themePreference)
    {
        _application.RequestedThemeVariant = themePreference switch
        {
            ThemePreference.Light => ThemeVariant.Light,
            ThemePreference.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }

    private ThemePreference GetCurrentSystemTheme()
    {
        var platformSettings = _application.PlatformSettings;
        if (platformSettings is null)
        {
            return ThemePreference.Light;
        }

        var colorValues = platformSettings.GetColorValues();
        return colorValues.ThemeVariant == PlatformThemeVariant.Dark
            ? ThemePreference.Dark
            : ThemePreference.Light;
    }
}
