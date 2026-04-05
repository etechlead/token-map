using System.Collections.Concurrent;
using System.Text;
using Clever.TokenMap.Core.Analysis.Git;
using Clever.TokenMap.Core.Analysis.Syntax;
using Clever.TokenMap.Core.Diagnostics;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Logging;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Infrastructure.Analysis.Git;
using Clever.TokenMap.Metrics;
using Clever.TokenMap.Metrics.Calculators;
using Clever.TokenMap.Metrics.Calculators.Derived;
using Clever.TokenMap.Metrics.Syntax;

namespace Clever.TokenMap.Infrastructure.Analysis;

public sealed class ProjectSnapshotMetricsEnricher : IProjectSnapshotMetricEngine
{
    private const long LargeFileTokenizationThresholdBytes = 1024L * 1024L;
    private const long LargeFileSyntaxAnalysisThresholdBytes = 4L * 1024L * 1024L;
    private const int LargeFileChunkSizeChars = 256 * 1024;

    private readonly ICacheStore? _cacheStore;
    private readonly int _largeFileChunkSizeChars;
    private readonly long _largeFileSyntaxAnalysisThresholdBytes;
    private readonly long _largeFileTokenizationThresholdBytes;
    private readonly IAppLogger _logger;
    private readonly IGitHistorySnapshotProvider? _gitHistorySnapshotProvider;
    private readonly IFileDerivedMetricCalculator[] _fileDerivedMetricCalculators;
    private readonly IReadOnlyList<IFileMetricCalculator> _fileMetricCalculators;
    private readonly int _maxDegreeOfParallelism;
    private readonly MetricSetRollupService _metricSetRollupService;
    private readonly ISyntaxAnalyzerRegistry _syntaxAnalyzerRegistry;
    private readonly ITextFileDetector _textFileDetector;
    private readonly ITokenCounter _tokenCounter;

    public ProjectSnapshotMetricsEnricher(
        ITextFileDetector textFileDetector,
        ITokenCounter tokenCounter,
        ICacheStore? cacheStore = null,
        IAppLogger? logger = null,
        IGitHistorySnapshotProvider? gitHistorySnapshotProvider = null,
        long largeFileTokenizationThresholdBytes = LargeFileTokenizationThresholdBytes,
        long largeFileSyntaxAnalysisThresholdBytes = LargeFileSyntaxAnalysisThresholdBytes,
        int largeFileChunkSizeChars = LargeFileChunkSizeChars,
        int? maxDegreeOfParallelism = null,
        IMetricCatalog? metricCatalog = null,
        IEnumerable<IFileMetricCalculator>? fileMetricCalculators = null,
        IEnumerable<IFileDerivedMetricCalculator>? fileDerivedMetricCalculators = null,
        ISyntaxAnalyzerRegistry? syntaxAnalyzerRegistry = null)
    {
        _cacheStore = cacheStore;
        _largeFileChunkSizeChars = largeFileChunkSizeChars > 0
            ? largeFileChunkSizeChars
            : throw new ArgumentOutOfRangeException(nameof(largeFileChunkSizeChars));
        _largeFileTokenizationThresholdBytes = largeFileTokenizationThresholdBytes >= 0
            ? largeFileTokenizationThresholdBytes
            : throw new ArgumentOutOfRangeException(nameof(largeFileTokenizationThresholdBytes));
        _largeFileSyntaxAnalysisThresholdBytes = largeFileSyntaxAnalysisThresholdBytes >= 0
            ? largeFileSyntaxAnalysisThresholdBytes
            : throw new ArgumentOutOfRangeException(nameof(largeFileSyntaxAnalysisThresholdBytes));
        _logger = logger ?? NullAppLogger.Instance;
        _gitHistorySnapshotProvider = gitHistorySnapshotProvider;
        _maxDegreeOfParallelism = maxDegreeOfParallelism.HasValue
            ? NormalizeMaxDegreeOfParallelism(maxDegreeOfParallelism.Value)
            : GetDefaultMaxDegreeOfParallelism();
        _syntaxAnalyzerRegistry = syntaxAnalyzerRegistry ?? DefaultSyntaxAnalyzerRegistry.Instance;
        _textFileDetector = textFileDetector;
        _tokenCounter = tokenCounter;
        var effectiveMetricCatalog = metricCatalog ?? DefaultMetricCatalog.Instance;
        _fileMetricCalculators = (fileMetricCalculators ?? CreateDefaultFileMetricCalculators())
            .OrderBy(calculator => calculator.Order)
            .ToArray();
        _fileDerivedMetricCalculators = (fileDerivedMetricCalculators ?? CreateDefaultFileDerivedMetricCalculators())
            .OrderBy(calculator => calculator.Order)
            .ToArray();
        _metricSetRollupService = new MetricSetRollupService(effectiveMetricCatalog);
    }

