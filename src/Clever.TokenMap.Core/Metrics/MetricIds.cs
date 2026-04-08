namespace Clever.TokenMap.Core.Metrics;

public static class MetricIds
{
    public static readonly MetricId Tokens = new("tokens");
    public static readonly MetricId NonEmptyLines = new("non_empty_lines");
    public static readonly MetricId CodeLines = new("code_lines");
    public static readonly MetricId FileSizeBytes = new("file_size_bytes");
    public static readonly MetricId MaxParameterCount = new("max_parameter_count");
    public static readonly MetricId CyclomaticComplexitySum = new("cc_sum");
    public static readonly MetricId CyclomaticComplexityMax = new("cc_max");
    public static readonly MetricId MaxNestingDepth = new("max_nesting_depth");
    public static readonly MetricId LongCallableCount = new("long_callable_count");
    public static readonly MetricId HighCyclomaticComplexityCallableCount = new("high_cc_callable_count");
    public static readonly MetricId DeepNestingCallableCount = new("deep_nesting_callable_count");
    public static readonly MetricId LongParameterListCount = new("long_parameter_list_count");
    public static readonly MetricId CallableHotspotPoints = new("callable_hotspot_points");
    public static readonly MetricId ComplexityPoints = new("complexity_points");
    public static readonly MetricId RefactorPriorityPoints = new("refactor_priority_points");

    // Internal operands for explainability and product metrics.
    public static readonly MetricId CallableCount = new("callable_count");
    public static readonly MetricId AffectedCallableCount = new("affected_callable_count");
    public static readonly MetricId AffectedCallableRatio = new("affected_callable_ratio");
    public static readonly MetricId TotalCallableBurdenPoints = new("total_callable_burden_points");
    public static readonly MetricId TopCallableBurdenPoints = new("top_callable_burden_points");
    public static readonly MetricId TopThreeCallableBurdenShare = new("top_three_callable_burden_share");
    public static readonly MetricId ChurnLines90d = new("churn_lines_90d");
    public static readonly MetricId TouchCount90d = new("touch_count_90d");
    public static readonly MetricId AuthorCount90d = new("author_count_90d");
    public static readonly MetricId UniqueCochangedFileCount90d = new("unique_cochanged_file_count_90d");
    public static readonly MetricId StrongCochangedFileCount90d = new("strong_cochanged_file_count_90d");
    public static readonly MetricId AverageCochangeSetSize90d = new("avg_cochange_set_size_90d");
}
