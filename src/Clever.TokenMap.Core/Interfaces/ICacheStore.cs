using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Core.Interfaces;

public interface ICacheStore
{
    ValueTask<NodeMetrics?> TryGetFileMetricsAsync(
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
        NodeMetrics metrics,
        CancellationToken cancellationToken);
}
