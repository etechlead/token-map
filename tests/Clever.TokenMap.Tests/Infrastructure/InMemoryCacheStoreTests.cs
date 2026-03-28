using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Infrastructure.Caching;
using Clever.TokenMap.Tests.Support;

namespace Clever.TokenMap.Tests.Infrastructure;

public sealed class InMemoryCacheStoreTests
{
    [Fact]
    public async Task Cache_IsPartitionedByRootAndUsesRelativePathKeys()
    {
        var store = new InMemoryCacheStore();
        var metrics = new NodeMetrics(12, 3, 1024, 1, 0);
        var timestamp = DateTimeOffset.UtcNow;
        var rootPathA = TestPaths.Folder("RepoA");
        var rootPathB = TestPaths.Folder("RepoB");
        var relativePath = TestPaths.Relative("src", "Program.cs");

        await store.SetFileMetricsAsync(
            rootPathA,
            relativePath,
            1024,
            timestamp,
            metrics,
            CancellationToken.None);

        var sameRootHit = await store.TryGetFileMetricsAsync(
            $"{rootPathA}{Path.DirectorySeparatorChar}",
            "src/Program.cs",
            1024,
            timestamp,
            CancellationToken.None);

        var otherRootMiss = await store.TryGetFileMetricsAsync(
            rootPathB,
            "src/Program.cs",
            1024,
            timestamp,
            CancellationToken.None);

        Assert.Equal(metrics, sameRootHit);
        Assert.Null(otherRootMiss);
    }
}
