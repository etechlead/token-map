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
            MetricIds.FileSizeBytes,
            "File size",
            "Size",
            MetricUnit.Bytes,
            MetricRollupKind.Sum,
            VisibleByDefault: true,
            SupportsTreemapWeight: true,
            "File size in bytes and summed directory size.")
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
}
