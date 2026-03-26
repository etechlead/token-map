using Clever.TokenMap.Core.Enums;

namespace Clever.TokenMap.Core.Models;

public static class AnalysisMetricExtensions
{
    public static AnalysisMetric Normalize(this AnalysisMetric metric) =>
        metric switch
        {
            AnalysisMetric.Lines => AnalysisMetric.Lines,
            AnalysisMetric.Size => AnalysisMetric.Size,
            _ => AnalysisMetric.Tokens,
        };

    public static long GetValue(this AnalysisMetric metric, NodeMetrics metrics) =>
        metric.Normalize() switch
        {
            AnalysisMetric.Lines => metrics.NonEmptyLines,
            AnalysisMetric.Size => metrics.FileSizeBytes,
            _ => metrics.Tokens,
        };
}
