using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Logging;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.Core.Settings;

public sealed class AppSettings
{
    public AnalysisSettings Analysis { get; set; } = new();

    public AppearanceSettings Appearance { get; set; } = new();

    public LocalizationSettings Localization { get; set; } = new();

    public PromptingSettings Prompting { get; set; } = new();

    public LoggingSettings Logging { get; set; } = new();

    public List<string> RecentFolderPaths { get; set; } = [];

    public static AppSettings CreateDefault() => new();

    public AppSettings Clone() =>
        new()
        {
            Analysis = Analysis.Clone(),
            Appearance = Appearance.Clone(),
            Localization = Localization.Clone(),
            Prompting = Prompting.Clone(),
            Logging = Logging.Clone(),
            RecentFolderPaths = [.. RecentFolderPaths],
        };
}

public sealed class AnalysisSettings
{
    public MetricId SelectedMetric { get; set; } = MetricIds.Tokens;

    public List<MetricId> VisibleMetricIds { get; set; } = [.. DefaultMetricCatalog.GetDefaultVisibleMetricIds()];

    public bool RespectGitIgnore { get; set; } = true;

    public bool UseGlobalExcludes { get; set; } = true;

    public List<string> GlobalExcludes { get; set; } = [.. GlobalExcludeDefaults.DefaultEntries];

    public AnalysisSettings Clone() =>
        new()
        {
            SelectedMetric = SelectedMetric,
            VisibleMetricIds = [.. VisibleMetricIds],
            RespectGitIgnore = RespectGitIgnore,
            UseGlobalExcludes = UseGlobalExcludes,
            GlobalExcludes = [.. GlobalExcludes],
        };
}

public sealed class AppearanceSettings
{
    public ThemePreference ThemePreference { get; set; } = ThemePreference.System;

    public WorkspaceLayoutMode WorkspaceLayoutMode { get; set; } = WorkspaceLayoutMode.SideBySide;

    public TreemapPalette TreemapPalette { get; set; } = TreemapPalette.Weighted;

    public bool ShowTreemapMetricValues { get; set; } = true;

    public AppearanceSettings Clone() =>
        new()
        {
            ThemePreference = ThemePreference,
            WorkspaceLayoutMode = WorkspaceLayoutMode,
            TreemapPalette = TreemapPalette,
            ShowTreemapMetricValues = ShowTreemapMetricValues,
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

public sealed class LocalizationSettings
{
    public string ApplicationLanguageTag { get; set; } = ApplicationLanguageTags.System;

    public LocalizationSettings Clone() =>
        new()
        {
            ApplicationLanguageTag = ApplicationLanguageTag,
        };
}

public sealed class PromptingSettings
{
    public string SelectedPromptLanguageTag { get; set; } = ApplicationLanguageTags.Default;

    public Dictionary<string, string> RefactorPromptTemplatesByLanguage { get; set; } = [];

    public PromptingSettings Clone() =>
        new()
        {
            SelectedPromptLanguageTag = SelectedPromptLanguageTag,
            RefactorPromptTemplatesByLanguage = RefactorPromptTemplatesByLanguage.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value,
                StringComparer.OrdinalIgnoreCase),
        };
}
