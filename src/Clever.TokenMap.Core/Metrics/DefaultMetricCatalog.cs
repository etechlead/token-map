namespace Clever.TokenMap.Core.Metrics;

public sealed class DefaultMetricCatalog : IMetricCatalog
{
    private static readonly IReadOnlyList<MetricDefinition> Definitions =
    [
        new(
            MetricIds.Tokens,
            "Tokens",
            "Tokens",
            MetricUnit.Count,
            MetricRollupKind.Sum,
            VisibleByDefault: true,
            SupportsTreemapWeight: true,
            "Token count for text files and directory rollups."),
        new(
            MetricIds.NonEmptyLines,
            "Non-empty lines",
            "Lines",
            MetricUnit.Count,
            MetricRollupKind.Sum,
            VisibleByDefault: true,
            SupportsTreemapWeight: true,
            "Non-empty line count for text files and directory rollups."),
        new(
            MetricIds.CodeLines,
            "Code lines",
            "Code",
            MetricUnit.Count,
            MetricRollupKind.Sum,
            VisibleByDefault: false,
            SupportsTreemapWeight: true,
            "Code line count for syntax-supported files and directory rollups."),
        new(
            MetricIds.FileSizeBytes,
            "File size",
            "Size",
            MetricUnit.Bytes,
            MetricRollupKind.Sum,
            VisibleByDefault: true,
            SupportsTreemapWeight: true,
            "File size in bytes and summed directory size."),
        new(
            MetricIds.CommentLines,
            "Comment lines",
            "Comments",
            MetricUnit.Count,
            MetricRollupKind.Sum,
            VisibleByDefault: false,
            SupportsTreemapWeight: true,
            "Comment-only line count for syntax-supported files and directory rollups."),
        new(
            MetricIds.FunctionCount,
            "Callable count",
            "Funcs",
            MetricUnit.Count,
            MetricRollupKind.Sum,
            VisibleByDefault: false,
            SupportsTreemapWeight: true,
            "Callable count for syntax-supported files and directory rollups."),
        new(
            MetricIds.TotalParameterCount,
            "Total parameter count",
            "Params",
            MetricUnit.Count,
            MetricRollupKind.Sum,
            VisibleByDefault: false,
            SupportsTreemapWeight: true,
            "Summed callable parameter count for syntax-supported files and directory rollups."),
        new(
            MetricIds.MaxParameterCount,
            "Max parameter count",
            "Param max",
            MetricUnit.Count,
            MetricRollupKind.Max,
            VisibleByDefault: false,
            SupportsTreemapWeight: false,
            "Maximum callable parameter count for syntax-supported files and directory rollups."),
        new(
            MetricIds.TypeCount,
            "Type count",
            "Types",
            MetricUnit.Count,
            MetricRollupKind.Sum,
            VisibleByDefault: false,
            SupportsTreemapWeight: true,
            "Named type declaration count for syntax-supported files and directory rollups."),
        new(
            MetricIds.CyclomaticComplexitySum,
            "Cyclomatic complexity sum",
            "CC sum",
            MetricUnit.Count,
            MetricRollupKind.Sum,
            VisibleByDefault: false,
            SupportsTreemapWeight: true,
            "Summed callable cyclomatic complexity for syntax-supported files and directory rollups."),
        new(
            MetricIds.CyclomaticComplexityMax,
            "Cyclomatic complexity max",
            "CC max",
            MetricUnit.Count,
            MetricRollupKind.Max,
            VisibleByDefault: false,
            SupportsTreemapWeight: false,
            "Maximum callable cyclomatic complexity for syntax-supported files and directory rollups."),
        new(
            MetricIds.MaxNestingDepth,
            "Max nesting depth",
            "Nest",
            MetricUnit.Count,
            MetricRollupKind.Max,
            VisibleByDefault: false,
            SupportsTreemapWeight: false,
            "Maximum callable nesting depth for syntax-supported files and directory rollups."),
        new(
            MetricIds.AverageParametersPerCallable,
            "Average parameters per callable",
            "Avg params",
            MetricUnit.Ratio,
            MetricRollupKind.None,
            VisibleByDefault: false,
            SupportsTreemapWeight: false,
            "Average callable parameter count for syntax-supported files."),
        new(
            MetricIds.AverageCyclomaticComplexityPerCallable,
            "Average cyclomatic complexity per callable",
            "Avg CC",
            MetricUnit.Ratio,
            MetricRollupKind.None,
            VisibleByDefault: false,
            SupportsTreemapWeight: false,
            "Average callable cyclomatic complexity for syntax-supported files."),
        new(
            MetricIds.CyclomaticComplexityPerCodeLine,
            "Cyclomatic complexity per code line",
            "CC/line",
            MetricUnit.Ratio,
            MetricRollupKind.None,
            VisibleByDefault: false,
            SupportsTreemapWeight: false,
            "Cyclomatic complexity sum divided by code lines for syntax-supported files."),
        new(
            MetricIds.CommentRatio,
            "Comment ratio",
            "Comment %",
            MetricUnit.Percent,
            MetricRollupKind.None,
            VisibleByDefault: false,
            SupportsTreemapWeight: false,
            "Comment-only lines divided by code plus comment lines for syntax-supported files."),
        new(
            MetricIds.MaxCallableLines,
            "Max callable lines",
            "Max lines",
            MetricUnit.Count,
            MetricRollupKind.Max,
            VisibleByDefault: false,
            SupportsTreemapWeight: false,
            "Maximum callable line span for syntax-supported files and directory rollups."),
        new(
            MetricIds.AverageCallableLines,
            "Average callable lines",
            "Avg lines",
            MetricUnit.AverageCount,
            MetricRollupKind.None,
            VisibleByDefault: false,
            SupportsTreemapWeight: false,
            "Average callable line span for syntax-supported files."),
        new(
            MetricIds.LongCallableCount,
            "Long callable count",
            "Long funcs",
            MetricUnit.Count,
            MetricRollupKind.Sum,
            VisibleByDefault: false,
            SupportsTreemapWeight: true,
            "Callable count meeting the long-callable threshold for files and summed directory rollups."),
        new(
            MetricIds.HighCyclomaticComplexityCallableCount,
            "High CC callable count",
            "High CC",
            MetricUnit.Count,
            MetricRollupKind.Sum,
            VisibleByDefault: true,
            SupportsTreemapWeight: true,
            "Callable count meeting the high cyclomatic complexity threshold for files and summed directory rollups."),
        new(
            MetricIds.DeepNestingCallableCount,
            "Deep nesting callable count",
            "Deep nest",
            MetricUnit.Count,
            MetricRollupKind.Sum,
            VisibleByDefault: false,
            SupportsTreemapWeight: true,
            "Callable count meeting the deep nesting threshold for files and summed directory rollups."),
        new(
            MetricIds.LongParameterListCount,
            "Long parameter list count",
            "Long params",
            MetricUnit.Count,
            MetricRollupKind.Sum,
            VisibleByDefault: false,
            SupportsTreemapWeight: false,
            "Callable count meeting the long parameter list threshold for files and summed directory rollups."),
        new(
            MetricIds.CallableHotspotPointsV0,
            "Callable hotspot points v0",
            "Hotspots",
            MetricUnit.Score,
            MetricRollupKind.Sum,
            VisibleByDefault: true,
            SupportsTreemapWeight: true,
            "Additive callable hotspot points for files and summed directory rollups."),
        new(
            MetricIds.ComplexityPointsV0,
            "Complexity points v0",
            "Complexity",
            MetricUnit.Score,
            MetricRollupKind.Sum,
            VisibleByDefault: false,
            SupportsTreemapWeight: true,
            "Composite additive complexity points for files and summed directory rollups.")
    ];

    private readonly Dictionary<MetricId, MetricDefinition> _definitionsById =
        Definitions.ToDictionary(definition => definition.Id);

    public static DefaultMetricCatalog Instance { get; } = new();

    public static MetricId NormalizeMetricId(MetricId metricId)
    {
        var rawValue = metricId.Value;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return MetricIds.Tokens;
        }

        var normalizedMetricId = new MetricId(rawValue.Trim().ToLowerInvariant());
        return Instance.TryGet(normalizedMetricId, out _)
            ? normalizedMetricId
            : MetricIds.Tokens;
    }

    public IReadOnlyList<MetricDefinition> GetAll() => Definitions;

    public bool TryGet(MetricId id, out MetricDefinition definition) =>
        _definitionsById.TryGetValue(id, out definition!);

    public static IReadOnlyList<MetricId> GetDefaultVisibleMetricIds() =>
        [.. Definitions
            .Where(definition => definition.VisibleByDefault)
            .Select(definition => definition.Id)];

    public static IReadOnlyList<MetricId> GetAllMetricIds() =>
        [.. Definitions.Select(definition => definition.Id)];
}
