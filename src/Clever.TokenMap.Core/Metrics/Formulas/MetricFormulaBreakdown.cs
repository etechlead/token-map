namespace Clever.TokenMap.Core.Metrics.Formulas;

public sealed record MetricFormulaBreakdown(
    double TotalPoints,
    IReadOnlyList<MetricComponentContribution> Components);
