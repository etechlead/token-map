using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Infrastructure.Analysis;
using Clever.TokenMap.Infrastructure.Scanning;
using Clever.TokenMap.Infrastructure.Text;

namespace Clever.TokenMap.Tests.Infrastructure;

public sealed class ProjectSnapshotMetricsEnricherTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), $"tokenmap-metrics-{Guid.NewGuid():N}");

    public ProjectSnapshotMetricsEnricherTests()
    {
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public async Task EnrichAsync_CountsLinesLocallyAndAggregatesDirectories()
    {
        Directory.CreateDirectory(Path.Combine(_rootPath, "src"));
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "src", "Program.cs"), "one\r\n\r\n  \r\ntwo");
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "notes.txt"), "hello\rworld");
        await File.WriteAllBytesAsync(Path.Combine(_rootPath, "image.bin"), [0x42, 0x00, 0x43]);

        var scanner = new FileSystemProjectScanner();
        var snapshot = await scanner.ScanAsync(_rootPath, ScanOptions.Default, progress: null, CancellationToken.None);
        var tokenCounter = new RecordingTokenCounter();
        var enricher = new ProjectSnapshotMetricsEnricher(
            new HeuristicTextFileDetector(),
            tokenCounter);

        var enriched = await enricher.EnrichAsync(snapshot, CancellationToken.None);

        var srcNode = Assert.Single(enriched.Root.Children, node => node.Name == "src");
        var programNode = Assert.Single(srcNode.Children);
        var notesNode = Assert.Single(enriched.Root.Children, node => node.Name == "notes.txt");
        var imageNode = Assert.Single(enriched.Root.Children, node => node.Name == "image.bin");

        Assert.Equal(
            ["hello\nworld", "one\n\n  \ntwo"],
            tokenCounter.GetSeenContents().OrderBy(content => content, StringComparer.Ordinal).ToArray());

        Assert.Equal(11, programNode.Metrics.Tokens);
        Assert.Equal(2, programNode.Metrics.NonEmptyLines);

        Assert.Equal(11, notesNode.Metrics.Tokens);
        Assert.Equal(2, notesNode.Metrics.NonEmptyLines);

        Assert.Equal(SkippedReason.Binary, imageNode.SkippedReason);
        Assert.Equal(0, imageNode.Metrics.Tokens);
        Assert.Equal(0, imageNode.Metrics.NonEmptyLines);

        Assert.Equal(22, enriched.Root.Metrics.Tokens);
        Assert.Equal(4, enriched.Root.Metrics.NonEmptyLines);
        Assert.Equal(3, enriched.Root.Metrics.DescendantFileCount);
        Assert.Equal(1, enriched.Root.Metrics.DescendantDirectoryCount);

        Assert.Equal(11, srcNode.Metrics.Tokens);
        Assert.Equal(2, srcNode.Metrics.NonEmptyLines);
        Assert.Equal(1, srcNode.Metrics.DescendantFileCount);
        Assert.Equal(0, srcNode.Metrics.DescendantDirectoryCount);
    }

    [Fact]
    public async Task EnrichAsync_ConvertsMissingFilesIntoWarningsWithoutFailingAnalysis()
    {
        var filePath = Path.Combine(_rootPath, "gone.txt");
        await File.WriteAllTextAsync(filePath, "temporary");

        var scanner = new FileSystemProjectScanner();
        var snapshot = await scanner.ScanAsync(_rootPath, ScanOptions.Default, progress: null, CancellationToken.None);
        File.Delete(filePath);

        var enricher = new ProjectSnapshotMetricsEnricher(
            new HeuristicTextFileDetector(),
            new RecordingTokenCounter());

        var enriched = await enricher.EnrichAsync(snapshot, CancellationToken.None);
        var fileNode = Assert.Single(enriched.Root.Children);

        Assert.Equal(SkippedReason.MissingDuringScan, fileNode.SkippedReason);
        Assert.Contains(
            enriched.Diagnostics,
            issue => issue.Context.TryGetValue("FullPath", out var fullPath) &&
                     fullPath.Contains("gone.txt", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, enriched.Root.Metrics.DescendantFileCount);
        Assert.Equal(0, enriched.Root.Metrics.Tokens);
        Assert.Equal(0, enriched.Root.Metrics.NonEmptyLines);
    }

    [Fact]
    public async Task EnrichAsync_CountsBlankWhitespaceOnlyAndTrailingLines()
    {
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "sample.txt"), "alpha\n \n\t\nbeta\n");
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "empty.txt"), string.Empty);

        var scanner = new FileSystemProjectScanner();
        var snapshot = await scanner.ScanAsync(_rootPath, ScanOptions.Default, progress: null, CancellationToken.None);
        var enricher = new ProjectSnapshotMetricsEnricher(
            new HeuristicTextFileDetector(),
            new RecordingTokenCounter());

        var enriched = await enricher.EnrichAsync(snapshot, CancellationToken.None);
        var sampleNode = Assert.Single(enriched.Root.Children, node => node.Name == "sample.txt");
        var emptyNode = Assert.Single(enriched.Root.Children, node => node.Name == "empty.txt");

        Assert.Equal(2, sampleNode.Metrics.NonEmptyLines);

        Assert.Equal(0, emptyNode.Metrics.NonEmptyLines);
    }

    [Fact]
    public async Task EnrichAsync_DoesNotMutateTheOriginalScannedTree()
    {
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "sample.txt"), "alpha\nbeta\n");

        var scanner = new FileSystemProjectScanner();
        var snapshot = await scanner.ScanAsync(_rootPath, ScanOptions.Default, progress: null, CancellationToken.None);
        var originalRoot = snapshot.Root;
        var originalChild = Assert.Single(originalRoot.Children);
        var enricher = new ProjectSnapshotMetricsEnricher(
            new HeuristicTextFileDetector(),
            new RecordingTokenCounter());

        var enriched = await enricher.EnrichAsync(snapshot, CancellationToken.None);

        Assert.Same(originalRoot, snapshot.Root);
        Assert.Same(originalChild, snapshot.Root.Children[0]);
        Assert.Equal(NodeMetrics.Empty, originalRoot.Metrics);
        Assert.Null(originalChild.SkippedReason);
        Assert.NotSame(originalRoot, enriched.Root);
        Assert.NotSame(originalChild, enriched.Root.Children[0]);
        Assert.Equal(NodeMetrics.Empty, originalChild.Metrics);
        Assert.Equal(2, enriched.Root.Metrics.NonEmptyLines);
        Assert.Equal(1, enriched.Root.Metrics.DescendantFileCount);
    }

    [Fact]
    public async Task EnrichAsync_ProcessesLargeFilesInChunks_WhilePreservingNormalizedContent()
    {
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "sample.txt"), "ab\r\ncd\r\nef");

        var scanner = new FileSystemProjectScanner();
        var snapshot = await scanner.ScanAsync(_rootPath, ScanOptions.Default, progress: null, CancellationToken.None);
        var tokenCounter = new RecordingTokenCounter();
        var enricher = new ProjectSnapshotMetricsEnricher(
            new AlwaysTextDetector(),
            tokenCounter,
            largeFileTokenizationThresholdBytes: 1,
            largeFileChunkSizeChars: 3);

        var enriched = await enricher.EnrichAsync(snapshot, CancellationToken.None);
        var sampleNode = Assert.Single(enriched.Root.Children);

        var seenContents = tokenCounter.GetSeenContents();
        Assert.True(seenContents.Count > 1);
        Assert.All(seenContents, chunk => Assert.True(chunk.Length <= 3));
        Assert.Equal("ab\ncd\nef", string.Concat(seenContents));
        Assert.Equal(8, sampleNode.Metrics.Tokens);
        Assert.Equal(3, sampleNode.Metrics.NonEmptyLines);
    }

    [Fact]
    public async Task EnrichAsync_ProcessesSingleLongLineAcrossChunks_AsOneNonEmptyLine()
    {
        const string content = "abcdefghijklmnopqrstuvwxyz";
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "sample.txt"), content);

        var scanner = new FileSystemProjectScanner();
        var snapshot = await scanner.ScanAsync(_rootPath, ScanOptions.Default, progress: null, CancellationToken.None);
        var tokenCounter = new RecordingTokenCounter();
        var enricher = new ProjectSnapshotMetricsEnricher(
            new AlwaysTextDetector(),
            tokenCounter,
            largeFileTokenizationThresholdBytes: 1,
            largeFileChunkSizeChars: 5);

        var enriched = await enricher.EnrichAsync(snapshot, CancellationToken.None);
        var sampleNode = Assert.Single(enriched.Root.Children);

        var seenContents = tokenCounter.GetSeenContents();
        Assert.True(seenContents.Count > 1);
        Assert.Equal(content, string.Concat(seenContents));
        Assert.Equal(content.Length, sampleNode.Metrics.Tokens);
        Assert.Equal(1, sampleNode.Metrics.NonEmptyLines);
    }

    [Fact]
    public async Task EnrichAsync_ProcessesFilesInParallelUpToConfiguredLimit()
    {
        for (var index = 0; index < 4; index++)
        {
            await File.WriteAllTextAsync(Path.Combine(_rootPath, $"File{index}.txt"), $"content-{index}");
        }

        var scanner = new FileSystemProjectScanner();
        var snapshot = await scanner.ScanAsync(_rootPath, ScanOptions.Default, progress: null, CancellationToken.None);
        var tokenCounter = new ConcurrentTrackingTokenCounter();
        var enricher = new ProjectSnapshotMetricsEnricher(
            new AlwaysTextDetector(),
            tokenCounter,
            maxDegreeOfParallelism: 2);

        var enriched = await enricher.EnrichAsync(snapshot, CancellationToken.None);

        Assert.Equal(4, enriched.Root.Metrics.DescendantFileCount);
        Assert.Equal(2, tokenCounter.MaxConcurrency);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private sealed class RecordingTokenCounter : ITokenCounter
    {
        private readonly object _gate = new();
        private readonly List<string> _seenContents = [];

        public ValueTask<int> CountTokensAsync(string content, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _seenContents.Add(content);
            }

            return ValueTask.FromResult(content.Length);
        }

        public IReadOnlyList<string> GetSeenContents()
        {
            lock (_gate)
            {
                return _seenContents.ToArray();
            }
        }
    }

    private sealed class AlwaysTextDetector : ITextFileDetector
    {
        public ValueTask<bool> IsTextAsync(string fullPath, CancellationToken cancellationToken) =>
            ValueTask.FromResult(true);
    }

    private sealed class ConcurrentTrackingTokenCounter : ITokenCounter
    {
        private int _currentConcurrency;
        private int _maxConcurrency;

        public int MaxConcurrency => Volatile.Read(ref _maxConcurrency);

        public async ValueTask<int> CountTokensAsync(string content, CancellationToken cancellationToken)
        {
            var currentConcurrency = Interlocked.Increment(ref _currentConcurrency);
            UpdateMaxConcurrency(currentConcurrency);

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                return content.Length;
            }
            finally
            {
                Interlocked.Decrement(ref _currentConcurrency);
            }
        }

        private void UpdateMaxConcurrency(int currentConcurrency)
        {
            while (true)
            {
                var snapshot = Volatile.Read(ref _maxConcurrency);
                if (snapshot >= currentConcurrency)
                {
                    return;
                }

                if (Interlocked.CompareExchange(ref _maxConcurrency, currentConcurrency, snapshot) == snapshot)
                {
                    return;
                }
            }
        }
    }
}
