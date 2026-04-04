namespace Clever.TokenMap.Core.Metrics;

public static class MetricIds
{
    public static readonly MetricId Tokens = new("tokens");
    public static readonly MetricId NonEmptyLines = new("non_empty_lines");
    public static readonly MetricId CodeLines = new("code_lines");
    public static readonly MetricId FileSizeBytes = new("file_size_bytes");
    public static readonly MetricId CommentLines = new("comment_lines");
    public static readonly MetricId FunctionCount = new("function_count");
    public static readonly MetricId TotalParameterCount = new("total_parameter_count");
    public static readonly MetricId MaxParameterCount = new("max_parameter_count");
    public static readonly MetricId TypeCount = new("type_count");
    public static readonly MetricId CyclomaticComplexitySum = new("cc_sum");
    public static readonly MetricId CyclomaticComplexityMax = new("cc_max");
    public static readonly MetricId MaxNestingDepth = new("max_nesting_depth");
    public static readonly MetricId AverageParametersPerCallable = new("avg_parameters_per_callable");
    public static readonly MetricId AverageCyclomaticComplexityPerCallable = new("avg_cc_per_callable");
    public static readonly MetricId CyclomaticComplexityPerCodeLine = new("cc_per_code_line");
    public static readonly MetricId CommentRatio = new("comment_ratio");
    public static readonly MetricId ComplexityPointsV0 = new("complexity_points_v0");
}