    public Task<ProjectSnapshot> EnrichAsync(ProjectSnapshot snapshot, CancellationToken cancellationToken) =>
        EnrichAsync(snapshot, progress: null, cancellationToken);

    public async Task<ProjectSnapshot> EnrichAsync(
        ProjectSnapshot snapshot,
        IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var diagnostics = new ConcurrentQueue<AnalysisIssue>(snapshot.Diagnostics);
        var gitHistorySnapshot = await TryCreateGitHistorySnapshotAsync(snapshot.RootPath, cancellationToken)
            .ConfigureAwait(false);
        var contextFingerprint = gitHistorySnapshot?.ContextFingerprint;
        var fileWorkItems = CollectFileWorkItems(snapshot.Root);
        var enrichedFiles = new ProjectNode?[fileWorkItems.Count];
        var totalFileCount = fileWorkItems.Count;
        var processedFileCount = 0;

        if (fileWorkItems.Count > 0)
        {
            await Parallel.ForEachAsync(
                fileWorkItems,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = _maxDegreeOfParallelism,
                },
                async (fileWorkItem, ct) =>
                {
                    var enrichedFile = await EnrichFileNodeAsync(
                        snapshot,
                        fileWorkItem.Node,
                        gitHistorySnapshot,
                        contextFingerprint,
                        diagnostics,
                        ct).ConfigureAwait(false);
                    enrichedFiles[fileWorkItem.Index] = enrichedFile;

                    var processed = Interlocked.Increment(ref processedFileCount);
                    progress?.Report(new AnalysisProgress(
                        Phase: "AnalyzingFiles",
                        ProcessedNodeCount: processed,
                        TotalNodeCount: totalFileCount,
                        CurrentPath: enrichedFile.RelativePath));
                }).ConfigureAwait(false);
        }

        var nextFileIndex = 0;
        var enrichedRoot = RebuildEnrichedTree(
            snapshot.Root,
            enrichedFiles,
            ref nextFileIndex,
            cancellationToken);

