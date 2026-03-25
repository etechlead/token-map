using System.Text;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Logging;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Infrastructure.Analysis;

public sealed class ProjectSnapshotMetricsEnricher
{
    private const long LargeFileTokenizationThresholdBytes = 1024L * 1024L;
    private const int LargeFileChunkSizeChars = 256 * 1024;

    private readonly ICacheStore? _cacheStore;
    private readonly int _largeFileChunkSizeChars;
    private readonly long _largeFileTokenizationThresholdBytes;
    private readonly IAppLogger _logger;
    private readonly ITextFileDetector _textFileDetector;
    private readonly ITokenCounter _tokenCounter;

    public ProjectSnapshotMetricsEnricher(
        ITextFileDetector textFileDetector,
        ITokenCounter tokenCounter,
        ICacheStore? cacheStore = null,
        IAppLogger? logger = null,
        long largeFileTokenizationThresholdBytes = LargeFileTokenizationThresholdBytes,
        int largeFileChunkSizeChars = LargeFileChunkSizeChars)
    {
        _cacheStore = cacheStore;
        _largeFileChunkSizeChars = largeFileChunkSizeChars > 0
            ? largeFileChunkSizeChars
            : throw new ArgumentOutOfRangeException(nameof(largeFileChunkSizeChars));
        _largeFileTokenizationThresholdBytes = largeFileTokenizationThresholdBytes >= 0
            ? largeFileTokenizationThresholdBytes
            : throw new ArgumentOutOfRangeException(nameof(largeFileTokenizationThresholdBytes));
        _logger = logger ?? NullAppLogger.Instance;
        _textFileDetector = textFileDetector;
        _tokenCounter = tokenCounter;
    }

    public Task<ProjectSnapshot> EnrichAsync(ProjectSnapshot snapshot, CancellationToken cancellationToken) =>
        EnrichAsync(snapshot, progress: null, cancellationToken);

    public async Task<ProjectSnapshot> EnrichAsync(
        ProjectSnapshot snapshot,
        IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var warnings = new List<string>(snapshot.Warnings);
        var totalFileCount = CountFileNodes(snapshot.Root);
        var progressState = new ProgressState();
        var enrichedRoot = await EnrichNodeAsync(
            snapshot.RootPath,
            snapshot.Root,
            warnings,
            progress,
            totalFileCount,
            progressState,
            cancellationToken);

        return new ProjectSnapshot
        {
            RootPath = snapshot.RootPath,
            CapturedAtUtc = snapshot.CapturedAtUtc,
            Options = snapshot.Options,
            Root = enrichedRoot,
            Warnings = warnings,
        };
    }

    private async Task<ProjectNode> EnrichNodeAsync(
        string rootPath,
        ProjectNode node,
        List<string> warnings,
        IProgress<AnalysisProgress>? progress,
        int totalFileCount,
        ProgressState progressState,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (node.Kind == ProjectNodeKind.File)
        {
            var enrichedFile = await EnrichFileNodeAsync(rootPath, node, warnings, cancellationToken);
            progressState.ProcessedFileCount++;
            progress?.Report(new AnalysisProgress(
                Phase: "AnalyzingFiles",
                ProcessedNodeCount: progressState.ProcessedFileCount,
                TotalNodeCount: totalFileCount,
                CurrentPath: enrichedFile.RelativePath));
            return enrichedFile;
        }

        var enrichedChildren = new List<ProjectNode>(node.Children.Count);
        foreach (var child in node.Children)
        {
            enrichedChildren.Add(await EnrichNodeAsync(
                rootPath,
                child,
                warnings,
                progress,
                totalFileCount,
                progressState,
                cancellationToken));
        }

        var metrics = AggregateNode(enrichedChildren, node.Kind);
        return CloneNode(node, metrics, node.SkippedReason, enrichedChildren);
    }

