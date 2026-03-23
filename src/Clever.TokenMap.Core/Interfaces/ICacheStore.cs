using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Core.Interfaces;

public interface ICacheStore
{
    ValueTask<NodeMetrics?> TryGetFileMetricsAsync(
        string fullPath,
        long fileSizeBytes,
        DateTimeOffset lastWriteTimeUtc,
        CancellationToken cancellationToken);

    ValueTask SetFileMetricsAsync(
        string fullPath,
        long fileSizeBytes,
        DateTimeOffset lastWriteTimeUtc,
        NodeMetrics metrics,
        CancellationToken cancellationToken);
}
