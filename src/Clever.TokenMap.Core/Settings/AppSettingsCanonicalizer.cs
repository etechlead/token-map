using System.Globalization;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Core.Paths;

namespace Clever.TokenMap.Core.Settings;

public static class AppSettingsCanonicalizer
{
    private const int MaxRecentFolderCount = 10;

    public static AppSettings Normalize(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        settings.Analysis.VisibleMetricIds = NormalizeVisibleMetricIds(settings.Analysis.VisibleMetricIds);
        settings.Analysis.SelectedMetric = NormalizeSelectedMetric(
            settings.Analysis.SelectedMetric,
            settings.Analysis.VisibleMetricIds);
        settings.Appearance.WorkspaceLayoutMode = NormalizeWorkspaceLayoutMode(settings.Appearance.WorkspaceLayoutMode);
        settings.Appearance.TreemapPalette = NormalizeTreemapPalette(settings.Appearance.TreemapPalette);
        settings.Localization.ApplicationLanguageTag =
            NormalizeApplicationLanguageTag(settings.Localization.ApplicationLanguageTag);
        settings.Analysis.GlobalExcludes = [.. GlobalExcludeList.Normalize(settings.Analysis.GlobalExcludes)];
        settings.Prompting.SelectedPromptLanguageTag =
            NormalizePromptLanguageTag(settings.Prompting.SelectedPromptLanguageTag)
            ?? NormalizePromptLanguageTag(settings.Localization.ApplicationLanguageTag)
            ?? ApplicationLanguageTags.Default;
        settings.Prompting.RefactorPromptTemplatesByLanguage =
            NormalizeRefactorPromptTemplatesByLanguage(settings.Prompting.RefactorPromptTemplatesByLanguage);
        settings.RecentFolderPaths = NormalizeRecentFolderPaths(settings.RecentFolderPaths);
        return settings;
    }

    public static WorkspaceLayoutMode NormalizeWorkspaceLayoutMode(WorkspaceLayoutMode mode) =>
        Enum.IsDefined(mode)
            ? mode
            : WorkspaceLayoutMode.SideBySide;

    public static TreemapPalette NormalizeTreemapPalette(TreemapPalette palette) =>
        Enum.IsDefined(palette)
            ? palette
            : TreemapPalette.Weighted;

    public static string NormalizeApplicationLanguageTag(string? languageTag) =>
        ApplicationLanguageTags.Normalize(languageTag);

    public static string? NormalizePromptLanguageTag(string? languageTag)
    {
        if (string.IsNullOrWhiteSpace(languageTag))
        {
            return null;
        }

        try
        {
            return CultureInfo.GetCultureInfo(languageTag.Trim()).Name;
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    public static List<string> NormalizeRecentFolderPaths(IEnumerable<string?> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var uniquePaths = new HashSet<string>(PathComparison.Comparer);
        var normalizedPaths = new List<string>();

        foreach (var path in paths)
        {
            if (NormalizeFolderPath(path) is not { } normalizedPath)
            {
                continue;
            }

            if (!uniquePaths.Add(normalizedPath))
            {
                continue;
            }

            normalizedPaths.Add(normalizedPath);

            if (normalizedPaths.Count >= MaxRecentFolderCount)
            {
                break;
            }
        }

        return normalizedPaths;
    }

    public static List<MetricId> NormalizeVisibleMetricIds(IEnumerable<MetricId>? metricIds)
    {
        var requestedIds = metricIds?
            .Select(metricId => DefaultMetricCatalog.NormalizeMetricId(metricId))
            .ToHashSet()
            ?? [];
        var visibleMetricIds = DefaultMetricCatalog
            .GetUserVisibleDefinitions()
            .Where(definition => requestedIds.Contains(definition.Id))
            .Select(definition => definition.Id)
            .ToList();

        if (visibleMetricIds.Count > 0)
        {
            return visibleMetricIds;
        }

        visibleMetricIds = [.. DefaultMetricCatalog.GetDefaultVisibleMetricIds()];
        if (visibleMetricIds.Count > 0)
        {
            return visibleMetricIds;
        }

        return [.. DefaultMetricCatalog.GetAllUserVisibleMetricIds().Take(1)];
    }

    public static Dictionary<string, string> NormalizeRefactorPromptTemplatesByLanguage(
        IReadOnlyDictionary<string, string>? templatesByLanguage)
    {
        var normalizedTemplates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (templatesByLanguage is null)
        {
            return normalizedTemplates;
        }

        foreach (var pair in templatesByLanguage)
        {
            var normalizedTag = NormalizePromptLanguageTag(pair.Key);
            if (normalizedTag is null)
            {
                continue;
            }

            normalizedTemplates[normalizedTag] = NormalizePromptTemplateText(pair.Value);
        }

        return normalizedTemplates;
    }

    private static MetricId NormalizeSelectedMetric(MetricId selectedMetric, List<MetricId> visibleMetricIds)
    {
        var normalizedMetric = DefaultMetricCatalog.NormalizeMetricId(selectedMetric);
        return visibleMetricIds.Contains(normalizedMetric)
            ? normalizedMetric
            : visibleMetricIds[0];
    }

    private static string? NormalizeFolderPath(string? path) =>
        string.IsNullOrWhiteSpace(path)
            ? null
            : path.Trim();

    private static string NormalizePromptTemplateText(string? template) =>
        string.IsNullOrWhiteSpace(template)
            ? string.Empty
            : template.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
}
