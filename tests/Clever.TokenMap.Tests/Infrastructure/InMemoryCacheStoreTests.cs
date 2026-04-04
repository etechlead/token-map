using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Infrastructure.Caching;
using Clever.TokenMap.Tests.Support;

namespace Clever.TokenMap.Tests.Infrastructure;

public sealed class InMemoryCacheStoreTests
{
    [Fact]
    public async Task Cache_IsPartitionedByRootAndUsesRelativePathKeys()
    {
        var store = new InMemoryCacheStore();
        var metrics = MetricTestData.CreateComputedMetrics(tokens: 12, nonEmptyLines: 3, fileSizeBytes: 1024);
        var timestamp = DateTimeOffset.UtcNow;
        var rootPathA = TestPaths.Folder("RepoA");
        var rootPathB = TestPaths.Folder("RepoB");
        var relativePath = TestPaths.Relative("src", "Program.cs");

        await store.SetFileMetricsAsync(
            rootPathA,
            relativePath,
            1024,
            timestamp,
            contextFingerprint: null,
            metrics,
            CancellationToken.None);

        var sameRootHit = await store.TryGetFileMetricsAsync(
            $"{rootPathA}{Path.DirectorySeparatorChar}",
            "src/Program.cs",
            1024,
            timestamp,
            contextFingerprint: null,
            CancellationToken.None);

        var otherRootMiss = await store.TryGetFileMetricsAsync(
            rootPathB,
            "src/Program.cs",
            1024,
            timestamp,
            contextFingerprint: null,
            CancellationToken.None);

        Assert.Equal(metrics.Values, sameRootHit?.Values);
        Assert.Null(otherRootMiss);
    }

    [Fact]
    public async Task Cache_IsPartitionedByContextFingerprint()
    {
        var store = new InMemoryCacheStore();
        var metrics = MetricTestData.CreateComputedMetrics(tokens: 12, nonEmptyLines: 3, fileSizeBytes: 1024);
        var timestamp = DateTimeOffset.UtcNow;
        var rootPath = TestPaths.Folder("RepoA");
        var relativePath = TestPaths.Relative("src", "Program.cs");

        await store.SetFileMetricsAsync(
            rootPath,
            relativePath,
            1024,
            timestamp,
            contextFingerprint: "head-a|git90d-v0",
            metrics,
            CancellationToken.None);

        var sameFingerprintHit = await store.TryGetFileMetricsAsync(
            rootPath,
            relativePath,
            1024,
            timestamp,
            contextFingerprint: "head-a|git90d-v0",
            CancellationToken.None);
        var differentFingerprintMiss = await store.TryGetFileMetricsAsync(
            rootPath,
            relativePath,
            1024,
            timestamp,
            contextFingerprint: "head-b|git90d-v0",
            CancellationToken.None);

        Assert.Equal(metrics.Values, sameFingerprintHit?.Values);
        Assert.Null(differentFingerprintMiss);
    }
}
