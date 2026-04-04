using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Metrics;

namespace Clever.TokenMap.Tests.Metrics;

public sealed class MetricSetBuilderTests
{
    [Fact]
    public void Build_ReturnsIndependentSnapshot()
    {
        var builder = new MetricSetBuilder();
        builder.SetValue(MetricIds.Tokens, 10);

        var snapshot = builder.Build();
        builder.SetValue(MetricIds.Tokens, 20);

        Assert.Equal(10d, snapshot.TryGetNumber(MetricIds.Tokens)!.Value);
    }

    [Fact]
    public void Constructor_SeedsValuesFromExistingMetricSet()
    {
        var seed = MetricSet.From(
            (MetricIds.Tokens, MetricValue.From(10)),
            (MetricIds.NonEmptyLines, MetricValue.From(3)));

        var builder = new MetricSetBuilder(seed);
        builder.SetValue(MetricIds.NonEmptyLines, 5);

        var result = builder.Build();

        Assert.Equal(10d, result.TryGetNumber(MetricIds.Tokens)!.Value);
        Assert.Equal(5d, result.TryGetNumber(MetricIds.NonEmptyLines)!.Value);
        Assert.Equal(3d, seed.TryGetNumber(MetricIds.NonEmptyLines)!.Value);
    }
}
