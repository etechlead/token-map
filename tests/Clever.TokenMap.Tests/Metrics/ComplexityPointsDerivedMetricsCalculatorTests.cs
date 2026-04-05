using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Metrics;
using Clever.TokenMap.Metrics.Calculators.Derived;

namespace Clever.TokenMap.Tests.Metrics;

public sealed class ComplexityPointsDerivedMetricsCalculatorTests
{
    private readonly ComplexityPointsDerivedMetricsCalculator _calculator = new();

    [Fact]
    public async Task ComputeAsync_ComputesCompositeComplexityPoints()
    {
        var inputMetrics = MetricSet.From(
            (MetricIds.CodeLines, MetricValue.From(100)),
            (MetricIds.CyclomaticComplexitySum, MetricValue.From(20)),
            (MetricIds.CyclomaticComplexityMax, MetricValue.From(8)),
            (MetricIds.MaxNestingDepth, MetricValue.From(4)),
            (MetricIds.MaxParameterCount, MetricValue.From(6)));
        var builder = new MetricSetBuilder();

        await _calculator.ComputeAsync(
            context: new StubFileMetricContext(),
            inputMetrics,
            builder,
            CancellationToken.None);

        var result = builder.Build();

        Assert.Equal(47.190668980142661, result.TryGetNumber(MetricIds.ComplexityPoints)!.Value, precision: 12);
    }

    [Fact]
    public async Task ComputeAsync_SetsNotApplicableWhenRawMetricsAreMissing()
    {
        var inputMetrics = MetricSet.From(
            (MetricIds.CodeLines, MetricValue.From(100)),
            (MetricIds.CyclomaticComplexitySum, MetricValue.From(20)));
        var builder = new MetricSetBuilder();

        await _calculator.ComputeAsync(
            context: new StubFileMetricContext(),
            inputMetrics,
            builder,
            CancellationToken.None);

        var result = builder.Build();

        Assert.Equal(MetricStatus.NotApplicable, result.GetOrDefault(MetricIds.ComplexityPoints).Status);
    }

    private sealed class StubFileMetricContext : IFileMetricContext
    {
        public long FileSizeBytes => 0;

        public ValueTask<TArtifact?> GetArtifactAsync<TArtifact>(CancellationToken cancellationToken)
            where TArtifact : class =>
            ValueTask.FromResult<TArtifact?>(null);
    }
}

