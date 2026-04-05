using Clever.TokenMap.Core.Analysis.Git;
using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.Metrics.Calculators;

public sealed class GitHistoryMetricsCalculator : IFileMetricCalculator
{
    public int Order => 300;

    public async ValueTask ComputeAsync(
        IFileMetricContext context,
        IMetricSink sink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sink);

        var gitFileHistory = await context.GetArtifactAsync<GitFileHistoryArtifact>(cancellationToken).ConfigureAwait(false);
        if (gitFileHistory is null)
        {
            return;
        }

        sink.SetValue(MetricIds.ChurnLines90d, gitFileHistory.ChurnLines90d);
        sink.SetValue(MetricIds.TouchCount90d, gitFileHistory.TouchCount90d);
        sink.SetValue(MetricIds.AuthorCount90d, gitFileHistory.AuthorCount90d);
        sink.SetValue(MetricIds.UniqueCochangedFileCount90d, gitFileHistory.UniqueCochangedFileCount90d);
        sink.SetValue(MetricIds.StrongCochangedFileCount90d, gitFileHistory.StrongCochangedFileCount90d);
        sink.SetValue(MetricIds.AverageCochangeSetSize90d, gitFileHistory.AverageCochangeSetSize90d);
    }
}
