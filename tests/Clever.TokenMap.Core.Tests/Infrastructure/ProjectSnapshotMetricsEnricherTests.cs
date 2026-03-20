using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Infrastructure.Analysis;
using Clever.TokenMap.Infrastructure.Scanning;
using Clever.TokenMap.Infrastructure.Text;

namespace Clever.TokenMap.Core.Tests.Infrastructure;

public sealed class ProjectSnapshotMetricsEnricherTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), $"tokenmap-metrics-{Guid.NewGuid():N}");

    public ProjectSnapshotMetricsEnricherTests()
    {
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public async Task EnrichAsync_MergesTokenCountsTokeiStatsAndAggregatesDirectories()
    {
        Directory.CreateDirectory(Path.Combine(_rootPath, "src"));
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "src", "Program.cs"), "one\r\ntwo");
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "notes.txt"), "hello\rworld");
        await File.WriteAllBytesAsync(Path.Combine(_rootPath, "image.bin"), [0x42, 0x00, 0x43]);

        var scanner = new FileSystemProjectScanner();
        var snapshot = await scanner.ScanAsync(_rootPath, ScanOptions.Default, progress: null, CancellationToken.None);
        var tokenCounter = new RecordingTokenCounter();
        var tokeiRunner = new StubTokeiRunner(new Dictionary<string, TokeiFileStats>(StringComparer.OrdinalIgnoreCase)
        {
            ["src/Program.cs"] = new()
            {
                RelativePath = "src/Program.cs",
                TotalLines = 5,
                CodeLines = 3,
                CommentLines = 1,
                BlankLines = 1,
                Language = "C#",
            },
        });
        var enricher = new ProjectSnapshotMetricsEnricher(
            new HeuristicTextFileDetector(),
            tokenCounter,
            tokeiRunner);

        var enriched = await enricher.EnrichAsync(snapshot, CancellationToken.None);

        var srcNode = Assert.Single(enriched.Root.Children, node => node.Name == "src");
        var programNode = Assert.Single(srcNode.Children);
        var notesNode = Assert.Single(enriched.Root.Children, node => node.Name == "notes.txt");
        var imageNode = Assert.Single(enriched.Root.Children, node => node.Name == "image.bin");

        Assert.Equal(["one\ntwo", "hello\nworld"], tokenCounter.SeenContents);

        Assert.Equal(7, programNode.Metrics.Tokens);
        Assert.Equal(5, programNode.Metrics.TotalLines);
        Assert.Equal(3, programNode.Metrics.CodeLines);
        Assert.Equal(1, programNode.Metrics.CommentLines);
        Assert.Equal(1, programNode.Metrics.BlankLines);
        Assert.Equal("C#", programNode.Metrics.Language);

        Assert.Equal(11, notesNode.Metrics.Tokens);
        Assert.Equal(2, notesNode.Metrics.TotalLines);
        Assert.Null(notesNode.Metrics.CodeLines);
        Assert.Null(notesNode.Metrics.Language);

        Assert.Equal(SkippedReason.Binary, imageNode.SkippedReason);
        Assert.Equal(0, imageNode.Metrics.Tokens);
        Assert.Equal(0, imageNode.Metrics.TotalLines);

        Assert.Equal(18, enriched.Root.Metrics.Tokens);
        Assert.Equal(7, enriched.Root.Metrics.TotalLines);
        Assert.Equal(3, enriched.Root.Metrics.CodeLines);
        Assert.Equal(1, enriched.Root.Metrics.CommentLines);
        Assert.Equal(1, enriched.Root.Metrics.BlankLines);
        Assert.Equal(3, enriched.Root.Metrics.DescendantFileCount);
        Assert.Equal(1, enriched.Root.Metrics.DescendantDirectoryCount);

        Assert.Equal(7, srcNode.Metrics.Tokens);
        Assert.Equal(5, srcNode.Metrics.TotalLines);
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
            new RecordingTokenCounter(),
            new StubTokeiRunner(new Dictionary<string, TokeiFileStats>()));

        var enriched = await enricher.EnrichAsync(snapshot, CancellationToken.None);
        var fileNode = Assert.Single(enriched.Root.Children);

        Assert.Equal(SkippedReason.MissingDuringScan, fileNode.SkippedReason);
        Assert.Contains(enriched.Warnings, warning => warning.Contains("gone.txt", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, enriched.Root.Metrics.DescendantFileCount);
        Assert.Equal(0, enriched.Root.Metrics.Tokens);
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
        public List<string> SeenContents { get; } = [];

        public ValueTask<int> CountTokensAsync(string content, TokenProfile tokenProfile, CancellationToken cancellationToken)
        {
            SeenContents.Add(content);
            return ValueTask.FromResult(content.Length);
        }
    }

    private sealed class StubTokeiRunner(IReadOnlyDictionary<string, TokeiFileStats> result) : ITokeiRunner
    {
        public Task<IReadOnlyDictionary<string, TokeiFileStats>> CollectAsync(
            string rootPath,
            IReadOnlyCollection<string> includedRelativePaths,
            CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }
}
