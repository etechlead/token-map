using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Core.Interfaces;

public interface ICacheStore
{
    ValueTask<NodeMetrics?> TryGetFileMetricsAsync(
        string fullPath,
        long fileSizeBytes,
        DateTimeOffset lastWriteTimeUtc,
        TokenProfile tokenProfile,
        CancellationToken cancellationToken);

    ValueTask SetFileMetricsAsync(
        string fullPath,
        long fileSizeBytes,
        DateTimeOffset lastWriteTimeUtc,
        TokenProfile tokenProfile,
        NodeMetrics metrics,
        CancellationToken cancellationToken);
}
