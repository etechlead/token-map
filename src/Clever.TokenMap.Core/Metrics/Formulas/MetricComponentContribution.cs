namespace Clever.TokenMap.Core.Metrics.Formulas;

public sealed record MetricComponentContribution(
    string Key,
    string Label,
    string Category,
    double RawValue,
    double? NormalizedValue,
    double Weight,
    double ContributionPoints);
