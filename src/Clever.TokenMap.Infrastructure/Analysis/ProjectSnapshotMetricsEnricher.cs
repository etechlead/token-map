using System.Text;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Infrastructure.Analysis;

public sealed class ProjectSnapshotMetricsEnricher
{
    private readonly ICacheStore? _cacheStore;
    private readonly ITextFileDetector _textFileDetector;
    private readonly ITokenCounter _tokenCounter;
    private readonly ITokeiRunner _tokeiRunner;
    private readonly StringComparer _pathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public ProjectSnapshotMetricsEnricher(
        ITextFileDetector textFileDetector,
        ITokenCounter tokenCounter,
        ITokeiRunner tokeiRunner,
        ICacheStore? cacheStore = null)
    {
        _cacheStore = cacheStore;
        _textFileDetector = textFileDetector;
        _tokenCounter = tokenCounter;
        _tokeiRunner = tokeiRunner;
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
        var includedFilePaths = CollectIncludedFilePaths(snapshot.Root);
        var totalFileCount = CountFileNodes(snapshot.Root);
        IReadOnlyDictionary<string, TokeiFileStats> tokeiStats;

        try
        {
            tokeiStats = await _tokeiRunner.CollectAsync(snapshot.RootPath, includedFilePaths, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            warnings.Add($"Unable to collect tokei metrics: {exception.Message}");
            tokeiStats = new Dictionary<string, TokeiFileStats>(_pathComparer);
        }

        var progressState = new ProgressState();
        await EnrichNodeAsync(
            snapshot.Root,
            snapshot.Options.TokenProfile,
            tokeiStats,
            warnings,
            progress,
            totalFileCount,
            progressState,
            cancellationToken);
        AggregateNode(snapshot.Root);

        return new ProjectSnapshot
        {
            RootPath = snapshot.RootPath,
            CapturedAtUtc = snapshot.CapturedAtUtc,
            Options = snapshot.Options,
            Root = snapshot.Root,
            Warnings = warnings,
        };
    }

    private async Task EnrichNodeAsync(
        ProjectNode node,
        TokenProfile tokenProfile,
        IReadOnlyDictionary<string, TokeiFileStats> tokeiStats,
        List<string> warnings,
        IProgress<AnalysisProgress>? progress,
        int totalFileCount,
        ProgressState progressState,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (node.Kind == ProjectNodeKind.File)
        {
            await EnrichFileNodeAsync(node, tokenProfile, tokeiStats, warnings, cancellationToken);
            progressState.ProcessedFileCount++;
            progress?.Report(new AnalysisProgress(
                Phase: "AnalyzingFiles",
                ProcessedNodeCount: progressState.ProcessedFileCount,
                TotalNodeCount: totalFileCount,
                CurrentPath: node.RelativePath));
            return;
        }

        foreach (var child in node.Children)
        {
            await EnrichNodeAsync(
                child,
                tokenProfile,
                tokeiStats,
                warnings,
                progress,
                totalFileCount,
                progressState,
                cancellationToken);
        }
    }

    private async Task EnrichFileNodeAsync(
        ProjectNode node,
        TokenProfile tokenProfile,
        IReadOnlyDictionary<string, TokeiFileStats> tokeiStats,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var fileState = TryGetFileState(node.FullPath);
        var fileSizeBytes = fileState.FileSizeBytes;

        if (node.SkippedReason is not null)
        {
            node.Metrics = CreateSkippedFileMetrics(fileSizeBytes);
            return;
        }

        if (fileState.Exists &&
            _cacheStore is not null &&
            await TryRestoreCachedMetricsAsync(node, fileState, tokenProfile, cancellationToken))
        {
            return;
        }

        bool isText;
        try
        {
            isText = await _textFileDetector.IsTextAsync(node.FullPath, cancellationToken);
        }
        catch (Exception exception) when (IsRecoverableFileException(exception))
        {
            ApplyRecoverableFileError(node, exception, warnings, fileSizeBytes);
            return;
        }

        if (!isText)
        {
            node.SkippedReason = SkippedReason.Binary;
            node.DiagnosticMessage = "Binary file is excluded from token and LOC analysis.";
            node.Metrics = CreateSkippedFileMetrics(fileSizeBytes);
            return;
        }

        string content;
        try
        {
            content = await File.ReadAllTextAsync(node.FullPath, cancellationToken);
        }
        catch (Exception exception) when (IsRecoverableFileException(exception))
        {
            ApplyRecoverableFileError(node, exception, warnings, fileSizeBytes);
            return;
        }
        catch (Exception exception) when (exception is DecoderFallbackException or InvalidDataException)
        {
            node.SkippedReason = SkippedReason.Unsupported;
            node.DiagnosticMessage = exception.Message;
            warnings.Add($"Unable to decode '{node.FullPath}': {exception.Message}");
            node.Metrics = CreateSkippedFileMetrics(fileSizeBytes);
            return;
        }

        var normalizedContent = NormalizeNewlines(content);
        var totalLines = CountTotalLines(normalizedContent);
        long tokens = 0;

        try
        {
            tokens = await _tokenCounter.CountTokensAsync(normalizedContent, tokenProfile, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            warnings.Add($"Unable to count tokens for '{node.FullPath}': {exception.Message}");
            node.DiagnosticMessage = $"Token counting failed: {exception.Message}";
        }

        tokeiStats.TryGetValue(node.RelativePath, out var tokeiFileStats);

        node.Metrics = new NodeMetrics(
            Tokens: tokens,
            TotalLines: tokeiFileStats?.TotalLines ?? totalLines,
            CodeLines: tokeiFileStats?.CodeLines,
            CommentLines: tokeiFileStats?.CommentLines,
            BlankLines: tokeiFileStats?.BlankLines,
            Language: tokeiFileStats?.Language,
            FileSizeBytes: fileSizeBytes,
            DescendantFileCount: 1,
            DescendantDirectoryCount: 0);

        if (fileState.Exists && _cacheStore is not null)
        {
            await _cacheStore.SetFileMetricsAsync(
                node.FullPath,
                fileState.FileSizeBytes,
                fileState.LastWriteTimeUtc,
                tokenProfile,
                node.Metrics,
                cancellationToken);
        }
    }

    private static NodeMetrics AggregateNode(ProjectNode node)
    {
        if (node.Kind == ProjectNodeKind.File)
        {
            return node.Metrics;
        }

        var childMetrics = new List<NodeMetrics>(node.Children.Count);
        foreach (var child in node.Children)
        {
            childMetrics.Add(AggregateNode(child));
        }

        long tokens = 0;
        var totalLines = 0;
        long fileSizeBytes = 0;
        var descendantFileCount = 0;
        var descendantDirectoryCount = 0;

        var codeLines = SumNullableMetric(childMetrics.Select(metrics => metrics.CodeLines));
        var commentLines = SumNullableMetric(childMetrics.Select(metrics => metrics.CommentLines));
        var blankLines = SumNullableMetric(childMetrics.Select(metrics => metrics.BlankLines));

        for (var index = 0; index < node.Children.Count; index++)
        {
            var child = node.Children[index];
            var metrics = childMetrics[index];
            tokens += metrics.Tokens;
            totalLines += metrics.TotalLines;
            fileSizeBytes += metrics.FileSizeBytes;
            descendantFileCount += metrics.DescendantFileCount;
            descendantDirectoryCount += metrics.DescendantDirectoryCount;

            if (child.Kind is ProjectNodeKind.Directory or ProjectNodeKind.Root)
            {
                descendantDirectoryCount++;
            }
        }

        node.Metrics = new NodeMetrics(
            Tokens: tokens,
            TotalLines: totalLines,
            CodeLines: codeLines,
            CommentLines: commentLines,
            BlankLines: blankLines,
            Language: null,
            FileSizeBytes: fileSizeBytes,
            DescendantFileCount: descendantFileCount,
            DescendantDirectoryCount: descendantDirectoryCount);

        return node.Metrics;
    }

    private static IReadOnlyCollection<string> CollectIncludedFilePaths(ProjectNode root)
    {
        var includedFiles = new List<string>();
        CollectIncludedFilePaths(root, includedFiles);
        return includedFiles;
    }

    private static void CollectIncludedFilePaths(ProjectNode node, List<string> includedFiles)
    {
        if (node.Kind == ProjectNodeKind.File && node.SkippedReason is null)
        {
            includedFiles.Add(node.RelativePath);
        }

        foreach (var child in node.Children)
        {
            CollectIncludedFilePaths(child, includedFiles);
        }
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
            CodeLines: null,
            CommentLines: null,
            BlankLines: null,
            Language: null,
            FileSizeBytes: fileSizeBytes,
            DescendantFileCount: 1,
            DescendantDirectoryCount: 0);

    private static int? SumNullableMetric(IEnumerable<int?> values)
    {
        var hasValue = false;
        var sum = 0;

        foreach (var value in values)
        {
            if (!value.HasValue)
            {
                continue;
            }

            hasValue = true;
            sum += value.Value;
        }

        return hasValue ? sum : null;
    }

    private static string NormalizeNewlines(string content) =>
        content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

    private static int CountTotalLines(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return 0;
        }

        var lines = 1;
        foreach (var character in content)
        {
            if (character == '\n')
            {
                lines++;
            }
        }

        return lines;
    }

    private static bool IsRecoverableFileException(Exception exception) =>
        exception is FileNotFoundException
            or DirectoryNotFoundException
            or UnauthorizedAccessException
            or IOException
            or PathTooLongException;

    private async Task<bool> TryRestoreCachedMetricsAsync(
        ProjectNode node,
        FileState fileState,
        TokenProfile tokenProfile,
        CancellationToken cancellationToken)
    {
        var cachedMetrics = await _cacheStore!.TryGetFileMetricsAsync(
            node.FullPath,
            fileState.FileSizeBytes,
            fileState.LastWriteTimeUtc,
            tokenProfile,
            cancellationToken);

        if (cachedMetrics is null)
        {
            return false;
        }

        node.Metrics = cachedMetrics;
        return true;
    }

    private static void ApplyRecoverableFileError(
        ProjectNode node,
        Exception exception,
        List<string> warnings,
        long fileSizeBytes)
    {
        node.SkippedReason = exception is FileNotFoundException or DirectoryNotFoundException
            ? SkippedReason.MissingDuringScan
            : exception is UnauthorizedAccessException
                ? SkippedReason.Inaccessible
                : SkippedReason.Error;
        node.DiagnosticMessage = exception.Message;
        node.Metrics = CreateSkippedFileMetrics(fileSizeBytes);

        warnings.Add($"Unable to analyze '{node.FullPath}': {exception.Message}");
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
