using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Infrastructure.Filtering;
using Clever.TokenMap.Infrastructure.Paths;

namespace Clever.TokenMap.Infrastructure.Scanning;

public sealed class FileSystemProjectScanner : IProjectScanner
{
    private readonly IPathFilter _pathFilter;
    private readonly PathNormalizer _pathNormalizer;
    private int _processedNodeCount;

    public FileSystemProjectScanner(IPathFilter? pathFilter = null, PathNormalizer? pathNormalizer = null)
    {
        _pathFilter = pathFilter ?? new AllowAllPathFilter();
        _pathNormalizer = pathNormalizer ?? new PathNormalizer();
    }

    public Task<ProjectSnapshot> ScanAsync(
        string rootPath,
        ScanOptions options,
        IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        cancellationToken.ThrowIfCancellationRequested();

        var normalizedRootPath = _pathNormalizer.NormalizeRootPath(rootPath);
        var directoryInfo = new DirectoryInfo(normalizedRootPath);

        if (!directoryInfo.Exists)
        {
            throw new DirectoryNotFoundException($"Project root was not found: {normalizedRootPath}");
        }

        _processedNodeCount = 0;
        var warnings = new List<string>();
        var rootNode = ScanDirectory(
            directoryInfo,
            normalizedRootPath,
            isRoot: true,
            warnings,
            progress,
            cancellationToken);

        var snapshot = new ProjectSnapshot
        {
            RootPath = normalizedRootPath,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Options = options,
            Root = rootNode,
            Warnings = warnings,
        };

        return Task.FromResult(snapshot);
    }

    private ProjectNode ScanDirectory(
        DirectoryInfo directoryInfo,
        string normalizedRootPath,
        bool isRoot,
        List<string> warnings,
        IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedRelativePath = _pathNormalizer.NormalizeRelativePath(normalizedRootPath, directoryInfo.FullName);
        var node = CreateNode(
            fullPath: directoryInfo.FullName,
            normalizedRelativePath,
            isDirectory: true,
            isRoot,
            skippedReason: null,
            diagnosticMessage: null);

        ReportProgress(progress, normalizedRelativePath);

        FileSystemInfo[] entries;
        try
        {
            entries = directoryInfo
                .EnumerateFileSystemInfos()
                .OrderByDescending(entry => entry.Attributes.HasFlag(FileAttributes.Directory))
                .ThenBy(entry => entry.Name, _pathNormalizer.PathComparer)
                .ThenBy(entry => entry.FullName, _pathNormalizer.PathComparer)
                .ToArray();
        }
        catch (Exception exception) when (IsRecoverableDirectoryException(exception))
        {
            warnings.Add($"Unable to enumerate '{directoryInfo.FullName}': {exception.Message}");
            return CreateNode(
                fullPath: directoryInfo.FullName,
                normalizedRelativePath,
                isDirectory: true,
                isRoot,
                skippedReason: SkippedReason.Inaccessible,
                diagnosticMessage: exception.Message);
        }

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entryRelativePath = _pathNormalizer.NormalizeRelativePath(normalizedRootPath, entry.FullName);
            var isDirectory = entry.Attributes.HasFlag(FileAttributes.Directory);

            if (!_pathFilter.IsIncluded(entry.FullName, entryRelativePath, isDirectory))
            {
                continue;
            }

            if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                node.Children.Add(CreateNode(
                    fullPath: entry.FullName,
                    normalizedRelativePath: entryRelativePath,
                    isDirectory,
                    isRoot: false,
                    skippedReason: SkippedReason.ReparsePoint,
                    diagnosticMessage: "Reparse points are skipped in MVP."));
                ReportProgress(progress, entryRelativePath);
                continue;
            }

            if (isDirectory)
            {
                try
                {
                    node.Children.Add(ScanDirectory(
                        (DirectoryInfo)entry,
                        normalizedRootPath,
                        isRoot: false,
                        warnings,
                        progress,
                        cancellationToken));
                }
                catch (Exception exception) when (IsRecoverableDirectoryException(exception))
                {
                    warnings.Add($"Unable to scan '{entry.FullName}': {exception.Message}");
                    node.Children.Add(CreateNode(
                        fullPath: entry.FullName,
                        normalizedRelativePath: entryRelativePath,
                        isDirectory: true,
                        isRoot: false,
                        skippedReason: SkippedReason.Inaccessible,
                        diagnosticMessage: exception.Message));
                    ReportProgress(progress, entryRelativePath);
                }

                continue;
            }

            node.Children.Add(CreateNode(
                fullPath: entry.FullName,
                normalizedRelativePath: entryRelativePath,
                isDirectory: false,
                isRoot: false,
                skippedReason: null,
                diagnosticMessage: null));
            ReportProgress(progress, entryRelativePath);
        }

        return node;
    }

    private ProjectNode CreateNode(
        string fullPath,
        string normalizedRelativePath,
        bool isDirectory,
        bool isRoot,
        SkippedReason? skippedReason,
        string? diagnosticMessage)
    {
        var normalizedFullPath = _pathNormalizer.NormalizeFullPath(fullPath);
        var name = isRoot
            ? GetRootNodeName(normalizedFullPath)
            : Path.GetFileName(normalizedFullPath);

        return new ProjectNode
        {
            Id = _pathNormalizer.GetNodeId(normalizedRelativePath),
            Name = name,
            FullPath = normalizedFullPath,
            RelativePath = normalizedRelativePath,
            Kind = _pathNormalizer.GetNodeKind(isDirectory, isRoot),
            Metrics = NodeMetrics.Empty,
            SkippedReason = skippedReason,
            DiagnosticMessage = diagnosticMessage,
        };
    }

    private void ReportProgress(IProgress<AnalysisProgress>? progress, string normalizedRelativePath)
    {
        _processedNodeCount++;
        progress?.Report(new AnalysisProgress(
            Phase: "ScanningTree",
            ProcessedNodeCount: _processedNodeCount,
            TotalNodeCount: null,
            CurrentPath: normalizedRelativePath));
    }

    private static string GetRootNodeName(string normalizedRootPath)
    {
        var name = Path.GetFileName(normalizedRootPath);

        return string.IsNullOrEmpty(name)
            ? normalizedRootPath
            : name;
    }

    private static bool IsRecoverableDirectoryException(Exception exception) =>
        exception is UnauthorizedAccessException
        or IOException
        or DirectoryNotFoundException
        or PathTooLongException;
}
