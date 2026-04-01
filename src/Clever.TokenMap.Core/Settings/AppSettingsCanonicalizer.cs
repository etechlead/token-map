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

        settings.Analysis.SelectedMetric = DefaultMetricCatalog.NormalizeMetricId(settings.Analysis.SelectedMetric);
        settings.Appearance.TreemapPalette = NormalizeTreemapPalette(settings.Appearance.TreemapPalette);
        settings.Analysis.GlobalExcludes = [.. GlobalExcludeList.Normalize(settings.Analysis.GlobalExcludes)];
        settings.RecentFolderPaths = NormalizeRecentFolderPaths(settings.RecentFolderPaths);
        return settings;
    }

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

    private static string? NormalizeFolderPath(string? path) =>
        string.IsNullOrWhiteSpace(path)
            ? null
            : path.Trim();
}
