using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.Metrics;

public interface IFileDerivedMetricCalculator
{
    int Order { get; }

    ValueTask ComputeAsync(
        IFileMetricContext context,
        MetricSet inputMetrics,
        IMetricSink sink,
        CancellationToken cancellationToken);
}
