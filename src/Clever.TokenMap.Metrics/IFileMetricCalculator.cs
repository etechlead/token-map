using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.Metrics;

public interface IFileMetricCalculator
{
    int Order { get; }

    ValueTask ComputeAsync(
        IFileMetricContext context,
        IMetricSink sink,
        CancellationToken cancellationToken);
}
