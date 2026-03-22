namespace Clever.TokenMap.Infrastructure.Settings;

public sealed class AppSettings
{
    public AnalysisSettings Analysis { get; set; } = new();

    public AppearanceSettings Appearance { get; set; } = new();

    public LoggingSettings Logging { get; set; } = new();

    public static AppSettings CreateDefault() => new();
}

public static class ThemePreferences
{
    public const string System = "System";

    public const string Light = "Light";

    public const string Dark = "Dark";
}

public sealed class AnalysisSettings
{
    public string SelectedMetric { get; set; } = "Tokens";

    public string SelectedTokenProfile { get; set; } = "o200k_base";

    public bool RespectGitIgnore { get; set; } = true;

    public bool RespectIgnore { get; set; } = true;

    public bool UseDefaultExcludes { get; set; } = true;
}

public sealed class AppearanceSettings
{
    public string ThemePreference { get; set; } = ThemePreferences.System;
}

public sealed class LoggingSettings
{
    public string MinLevel { get; set; } = GetDefaultMinimumLevel();

    private static string GetDefaultMinimumLevel()
    {
#if DEBUG
        return "Debug";
#else
        return "Warning";
#endif
    }
}
