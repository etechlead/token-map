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
        settings.Analysis.GlobalExcludes = [.. GlobalExcludeList.Normalize(settings.Analysis.GlobalExcludes)];
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
        var visibleMetricIds = DefaultMetricCatalog.Instance
            .GetAll()
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

        return [.. DefaultMetricCatalog.GetAllMetricIds().Take(1)];
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
}
