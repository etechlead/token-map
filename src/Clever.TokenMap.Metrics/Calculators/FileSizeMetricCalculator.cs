using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.Metrics.Calculators;

public sealed class FileSizeMetricCalculator : IFileMetricCalculator
{
    public int Order => 0;

    public ValueTask ComputeAsync(
        IFileMetricContext context,
        IMetricSink sink,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        sink.SetValue(MetricIds.FileSizeBytes, context.FileSizeBytes);
        return ValueTask.CompletedTask;
    }
}
