using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.Core.Interfaces;

public interface ICacheStore
{
    ValueTask<MetricSet?> TryGetFileMetricsAsync(
        string rootPath,
        string relativePath,
        long fileSizeBytes,
        DateTimeOffset lastWriteTimeUtc,
        CancellationToken cancellationToken);

    ValueTask SetFileMetricsAsync(
        string rootPath,
        string relativePath,
        long fileSizeBytes,
        DateTimeOffset lastWriteTimeUtc,
        MetricSet metrics,
        CancellationToken cancellationToken);
}
