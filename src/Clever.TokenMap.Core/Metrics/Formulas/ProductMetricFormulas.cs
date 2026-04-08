namespace Clever.TokenMap.Core.Metrics.Formulas;

public static class ProductMetricFormulas
{
    private const double MaxGitAmplification = 0.25d;
    private const double GitSignalWeightSum = 0.45d;

    public static bool TryComputeStructuralRisk(MetricSet metrics, out MetricFormulaBreakdown breakdown)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        var codeLines = metrics.TryGetNumber(MetricIds.CodeLines);
        var totalCallableBurdenPoints = metrics.TryGetNumber(MetricIds.TotalCallableBurdenPoints);
        var topCallableBurdenPoints = metrics.TryGetNumber(MetricIds.TopCallableBurdenPoints);
        var affectedCallableRatio = metrics.TryGetNumber(MetricIds.AffectedCallableRatio);
        var topThreeCallableBurdenShare = metrics.TryGetNumber(MetricIds.TopThreeCallableBurdenShare);

        if (!codeLines.HasValue ||
            !totalCallableBurdenPoints.HasValue ||
            !topCallableBurdenPoints.HasValue ||
            !affectedCallableRatio.HasValue ||
            !topThreeCallableBurdenShare.HasValue)
        {
            breakdown = Empty;
            return false;
        }

        breakdown = CreateBreakdown(
            CreateOpenEndedContribution("code_lines", "File scale", "File scale", codeLines.Value, good: 20d, bad: 300d, weight: 0.20d),
            CreateOpenEndedContribution("total_callable_burden_points", "Total callable burden", "Callable burden", totalCallableBurdenPoints.Value, good: 0d, bad: 120d, weight: 0.35d),
            CreateOpenEndedContribution("top_callable_burden_points", "Top callable burden", "Callable burden", topCallableBurdenPoints.Value, good: 0d, bad: 45d, weight: 0.20d),
            CreateNormalizedContribution("affected_callable_ratio", "Affected callable ratio", "Risk distribution", affectedCallableRatio.Value, good: 0.10d, bad: 0.60d, weight: 0.15d),
            CreateNormalizedContribution("top_three_callable_burden_share", "Risk concentrated in top methods", "Risk distribution", topThreeCallableBurdenShare.Value, good: 0.35d, bad: 0.85d, weight: 0.10d));
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

        var structuralRiskPoints = metrics.TryGetNumber(MetricIds.ComplexityPoints);
        if (!structuralRiskPoints.HasValue)
        {
            breakdown = Empty;
            return false;
        }

        if (!TryGetGitOperands(metrics, out var churnLines90d, out var touchCount90d, out var authorCount90d, out var uniqueCochangedFileCount90d, out var strongCochangedFileCount90d, out var averageCochangeSetSize90d))
        {
            breakdown = CreateBreakdown(
                CreateWeightedScoreContribution("structural_risk_points", "Structural Risk", "Structural", structuralRiskPoints.Value, weight: 1.00d));
            return true;
        }

        // Git history can raise urgency, but it should only amplify existing structural risk rather
        // than dominate the score for otherwise modest files.
        breakdown = CreateBreakdown(
            CreateWeightedScoreContribution("structural_risk_points", "Structural Risk", "Structural", structuralRiskPoints.Value, weight: 1.00d),
            CreateStructuralScaledContribution("churn_lines_90d", "Churn lines (90d)", "Change pressure", churnLines90d, good: 50d, bad: 500d, structuralRiskPoints.Value, signalWeight: 0.10d),
            CreateStructuralScaledContribution("touch_count_90d", "Touch count (90d)", "Change pressure", touchCount90d, good: 2d, bad: 15d, structuralRiskPoints.Value, signalWeight: 0.08d),
            CreateStructuralScaledContribution("author_count_90d", "Author count (90d)", "Change pressure", authorCount90d, good: 1d, bad: 4d, structuralRiskPoints.Value, signalWeight: 0.05d),
            CreateStructuralScaledContribution("strong_cochanged_file_count_90d", "Strong co-change files (90d)", "Co-change pressure", strongCochangedFileCount90d, good: 5d, bad: 80d, structuralRiskPoints.Value, signalWeight: 0.10d, signalScale: ComputeCochangeEngagement(touchCount90d)),
            CreateStructuralScaledContribution("unique_cochanged_file_count_90d", "Unique co-change files (90d)", "Co-change pressure", uniqueCochangedFileCount90d, good: 30d, bad: 200d, structuralRiskPoints.Value, signalWeight: 0.07d, signalScale: ComputeCochangeEngagement(touchCount90d)),
            CreateStructuralScaledContribution("avg_cochange_set_size_90d", "Avg co-change set size (90d)", "Co-change pressure", averageCochangeSetSize90d, good: 10d, bad: 60d, structuralRiskPoints.Value, signalWeight: 0.05d, signalScale: ComputeCochangeEngagement(touchCount90d)));
        return true;
    }
    private static MetricFormulaBreakdown Empty { get; } = new(0d, []);

    private static MetricFormulaBreakdown CreateBreakdown(params MetricComponentContribution[] components) =>
        new(components.Sum(component => component.ContributionPoints), components);

    private static MetricComponentContribution CreateOpenEndedContribution(
        string key,
        string label,
        string category,
        double rawValue,
        double good,
        double bad,
        double weight)
    {
        var normalizedValue = NormalizeOpenEnded(rawValue, good, bad);
        return new MetricComponentContribution(
            key,
            label,
            category,
            rawValue,
            normalizedValue,
            weight,
            100d * weight * normalizedValue);
    }

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

    private static MetricComponentContribution CreateStructuralScaledContribution(
        string key,
        string label,
        string category,
        double rawValue,
        double good,
        double bad,
        double structuralRiskPoints,
        double signalWeight,
        double signalScale = 1d)
    {
        var normalizedValue = Normalize(rawValue, good, bad);
        var effectiveWeight = MaxGitAmplification * (signalWeight / GitSignalWeightSum) * signalScale;
        return new MetricComponentContribution(
            key,
            label,
            category,
            rawValue,
            normalizedValue,
            effectiveWeight,
            structuralRiskPoints * effectiveWeight * normalizedValue);
    }

    private static double Normalize(double value, double good, double bad)
    {
        if (bad <= good)
        {
            throw new ArgumentOutOfRangeException(nameof(bad), "Normalization requires bad > good.");
        }

        return Math.Clamp((value - good) / (bad - good), 0d, 1d);
    }

    private static double NormalizeOpenEnded(double value, double good, double bad)
    {
        if (bad <= good)
        {
            throw new ArgumentOutOfRangeException(nameof(bad), "Normalization requires bad > good.");
        }

        if (value <= good)
        {
            return 0d;
        }

        if (value <= bad)
        {
            return (value - good) / (bad - good);
        }

        var excessRatio = (value - bad) / (bad - good);
        return 1d + Math.Log(1d + excessRatio);
    }

    private static double ComputeCochangeEngagement(double touchCount90d) =>
        Normalize(touchCount90d, good: 1d, bad: 5d);

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
