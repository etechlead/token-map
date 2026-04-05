using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Core.Metrics.Formulas;

namespace Clever.TokenMap.Tests.Metrics;

public sealed class ProductMetricFormulasTests
{
    [Fact]
    public void TryComputeComplexity_ReturnsWeightedBreakdown()
    {
        var metrics = MetricSet.From(
            (MetricIds.CodeLines, MetricValue.From(100)),
            (MetricIds.CyclomaticComplexitySum, MetricValue.From(20)),
            (MetricIds.CyclomaticComplexityMax, MetricValue.From(8)),
            (MetricIds.MaxNestingDepth, MetricValue.From(4)),
            (MetricIds.MaxParameterCount, MetricValue.From(6)));

        var success = ProductMetricFormulas.TryComputeComplexity(metrics, out var breakdown);

        Assert.True(success);
        Assert.Equal(47.190668980142661, breakdown.TotalPoints, precision: 12);
        Assert.Equal(5, breakdown.Components.Count);
        Assert.Equal("Cyclomatic complexity sum", breakdown.Components[1].Label);
        Assert.Equal(20d, breakdown.Components[1].RawValue);
        Assert.Equal(0.35d, breakdown.Components[1].Weight, precision: 12);
    }

    [Fact]
    public void TryComputeHotspots_ReturnsAdditiveBreakdown()
    {
        var metrics = MetricSet.From(
            (MetricIds.LongCallableCount, MetricValue.From(2)),
            (MetricIds.HighCyclomaticComplexityCallableCount, MetricValue.From(1)),
            (MetricIds.DeepNestingCallableCount, MetricValue.From(3)),
            (MetricIds.LongParameterListCount, MetricValue.From(4)));

        var success = ProductMetricFormulas.TryComputeHotspots(metrics, out var breakdown);

        Assert.True(success);
        Assert.Equal(17d, breakdown.TotalPoints, precision: 12);
        Assert.Collection(
            breakdown.Components,
            component =>
            {
                Assert.Equal("Long callables", component.Label);
                Assert.Equal(4d, component.ContributionPoints, precision: 12);
            },
            component =>
            {
                Assert.Equal("High complexity callables", component.Label);
                Assert.Equal(3d, component.ContributionPoints, precision: 12);
            },
            component =>
            {
                Assert.Equal("Deep nesting callables", component.Label);
                Assert.Equal(6d, component.ContributionPoints, precision: 12);
            },
            component =>
            {
                Assert.Equal("Long parameter lists", component.Label);
                Assert.Equal(4d, component.ContributionPoints, precision: 12);
            });
    }

    [Fact]
    public void TryComputeRefactorPriority_FallsBackToIntrinsicInputsWithoutGitContext()
    {
        var metrics = MetricSet.From(
            (MetricIds.ComplexityPoints, MetricValue.From(60)),
            (MetricIds.CallableHotspotPoints, MetricValue.From(5)));

        var success = ProductMetricFormulas.TryComputeRefactorPriority(metrics, out var breakdown);

        Assert.True(success);
        Assert.Equal(58d, breakdown.TotalPoints, precision: 12);
        Assert.Equal(2, breakdown.Components.Count);
        Assert.All(breakdown.Components, component => Assert.Equal("Intrinsic", component.Category));
    }

    [Fact]
    public void TryComputeRefactorPriority_ExpandsGitAwareContributorsWhenGitMetricsExist()
    {
        var metrics = MetricSet.From(
            (MetricIds.ComplexityPoints, MetricValue.From(60)),
            (MetricIds.CallableHotspotPoints, MetricValue.From(5)),
            (MetricIds.ChurnLines90d, MetricValue.From(210)),
            (MetricIds.TouchCount90d, MetricValue.From(7)),
            (MetricIds.AuthorCount90d, MetricValue.From(3)),
            (MetricIds.UniqueCochangedFileCount90d, MetricValue.From(10)),
            (MetricIds.StrongCochangedFileCount90d, MetricValue.From(4)),
            (MetricIds.AverageCochangeSetSize90d, MetricValue.From(3.5d)));

        var success = ProductMetricFormulas.TryComputeRefactorPriority(metrics, out var breakdown);

        Assert.True(success);
        Assert.Equal(55.95151515151515, breakdown.TotalPoints, precision: 12);
        Assert.Equal(8, breakdown.Components.Count);
        Assert.Contains(
            breakdown.Components,
            component => component.Key == "churn_lines_90d" &&
                component.Category == "Change pressure" &&
                component.RawValue == 210d);
        Assert.Contains(
            breakdown.Components,
            component => component.Key == "strong_cochanged_file_count_90d" &&
                component.Category == "Co-change pressure" &&
                component.RawValue == 4d);
    }
}
