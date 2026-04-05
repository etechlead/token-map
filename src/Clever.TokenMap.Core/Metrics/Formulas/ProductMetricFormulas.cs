namespace Clever.TokenMap.Core.Metrics.Formulas;

public static class ProductMetricFormulas
{
    public static bool TryComputeComplexity(MetricSet metrics, out MetricFormulaBreakdown breakdown)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        var codeLines = metrics.TryGetNumber(MetricIds.CodeLines);
        var cyclomaticComplexitySum = metrics.TryGetNumber(MetricIds.CyclomaticComplexitySum);
        var cyclomaticComplexityMax = metrics.TryGetNumber(MetricIds.CyclomaticComplexityMax);
        var maxNestingDepth = metrics.TryGetNumber(MetricIds.MaxNestingDepth);
        var maxParameterCount = metrics.TryGetNumber(MetricIds.MaxParameterCount);

        if (!codeLines.HasValue ||
            !cyclomaticComplexitySum.HasValue ||
            !cyclomaticComplexityMax.HasValue ||
            !maxNestingDepth.HasValue ||
            !maxParameterCount.HasValue)
        {
            breakdown = Empty;
            return false;
        }

        breakdown = CreateBreakdown(
            CreateNormalizedContribution("code_lines", "Code lines", "Structure", codeLines.Value, good: 20d, bad: 300d, weight: 0.20d),
            CreateNormalizedContribution("cc_sum", "Cyclomatic complexity sum", "Structure", cyclomaticComplexitySum.Value, good: 2d, bad: 40d, weight: 0.35d),
            CreateNormalizedContribution("cc_max", "Cyclomatic complexity max", "Structure", cyclomaticComplexityMax.Value, good: 2d, bad: 15d, weight: 0.20d),
            CreateNormalizedContribution("max_nesting_depth", "Max nesting depth", "Structure", maxNestingDepth.Value, good: 1d, bad: 6d, weight: 0.15d),
            CreateNormalizedContribution("max_parameter_count", "Max parameter count", "Structure", maxParameterCount.Value, good: 2d, bad: 8d, weight: 0.10d));
        return true;
    }

    public static bool TryComputeHotspots(MetricSet metrics, out MetricFormulaBreakdown breakdown)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        var longCallableCount = metrics.TryGetNumber(MetricIds.LongCallableCount);
        var highCyclomaticComplexityCallableCount = metrics.TryGetNumber(MetricIds.HighCyclomaticComplexityCallableCount);
        var deepNestingCallableCount = metrics.TryGetNumber(MetricIds.DeepNestingCallableCount);
        var longParameterListCount = metrics.TryGetNumber(MetricIds.LongParameterListCount);

        if (!longCallableCount.HasValue ||
            !highCyclomaticComplexityCallableCount.HasValue ||
            !deepNestingCallableCount.HasValue ||
            !longParameterListCount.HasValue)
        {
            breakdown = Empty;
            return false;
        }

        breakdown = CreateBreakdown(
            CreateWeightedCountContribution("long_callable_count", "Long callables", "Callable risk", longCallableCount.Value, weight: 2d),
            CreateWeightedCountContribution("high_cc_callable_count", "High complexity callables", "Callable risk", highCyclomaticComplexityCallableCount.Value, weight: 3d),
            CreateWeightedCountContribution("deep_nesting_callable_count", "Deep nesting callables", "Callable risk", deepNestingCallableCount.Value, weight: 2d),
            CreateWeightedCountContribution("long_parameter_list_count", "Long parameter lists", "Callable risk", longParameterListCount.Value, weight: 1d));
        return true;
    }

    public static bool TryComputeRefactorPriority(MetricSet metrics, out MetricFormulaBreakdown breakdown)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        var complexityPoints = metrics.TryGetNumber(MetricIds.ComplexityPoints);
        var callableHotspotPoints = metrics.TryGetNumber(MetricIds.CallableHotspotPoints);
        if (!complexityPoints.HasValue || !callableHotspotPoints.HasValue)
        {
            breakdown = Empty;
            return false;
        }

        if (!TryGetGitOperands(metrics, out var churnLines90d, out var touchCount90d, out var authorCount90d, out var uniqueCochangedFileCount90d, out var strongCochangedFileCount90d, out var averageCochangeSetSize90d))
        {
            breakdown = CreateBreakdown(
                CreateWeightedScoreContribution("complexity_points", "Complexity", "Intrinsic", complexityPoints.Value, weight: 0.80d),
                CreateNormalizedContribution("callable_hotspot_points", "Hotspots", "Intrinsic", callableHotspotPoints.Value, good: 0d, bad: 10d, weight: 0.20d));
            return true;
        }

        breakdown = CreateBreakdown(
            CreateWeightedScoreContribution("complexity_points", "Complexity", "Intrinsic", complexityPoints.Value, weight: 0.48d),
            CreateNormalizedContribution("callable_hotspot_points", "Hotspots", "Intrinsic", callableHotspotPoints.Value, good: 0d, bad: 10d, weight: 0.12d),
            CreateNormalizedContribution("churn_lines_90d", "Churn lines (90d)", "Change pressure", churnLines90d, good: 20d, bad: 400d, weight: 0.10d),
            CreateNormalizedContribution("touch_count_90d", "Touch count (90d)", "Change pressure", touchCount90d, good: 1d, bad: 12d, weight: 0.07d),
            CreateNormalizedContribution("author_count_90d", "Author count (90d)", "Change pressure", authorCount90d, good: 1d, bad: 4d, weight: 0.03d),
            CreateNormalizedContribution("strong_cochanged_file_count_90d", "Strong co-change files (90d)", "Co-change pressure", strongCochangedFileCount90d, good: 0d, bad: 8d, weight: 0.10d),
            CreateNormalizedContribution("unique_cochanged_file_count_90d", "Unique co-change files (90d)", "Co-change pressure", uniqueCochangedFileCount90d, good: 0d, bad: 20d, weight: 0.06d),
            CreateNormalizedContribution("avg_cochange_set_size_90d", "Avg co-change set size (90d)", "Co-change pressure", averageCochangeSetSize90d, good: 0d, bad: 6d, weight: 0.04d));
        return true;
    }

    private static MetricFormulaBreakdown Empty { get; } = new(0d, []);

    private static MetricFormulaBreakdown CreateBreakdown(params MetricComponentContribution[] components) =>
        new(components.Sum(component => component.ContributionPoints), components);

    private static MetricComponentContribution CreateNormalizedContribution(
        string key,
        string label,
        string category,
        double rawValue,
        double good,
        double bad,
        double weight)
    {
        var normalizedValue = Normalize(rawValue, good, bad);
        return new MetricComponentContribution(
            key,
            label,
            category,
            rawValue,
            normalizedValue,
            weight,
            100d * weight * normalizedValue);
    }

    private static MetricComponentContribution CreateWeightedCountContribution(
        string key,
        string label,
        string category,
        double rawValue,
        double weight) =>
        new(
            key,
            label,
            category,
            rawValue,
            NormalizedValue: null,
            weight,
            weight * rawValue);

    private static MetricComponentContribution CreateWeightedScoreContribution(
        string key,
        string label,
        string category,
        double rawValue,
        double weight) =>
        new(
            key,
            label,
            category,
            rawValue,
            NormalizedValue: null,
            weight,
            weight * rawValue);

    private static double Normalize(double value, double good, double bad)
    {
        if (bad <= good)
        {
            throw new ArgumentOutOfRangeException(nameof(bad), "Normalization requires bad > good.");
        }

        return Math.Clamp((value - good) / (bad - good), 0d, 1d);
    }

    private static bool TryGetGitOperands(
        MetricSet metrics,
        out double churnLines90d,
        out double touchCount90d,
        out double authorCount90d,
        out double uniqueCochangedFileCount90d,
        out double strongCochangedFileCount90d,
        out double averageCochangeSetSize90d)
    {
        churnLines90d = metrics.TryGetNumber(MetricIds.ChurnLines90d) ?? 0d;
        touchCount90d = metrics.TryGetNumber(MetricIds.TouchCount90d) ?? 0d;
        authorCount90d = metrics.TryGetNumber(MetricIds.AuthorCount90d) ?? 0d;
        uniqueCochangedFileCount90d = metrics.TryGetNumber(MetricIds.UniqueCochangedFileCount90d) ?? 0d;
        strongCochangedFileCount90d = metrics.TryGetNumber(MetricIds.StrongCochangedFileCount90d) ?? 0d;
        averageCochangeSetSize90d = metrics.TryGetNumber(MetricIds.AverageCochangeSetSize90d) ?? 0d;

        return metrics.TryGetNumber(MetricIds.ChurnLines90d).HasValue &&
            metrics.TryGetNumber(MetricIds.TouchCount90d).HasValue &&
            metrics.TryGetNumber(MetricIds.AuthorCount90d).HasValue &&
            metrics.TryGetNumber(MetricIds.UniqueCochangedFileCount90d).HasValue &&
            metrics.TryGetNumber(MetricIds.StrongCochangedFileCount90d).HasValue &&
            metrics.TryGetNumber(MetricIds.AverageCochangeSetSize90d).HasValue;
    }

}
