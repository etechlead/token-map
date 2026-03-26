using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Infrastructure.Caching;

namespace Clever.TokenMap.Tests.Infrastructure;

public sealed class InMemoryCacheStoreTests
{
    [Fact]
    public async Task Cache_IsPartitionedByRootAndUsesRelativePathKeys()
    {
        var store = new InMemoryCacheStore();
        var metrics = new NodeMetrics(12, 3, 1024, 1, 0);
        var timestamp = DateTimeOffset.UtcNow;

        await store.SetFileMetricsAsync(
            @"C:\RepoA",
            @"src\Program.cs",
            1024,
            timestamp,
            metrics,
            CancellationToken.None);

        var sameRootHit = await store.TryGetFileMetricsAsync(
            @"C:\RepoA\",
            "src/Program.cs",
            1024,
            timestamp,
            CancellationToken.None);

        var otherRootMiss = await store.TryGetFileMetricsAsync(
            @"C:\RepoB",
            "src/Program.cs",
            1024,
            timestamp,
            CancellationToken.None);

        Assert.Equal(metrics, sameRootHit);
        Assert.Null(otherRootMiss);
    }
}
