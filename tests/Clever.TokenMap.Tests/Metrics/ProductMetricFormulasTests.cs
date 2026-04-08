using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Core.Metrics.Formulas;

namespace Clever.TokenMap.Tests.Metrics;

public sealed class ProductMetricFormulasTests
{
    [Fact]
    public void TryComputeStructuralRisk_ReturnsWeightedBreakdown()
    {
        var metrics = MetricSet.From(
            (MetricIds.CodeLines, MetricValue.From(100)),
            (MetricIds.TotalCallableBurdenPoints, MetricValue.From(80)),
            (MetricIds.TopCallableBurdenPoints, MetricValue.From(35)),
            (MetricIds.AffectedCallableRatio, MetricValue.From(0.75d)),
            (MetricIds.TopThreeCallableBurdenShare, MetricValue.From(0.90d)));

        var success = ProductMetricFormulas.TryComputeStructuralRisk(metrics, out var breakdown);

        Assert.True(success);
        Assert.Equal(69.60317460317461, breakdown.TotalPoints, precision: 12);
        Assert.Equal(5, breakdown.Components.Count);
        Assert.Equal("Total callable burden", breakdown.Components[1].Label);
        Assert.Equal(80d, breakdown.Components[1].RawValue);
        Assert.Equal(0.35d, breakdown.Components[1].Weight, precision: 12);
    }

    [Fact]
    public void TryComputeStructuralRisk_ContinuesGrowingPastBadThresholds()
    {
        var metrics = MetricSet.From(
            (MetricIds.CodeLines, MetricValue.From(600)),
            (MetricIds.TotalCallableBurdenPoints, MetricValue.From(260)),
            (MetricIds.TopCallableBurdenPoints, MetricValue.From(120)),
            (MetricIds.AffectedCallableRatio, MetricValue.From(1d)),
            (MetricIds.TopThreeCallableBurdenShare, MetricValue.From(1d)));

        var success = ProductMetricFormulas.TryComputeStructuralRisk(metrics, out var breakdown);

        Assert.True(success);
        Assert.True(breakdown.TotalPoints > 100d);
        Assert.True(breakdown.Components[0].NormalizedValue > 1d);
        Assert.True(breakdown.Components[1].NormalizedValue > 1d);
        Assert.True(breakdown.Components[2].NormalizedValue > 1d);
        Assert.Equal(1d, breakdown.Components[3].NormalizedValue);
        Assert.Equal(1d, breakdown.Components[4].NormalizedValue);
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
        var metrics = MetricSet.From((MetricIds.ComplexityPoints, MetricValue.From(60)));

        var success = ProductMetricFormulas.TryComputeRefactorPriority(metrics, out var breakdown);

        Assert.True(success);
        Assert.Equal(60d, breakdown.TotalPoints, precision: 12);
        Assert.Single(breakdown.Components);
        Assert.All(breakdown.Components, component => Assert.Equal("Structural", component.Category));
    }

    [Fact]
    public void TryComputeRefactorPriority_ExpandsGitAwareContributorsWhenGitMetricsExist()
    {
        var metrics = MetricSet.From(
            (MetricIds.ComplexityPoints, MetricValue.From(60)),
            (MetricIds.ChurnLines90d, MetricValue.From(210)),
            (MetricIds.TouchCount90d, MetricValue.From(7)),
            (MetricIds.AuthorCount90d, MetricValue.From(3)),
            (MetricIds.UniqueCochangedFileCount90d, MetricValue.From(10)),
            (MetricIds.StrongCochangedFileCount90d, MetricValue.From(4)),
            (MetricIds.AverageCochangeSetSize90d, MetricValue.From(3.5d)));

        var success = ProductMetricFormulas.TryComputeRefactorPriority(metrics, out var breakdown);

        Assert.True(success);
        Assert.Equal(63.32193732193732, breakdown.TotalPoints, precision: 12);
        Assert.Equal(7, breakdown.Components.Count);
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

    [Fact]
    public void TryComputeRefactorPriority_CapsGitAmplificationAtThresholds()
    {
        var metrics = MetricSet.From(
            (MetricIds.ComplexityPoints, MetricValue.From(140)),
            (MetricIds.ChurnLines90d, MetricValue.From(800)),
            (MetricIds.TouchCount90d, MetricValue.From(24)),
            (MetricIds.AuthorCount90d, MetricValue.From(6)),
            (MetricIds.UniqueCochangedFileCount90d, MetricValue.From(40)),
            (MetricIds.StrongCochangedFileCount90d, MetricValue.From(16)),
            (MetricIds.AverageCochangeSetSize90d, MetricValue.From(12d)));

        var success = ProductMetricFormulas.TryComputeRefactorPriority(metrics, out var breakdown);

        Assert.True(success);
        Assert.Equal(159.50544662309369, breakdown.TotalPoints, precision: 12);

        var normalizedComponents = breakdown.Components
            .Where(component => component.NormalizedValue.HasValue)
            .ToArray();

        Assert.NotEmpty(normalizedComponents);
        Assert.Contains(normalizedComponents, component => component.Key == "churn_lines_90d" && component.NormalizedValue == 1d);
        Assert.Contains(normalizedComponents, component => component.Key == "touch_count_90d" && component.NormalizedValue == 1d);
        Assert.Contains(normalizedComponents, component => component.Key == "author_count_90d" && component.NormalizedValue == 1d);
        Assert.Contains(normalizedComponents, component => component.Key == "strong_cochanged_file_count_90d" && component.NormalizedValue < 1d);
        Assert.Contains(normalizedComponents, component => component.Key == "unique_cochanged_file_count_90d" && component.NormalizedValue < 1d);
        Assert.Contains(normalizedComponents, component => component.Key == "avg_cochange_set_size_90d" && component.NormalizedValue < 1d);
    }
}
