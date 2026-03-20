using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Clever.TokenMap.Core.Enums;
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
        TokenProfile tokenProfile,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var key = CreateKey(fullPath, fileSizeBytes, lastWriteTimeUtc, tokenProfile);
        return ValueTask.FromResult(_entries.TryGetValue(key, out var metrics) ? metrics : null);
    }

    public ValueTask SetFileMetricsAsync(
        string fullPath,
        long fileSizeBytes,
        DateTimeOffset lastWriteTimeUtc,
        TokenProfile tokenProfile,
        NodeMetrics metrics,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        cancellationToken.ThrowIfCancellationRequested();

        var key = CreateKey(fullPath, fileSizeBytes, lastWriteTimeUtc, tokenProfile);
        _entries[key] = metrics;

        return ValueTask.CompletedTask;
    }

    private CacheKey CreateKey(
        string fullPath,
        long fileSizeBytes,
        DateTimeOffset lastWriteTimeUtc,
        TokenProfile tokenProfile) =>
        new(
            _pathNormalizer.NormalizeFullPath(fullPath),
            fileSizeBytes,
            lastWriteTimeUtc,
            tokenProfile);

    private readonly record struct CacheKey(
        string FullPath,
        long FileSizeBytes,
        DateTimeOffset LastWriteTimeUtc,
        TokenProfile TokenProfile);

    private sealed class CacheKeyComparer(StringComparer pathComparer) : IEqualityComparer<CacheKey>
    {
        public bool Equals(CacheKey x, CacheKey y) =>
            pathComparer.Equals(x.FullPath, y.FullPath) &&
            x.FileSizeBytes == y.FileSizeBytes &&
            x.LastWriteTimeUtc.Equals(y.LastWriteTimeUtc) &&
            x.TokenProfile == y.TokenProfile;

        public int GetHashCode([DisallowNull] CacheKey obj)
        {
            var hash = new HashCode();
            hash.Add(obj.FullPath, pathComparer);
            hash.Add(obj.FileSizeBytes);
            hash.Add(obj.LastWriteTimeUtc);
            hash.Add((int)obj.TokenProfile);
            return hash.ToHashCode();
        }
    }
}
