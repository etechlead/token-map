using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Infrastructure.Paths;

namespace Clever.TokenMap.Infrastructure.Caching;

public sealed class InMemoryCacheStore : ICacheStore
{
    private readonly ConcurrentDictionary<CacheKey, NodeMetrics> _entries;
    private readonly PathNormalizer _pathNormalizer;

    public InMemoryCacheStore(PathNormalizer? pathNormalizer = null)
    {
        _pathNormalizer = pathNormalizer ?? new PathNormalizer();
        _entries = new ConcurrentDictionary<CacheKey, NodeMetrics>(new CacheKeyComparer(_pathNormalizer.PathComparer));
    }

    public ValueTask<NodeMetrics?> TryGetFileMetricsAsync(
        string fullPath,
        long fileSizeBytes,
        DateTimeOffset lastWriteTimeUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var key = CreateKey(fullPath, fileSizeBytes, lastWriteTimeUtc);
        return ValueTask.FromResult(_entries.TryGetValue(key, out var metrics) ? metrics : null);
    }

    public ValueTask SetFileMetricsAsync(
        string fullPath,
        long fileSizeBytes,
        DateTimeOffset lastWriteTimeUtc,
        NodeMetrics metrics,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        cancellationToken.ThrowIfCancellationRequested();

        var key = CreateKey(fullPath, fileSizeBytes, lastWriteTimeUtc);
        _entries[key] = metrics;

        return ValueTask.CompletedTask;
    }

    private CacheKey CreateKey(
        string fullPath,
        long fileSizeBytes,
        DateTimeOffset lastWriteTimeUtc) =>
        new(
            _pathNormalizer.NormalizeFullPath(fullPath),
            fileSizeBytes,
            lastWriteTimeUtc);

    private readonly record struct CacheKey(
        string FullPath,
        long FileSizeBytes,
        DateTimeOffset LastWriteTimeUtc);

    private sealed class CacheKeyComparer(StringComparer pathComparer) : IEqualityComparer<CacheKey>
    {
        public bool Equals(CacheKey x, CacheKey y) =>
            pathComparer.Equals(x.FullPath, y.FullPath) &&
            x.FileSizeBytes == y.FileSizeBytes &&
            x.LastWriteTimeUtc.Equals(y.LastWriteTimeUtc);

        public int GetHashCode([DisallowNull] CacheKey obj)
        {
            var hash = new HashCode();
            hash.Add(obj.FullPath, pathComparer);
            hash.Add(obj.FileSizeBytes);
            hash.Add(obj.LastWriteTimeUtc);
            return hash.ToHashCode();
        }
    }
}
