using Clever.TokenMap.Core.Analysis.Git;
using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.Metrics.Calculators.Derived;

public sealed class RefactorPriorityPointsV0DerivedMetricsCalculator : IFileDerivedMetricCalculator
{
    public int Order => 250;

    public async ValueTask ComputeAsync(
        IFileMetricContext context,
        MetricSet inputMetrics,
        IMetricSink sink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(inputMetrics);
        ArgumentNullException.ThrowIfNull(sink);

        cancellationToken.ThrowIfCancellationRequested();

        var complexityPoints = inputMetrics.TryGetNumber(MetricIds.ComplexityPointsV0);
        var callableHotspotPoints = inputMetrics.TryGetNumber(MetricIds.CallableHotspotPointsV0);
        if (!complexityPoints.HasValue || !callableHotspotPoints.HasValue)
        {
            sink.SetNotApplicable(MetricIds.RefactorPriorityPointsV0);
            return;
        }

        var hotspotPressure = 100d * Normalize(callableHotspotPoints.Value, good: 0d, bad: 10d);
        var basePriority =
            (0.80d * complexityPoints.Value) +
            (0.20d * hotspotPressure);

        var gitFileHistory = await context.GetArtifactAsync<GitFileHistoryArtifact>(cancellationToken)
            .ConfigureAwait(false);
        if (gitFileHistory is null)
        {
            sink.SetValue(MetricIds.RefactorPriorityPointsV0, basePriority);
            return;
        }

        var changePressure =
            100d * (
                (0.50d * Normalize(gitFileHistory.ChurnLines90d, good: 20d, bad: 400d)) +
                (0.35d * Normalize(gitFileHistory.TouchCount90d, good: 1d, bad: 12d)) +
                (0.15d * Normalize(gitFileHistory.AuthorCount90d, good: 1d, bad: 4d)));
        var refactorPriority =
            (0.75d * basePriority) +
            (0.25d * changePressure);

        sink.SetValue(MetricIds.RefactorPriorityPointsV0, refactorPriority);
    }

    private static double Normalize(double value, double good, double bad)
    {
        if (bad <= good)
        {
            throw new ArgumentOutOfRangeException(nameof(bad), "Normalization requires bad > good.");
        }

        return Math.Clamp((value - good) / (bad - good), 0d, 1d);
    }
}
