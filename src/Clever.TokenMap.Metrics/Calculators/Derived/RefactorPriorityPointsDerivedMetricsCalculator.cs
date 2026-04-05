using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Core.Metrics.Formulas;
using Clever.TokenMap.Core.Analysis.Git;

namespace Clever.TokenMap.Metrics.Calculators.Derived;

public sealed class RefactorPriorityPointsDerivedMetricsCalculator : IFileDerivedMetricCalculator
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

        var effectiveMetrics = inputMetrics;
        if (!HasGitContextMetrics(inputMetrics))
        {
            var gitFileHistoryArtifact = await context.GetArtifactAsync<GitFileHistoryArtifact>(cancellationToken)
                .ConfigureAwait(false);
            if (gitFileHistoryArtifact is not null)
            {
                var builder = new MetricSetBuilder(inputMetrics);
                builder.SetValue(MetricIds.ChurnLines90d, gitFileHistoryArtifact.ChurnLines90d);
                builder.SetValue(MetricIds.TouchCount90d, gitFileHistoryArtifact.TouchCount90d);
                builder.SetValue(MetricIds.AuthorCount90d, gitFileHistoryArtifact.AuthorCount90d);
                builder.SetValue(MetricIds.UniqueCochangedFileCount90d, gitFileHistoryArtifact.UniqueCochangedFileCount90d);
                builder.SetValue(MetricIds.StrongCochangedFileCount90d, gitFileHistoryArtifact.StrongCochangedFileCount90d);
                builder.SetValue(MetricIds.AverageCochangeSetSize90d, gitFileHistoryArtifact.AverageCochangeSetSize90d);
                effectiveMetrics = builder.Build();
            }
        }

        if (!ProductMetricFormulas.TryComputeRefactorPriority(effectiveMetrics, out var breakdown))
        {
            sink.SetNotApplicable(MetricIds.RefactorPriorityPoints);
            return;
        }

        sink.SetValue(MetricIds.RefactorPriorityPoints, breakdown.TotalPoints);
    }

    private static bool HasGitContextMetrics(MetricSet metrics) =>
        metrics.TryGetNumber(MetricIds.ChurnLines90d).HasValue &&
        metrics.TryGetNumber(MetricIds.TouchCount90d).HasValue &&
        metrics.TryGetNumber(MetricIds.AuthorCount90d).HasValue &&
        metrics.TryGetNumber(MetricIds.UniqueCochangedFileCount90d).HasValue &&
        metrics.TryGetNumber(MetricIds.StrongCochangedFileCount90d).HasValue &&
        metrics.TryGetNumber(MetricIds.AverageCochangeSetSize90d).HasValue;
}
