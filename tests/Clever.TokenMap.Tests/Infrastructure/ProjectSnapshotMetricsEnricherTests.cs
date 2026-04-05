using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Analysis.Git;
using Clever.TokenMap.Core.Analysis.Syntax;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Paths;
using Clever.TokenMap.Infrastructure.Analysis;
using Clever.TokenMap.Infrastructure.Analysis.Git;
using Clever.TokenMap.Infrastructure.Caching;
using Clever.TokenMap.Infrastructure.Scanning;
using Clever.TokenMap.Infrastructure.Text;
using Clever.TokenMap.Metrics;
using Clever.TokenMap.Metrics.Syntax;

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

        Assert.Equal(11L, programNode.ComputedMetrics.TryGetRoundedInt64(MetricIds.Tokens));
        Assert.Equal(2, programNode.ComputedMetrics.TryGetRoundedInt32(MetricIds.NonEmptyLines));
        Assert.Equal(1, programNode.Summary.DescendantFileCount);
        Assert.Equal(0, programNode.Summary.DescendantDirectoryCount);

        Assert.Equal(11L, notesNode.ComputedMetrics.TryGetRoundedInt64(MetricIds.Tokens));
        Assert.Equal(2, notesNode.ComputedMetrics.TryGetRoundedInt32(MetricIds.NonEmptyLines));

        Assert.Equal(SkippedReason.Binary, imageNode.SkippedReason);
        Assert.Null(imageNode.ComputedMetrics.TryGetRoundedInt64(MetricIds.Tokens));
        Assert.Null(imageNode.ComputedMetrics.TryGetRoundedInt32(MetricIds.NonEmptyLines));
        Assert.Equal(MetricStatus.NotApplicable, imageNode.ComputedMetrics.GetOrDefault(MetricIds.Tokens).Status);
        Assert.Equal(3L, imageNode.ComputedMetrics.TryGetRoundedInt64(MetricIds.FileSizeBytes));

        Assert.Equal(22L, enriched.Root.ComputedMetrics.TryGetRoundedInt64(MetricIds.Tokens));
        Assert.Equal(4, enriched.Root.ComputedMetrics.TryGetRoundedInt32(MetricIds.NonEmptyLines));
        Assert.Equal(3, enriched.Root.Summary.DescendantFileCount);
        Assert.Equal(1, enriched.Root.Summary.DescendantDirectoryCount);

        Assert.Equal(11L, srcNode.ComputedMetrics.TryGetRoundedInt64(MetricIds.Tokens));
        Assert.Equal(2, srcNode.ComputedMetrics.TryGetRoundedInt32(MetricIds.NonEmptyLines));
        Assert.Equal(1, srcNode.Summary.DescendantFileCount);
        Assert.Equal(0, srcNode.Summary.DescendantDirectoryCount);
    }

    [Fact]
    public async Task EnrichAsync_UsesSyntaxArtifactWhenAnalyzerExists()
    {
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "Program.cs"), "class Program { }");

        var scanner = new FileSystemProjectScanner();
        var snapshot = await scanner.ScanAsync(_rootPath, ScanOptions.Default, progress: null, CancellationToken.None);
        var syntaxArtifact = new SyntaxSummaryArtifact(
            LanguageId: "csharp",
            ParseQuality: SyntaxParseQuality.Full,
            CodeLineCount: 1,
            CyclomaticComplexitySum: 7,
            CyclomaticComplexityMax: 4,
            MaxNestingDepth: 2,
            Callables:
            [
                new CallableSyntaxFact("First", CallableKind.Method, new LineRange(1, 1), 2, 3, 1),
                new CallableSyntaxFact("Second", CallableKind.Method, new LineRange(2, 2), 5, 4, 2),
                new CallableSyntaxFact("Third", CallableKind.Method, new LineRange(3, 3), 1, 0, 0),
            ]);
        var enricher = new ProjectSnapshotMetricsEnricher(
            new HeuristicTextFileDetector(),
            new RecordingTokenCounter(),
            syntaxAnalyzerRegistry: new ExtensionSyntaxAnalyzerRegistry(
            [
                new StaticSyntaxAnalyzer(".cs", syntaxArtifact),
            ]));

        var enriched = await enricher.EnrichAsync(snapshot, CancellationToken.None);
        var programNode = Assert.Single(enriched.Root.Children);

        Assert.Equal(1, programNode.ComputedMetrics.TryGetRoundedInt32(MetricIds.CodeLines));
        Assert.Equal(5, programNode.ComputedMetrics.TryGetRoundedInt32(MetricIds.MaxParameterCount));
        Assert.Equal(7, programNode.ComputedMetrics.TryGetRoundedInt32(MetricIds.CyclomaticComplexitySum));
        Assert.Equal(4, programNode.ComputedMetrics.TryGetRoundedInt32(MetricIds.CyclomaticComplexityMax));
        Assert.Equal(2, programNode.ComputedMetrics.TryGetRoundedInt32(MetricIds.MaxNestingDepth));
        Assert.Equal(0, programNode.ComputedMetrics.TryGetRoundedInt32(MetricIds.LongCallableCount));
        Assert.Equal(0, programNode.ComputedMetrics.TryGetRoundedInt32(MetricIds.HighCyclomaticComplexityCallableCount));
        Assert.Equal(0, programNode.ComputedMetrics.TryGetRoundedInt32(MetricIds.DeepNestingCallableCount));
        Assert.Equal(1, programNode.ComputedMetrics.TryGetRoundedInt32(MetricIds.LongParameterListCount));
        Assert.Equal(1, programNode.ComputedMetrics.TryGetRoundedInt32(MetricIds.CallableHotspotPoints));
        Assert.Equal(15.682186234817814, programNode.ComputedMetrics.TryGetNumber(MetricIds.ComplexityPoints)!.Value, precision: 12);
        Assert.Equal(14.545748987854251, programNode.ComputedMetrics.TryGetNumber(MetricIds.RefactorPriorityPoints)!.Value, precision: 12);
        Assert.Equal(1, enriched.Root.ComputedMetrics.TryGetRoundedInt32(MetricIds.CallableHotspotPoints));
        Assert.Equal(15.682186234817814, enriched.Root.ComputedMetrics.TryGetNumber(MetricIds.ComplexityPoints)!.Value, precision: 12);
        Assert.Equal(14.545748987854251, enriched.Root.ComputedMetrics.TryGetNumber(MetricIds.RefactorPriorityPoints)!.Value, precision: 12);
    }

    [Fact]
    public async Task EnrichAsync_TreatsSyntaxAnalyzerFailuresAsWarnings()
    {
        var fullPath = Path.Combine(_rootPath, "Program.cs");
        await File.WriteAllTextAsync(fullPath, "class Program { }");

        var scanner = new FileSystemProjectScanner();
        var snapshot = await scanner.ScanAsync(_rootPath, ScanOptions.Default, progress: null, CancellationToken.None);
        var enricher = new ProjectSnapshotMetricsEnricher(
            new HeuristicTextFileDetector(),
            new RecordingTokenCounter(),
            syntaxAnalyzerRegistry: new ExtensionSyntaxAnalyzerRegistry(
            [
                new ThrowingSyntaxAnalyzer(".cs", "csharp"),
            ]));

        var enriched = await enricher.EnrichAsync(snapshot, CancellationToken.None);
        var programNode = Assert.Single(enriched.Root.Children);

        Assert.NotNull(programNode.ComputedMetrics.TryGetRoundedInt64(MetricIds.Tokens));
        Assert.NotNull(programNode.ComputedMetrics.TryGetRoundedInt32(MetricIds.NonEmptyLines));
        Assert.Equal(MetricStatus.NotApplicable, programNode.ComputedMetrics.GetOrDefault(MetricIds.CodeLines).Status);
        Assert.Equal(MetricStatus.NotApplicable, programNode.ComputedMetrics.GetOrDefault(MetricIds.MaxParameterCount).Status);
        Assert.Equal(MetricStatus.NotApplicable, programNode.ComputedMetrics.GetOrDefault(MetricIds.CyclomaticComplexitySum).Status);
        Assert.Equal(MetricStatus.NotApplicable, programNode.ComputedMetrics.GetOrDefault(MetricIds.CyclomaticComplexityMax).Status);
        Assert.Equal(MetricStatus.NotApplicable, programNode.ComputedMetrics.GetOrDefault(MetricIds.MaxNestingDepth).Status);
        Assert.Contains(
            enriched.Diagnostics,
            issue => issue.Context.TryGetValue("FullPath", out var path) &&
                     string.Equals(path, fullPath, StringComparison.OrdinalIgnoreCase));
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
        Assert.Equal(1, enriched.Root.Summary.DescendantFileCount);
        Assert.Equal(MetricStatus.NotApplicable, enriched.Root.ComputedMetrics.GetOrDefault(MetricIds.Tokens).Status);
        Assert.Equal(MetricStatus.NotApplicable, enriched.Root.ComputedMetrics.GetOrDefault(MetricIds.NonEmptyLines).Status);
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

        Assert.Equal(2, sampleNode.ComputedMetrics.TryGetRoundedInt32(MetricIds.NonEmptyLines));

        Assert.Equal(0, emptyNode.ComputedMetrics.TryGetRoundedInt32(MetricIds.NonEmptyLines));
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
        Assert.Equal(NodeSummary.Empty, originalRoot.Summary);
        Assert.Same(MetricSet.Empty, originalRoot.ComputedMetrics);
        Assert.Null(originalChild.SkippedReason);
        Assert.NotSame(originalRoot, enriched.Root);
        Assert.NotSame(originalChild, enriched.Root.Children[0]);
        Assert.Equal(NodeSummary.Empty, originalChild.Summary);
        Assert.Same(MetricSet.Empty, originalChild.ComputedMetrics);
        Assert.Equal(2, enriched.Root.ComputedMetrics.TryGetRoundedInt32(MetricIds.NonEmptyLines));
        Assert.Equal(1, enriched.Root.Summary.DescendantFileCount);
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
        Assert.Equal(8L, sampleNode.ComputedMetrics.TryGetRoundedInt64(MetricIds.Tokens));
        Assert.Equal(3, sampleNode.ComputedMetrics.TryGetRoundedInt32(MetricIds.NonEmptyLines));
    }

    [Fact]
    public async Task EnrichAsync_UsesSeparateSyntaxThresholdForLargeFiles()
    {
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "Program.cs"), "class Program { int Run(int x, int y) { return x + y; } }");

        var scanner = new FileSystemProjectScanner();
        var snapshot = await scanner.ScanAsync(_rootPath, ScanOptions.Default, progress: null, CancellationToken.None);
        var syntaxArtifact = new SyntaxSummaryArtifact(
            LanguageId: "csharp",
            ParseQuality: SyntaxParseQuality.Full,
            CodeLineCount: 1,
            CyclomaticComplexitySum: 1,
            CyclomaticComplexityMax: 1,
            MaxNestingDepth: 0,
            Callables:
            [
                new CallableSyntaxFact("Run", CallableKind.Method, new LineRange(1, 1), 2, 1, 0),
            ]);
        var enricher = new ProjectSnapshotMetricsEnricher(
            new AlwaysTextDetector(),
            new RecordingTokenCounter(),
            largeFileTokenizationThresholdBytes: 1,
            largeFileSyntaxAnalysisThresholdBytes: long.MaxValue,
            syntaxAnalyzerRegistry: new ExtensionSyntaxAnalyzerRegistry(
            [
                new StaticSyntaxAnalyzer(".cs", syntaxArtifact),
            ]));

        var enriched = await enricher.EnrichAsync(snapshot, CancellationToken.None);
        var programNode = Assert.Single(enriched.Root.Children);

        Assert.True(programNode.ComputedMetrics.TryGetRoundedInt64(MetricIds.Tokens) > 0);
        Assert.Equal(1, programNode.ComputedMetrics.TryGetRoundedInt32(MetricIds.CodeLines));
        Assert.Equal(2, programNode.ComputedMetrics.TryGetRoundedInt32(MetricIds.MaxParameterCount));
    }

    [Fact]
    public async Task EnrichAsync_MarksNewSyntaxMetricsAsNotApplicableForBinaryFiles()
    {
        await File.WriteAllBytesAsync(Path.Combine(_rootPath, "image.bin"), [0x42, 0x00, 0x43]);

        var scanner = new FileSystemProjectScanner();
        var snapshot = await scanner.ScanAsync(_rootPath, ScanOptions.Default, progress: null, CancellationToken.None);
        var enricher = new ProjectSnapshotMetricsEnricher(
            new HeuristicTextFileDetector(),
            new RecordingTokenCounter());

        var enriched = await enricher.EnrichAsync(snapshot, CancellationToken.None);
        var imageNode = Assert.Single(enriched.Root.Children);

        Assert.Equal(MetricStatus.NotApplicable, imageNode.ComputedMetrics.GetOrDefault(MetricIds.CodeLines).Status);
        Assert.Equal(MetricStatus.NotApplicable, imageNode.ComputedMetrics.GetOrDefault(MetricIds.MaxParameterCount).Status);
        Assert.Equal(MetricStatus.NotApplicable, imageNode.ComputedMetrics.GetOrDefault(MetricIds.LongCallableCount).Status);
        Assert.Equal(MetricStatus.NotApplicable, imageNode.ComputedMetrics.GetOrDefault(MetricIds.HighCyclomaticComplexityCallableCount).Status);
        Assert.Equal(MetricStatus.NotApplicable, imageNode.ComputedMetrics.GetOrDefault(MetricIds.DeepNestingCallableCount).Status);
        Assert.Equal(MetricStatus.NotApplicable, imageNode.ComputedMetrics.GetOrDefault(MetricIds.LongParameterListCount).Status);
        Assert.Equal(MetricStatus.NotApplicable, imageNode.ComputedMetrics.GetOrDefault(MetricIds.CallableHotspotPoints).Status);
        Assert.Equal(MetricStatus.NotApplicable, imageNode.ComputedMetrics.GetOrDefault(MetricIds.ComplexityPoints).Status);
        Assert.Equal(MetricStatus.NotApplicable, imageNode.ComputedMetrics.GetOrDefault(MetricIds.RefactorPriorityPoints).Status);
    }

    [Fact]
    public async Task EnrichAsync_RollsUpCallableHotspotMetricsAcrossDirectories()
    {
        Directory.CreateDirectory(Path.Combine(_rootPath, "src"));
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "src", "A.cs"), "class A { }");
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "src", "B.cs"), "class B { }");
        await File.WriteAllBytesAsync(Path.Combine(_rootPath, "src", "image.bin"), [0x01, 0x02, 0x03]);

        var scanner = new FileSystemProjectScanner();
        var snapshot = await scanner.ScanAsync(_rootPath, ScanOptions.Default, progress: null, CancellationToken.None);
        var enricher = new ProjectSnapshotMetricsEnricher(
            new HeuristicTextFileDetector(),
            new RecordingTokenCounter(),
            syntaxAnalyzerRegistry: new ExtensionSyntaxAnalyzerRegistry(
            [
                new PathMappedSyntaxAnalyzer(new Dictionary<string, SyntaxSummaryArtifact>(StringComparer.OrdinalIgnoreCase)
                {
                    ["A.cs"] = new(
                        LanguageId: "csharp",
                        ParseQuality: SyntaxParseQuality.Full,
                        CodeLineCount: 12,
                        CyclomaticComplexitySum: 13,
                        CyclomaticComplexityMax: 10,
                        MaxNestingDepth: 4,
                        Callables:
                        [
                            new CallableSyntaxFact("A1", CallableKind.Method, new LineRange(1, 31), 2, 10, 4),
                            new CallableSyntaxFact("A2", CallableKind.Method, new LineRange(40, 44), 1, 3, 1),
                        ]),
                    ["B.cs"] = new(
                        LanguageId: "csharp",
                        ParseQuality: SyntaxParseQuality.Full,
                        CodeLineCount: 14,
                        CyclomaticComplexitySum: 16,
                        CyclomaticComplexityMax: 12,
                        MaxNestingDepth: 5,
                        Callables:
                        [
                            new CallableSyntaxFact("B1", CallableKind.Method, new LineRange(1, 10), 6, 12, 2),
                            new CallableSyntaxFact("B2", CallableKind.Method, new LineRange(20, 55), 7, 4, 5),
                        ]),
                }),
            ]));

        var enriched = await enricher.EnrichAsync(snapshot, CancellationToken.None);
        var srcNode = Assert.Single(enriched.Root.Children, node => node.Name == "src");
        var fileA = Assert.Single(srcNode.Children, node => node.Name == "A.cs");
        var fileB = Assert.Single(srcNode.Children, node => node.Name == "B.cs");
        var binaryNode = Assert.Single(srcNode.Children, node => node.Name == "image.bin");

        Assert.Equal(1, fileA.ComputedMetrics.TryGetRoundedInt32(MetricIds.LongCallableCount));
        Assert.Equal(1, fileA.ComputedMetrics.TryGetRoundedInt32(MetricIds.HighCyclomaticComplexityCallableCount));
        Assert.Equal(1, fileA.ComputedMetrics.TryGetRoundedInt32(MetricIds.DeepNestingCallableCount));
        Assert.Equal(0, fileA.ComputedMetrics.TryGetRoundedInt32(MetricIds.LongParameterListCount));
        Assert.Equal(7, fileA.ComputedMetrics.TryGetRoundedInt32(MetricIds.CallableHotspotPoints));

        Assert.Equal(1, fileB.ComputedMetrics.TryGetRoundedInt32(MetricIds.LongCallableCount));
        Assert.Equal(1, fileB.ComputedMetrics.TryGetRoundedInt32(MetricIds.HighCyclomaticComplexityCallableCount));
        Assert.Equal(1, fileB.ComputedMetrics.TryGetRoundedInt32(MetricIds.DeepNestingCallableCount));
        Assert.Equal(2, fileB.ComputedMetrics.TryGetRoundedInt32(MetricIds.LongParameterListCount));
        Assert.Equal(9, fileB.ComputedMetrics.TryGetRoundedInt32(MetricIds.CallableHotspotPoints));

        Assert.Equal(SkippedReason.Binary, binaryNode.SkippedReason);
        Assert.Equal(MetricStatus.NotApplicable, binaryNode.ComputedMetrics.GetOrDefault(MetricIds.CallableHotspotPoints).Status);

        Assert.Equal(16, srcNode.ComputedMetrics.TryGetRoundedInt32(MetricIds.CallableHotspotPoints));

        Assert.Equal(16, enriched.Root.ComputedMetrics.TryGetRoundedInt32(MetricIds.CallableHotspotPoints));
    }

    [Fact]
    public async Task EnrichAsync_SumsComplexityPointsAcrossDirectories()
    {
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "A.cs"), "class A { }");
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "B.cs"), "class B { }");

        var scanner = new FileSystemProjectScanner();
        var snapshot = await scanner.ScanAsync(_rootPath, ScanOptions.Default, progress: null, CancellationToken.None);
        var enricher = new ProjectSnapshotMetricsEnricher(
            new HeuristicTextFileDetector(),
            new RecordingTokenCounter(),
            syntaxAnalyzerRegistry: new ExtensionSyntaxAnalyzerRegistry(
            [
                new PathMappedSyntaxAnalyzer(new Dictionary<string, SyntaxSummaryArtifact>(StringComparer.OrdinalIgnoreCase)
                {
                    ["A.cs"] = new(
                        LanguageId: "csharp",
                        ParseQuality: SyntaxParseQuality.Full,
                        CodeLineCount: 50,
                        CyclomaticComplexitySum: 10,
                        CyclomaticComplexityMax: 6,
                        MaxNestingDepth: 3,
                        Callables:
                        [
                            new CallableSyntaxFact("A1", CallableKind.Method, new LineRange(1, 1), 2, 6, 3),
                            new CallableSyntaxFact("A2", CallableKind.Method, new LineRange(2, 2), 1, 4, 2),
                        ]),
                    ["B.cs"] = new(
                        LanguageId: "csharp",
                        ParseQuality: SyntaxParseQuality.Full,
                        CodeLineCount: 200,
                        CyclomaticComplexitySum: 25,
                        CyclomaticComplexityMax: 10,
                        MaxNestingDepth: 4,
                        Callables:
                        [
                            new CallableSyntaxFact("B1", CallableKind.Method, new LineRange(1, 1), 2, 10, 4),
                            new CallableSyntaxFact("B2", CallableKind.Method, new LineRange(2, 2), 4, 7, 2),
                            new CallableSyntaxFact("B3", CallableKind.Method, new LineRange(3, 3), 3, 5, 3),
                            new CallableSyntaxFact("B4", CallableKind.Method, new LineRange(4, 4), 1, 3, 1),
                        ]),
                }),
            ]));

        var enriched = await enricher.EnrichAsync(snapshot, CancellationToken.None);
        var firstFile = Assert.Single(enriched.Root.Children, node => node.Name == "A.cs");
        var secondFile = Assert.Single(enriched.Root.Children, node => node.Name == "B.cs");

        var firstScore = firstFile.ComputedMetrics.TryGetNumber(MetricIds.ComplexityPoints);
        var secondScore = secondFile.ComputedMetrics.TryGetNumber(MetricIds.ComplexityPoints);
        var rootScore = enriched.Root.ComputedMetrics.TryGetNumber(MetricIds.ComplexityPoints);

        Assert.NotNull(firstScore);
        Assert.NotNull(secondScore);
        Assert.NotNull(rootScore);
        Assert.Equal(firstScore.Value + secondScore.Value, rootScore.Value, precision: 12);
    }

    [Fact]
    public async Task EnrichAsync_ComputesRefactorPriorityWithGitBlastRadiusAndZeroHistoryArtifacts()
    {
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "A.cs"), "class A { }");
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "B.cs"), "class B { }");

        var scanner = new FileSystemProjectScanner();
        var snapshot = await scanner.ScanAsync(_rootPath, ScanOptions.Default, progress: null, CancellationToken.None);
        var syntaxArtifact = new SyntaxSummaryArtifact(
            LanguageId: "csharp",
            ParseQuality: SyntaxParseQuality.Full,
            CodeLineCount: 100,
            CyclomaticComplexitySum: 20,
            CyclomaticComplexityMax: 8,
            MaxNestingDepth: 4,
            Callables:
            [
                new CallableSyntaxFact("Run", CallableKind.Method, new LineRange(1, 30), 6, 11, 4),
            ]);
        var enricher = new ProjectSnapshotMetricsEnricher(
            new HeuristicTextFileDetector(),
            new RecordingTokenCounter(),
            gitHistorySnapshotProvider: new StubGitHistorySnapshotProvider(CreateGitHistorySnapshot(
                _rootPath,
                "head-a",
                new Dictionary<string, GitFileHistoryArtifact>(PathComparison.Comparer)
                {
                    ["A.cs"] = new(
                        ChurnLines90d: 400,
                        TouchCount90d: 12,
                        AuthorCount90d: 4,
                        UniqueCochangedFileCount90d: 20,
                        StrongCochangedFileCount90d: 8,
                        AverageCochangeSetSize90d: 6d),
                })),
            syntaxAnalyzerRegistry: new ExtensionSyntaxAnalyzerRegistry(
            [
                new PathMappedSyntaxAnalyzer(new Dictionary<string, SyntaxSummaryArtifact>(StringComparer.OrdinalIgnoreCase)
                {
                    ["A.cs"] = syntaxArtifact,
                    ["B.cs"] = syntaxArtifact,
                }),
            ]));

        var enriched = await enricher.EnrichAsync(snapshot, CancellationToken.None);
        var firstFile = Assert.Single(enriched.Root.Children, node => node.Name == "A.cs");
        var secondFile = Assert.Single(enriched.Root.Children, node => node.Name == "B.cs");

        var firstPriority = firstFile.ComputedMetrics.TryGetNumber(MetricIds.RefactorPriorityPoints);
        var secondPriority = secondFile.ComputedMetrics.TryGetNumber(MetricIds.RefactorPriorityPoints);
        var rootPriority = enriched.Root.ComputedMetrics.TryGetNumber(MetricIds.RefactorPriorityPoints);
        var secondComplexity = secondFile.ComputedMetrics.TryGetNumber(MetricIds.ComplexityPoints);
        var secondHotspotPoints = secondFile.ComputedMetrics.TryGetNumber(MetricIds.CallableHotspotPoints);

        Assert.NotNull(firstPriority);
        Assert.NotNull(secondPriority);
        Assert.NotNull(rootPriority);
        Assert.NotNull(secondComplexity);
        Assert.NotNull(secondHotspotPoints);

        var secondBasePriority =
            (0.80d * secondComplexity.Value) +
            (0.20d * (100d * secondHotspotPoints.Value / 10d));

        Assert.True(firstPriority.Value > secondPriority.Value);
        Assert.Equal(0.60d * secondBasePriority, secondPriority.Value, precision: 12);
        Assert.Equal(firstPriority.Value + secondPriority.Value, rootPriority.Value, precision: 12);
    }

    [Fact]
    public async Task EnrichAsync_InvalidatesCacheWhenGitContextFingerprintChanges()
    {
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "Program.cs"), "alpha");

        var scanner = new FileSystemProjectScanner();
        var snapshot = await scanner.ScanAsync(_rootPath, ScanOptions.Default, progress: null, CancellationToken.None);
        var tokenCounter = new RecordingTokenCounter();
        var cacheStore = new InMemoryCacheStore();

        var firstEnricher = new ProjectSnapshotMetricsEnricher(
            new AlwaysTextDetector(),
            tokenCounter,
            cacheStore: cacheStore,
            gitHistorySnapshotProvider: new StubGitHistorySnapshotProvider(CreateGitHistorySnapshot(
                _rootPath,
                "head-a",
                historyWindowEndUtc: new DateTimeOffset(2026, 04, 04, 0, 0, 0, TimeSpan.Zero))));
        var secondEnricher = new ProjectSnapshotMetricsEnricher(
            new AlwaysTextDetector(),
            tokenCounter,
            cacheStore: cacheStore,
            gitHistorySnapshotProvider: new StubGitHistorySnapshotProvider(CreateGitHistorySnapshot(
                _rootPath,
                "head-a",
                historyWindowEndUtc: new DateTimeOffset(2026, 04, 05, 0, 0, 0, TimeSpan.Zero))));

        await firstEnricher.EnrichAsync(snapshot, CancellationToken.None);
        await firstEnricher.EnrichAsync(snapshot, CancellationToken.None);
        await secondEnricher.EnrichAsync(snapshot, CancellationToken.None);

        Assert.Equal(2, tokenCounter.GetSeenContents().Count);
    }

    [Fact]
    public void EnrichAsync_UsesV1GitFingerprintForCacheContext()
    {
        var gitHistorySnapshot = CreateGitHistorySnapshot(
            _rootPath,
            "head-a",
            historyWindowEndUtc: new DateTimeOffset(2026, 04, 04, 0, 0, 0, TimeSpan.Zero));

        Assert.Contains("|git90d-v1|20260404", gitHistorySnapshot.ContextFingerprint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnrichAsync_ContinuesWhenGitSnapshotIsUnavailable()
    {
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "Program.cs"), "class Program { }");

        var scanner = new FileSystemProjectScanner();
        var snapshot = await scanner.ScanAsync(_rootPath, ScanOptions.Default, progress: null, CancellationToken.None);
        var syntaxArtifact = new SyntaxSummaryArtifact(
            LanguageId: "csharp",
            ParseQuality: SyntaxParseQuality.Full,
            CodeLineCount: 1,
            CyclomaticComplexitySum: 7,
            CyclomaticComplexityMax: 4,
            MaxNestingDepth: 2,
            Callables:
            [
                new CallableSyntaxFact("Run", CallableKind.Method, new LineRange(1, 1), 2, 3, 1),
            ]);
        var enricher = new ProjectSnapshotMetricsEnricher(
            new HeuristicTextFileDetector(),
            new RecordingTokenCounter(),
            gitHistorySnapshotProvider: new StubGitHistorySnapshotProvider(null),
            syntaxAnalyzerRegistry: new ExtensionSyntaxAnalyzerRegistry(
            [
                new StaticSyntaxAnalyzer(".cs", syntaxArtifact),
            ]));

        var enriched = await enricher.EnrichAsync(snapshot, CancellationToken.None);
        var programNode = Assert.Single(enriched.Root.Children);
        var priority = programNode.ComputedMetrics.TryGetNumber(MetricIds.RefactorPriorityPoints);
        var complexity = programNode.ComputedMetrics.TryGetNumber(MetricIds.ComplexityPoints);
        var hotspotPoints = programNode.ComputedMetrics.TryGetNumber(MetricIds.CallableHotspotPoints);

        Assert.NotNull(priority);
        Assert.NotNull(complexity);
        Assert.NotNull(hotspotPoints);

        var basePriority =
            (0.80d * complexity.Value) +
            (0.20d * (100d * hotspotPoints.Value / 10d));

        Assert.Equal(basePriority, priority.Value, precision: 12);
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
        Assert.Equal((long)content.Length, sampleNode.ComputedMetrics.TryGetRoundedInt64(MetricIds.Tokens));
        Assert.Equal(1, sampleNode.ComputedMetrics.TryGetRoundedInt32(MetricIds.NonEmptyLines));
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

        Assert.Equal(4, enriched.Root.Summary.DescendantFileCount);
        Assert.InRange(tokenCounter.MaxConcurrency, 1, 2);
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

    private sealed class StubGitHistorySnapshotProvider(GitHistorySnapshot? snapshot) : IGitHistorySnapshotProvider
    {
        public ValueTask<GitHistorySnapshot?> TryCreateAsync(string analysisRootPath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(snapshot);
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

    private sealed class StaticSyntaxAnalyzer(string extension, SyntaxSummaryArtifact artifact) : ISyntaxAnalyzer
    {
        public string LanguageId => artifact.LanguageId;

        public IReadOnlyCollection<string> FileExtensions { get; } = [extension];

        public bool CanAnalyze(string fullPath) =>
            string.Equals(Path.GetExtension(fullPath), extension, StringComparison.OrdinalIgnoreCase);

        public ValueTask<SyntaxSummaryArtifact> AnalyzeAsync(
            string fullPath,
            string sourceText,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(artifact);
        }
    }

    private sealed class ThrowingSyntaxAnalyzer(string extension, string languageId) : ISyntaxAnalyzer
    {
        public string LanguageId { get; } = languageId;

        public IReadOnlyCollection<string> FileExtensions { get; } = [extension];

        public bool CanAnalyze(string fullPath) =>
            string.Equals(Path.GetExtension(fullPath), extension, StringComparison.OrdinalIgnoreCase);

        public ValueTask<SyntaxSummaryArtifact> AnalyzeAsync(
            string fullPath,
            string sourceText,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");
    }

    private sealed class PathMappedSyntaxAnalyzer(IReadOnlyDictionary<string, SyntaxSummaryArtifact> artifactsByFileName) : ISyntaxAnalyzer
    {
        public string LanguageId => "csharp";

        public IReadOnlyCollection<string> FileExtensions { get; } = [".cs"];

        public bool CanAnalyze(string fullPath) =>
            string.Equals(Path.GetExtension(fullPath), ".cs", StringComparison.OrdinalIgnoreCase);

        public ValueTask<SyntaxSummaryArtifact> AnalyzeAsync(
            string fullPath,
            string sourceText,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(fullPath);
            if (!artifactsByFileName.TryGetValue(fileName, out var artifact))
            {
                throw new KeyNotFoundException($"Missing syntax artifact for '{fileName}'.");
            }

            return ValueTask.FromResult(artifact);
        }
    }

    private static GitHistorySnapshot CreateGitHistorySnapshot(
        string rootPath,
        string headCommitSha,
        IReadOnlyDictionary<string, GitFileHistoryArtifact>? fileHistoryByAnalysisRelativePath = null,
        DateTimeOffset? historyWindowEndUtc = null) =>
        new(
            headCommitSha,
            fileHistoryByAnalysisRelativePath ??
            new Dictionary<string, GitFileHistoryArtifact>(PathComparison.Comparer),
            historyWindowEndUtc);
}

