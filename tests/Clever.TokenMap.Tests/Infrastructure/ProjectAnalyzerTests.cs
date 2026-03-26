using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Infrastructure.Analysis;
using Clever.TokenMap.Infrastructure.Caching;
using Clever.TokenMap.Infrastructure.Scanning;

namespace Clever.TokenMap.Tests.Infrastructure;

public sealed class ProjectAnalyzerTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), $"tokenmap-analyzer-{Guid.NewGuid():N}");

    public ProjectAnalyzerTests()
    {
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public async Task AnalyzeAsync_UsesCacheOnRepeatedRun()
    {
        var filePath = Path.Combine(_rootPath, "Program.cs");
        await File.WriteAllTextAsync(filePath, "alpha");

        var tokenCounter = new RecordingTokenCounter();
        var analyzer = CreateAnalyzer(tokenCounter, new InMemoryCacheStore());

        var first = await analyzer.AnalyzeAsync(_rootPath, ScanOptions.Default, progress: null, CancellationToken.None);
        var second = await analyzer.AnalyzeAsync(_rootPath, ScanOptions.Default, progress: null, CancellationToken.None);

        Assert.Equal(1, tokenCounter.CallCount);
        Assert.Equal(first.Root.Metrics.Tokens, second.Root.Metrics.Tokens);
        Assert.Equal(5, second.Root.Metrics.Tokens);
    }

    [Fact]
    public async Task AnalyzeAsync_DoesNotCrossPollinateCacheBetweenRootsWithSameRelativePath()
    {
        var rootA = Path.Combine(_rootPath, "RepoA");
        var rootB = Path.Combine(_rootPath, "RepoB");
        Directory.CreateDirectory(rootA);
        Directory.CreateDirectory(rootB);
        await File.WriteAllTextAsync(Path.Combine(rootA, "Program.cs"), "alpha");
        await File.WriteAllTextAsync(Path.Combine(rootB, "Program.cs"), "beta gamma");

        var tokenCounter = new RecordingTokenCounter();
        var cacheStore = new InMemoryCacheStore();
        var analyzer = CreateAnalyzer(tokenCounter, cacheStore);

        var first = await analyzer.AnalyzeAsync(rootA, ScanOptions.Default, progress: null, CancellationToken.None);
        var second = await analyzer.AnalyzeAsync(rootB, ScanOptions.Default, progress: null, CancellationToken.None);

        Assert.Equal(2, tokenCounter.CallCount);
        Assert.Equal(5, first.Root.Metrics.Tokens);
        Assert.Equal(10, second.Root.Metrics.Tokens);
    }

    [Fact]
    public async Task AnalyzeAsync_InvalidatesCacheWhenFileChanges()
    {
        var filePath = Path.Combine(_rootPath, "Program.cs");
        await File.WriteAllTextAsync(filePath, "alpha");

        var tokenCounter = new RecordingTokenCounter();
        var analyzer = CreateAnalyzer(tokenCounter, new InMemoryCacheStore());

        var first = await analyzer.AnalyzeAsync(_rootPath, ScanOptions.Default, progress: null, CancellationToken.None);

        await File.WriteAllTextAsync(filePath, "alpha beta");
        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddSeconds(1));

        var second = await analyzer.AnalyzeAsync(_rootPath, ScanOptions.Default, progress: null, CancellationToken.None);

        Assert.Equal(2, tokenCounter.CallCount);
        Assert.Equal(5, first.Root.Metrics.Tokens);
        Assert.Equal(10, second.Root.Metrics.Tokens);
    }

    [Fact]
    public async Task AnalyzeAsync_ReportsProgressInBatches()
    {
        for (var index = 0; index < 7; index++)
        {
            await File.WriteAllTextAsync(Path.Combine(_rootPath, $"File{index}.txt"), $"content-{index}");
        }

        var progress = new CapturingProgress();
        var analyzer = new ProjectAnalyzer(
            new FileSystemProjectScanner(),
            new AlwaysTextDetector(),
            new RecordingTokenCounter(),
            cacheStore: new InMemoryCacheStore(),
            progressBatchSize: 3);

        var snapshot = await analyzer.AnalyzeAsync(_rootPath, ScanOptions.Default, progress, CancellationToken.None);

        var progressEvents = progress.GetSnapshot();

        Assert.Equal(7, snapshot.Root.Metrics.DescendantFileCount);
        Assert.Contains(progressEvents, value => value.Phase == "Initializing");
        Assert.Contains(progressEvents, value => value.Phase == "ScanningTree");
        Assert.Contains(progressEvents, value => value.Phase == "AnalyzingFiles");
        Assert.Contains(progressEvents, value => value.Phase == "Completed");
        Assert.True(progressEvents.Count < 16);
    }

    [Fact]
    public async Task AnalyzeAsync_HonorsCancellationDuringMetricsStage()
    {
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "Program.cs"), "alpha");

        var analyzer = new ProjectAnalyzer(
            new FileSystemProjectScanner(),
            new AlwaysTextDetector(),
            new BlockingTokenCounter(),
            cacheStore: null,
            progressBatchSize: 2);

        using var cancellationTokenSource = new CancellationTokenSource();
        var analyzeTask = analyzer.AnalyzeAsync(_rootPath, ScanOptions.Default, progress: null, cancellationTokenSource.Token);

        cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await analyzeTask);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private ProjectAnalyzer CreateAnalyzer(ITokenCounter tokenCounter, ICacheStore cacheStore) =>
        new(
            new FileSystemProjectScanner(),
            new AlwaysTextDetector(),
            tokenCounter,
            cacheStore,
            progressBatchSize: 2);

    private sealed class AlwaysTextDetector : ITextFileDetector
    {
        public ValueTask<bool> IsTextAsync(string fullPath, CancellationToken cancellationToken) =>
            ValueTask.FromResult(true);
    }

    private sealed class RecordingTokenCounter : ITokenCounter
    {
        public int CallCount { get; private set; }

        public ValueTask<int> CountTokensAsync(string content, CancellationToken cancellationToken)
        {
            CallCount++;
            return ValueTask.FromResult(content.Length);
        }
    }

    private sealed class BlockingTokenCounter : ITokenCounter
    {
        public async ValueTask<int> CountTokensAsync(string content, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            return content.Length;
        }
    }

    private sealed class CapturingProgress : IProgress<AnalysisProgress>
    {
        private readonly object _gate = new();
        private readonly List<AnalysisProgress> _events = [];

        public void Report(AnalysisProgress value)
        {
            lock (_gate)
            {
                _events.Add(value);
            }
        }

        public IReadOnlyList<AnalysisProgress> GetSnapshot()
        {
            lock (_gate)
            {
                return _events.ToArray();
            }
        }
    }
}
