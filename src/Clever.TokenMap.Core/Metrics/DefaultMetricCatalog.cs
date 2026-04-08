namespace Clever.TokenMap.Core.Metrics;

public sealed class DefaultMetricCatalog : IMetricCatalog
{
    private static readonly HashSet<MetricId> UserHiddenMetricIds =
    [
        MetricIds.ComplexityPoints,
    ];

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
            MetricIds.FileSizeBytes,
            "File size",
            "Size",
            MetricUnit.Bytes,
            MetricRollupKind.Sum,
            VisibleByDefault: true,
            SupportsTreemapWeight: true,
            "File size in bytes and summed directory size."),
        new(
            MetricIds.ComplexityPoints,
            "Structural Risk",
            "Risk",
            MetricUnit.Score,
            MetricRollupKind.Sum,
            VisibleByDefault: false,
            SupportsTreemapWeight: true,
            "Continuous intrinsic structural-risk score based on file scale, callable burden, and risk distribution."),
        new(
            MetricIds.RefactorPriorityPoints,
            "Refactor Priority",
            "Refactor",
            MetricUnit.Score,
            MetricRollupKind.Sum,
            VisibleByDefault: true,
            SupportsTreemapWeight: true,
            "Refactoring priority score that combines structural risk with recent change and co-change pressure.")
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

        var normalizedRawValue = rawValue.Trim().ToLowerInvariant();
        var normalizedMetricId = new MetricId(normalizedRawValue);
        return Instance.TryGet(normalizedMetricId, out _)
            ? normalizedMetricId
            : MetricIds.Tokens;
    }

    public IReadOnlyList<MetricDefinition> GetAll() => Definitions;

    public bool TryGet(MetricId id, out MetricDefinition definition) =>
        _definitionsById.TryGetValue(id, out definition!);

    public static IReadOnlyList<MetricDefinition> GetUserVisibleDefinitions() =>
        [.. Definitions.Where(definition => IsUserVisible(definition.Id))];

    public static IReadOnlyList<MetricId> GetDefaultVisibleMetricIds() =>
        [.. Definitions
            .Where(definition => definition.VisibleByDefault && IsUserVisible(definition.Id))
            .Select(definition => definition.Id)];

    public static IReadOnlyList<MetricId> GetAllUserVisibleMetricIds() =>
        [.. Definitions
            .Where(definition => IsUserVisible(definition.Id))
            .Select(definition => definition.Id)];

    public static bool IsUserVisible(MetricId metricId) =>
        !UserHiddenMetricIds.Contains(metricId);
}
