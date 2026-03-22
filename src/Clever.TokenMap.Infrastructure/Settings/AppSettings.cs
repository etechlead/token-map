using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Infrastructure.Logging;

namespace Clever.TokenMap.Infrastructure.Settings;

public sealed class AppSettings
{
    public AnalysisSettings Analysis { get; set; } = new();

    public AppearanceSettings Appearance { get; set; } = new();

    public LoggingSettings Logging { get; set; } = new();

    public static AppSettings CreateDefault() => new();

    public AppSettings Clone() =>
        new()
        {
            Analysis = Analysis.Clone(),
            Appearance = Appearance.Clone(),
            Logging = Logging.Clone(),
        };
}

public sealed class AnalysisSettings
{
    public AnalysisMetric SelectedMetric { get; set; } = AnalysisMetric.Tokens;

    public TokenProfile SelectedTokenProfile { get; set; } = TokenProfile.O200KBase;

    public bool RespectGitIgnore { get; set; } = true;

    public bool RespectIgnore { get; set; } = true;

    public bool UseDefaultExcludes { get; set; } = true;

    public AnalysisSettings Clone() =>
        new()
        {
            SelectedMetric = SelectedMetric,
            SelectedTokenProfile = SelectedTokenProfile,
            RespectGitIgnore = RespectGitIgnore,
            RespectIgnore = RespectIgnore,
            UseDefaultExcludes = UseDefaultExcludes,
        };
}

public sealed class AppearanceSettings
{
    public ThemePreference ThemePreference { get; set; } = ThemePreference.System;

    public AppearanceSettings Clone() =>
        new()
        {
            ThemePreference = ThemePreference,
        };
}

public sealed class LoggingSettings
{
    public AppLogLevel MinLevel { get; set; } = GetDefaultMinimumLevel();

    public LoggingSettings Clone() =>
        new()
        {
            MinLevel = MinLevel,
        };

    private static AppLogLevel GetDefaultMinimumLevel()
    {
#if DEBUG
        return AppLogLevel.Debug;
#else
        return AppLogLevel.Warning;
#endif
    }
}
