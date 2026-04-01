namespace Clever.TokenMap.Core.Metrics;

public sealed record MetricDefinition(
    MetricId Id,
    string DisplayName,
    string ShortName,
    MetricUnit Unit,
    MetricRollupKind DirectoryRollup,
    bool VisibleByDefault,
    bool SupportsTreemapWeight,
    string Description);
