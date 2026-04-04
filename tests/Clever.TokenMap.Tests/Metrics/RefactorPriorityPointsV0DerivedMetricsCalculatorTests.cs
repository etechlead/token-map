using Clever.TokenMap.Core.Analysis.Git;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Metrics;
using Clever.TokenMap.Metrics.Calculators.Derived;

namespace Clever.TokenMap.Tests.Metrics;

public sealed class RefactorPriorityPointsV0DerivedMetricsCalculatorTests
{
    private readonly RefactorPriorityPointsV0DerivedMetricsCalculator _calculator = new();

    [Fact]
    public async Task ComputeAsync_UsesBasePriorityWhenGitHistoryIsUnavailable()
    {
        var result = await ComputeAsync(
            MetricSet.From(
                (MetricIds.ComplexityPointsV0, MetricValue.From(60)),
                (MetricIds.CallableHotspotPointsV0, MetricValue.From(5))));

        Assert.Equal(58d, result.TryGetNumber(MetricIds.RefactorPriorityPointsV0)!.Value, precision: 12);
    }

    [Fact]
    public async Task ComputeAsync_BlendsGitChangePressureWhenHistoryIsAvailable()
    {
        var result = await ComputeAsync(
            MetricSet.From(
                (MetricIds.ComplexityPointsV0, MetricValue.From(60)),
                (MetricIds.CallableHotspotPointsV0, MetricValue.From(5))),
            new GitFileHistoryArtifact(
                ChurnLines90d: 210,
                TouchCount90d: 7,
                AuthorCount90d: 3));

        Assert.Equal(57.022727272727273, result.TryGetNumber(MetricIds.RefactorPriorityPointsV0)!.Value, precision: 12);
    }

    [Fact]
    public async Task ComputeAsync_RaisesPriorityForHighChangePressureAtModerateComplexity()
    {
        var result = await ComputeAsync(
            MetricSet.From(
                (MetricIds.ComplexityPointsV0, MetricValue.From(35)),
                (MetricIds.CallableHotspotPointsV0, MetricValue.From(2))),
            new GitFileHistoryArtifact(
                ChurnLines90d: 400,
                TouchCount90d: 12,
                AuthorCount90d: 4));

        Assert.Equal(49d, result.TryGetNumber(MetricIds.RefactorPriorityPointsV0)!.Value, precision: 12);
    }

    [Fact]
    public async Task ComputeAsync_KeepsHighComplexityAheadOfLowChangePressurePenalty()
    {
        var result = await ComputeAsync(
            MetricSet.From(
                (MetricIds.ComplexityPointsV0, MetricValue.From(90)),
                (MetricIds.CallableHotspotPointsV0, MetricValue.From(1))),
            new GitFileHistoryArtifact(
                ChurnLines90d: 20,
                TouchCount90d: 1,
                AuthorCount90d: 1));

        Assert.Equal(55.5d, result.TryGetNumber(MetricIds.RefactorPriorityPointsV0)!.Value, precision: 12);
    }

    [Fact]
    public async Task ComputeAsync_SetsNotApplicableWhenComplexityInputsAreMissing()
    {
        var result = await ComputeAsync(
            MetricSet.From((MetricIds.CallableHotspotPointsV0, MetricValue.From(4))),
            new GitFileHistoryArtifact(
                ChurnLines90d: 300,
                TouchCount90d: 8,
                AuthorCount90d: 2));

        Assert.Equal(MetricStatus.NotApplicable, result.GetOrDefault(MetricIds.RefactorPriorityPointsV0).Status);
    }

    private async Task<MetricSet> ComputeAsync(MetricSet inputMetrics, GitFileHistoryArtifact? gitFileHistoryArtifact = null)
    {
        var builder = new MetricSetBuilder();

        await _calculator.ComputeAsync(
            new StubFileMetricContext(gitFileHistoryArtifact),
            inputMetrics,
            builder,
            CancellationToken.None);

        return builder.Build();
    }

    private sealed class StubFileMetricContext(GitFileHistoryArtifact? gitFileHistoryArtifact) : IFileMetricContext
    {
        public long FileSizeBytes => 0;

        public ValueTask<TArtifact?> GetArtifactAsync<TArtifact>(CancellationToken cancellationToken)
            where TArtifact : class
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(gitFileHistoryArtifact as TArtifact);
        }
    }
}
