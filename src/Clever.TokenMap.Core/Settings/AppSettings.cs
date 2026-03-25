using System.Collections.Generic;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Core.Settings;

public sealed class AppSettings
{
    public AnalysisSettings Analysis { get; set; } = new();

    public AppearanceSettings Appearance { get; set; } = new();

    public LoggingSettings Logging { get; set; } = new();

    public List<string> RecentFolderPaths { get; set; } = [];

    public static AppSettings CreateDefault() => new();

    public AppSettings Clone() =>
        new()
        {
            Analysis = Analysis.Clone(),
            Appearance = Appearance.Clone(),
            Logging = Logging.Clone(),
            RecentFolderPaths = [.. RecentFolderPaths],
        };
}

public sealed class AnalysisSettings
{
    public AnalysisMetric SelectedMetric { get; set; } = AnalysisMetric.Tokens;

    public bool RespectGitIgnore { get; set; } = true;

    public bool UseGlobalExcludes { get; set; } = true;

    public List<string> GlobalExcludes { get; set; } = [.. GlobalExcludeDefaults.DefaultEntries];

    public AnalysisSettings Clone() =>
        new()
        {
            SelectedMetric = SelectedMetric,
            RespectGitIgnore = RespectGitIgnore,
            UseGlobalExcludes = UseGlobalExcludes,
            GlobalExcludes = [.. GlobalExcludes],
        };
}

public sealed class AppearanceSettings
{
    public ThemePreference ThemePreference { get; set; } = ThemePreference.System;

    public TreemapPalette TreemapPalette { get; set; } = TreemapPalette.Weighted;

    public AppearanceSettings Clone() =>
        new()
        {
            ThemePreference = ThemePreference,
            TreemapPalette = TreemapPalette,
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