    private async Task<ProjectNode> EnrichFileNodeAsync(
        string rootPath,
        ProjectNode node,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var fileState = TryGetFileState(node.FullPath);
        var fileSizeBytes = fileState.FileSizeBytes;

        if (node.SkippedReason is not null)
        {
            return CloneNode(node, CreateSkippedFileMetrics(fileSizeBytes), node.SkippedReason);
        }

        if (fileState.Exists && _cacheStore is not null)
        {
            var cachedMetrics = await TryRestoreCachedMetricsAsync(
                rootPath,
                node.RelativePath,
                fileState,
                cancellationToken);
            if (cachedMetrics is not null)
            {
                _logger.LogTrace($"Restored cached metrics for '{node.FullPath}'.");
                return CloneNode(node, cachedMetrics, node.SkippedReason);
            }
        }

        bool isText;
        try
        {
            isText = await _textFileDetector.IsTextAsync(node.FullPath, cancellationToken);
        }
        catch (Exception exception) when (IsRecoverableFileException(exception))
        {
            return ApplyRecoverableFileError(node, exception, warnings, fileSizeBytes, _logger);
        }

        if (!isText)
        {
            return CloneNode(node, CreateSkippedFileMetrics(fileSizeBytes), SkippedReason.Binary);
        }

        TextAnalysisResult textAnalysis;
        try
        {
            textAnalysis = fileState.FileSizeBytes > _largeFileTokenizationThresholdBytes
                ? await AnalyzeLargeTextFileAsync(node.FullPath, warnings, cancellationToken).ConfigureAwait(false)
                : await AnalyzeSmallTextFileAsync(node.FullPath, warnings, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsRecoverableFileException(exception))
        {
            return ApplyRecoverableFileError(node, exception, warnings, fileSizeBytes, _logger);
        }
        catch (Exception exception) when (exception is DecoderFallbackException or InvalidDataException)
        {
            warnings.Add($"Unable to decode '{node.FullPath}': {exception.Message}");
            _logger.LogWarning(exception, $"Unable to decode '{node.FullPath}'.");
            return CloneNode(node, CreateSkippedFileMetrics(fileSizeBytes), SkippedReason.Unsupported);
        }

        var metrics = new NodeMetrics(
            Tokens: textAnalysis.Tokens,
            NonEmptyLines: textAnalysis.NonEmptyLineCount,
            FileSizeBytes: fileSizeBytes,
            DescendantFileCount: 1,
            DescendantDirectoryCount: 0);

        if (fileState.Exists && _cacheStore is not null)
        {
            await _cacheStore.SetFileMetricsAsync(
                rootPath,
                node.RelativePath,
                fileState.FileSizeBytes,
                fileState.LastWriteTimeUtc,
                metrics,
                cancellationToken);
        }

        return CloneNode(node, metrics, node.SkippedReason);
    }

    private static NodeMetrics AggregateNode(IEnumerable<ProjectNode> children, ProjectNodeKind kind)
    {
        if (kind == ProjectNodeKind.File)
        {
            return NodeMetrics.Empty;
        }

        long tokens = 0;
        var nonEmptyLines = 0;
        long fileSizeBytes = 0;
        var descendantFileCount = 0;
        var descendantDirectoryCount = 0;

        foreach (var child in children)
        {
            tokens += child.Metrics.Tokens;
            nonEmptyLines += child.Metrics.NonEmptyLines;
            fileSizeBytes += child.Metrics.FileSizeBytes;
            descendantFileCount += child.Metrics.DescendantFileCount;
            descendantDirectoryCount += child.Metrics.DescendantDirectoryCount;

            if (child.Kind is ProjectNodeKind.Directory or ProjectNodeKind.Root)
            {
                descendantDirectoryCount++;
            }
        }

        return new NodeMetrics(
            Tokens: tokens,
            NonEmptyLines: nonEmptyLines,
            FileSizeBytes: fileSizeBytes,
            DescendantFileCount: descendantFileCount,
            DescendantDirectoryCount: descendantDirectoryCount);
    }

    private static ProjectNode CloneNode(
        ProjectNode source,
        NodeMetrics metrics,
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
            Metrics = metrics,
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

    private static int CountFileNodes(ProjectNode node)
    {
        if (node.Kind == ProjectNodeKind.File)
        {
            return 1;
        }

        var count = 0;
        foreach (var child in node.Children)
        {
            count += CountFileNodes(child);
        }

        return count;
    }

    private static NodeMetrics CreateSkippedFileMetrics(long fileSizeBytes) =>
        new(
            Tokens: 0,
            NonEmptyLines: 0,
            FileSizeBytes: fileSizeBytes,
            DescendantFileCount: 1,
            DescendantDirectoryCount: 0);

    private async Task<TextAnalysisResult> AnalyzeSmallTextFileAsync(
        string fullPath,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
        var normalizedContent = NormalizeNewlines(content);
        var nonEmptyLineCount = CountNonEmptyLines(normalizedContent);
        var tokens = await CountTokensWithWarningAsync(fullPath, normalizedContent, warnings, cancellationToken)
            .ConfigureAwait(false);

        return new TextAnalysisResult(tokens ?? 0, nonEmptyLineCount);
    }

    private async Task<TextAnalysisResult> AnalyzeLargeTextFileAsync(
        string fullPath,
        List<string> warnings,
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

            var chunkTokens = await CountTokensWithWarningAsync(fullPath, normalizedChunk, warnings, cancellationToken)
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
            var chunkTokens = await CountTokensWithWarningAsync(fullPath, finalChunk, warnings, cancellationToken)
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
        List<string> warnings,
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
            warnings.Add($"Unable to count tokens for '{fullPath}': {exception.Message}");
            _logger.LogWarning(exception, $"Token counting failed for '{fullPath}'.");
            return null;
        }
    }

    private async Task<NodeMetrics?> TryRestoreCachedMetricsAsync(
        string rootPath,
        string relativePath,
        FileState fileState,
        CancellationToken cancellationToken)
    {
        var cachedMetrics = await _cacheStore!.TryGetFileMetricsAsync(
            rootPath,
            relativePath,
            fileState.FileSizeBytes,
            fileState.LastWriteTimeUtc,
            cancellationToken);

        if (cachedMetrics is null)
        {
            return null;
        }

        return cachedMetrics;
    }

    private static ProjectNode ApplyRecoverableFileError(
        ProjectNode node,
        Exception exception,
        List<string> warnings,
        long fileSizeBytes,
        IAppLogger logger)
    {
        var skippedReason = exception is FileNotFoundException or DirectoryNotFoundException
            ? SkippedReason.MissingDuringScan
            : exception is UnauthorizedAccessException
                ? SkippedReason.Inaccessible
                : SkippedReason.Error;

        warnings.Add($"Unable to analyze '{node.FullPath}': {exception.Message}");
        logger.LogWarning(exception, $"Unable to analyze '{node.FullPath}'.");
        return CloneNode(node, CreateSkippedFileMetrics(fileSizeBytes), skippedReason);
    }

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

    private sealed class ProgressState
    {
        public int ProcessedFileCount { get; set; }
    }

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
