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
    public async Task ComputeAsync_BlendsGitChangeAndBlastRadiusPressureWhenHistoryIsAvailable()
    {
        var result = await ComputeAsync(
            MetricSet.From(
                (MetricIds.ComplexityPointsV0, MetricValue.From(60)),
                (MetricIds.CallableHotspotPointsV0, MetricValue.From(5))),
            CreateArtifact(
                churnLines90d: 210,
                touchCount90d: 7,
                authorCount90d: 3,
                uniqueCochangedFileCount90d: 10,
                strongCochangedFileCount90d: 4,
                averageCochangeSetSize90d: 3.5d));

        Assert.Equal(55.95151515151515, result.TryGetNumber(MetricIds.RefactorPriorityPointsV0)!.Value, precision: 12);
    }

    [Fact]
    public async Task ComputeAsync_UsesZeroHistoryArtifactInsteadOfFallbackWhenGitSnapshotExists()
    {
        var result = await ComputeAsync(
            MetricSet.From(
                (MetricIds.ComplexityPointsV0, MetricValue.From(60)),
                (MetricIds.CallableHotspotPointsV0, MetricValue.From(5))),
            GitFileHistoryArtifact.Zero);

        Assert.Equal(34.8d, result.TryGetNumber(MetricIds.RefactorPriorityPointsV0)!.Value, precision: 12);
    }

    [Fact]
    public async Task ComputeAsync_RaisesPriorityForHighBlastRadiusAtModerateComplexity()
    {
        var result = await ComputeAsync(
            MetricSet.From(
                (MetricIds.ComplexityPointsV0, MetricValue.From(35)),
                (MetricIds.CallableHotspotPointsV0, MetricValue.From(2))),
            CreateArtifact(
                churnLines90d: 20,
                touchCount90d: 1,
                authorCount90d: 1,
                uniqueCochangedFileCount90d: 20,
                strongCochangedFileCount90d: 8,
                averageCochangeSetSize90d: 6d));

        Assert.Equal(39.2d, result.TryGetNumber(MetricIds.RefactorPriorityPointsV0)!.Value, precision: 12);
    }

    [Fact]
    public async Task ComputeAsync_KeepsHighIntrinsicComplexityAheadOfLowBlastRadiusPressure()
    {
        var result = await ComputeAsync(
            MetricSet.From(
                (MetricIds.ComplexityPointsV0, MetricValue.From(90)),
                (MetricIds.CallableHotspotPointsV0, MetricValue.From(1))),
            CreateArtifact(
                churnLines90d: 20,
                touchCount90d: 1,
                authorCount90d: 1,
                uniqueCochangedFileCount90d: 1,
                strongCochangedFileCount90d: 0,
                averageCochangeSetSize90d: 1d));

        Assert.Equal(45.36666666666667, result.TryGetNumber(MetricIds.RefactorPriorityPointsV0)!.Value, precision: 12);
    }

    [Fact]
    public async Task ComputeAsync_SetsNotApplicableWhenComplexityInputsAreMissing()
    {
        var result = await ComputeAsync(
            MetricSet.From((MetricIds.CallableHotspotPointsV0, MetricValue.From(4))),
            CreateArtifact(
                churnLines90d: 300,
                touchCount90d: 8,
                authorCount90d: 2,
                uniqueCochangedFileCount90d: 6,
                strongCochangedFileCount90d: 2,
                averageCochangeSetSize90d: 2.5d));

        Assert.Equal(MetricStatus.NotApplicable, result.GetOrDefault(MetricIds.RefactorPriorityPointsV0).Status);
    }

    private static GitFileHistoryArtifact CreateArtifact(
        int churnLines90d,
        int touchCount90d,
        int authorCount90d,
        int uniqueCochangedFileCount90d,
        int strongCochangedFileCount90d,
        double averageCochangeSetSize90d) =>
        new(
            ChurnLines90d: churnLines90d,
            TouchCount90d: touchCount90d,
            AuthorCount90d: authorCount90d,
            UniqueCochangedFileCount90d: uniqueCochangedFileCount90d,
            StrongCochangedFileCount90d: strongCochangedFileCount90d,
            AverageCochangeSetSize90d: averageCochangeSetSize90d);

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
