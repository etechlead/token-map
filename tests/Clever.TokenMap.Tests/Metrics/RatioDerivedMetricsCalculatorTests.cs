using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Metrics;
using Clever.TokenMap.Metrics.Calculators.Derived;

namespace Clever.TokenMap.Tests.Metrics;

public sealed class RatioDerivedMetricsCalculatorTests
{
    private readonly RatioDerivedMetricsCalculator _calculator = new();

    [Fact]
    public async Task ComputeAsync_ComputesDerivedRatios()
    {
        var inputMetrics = MetricSet.From(
            (MetricIds.TotalParameterCount, MetricValue.From(8)),
            (MetricIds.FunctionCount, MetricValue.From(4)),
            (MetricIds.CyclomaticComplexitySum, MetricValue.From(10)),
            (MetricIds.CodeLines, MetricValue.From(20)),
            (MetricIds.CommentLines, MetricValue.From(5)));
        var builder = new MetricSetBuilder();

        await _calculator.ComputeAsync(
            context: new StubFileMetricContext(),
            inputMetrics,
            builder,
            CancellationToken.None);

        var result = builder.Build();

        Assert.Equal(2d, result.TryGetNumber(MetricIds.AverageParametersPerCallable)!.Value);
        Assert.Equal(2.5d, result.TryGetNumber(MetricIds.AverageCyclomaticComplexityPerCallable)!.Value);
        Assert.Equal(0.5d, result.TryGetNumber(MetricIds.CyclomaticComplexityPerCodeLine)!.Value);
        Assert.Equal(0.2d, result.TryGetNumber(MetricIds.CommentRatio)!.Value);
    }

    [Fact]
    public async Task ComputeAsync_SetsNotApplicableWhenOperandsAreMissingOrInvalid()
    {
        var inputMetrics = MetricSet.From(
            (MetricIds.TotalParameterCount, MetricValue.From(8)),
            (MetricIds.FunctionCount, MetricValue.From(0)),
            (MetricIds.CodeLines, MetricValue.From(0)),
            (MetricIds.CommentLines, MetricValue.From(0)));
        var builder = new MetricSetBuilder();

        await _calculator.ComputeAsync(
            context: new StubFileMetricContext(),
            inputMetrics,
            builder,
            CancellationToken.None);

        var result = builder.Build();

        Assert.Equal(MetricStatus.NotApplicable, result.GetOrDefault(MetricIds.AverageParametersPerCallable).Status);
        Assert.Equal(MetricStatus.NotApplicable, result.GetOrDefault(MetricIds.AverageCyclomaticComplexityPerCallable).Status);
        Assert.Equal(MetricStatus.NotApplicable, result.GetOrDefault(MetricIds.CyclomaticComplexityPerCodeLine).Status);
        Assert.Equal(MetricStatus.NotApplicable, result.GetOrDefault(MetricIds.CommentRatio).Status);
    }

    private sealed class StubFileMetricContext : IFileMetricContext
    {
        public long FileSizeBytes => 0;

        public ValueTask<TArtifact?> GetArtifactAsync<TArtifact>(CancellationToken cancellationToken)
            where TArtifact : class =>
            ValueTask.FromResult<TArtifact?>(null);
    }
}
