using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Core.Paths;

namespace Clever.TokenMap.Infrastructure.Caching;

public sealed class InMemoryCacheStore : ICacheStore
{
    private readonly ConcurrentDictionary<CacheKey, MetricSet> _entries;
    private readonly PathNormalizer _pathNormalizer;

    public InMemoryCacheStore(PathNormalizer? pathNormalizer = null)
    {
        _pathNormalizer = pathNormalizer ?? new PathNormalizer();
        _entries = new ConcurrentDictionary<CacheKey, MetricSet>(new CacheKeyComparer(_pathNormalizer.PathComparer));
    }

    public ValueTask<MetricSet?> TryGetFileMetricsAsync(
        string rootPath,
        string relativePath,
        long fileSizeBytes,
        DateTimeOffset lastWriteTimeUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var key = CreateKey(rootPath, relativePath, fileSizeBytes, lastWriteTimeUtc);
        return ValueTask.FromResult(_entries.TryGetValue(key, out var metrics) ? metrics : null);
    }

    public ValueTask SetFileMetricsAsync(
        string rootPath,
        string relativePath,
        long fileSizeBytes,
        DateTimeOffset lastWriteTimeUtc,
        MetricSet metrics,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        cancellationToken.ThrowIfCancellationRequested();

        var key = CreateKey(rootPath, relativePath, fileSizeBytes, lastWriteTimeUtc);
        _entries[key] = metrics;

        return ValueTask.CompletedTask;
    }

    private CacheKey CreateKey(
        string rootPath,
        string relativePath,
        long fileSizeBytes,
        DateTimeOffset lastWriteTimeUtc) =>
        new(
            _pathNormalizer.NormalizeRootPath(rootPath),
            PathNormalizer.NormalizeRelativePath(relativePath),
            fileSizeBytes,
            lastWriteTimeUtc);

    private readonly record struct CacheKey(
        string RootPath,
        string RelativePath,
        long FileSizeBytes,
        DateTimeOffset LastWriteTimeUtc);

    private sealed class CacheKeyComparer(StringComparer pathComparer) : IEqualityComparer<CacheKey>
    {
        public bool Equals(CacheKey x, CacheKey y) =>
            pathComparer.Equals(x.RootPath, y.RootPath) &&
            pathComparer.Equals(x.RelativePath, y.RelativePath) &&
            x.FileSizeBytes == y.FileSizeBytes &&
            x.LastWriteTimeUtc.Equals(y.LastWriteTimeUtc);

        public int GetHashCode([DisallowNull] CacheKey obj)
        {
            var hash = new HashCode();
            hash.Add(obj.RootPath, pathComparer);
            hash.Add(obj.RelativePath, pathComparer);
            hash.Add(obj.FileSizeBytes);
            hash.Add(obj.LastWriteTimeUtc);
            return hash.ToHashCode();
        }
    }
}
