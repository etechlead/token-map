using System.Text;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Infrastructure.Logging;

namespace Clever.TokenMap.Infrastructure.Analysis;

public sealed class ProjectSnapshotMetricsEnricher
{
    private readonly ICacheStore? _cacheStore;
    private readonly IAppLogger _logger;
    private readonly ITextFileDetector _textFileDetector;
    private readonly ITokenCounter _tokenCounter;

    public ProjectSnapshotMetricsEnricher(
        ITextFileDetector textFileDetector,
        ITokenCounter tokenCounter,
        ICacheStore? cacheStore = null,
        IAppLogger? logger = null)
    {
        _cacheStore = cacheStore;
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
            var enrichedFile = await EnrichFileNodeAsync(node, warnings, cancellationToken);
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
            var cachedMetrics = await TryRestoreCachedMetricsAsync(node, fileState, cancellationToken);
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

        string content;
        try
        {
            content = await File.ReadAllTextAsync(node.FullPath, cancellationToken);
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

        var normalizedContent = NormalizeNewlines(content);
        var nonEmptyLineCount = CountNonEmptyLines(normalizedContent);
        long tokens = 0;

        try
        {
            tokens = await _tokenCounter.CountTokensAsync(normalizedContent, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            warnings.Add($"Unable to count tokens for '{node.FullPath}': {exception.Message}");
            _logger.LogWarning(exception, $"Token counting failed for '{node.FullPath}'.");
        }

        var metrics = new NodeMetrics(
            Tokens: tokens,
            TotalLines: nonEmptyLineCount,
            FileSizeBytes: fileSizeBytes,
            DescendantFileCount: 1,
            DescendantDirectoryCount: 0);

        if (fileState.Exists && _cacheStore is not null)
        {
            await _cacheStore.SetFileMetricsAsync(
                node.FullPath,
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
        var totalLines = 0;
        long fileSizeBytes = 0;
        var descendantFileCount = 0;
        var descendantDirectoryCount = 0;

        foreach (var child in children)
        {
            tokens += child.Metrics.Tokens;
            totalLines += child.Metrics.TotalLines;
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
            TotalLines: totalLines,
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
            TotalLines: 0,
            FileSizeBytes: fileSizeBytes,
            DescendantFileCount: 1,
            DescendantDirectoryCount: 0);

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

    private async Task<NodeMetrics?> TryRestoreCachedMetricsAsync(
        ProjectNode node,
        FileState fileState,
        CancellationToken cancellationToken)
    {
        var cachedMetrics = await _cacheStore!.TryGetFileMetricsAsync(
            node.FullPath,
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

    private sealed class ProgressState
    {
        public int ProcessedFileCount { get; set; }
    }
}