        return new ProjectSnapshot
        {
            RootPath = snapshot.RootPath,
            CapturedAtUtc = snapshot.CapturedAtUtc,
            Options = snapshot.Options,
            Root = enrichedRoot,
            Diagnostics = [.. diagnostics],
        };
    }

    private static int GetDefaultMaxDegreeOfParallelism() =>
        Math.Max(1, Environment.ProcessorCount / 2);

    private static int NormalizeMaxDegreeOfParallelism(int maxDegreeOfParallelism) =>
        maxDegreeOfParallelism > 0
            ? maxDegreeOfParallelism
            : throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));

    private static IReadOnlyList<IFileMetricCalculator> CreateDefaultFileMetricCalculators() =>
    [
        new FileSizeMetricCalculator(),
        new TextMetricsCalculator(),
        new SyntaxMetricsCalculator(),
        new GitHistoryMetricsCalculator(),
    ];

    private static IFileDerivedMetricCalculator[] CreateDefaultFileDerivedMetricCalculators() =>
    [
        new CallableHotspotMetricsCalculator(),
        new ComplexityPointsDerivedMetricsCalculator(),
        new RefactorPriorityPointsDerivedMetricsCalculator(),
    ];

    private static List<FileWorkItem> CollectFileWorkItems(ProjectNode root)
    {
        var fileWorkItems = new List<FileWorkItem>();
        CollectFileWorkItems(root, fileWorkItems);
        return fileWorkItems;
    }

    private static void CollectFileWorkItems(ProjectNode node, List<FileWorkItem> fileWorkItems)
    {
        if (node.Kind == ProjectNodeKind.File)
        {
            fileWorkItems.Add(new FileWorkItem(fileWorkItems.Count, node));
            return;
        }

        foreach (var child in node.Children)
        {
            CollectFileWorkItems(child, fileWorkItems);
        }
    }

    private ProjectNode RebuildEnrichedTree(
        ProjectNode node,
        IReadOnlyList<ProjectNode?> enrichedFiles,
        ref int nextFileIndex,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (node.Kind == ProjectNodeKind.File)
        {
            var enrichedFile = enrichedFiles[nextFileIndex++];
            return enrichedFile ?? throw new InvalidOperationException(
                $"Missing enriched file metrics for '{node.RelativePath}'.");
        }

        var enrichedChildren = new List<ProjectNode>(node.Children.Count);
        foreach (var child in node.Children)
        {
            enrichedChildren.Add(RebuildEnrichedTree(
                child,
                enrichedFiles,
                ref nextFileIndex,
                cancellationToken));
        }

        var computedMetrics = _metricSetRollupService.Rollup(enrichedChildren.Select(child => child.ComputedMetrics));
        var summary = AggregateDirectorySummary(enrichedChildren, node.Kind);
        return CloneNode(
            node,
            summary,
            computedMetrics,
            node.SkippedReason,
            enrichedChildren);
    }

    private async Task<ProjectNode> EnrichFileNodeAsync(
        ProjectSnapshot snapshot,
        ProjectNode node,
        GitHistorySnapshot? gitHistorySnapshot,
        string? contextFingerprint,
        ConcurrentQueue<AnalysisIssue> diagnostics,
        CancellationToken cancellationToken)
    {
        var fileState = TryGetFileState(node.FullPath);
        var fileSizeBytes = fileState.FileSizeBytes;

        if (node.SkippedReason is not null)
        {
            var skippedComputedMetrics = CreateSkippedComputedMetrics(fileSizeBytes);
            return CloneNode(
                node,
                CreateFileSummary(),
                skippedComputedMetrics,
                node.SkippedReason);
        }

        if (fileState.Exists && _cacheStore is not null)
        {
            var cachedMetrics = await TryRestoreCachedMetricsAsync(
                snapshot.RootPath,
                node.RelativePath,
                fileState,
                contextFingerprint,
                cancellationToken);
            if (cachedMetrics is not null)
            {
                _logger.LogTrace(
                    "Restored cached metrics for the file.",
                    eventCode: "analysis.cache_hit",
                    context: AppIssueContext.Create(
                        ("RootPath", snapshot.RootPath),
                        ("RelativePath", node.RelativePath),
                        ("FullPath", node.FullPath)));
                return CloneNode(
                    node,
                    CreateFileSummary(),
                    cachedMetrics,
                    node.SkippedReason);
            }
        }

        bool isText;
        try
        {
            isText = await _textFileDetector.IsTextAsync(node.FullPath, cancellationToken);
        }
        catch (Exception exception) when (IsRecoverableFileException(exception))
        {
            return ApplyRecoverableFileError(node, exception, diagnostics, fileSizeBytes, _logger);
        }

        if (!isText)
        {
            var skippedComputedMetrics = CreateSkippedComputedMetrics(fileSizeBytes);
            return CloneNode(
                node,
                CreateFileSummary(),
                skippedComputedMetrics,
                SkippedReason.Binary);
        }

        TextMetricsArtifact textMetricsArtifact;
        try
        {
            var textAnalysis = fileState.FileSizeBytes > _largeFileTokenizationThresholdBytes
                ? await AnalyzeLargeTextFileAsync(node.FullPath, diagnostics, cancellationToken).ConfigureAwait(false)
                : await AnalyzeSmallTextFileAsync(node.FullPath, diagnostics, cancellationToken).ConfigureAwait(false);
            textMetricsArtifact = new TextMetricsArtifact(textAnalysis.Tokens, textAnalysis.NonEmptyLineCount);
        }
        catch (Exception exception) when (IsRecoverableFileException(exception))
        {
            return ApplyRecoverableFileError(node, exception, diagnostics, fileSizeBytes, _logger);
        }
        catch (Exception exception) when (exception is DecoderFallbackException or InvalidDataException)
        {
            diagnostics.Enqueue(CreateDiagnostic(
                message: $"TokenMap could not decode '{node.FullPath}'.",
                context: AppIssueContext.Create(("FullPath", node.FullPath))));
            _logger.LogWarning(
                exception,
                "Decoding a text file failed during analysis.",
                eventCode: "analysis.decode_failed",
                context: AppIssueContext.Create(("FullPath", node.FullPath)));
            var skippedComputedMetrics = CreateSkippedComputedMetrics(fileSizeBytes);
            return CloneNode(
                node,
                CreateFileSummary(),
                skippedComputedMetrics,
                SkippedReason.Unsupported);
        }

        var computedMetrics = await ComputeFileMetricsAsync(
            node,
            fileSizeBytes,
            textMetricsArtifact,
            gitHistorySnapshot,
            diagnostics,
            cancellationToken).ConfigureAwait(false);

        if (fileState.Exists && _cacheStore is not null)
        {
            await _cacheStore.SetFileMetricsAsync(
                snapshot.RootPath,
                node.RelativePath,
                fileState.FileSizeBytes,
                fileState.LastWriteTimeUtc,
                contextFingerprint,
                computedMetrics,
                cancellationToken);
        }

        return CloneNode(
            node,
            CreateFileSummary(),
            computedMetrics,
            node.SkippedReason);
    }

    private static ProjectNode CloneNode(
        ProjectNode source,
        NodeSummary summary,
        MetricSet computedMetrics,
        SkippedReason? skippedReason,
        IEnumerable<ProjectNode>? children = null)
    {
        var clone = new ProjectNode
        {
            Id = source.Id,
            Name = source.Name,
            FullPath = source.FullPath,
            RelativePath = source.RelativePath,
            Kind = source.Kind,
            Summary = summary,
            ComputedMetrics = computedMetrics,
            SkippedReason = skippedReason,
        };

        if (children is not null)
        {
            foreach (var child in children)
            {
                clone.Children.Add(child);
            }
        }

        return clone;
    }

    private async Task<MetricSet> ComputeFileMetricsAsync(
        ProjectNode node,
        long fileSizeBytes,
        TextMetricsArtifact textMetricsArtifact,
        GitHistorySnapshot? gitHistorySnapshot,
        ConcurrentQueue<AnalysisIssue> diagnostics,
        CancellationToken cancellationToken)
    {
        var artifacts = new Dictionary<Type, object?>
        {
            [typeof(TextMetricsArtifact)] = textMetricsArtifact,
        };
        if (gitHistorySnapshot is not null)
        {
            var gitFileHistoryArtifact = gitHistorySnapshot.TryGetFileHistory(node.RelativePath, out var historyArtifact)
                ? historyArtifact
                : GitFileHistoryArtifact.Zero;
            artifacts[typeof(GitFileHistoryArtifact)] = gitFileHistoryArtifact;
        }

        var context = new FileMetricContext(
            fileSizeBytes,
            artifacts: artifacts,
            artifactFactories: new Dictionary<Type, FileArtifactFactory>
            {
                [typeof(SyntaxSummaryArtifact)] = ct => CreateSyntaxSummaryArtifactAsync(node.FullPath, fileSizeBytes, diagnostics, ct),
            });
        var rawBuilder = new MetricSetBuilder();

        foreach (var calculator in _fileMetricCalculators)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await calculator.ComputeAsync(context, rawBuilder, cancellationToken).ConfigureAwait(false);
        }

        var rawMetrics = rawBuilder.Build();
        if (_fileDerivedMetricCalculators.Length == 0)
        {
            return rawMetrics;
        }

        var accumulatedMetrics = rawMetrics;
        foreach (var calculator in _fileDerivedMetricCalculators)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var derivedBuilder = new MetricSetBuilder(accumulatedMetrics);
            await calculator.ComputeAsync(context, accumulatedMetrics, derivedBuilder, cancellationToken).ConfigureAwait(false);
            accumulatedMetrics = derivedBuilder.Build();
        }

        return accumulatedMetrics;
    }

    private static NodeSummary AggregateDirectorySummary(
        IEnumerable<ProjectNode> children,
        ProjectNodeKind kind)
    {
        if (kind == ProjectNodeKind.File)
        {
            return NodeSummary.Empty;
        }

        var materializedChildren = children.ToArray();
        var descendantFileCount = 0;
        var descendantDirectoryCount = 0;

        foreach (var child in materializedChildren)
        {
            descendantFileCount += child.Summary.DescendantFileCount;
            descendantDirectoryCount += child.Summary.DescendantDirectoryCount;

            if (child.Kind is ProjectNodeKind.Directory or ProjectNodeKind.Root)
            {
                descendantDirectoryCount++;
            }
        }

        return new NodeSummary(
            DescendantFileCount: descendantFileCount,
            DescendantDirectoryCount: descendantDirectoryCount);
    }

    private static NodeSummary CreateFileSummary() =>
        new(
            DescendantFileCount: 1,
            DescendantDirectoryCount: 0);

    private static MetricSet CreateSkippedComputedMetrics(long fileSizeBytes)
    {
        var builder = new MetricSetBuilder();
        builder.SetValue(MetricIds.FileSizeBytes, fileSizeBytes);
        builder.SetNotApplicable(MetricIds.Tokens);
        builder.SetNotApplicable(MetricIds.NonEmptyLines);
        builder.SetNotApplicable(MetricIds.CodeLines);
        builder.SetNotApplicable(MetricIds.MaxParameterCount);
        builder.SetNotApplicable(MetricIds.CyclomaticComplexitySum);
        builder.SetNotApplicable(MetricIds.CyclomaticComplexityMax);
        builder.SetNotApplicable(MetricIds.MaxNestingDepth);
        builder.SetNotApplicable(MetricIds.LongCallableCount);
        builder.SetNotApplicable(MetricIds.HighCyclomaticComplexityCallableCount);
        builder.SetNotApplicable(MetricIds.DeepNestingCallableCount);
        builder.SetNotApplicable(MetricIds.LongParameterListCount);
        builder.SetNotApplicable(MetricIds.CallableHotspotPoints);
        builder.SetNotApplicable(MetricIds.ComplexityPoints);
        builder.SetNotApplicable(MetricIds.RefactorPriorityPoints);
        return builder.Build();
    }

    private async ValueTask<object?> CreateSyntaxSummaryArtifactAsync(
        string fullPath,
        long fileSizeBytes,
        ConcurrentQueue<AnalysisIssue> diagnostics,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_syntaxAnalyzerRegistry.TryResolve(fullPath, out var analyzer))
        {
            return SyntaxSummaryArtifact.Unsupported();
        }

        if (fileSizeBytes > _largeFileSyntaxAnalysisThresholdBytes)
        {
            return SyntaxSummaryArtifact.Unsupported(analyzer.LanguageId);
        }

        try
        {
            var sourceText = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
            return await analyzer.AnalyzeAsync(fullPath, sourceText, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is DecoderFallbackException or InvalidDataException)
        {
            diagnostics.Enqueue(CreateDiagnostic(
                message: $"TokenMap could not decode '{fullPath}' for syntax analysis.",
                context: AppIssueContext.Create(("FullPath", fullPath))));
            _logger.LogWarning(
                exception,
                "Decoding a text file failed during syntax analysis.",
                eventCode: "analysis.syntax_decode_failed",
                context: AppIssueContext.Create(("FullPath", fullPath)));
            return SyntaxSummaryArtifact.Failed(analyzer.LanguageId);
        }
        catch (Exception exception) when (IsRecoverableFileException(exception))
        {
            diagnostics.Enqueue(CreateDiagnostic(
                message: $"TokenMap could not analyze syntax for '{fullPath}'.",
                context: AppIssueContext.Create(("FullPath", fullPath))));
            _logger.LogWarning(
                exception,
                "Analyzing syntax for a text file failed.",
                eventCode: "analysis.syntax_file_failed",
                context: AppIssueContext.Create(("FullPath", fullPath)));
            return SyntaxSummaryArtifact.Failed(analyzer.LanguageId);
        }
        catch (Exception exception)
        {
            diagnostics.Enqueue(CreateDiagnostic(
                message: $"TokenMap could not analyze syntax for '{fullPath}'.",
                context: AppIssueContext.Create(("FullPath", fullPath))));
            _logger.LogWarning(
                exception,
                "Analyzing syntax for a text file failed.",
                eventCode: "analysis.syntax_failed",
                context: AppIssueContext.Create(("FullPath", fullPath), ("LanguageId", analyzer.LanguageId)));
            return SyntaxSummaryArtifact.Failed(analyzer.LanguageId);
        }
    }

    private async Task<TextAnalysisResult> AnalyzeSmallTextFileAsync(
        string fullPath,
        ConcurrentQueue<AnalysisIssue> diagnostics,
        CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
        var normalizedContent = NormalizeNewlines(content);
        var nonEmptyLineCount = CountNonEmptyLines(normalizedContent);
        var tokens = await CountTokensWithWarningAsync(fullPath, normalizedContent, diagnostics, cancellationToken)
            .ConfigureAwait(false);

        return new TextAnalysisResult(tokens ?? 0, nonEmptyLineCount);
    }

    private async Task<TextAnalysisResult> AnalyzeLargeTextFileAsync(
        string fullPath,
        ConcurrentQueue<AnalysisIssue> diagnostics,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 16 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(
            stream,
            encoding: Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: _largeFileChunkSizeChars,
            leaveOpen: false);

        var lineCounter = new StreamingNonEmptyLineCounter();
        var readBuffer = new char[_largeFileChunkSizeChars];
        long totalTokens = 0;
        var tokenCountingFailed = false;

        while (true)
        {
            var charsRead = await reader.ReadAsync(readBuffer.AsMemory(0, readBuffer.Length), cancellationToken)
                .ConfigureAwait(false);
            if (charsRead == 0)
            {
                break;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var normalizedChunk = lineCounter.NormalizeChunk(readBuffer.AsSpan(0, charsRead));
            if (tokenCountingFailed || normalizedChunk.Length == 0)
            {
                continue;
            }

            var chunkTokens = await CountTokensWithWarningAsync(fullPath, normalizedChunk, diagnostics, cancellationToken)
                .ConfigureAwait(false);
            if (chunkTokens is null)
            {
                totalTokens = 0;
                tokenCountingFailed = true;
                continue;
            }

            totalTokens += chunkTokens.Value;
        }

        var finalChunk = lineCounter.Complete();
        if (!tokenCountingFailed && finalChunk.Length > 0)
        {
            var chunkTokens = await CountTokensWithWarningAsync(fullPath, finalChunk, diagnostics, cancellationToken)
                .ConfigureAwait(false);
            if (chunkTokens is null)
            {
                totalTokens = 0;
                tokenCountingFailed = true;
            }
            else
            {
                totalTokens += chunkTokens.Value;
            }
        }

        return new TextAnalysisResult(totalTokens, lineCounter.NonEmptyLineCount);
    }

    private static string NormalizeNewlines(string content) =>
        content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

    private static int CountNonEmptyLines(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return 0;
        }

        var hasVisibleCharacters = false;
        var nonEmptyLineCount = 0;

        foreach (var character in content)
        {
            if (character == '\n')
            {
                if (hasVisibleCharacters)
                {
                    nonEmptyLineCount++;
                }

                hasVisibleCharacters = false;
                continue;
            }

            if (!char.IsWhiteSpace(character))
            {
                hasVisibleCharacters = true;
            }
        }

        if (hasVisibleCharacters)
        {
            nonEmptyLineCount++;
        }

        return nonEmptyLineCount;
    }

    private static bool IsRecoverableFileException(Exception exception) =>
        exception is FileNotFoundException
            or DirectoryNotFoundException
            or UnauthorizedAccessException
            or IOException
            or PathTooLongException;

    private async Task<long?> CountTokensWithWarningAsync(
        string fullPath,
        string content,
        ConcurrentQueue<AnalysisIssue> diagnostics,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _tokenCounter.CountTokensAsync(content, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            diagnostics.Enqueue(CreateDiagnostic(
                message: $"TokenMap could not count tokens for '{fullPath}'.",
                context: AppIssueContext.Create(("FullPath", fullPath))));
            _logger.LogWarning(
                exception,
                "Counting tokens for a text file failed during analysis.",
                eventCode: "analysis.token_count_failed",
                context: AppIssueContext.Create(("FullPath", fullPath)));
            return null;
        }
    }

    private async Task<MetricSet?> TryRestoreCachedMetricsAsync(
        string rootPath,
        string relativePath,
        FileState fileState,
        string? contextFingerprint,
        CancellationToken cancellationToken)
    {
        var cachedMetrics = await _cacheStore!.TryGetFileMetricsAsync(
            rootPath,
            relativePath,
            fileState.FileSizeBytes,
            fileState.LastWriteTimeUtc,
            contextFingerprint,
            cancellationToken);

        if (cachedMetrics is null)
        {
            return null;
        }

        return cachedMetrics;
    }

    private async Task<GitHistorySnapshot?> TryCreateGitHistorySnapshotAsync(
        string rootPath,
        CancellationToken cancellationToken)
    {
        if (_gitHistorySnapshotProvider is null)
        {
            return null;
        }

        return await _gitHistorySnapshotProvider.TryCreateAsync(rootPath, cancellationToken)
            .ConfigureAwait(false);
    }

    private static ProjectNode ApplyRecoverableFileError(
        ProjectNode node,
        Exception exception,
        ConcurrentQueue<AnalysisIssue> diagnostics,
        long fileSizeBytes,
        IAppLogger logger)
    {
        var skippedReason = exception is FileNotFoundException or DirectoryNotFoundException
            ? SkippedReason.MissingDuringScan
            : exception is UnauthorizedAccessException
                ? SkippedReason.Inaccessible
                : SkippedReason.Error;

        diagnostics.Enqueue(CreateDiagnostic(
            message: $"TokenMap could not analyze '{node.FullPath}'.",
            context: AppIssueContext.Create(("FullPath", node.FullPath))));
        logger.LogWarning(
            exception,
            "Analyzing a file during metrics enrichment failed.",
            eventCode: "analysis.file_analyze_failed",
            context: AppIssueContext.Create(("FullPath", node.FullPath)));
        var skippedComputedMetrics = CreateSkippedComputedMetrics(fileSizeBytes);
        return CloneNode(
            node,
            CreateFileSummary(),
            skippedComputedMetrics,
            skippedReason);
    }

    private static AnalysisIssue CreateDiagnostic(
        string message,
        IReadOnlyDictionary<string, string> context) =>
        new()
        {
            Message = message,
            Context = context,
        };

    private static FileState TryGetFileState(string fullPath)
    {
        try
        {
            var fileInfo = new FileInfo(fullPath);
            return new FileState(
                Exists: fileInfo.Exists,
                FileSizeBytes: fileInfo.Exists ? fileInfo.Length : 0,
                LastWriteTimeUtc: fileInfo.Exists
                    ? new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero)
                    : DateTimeOffset.MinValue);
        }
        catch (Exception exception) when (IsRecoverableFileException(exception))
        {
            return new FileState(
                Exists: false,
                FileSizeBytes: 0,
                LastWriteTimeUtc: DateTimeOffset.MinValue);
        }
    }

    private readonly record struct FileState(
        bool Exists,
        long FileSizeBytes,
        DateTimeOffset LastWriteTimeUtc);

    private readonly record struct TextAnalysisResult(
        long Tokens,
        int NonEmptyLineCount);

    private readonly record struct FileWorkItem(int Index, ProjectNode Node);

    private sealed class StreamingNonEmptyLineCounter
    {
        private readonly StringBuilder _normalizedChunkBuilder = new();
        private bool _hasVisibleCharacters;
        private bool _pendingCarriageReturn;

        public int NonEmptyLineCount { get; private set; }

        public string NormalizeChunk(ReadOnlySpan<char> rawChunk)
        {
            _normalizedChunkBuilder.Clear();

            foreach (var character in rawChunk)
            {
                if (_pendingCarriageReturn)
                {
                    AppendNormalizedNewline();
                    _pendingCarriageReturn = false;

                    if (character == '\n')
                    {
                        continue;
                    }
                }

                if (character == '\r')
                {
                    _pendingCarriageReturn = true;
                    continue;
                }

                if (character == '\n')
                {
                    AppendNormalizedNewline();
                    continue;
                }

                _normalizedChunkBuilder.Append(character);
                if (!char.IsWhiteSpace(character))
                {
                    _hasVisibleCharacters = true;
                }
            }

            return _normalizedChunkBuilder.ToString();
        }

        public string Complete()
        {
            _normalizedChunkBuilder.Clear();

            if (_pendingCarriageReturn)
            {
                AppendNormalizedNewline();
                _pendingCarriageReturn = false;
            }

            if (_hasVisibleCharacters)
            {
                NonEmptyLineCount++;
                _hasVisibleCharacters = false;
            }

            return _normalizedChunkBuilder.ToString();
        }

        private void AppendNormalizedNewline()
        {
            _normalizedChunkBuilder.Append('\n');
            if (_hasVisibleCharacters)
            {
                NonEmptyLineCount++;
            }

            _hasVisibleCharacters = false;
        }
    }
}
