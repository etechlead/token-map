using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.Metrics;

public interface IFileMetricCalculator
{
    int Order { get; }

    IReadOnlyCollection<MetricId> Outputs { get; }

    ValueTask ComputeAsync(
        IFileMetricContext context,
        IMetricSink sink,
        CancellationToken cancellationToken);
}
