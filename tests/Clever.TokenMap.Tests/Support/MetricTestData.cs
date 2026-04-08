using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.Tests.Support;

internal static class MetricTestData
{
    internal static MetricSet CreateComputedMetrics(long tokens, int nonEmptyLines, long fileSizeBytes) =>
        MetricSet.From(
            (MetricIds.Tokens, MetricValue.From(tokens)),
            (MetricIds.NonEmptyLines, MetricValue.From(nonEmptyLines)),
            (MetricIds.FileSizeBytes, MetricValue.From(fileSizeBytes)));

    internal static MetricSet CreateSkippedComputedMetrics(long fileSizeBytes) =>
        MetricSet.From(
            (MetricIds.Tokens, MetricValue.NotApplicable()),
            (MetricIds.NonEmptyLines, MetricValue.NotApplicable()),
            (MetricIds.CodeLines, MetricValue.NotApplicable()),
            (MetricIds.FileSizeBytes, MetricValue.From(fileSizeBytes)),
            (MetricIds.MaxParameterCount, MetricValue.NotApplicable()),
            (MetricIds.CyclomaticComplexitySum, MetricValue.NotApplicable()),
            (MetricIds.CyclomaticComplexityMax, MetricValue.NotApplicable()),
            (MetricIds.MaxNestingDepth, MetricValue.NotApplicable()),
            (MetricIds.LongCallableCount, MetricValue.NotApplicable()),
            (MetricIds.HighCyclomaticComplexityCallableCount, MetricValue.NotApplicable()),
            (MetricIds.DeepNestingCallableCount, MetricValue.NotApplicable()),
            (MetricIds.LongParameterListCount, MetricValue.NotApplicable()),
            (MetricIds.CallableHotspotPoints, MetricValue.NotApplicable()),
            (MetricIds.CallableCount, MetricValue.NotApplicable()),
            (MetricIds.AffectedCallableCount, MetricValue.NotApplicable()),
            (MetricIds.AffectedCallableRatio, MetricValue.NotApplicable()),
            (MetricIds.TotalCallableBurdenPoints, MetricValue.NotApplicable()),
            (MetricIds.TopCallableBurdenPoints, MetricValue.NotApplicable()),
            (MetricIds.TopThreeCallableBurdenShare, MetricValue.NotApplicable()),
            (MetricIds.ComplexityPoints, MetricValue.NotApplicable()),
            (MetricIds.RefactorPriorityPoints, MetricValue.NotApplicable()));

    internal static NodeSummary CreateFileSummary() =>
        new(
            DescendantFileCount: 1,
            DescendantDirectoryCount: 0);

    internal static NodeSummary CreateDirectorySummary(int descendantFileCount, int descendantDirectoryCount) =>
        new(
            DescendantFileCount: descendantFileCount,
            DescendantDirectoryCount: descendantDirectoryCount);
}

