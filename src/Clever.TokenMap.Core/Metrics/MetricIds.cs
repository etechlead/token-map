namespace Clever.TokenMap.Core.Metrics;

public static class MetricIds
{
    public static readonly MetricId Tokens = new("tokens");
    public static readonly MetricId NonEmptyLines = new("non_empty_lines");
    public static readonly MetricId FileSizeBytes = new("file_size_bytes");
}
