using Clever.TokenMap.Core.Diagnostics;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Logging;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Paths;
using Clever.TokenMap.Infrastructure.Filtering;
using Clever.TokenMap.Infrastructure.Filtering.Ignore;

namespace Clever.TokenMap.Infrastructure.Scanning;

public sealed class FileSystemProjectScanner : IProjectScanner
{
    private const string GitIgnoreFileName = ".gitignore";

    private readonly IAppLogger _logger;
    private readonly IPathFilter _pathFilter;
    private readonly PathNormalizer _pathNormalizer;

    public FileSystemProjectScanner(
        IPathFilter? pathFilter = null,
        PathNormalizer? pathNormalizer = null,
        IAppLogger? logger = null)
    {
        _logger = logger ?? NullAppLogger.Instance;
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

        var processedNodeCount = 0;
        var discoveredFileCount = 0;
        var diagnostics = new List<AnalysisIssue>();
        var rootNode = ScanDirectory(
            directoryInfo,
            normalizedRootPath,
            CreateInitialIgnoreContext(options),
            isRoot: true,
            options,
            diagnostics,
            ref processedNodeCount,
            ref discoveredFileCount,
            progress,
            cancellationToken);

        var snapshot = new ProjectSnapshot
        {
            RootPath = normalizedRootPath,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Options = options,
            Root = rootNode,
            Diagnostics = diagnostics,
        };

        return Task.FromResult(snapshot);
    }

    private ProjectNode ScanDirectory(
        DirectoryInfo directoryInfo,
        string normalizedRootPath,
        IgnoreDirectoryContext parentIgnoreContext,
        bool isRoot,
        ScanOptions options,
        List<AnalysisIssue> diagnostics,
        ref int processedNodeCount,
        ref int discoveredFileCount,
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
            skippedReason: null);

        ReportProgress(
            progress,
            normalizedRelativePath,
            isFile: false,
            ref processedNodeCount,
            ref discoveredFileCount);
        var ignoreContext = LoadIgnoreContext(
            directoryInfo,
            normalizedRootPath,
            parentIgnoreContext,
            options,
            diagnostics);

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
            diagnostics.Add(CreateDiagnostic(
                message: $"TokenMap could not enumerate '{directoryInfo.FullName}'.",
                context: AppIssueContext.Create(("DirectoryPath", directoryInfo.FullName))));
            _logger.LogWarning(
                exception,
                "Enumerating the directory during scanning failed.",
                eventCode: "scanner.enumerate_failed",
                context: AppIssueContext.Create(("DirectoryPath", directoryInfo.FullName)));
            return CreateNode(
                fullPath: directoryInfo.FullName,
                normalizedRelativePath,
                isDirectory: true,
                isRoot,
                skippedReason: SkippedReason.Inaccessible);
        }

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entryRelativePath = _pathNormalizer.NormalizeRelativePath(normalizedRootPath, entry.FullName);
            var isDirectory = entry.Attributes.HasFlag(FileAttributes.Directory);

            if (!IgnoreFileEvaluator.IsIncluded(ignoreContext, entryRelativePath, isDirectory))
            {
                continue;
            }

            if (!_pathFilter.IsIncluded(entryRelativePath))
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
                    skippedReason: SkippedReason.ReparsePoint));
                ReportProgress(
                    progress,
                    entryRelativePath,
                    isFile: !isDirectory,
                    ref processedNodeCount,
                    ref discoveredFileCount);
                continue;
            }

            if (isDirectory)
            {
                try
                {
                    node.Children.Add(ScanDirectory(
                        (DirectoryInfo)entry,
                        normalizedRootPath,
                        ignoreContext,
                        isRoot: false,
                        options,
                        diagnostics,
                        ref processedNodeCount,
                        ref discoveredFileCount,
                        progress,
                        cancellationToken));
                }
                catch (Exception exception) when (IsRecoverableDirectoryException(exception))
                {
                    diagnostics.Add(CreateDiagnostic(
                        message: $"TokenMap could not scan '{entry.FullName}'.",
                        context: AppIssueContext.Create(("EntryPath", entry.FullName))));
                    _logger.LogWarning(
                        exception,
                        "Scanning a directory entry failed.",
                        eventCode: "scanner.scan_entry_failed",
                        context: AppIssueContext.Create(("EntryPath", entry.FullName)));
                    node.Children.Add(CreateNode(
                        fullPath: entry.FullName,
                        normalizedRelativePath: entryRelativePath,
                        isDirectory: true,
                        isRoot: false,
                        skippedReason: SkippedReason.Inaccessible));
                    ReportProgress(
                        progress,
                        entryRelativePath,
                        isFile: false,
                        ref processedNodeCount,
                        ref discoveredFileCount);
                }

                continue;
            }

            node.Children.Add(CreateNode(
                fullPath: entry.FullName,
                normalizedRelativePath: entryRelativePath,
                isDirectory: false,
                isRoot: false,
                skippedReason: null));
            ReportProgress(
                progress,
                entryRelativePath,
                isFile: true,
                ref processedNodeCount,
                ref discoveredFileCount);
        }

        return node;
    }

    private ProjectNode CreateNode(
        string fullPath,
        string normalizedRelativePath,
        bool isDirectory,
        bool isRoot,
        SkippedReason? skippedReason)
    {
        var normalizedFullPath = _pathNormalizer.NormalizeFullPath(fullPath);
        var name = isRoot
            ? GetRootNodeName(normalizedFullPath)
            : Path.GetFileName(normalizedFullPath);

        return new ProjectNode
        {
            Id = PathNormalizer.GetNodeId(normalizedRelativePath),
            Name = name,
            FullPath = normalizedFullPath,
            RelativePath = normalizedRelativePath,
            Kind = PathNormalizer.GetNodeKind(isDirectory, isRoot),
            Metrics = NodeMetrics.Empty,
            SkippedReason = skippedReason,
        };
    }

    private static void ReportProgress(
        IProgress<AnalysisProgress>? progress,
        string normalizedRelativePath,
        bool isFile,
        ref int processedNodeCount,
        ref int discoveredFileCount)
    {
        processedNodeCount++;
        if (isFile)
        {
            discoveredFileCount++;
        }

        progress?.Report(new AnalysisProgress(
            Phase: "ScanningTree",
            ProcessedNodeCount: processedNodeCount,
            TotalNodeCount: null,
            CurrentPath: normalizedRelativePath,
            DiscoveredFileCount: discoveredFileCount));
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

    private IgnoreDirectoryContext LoadIgnoreContext(
        DirectoryInfo directoryInfo,
        string normalizedRootPath,
        IgnoreDirectoryContext parentContext,
        ScanOptions options,
        List<AnalysisIssue> diagnostics)
    {
        var additionalRules = new List<IgnoreRule>();
        var directoryRelativePath = _pathNormalizer.NormalizeRelativePath(normalizedRootPath, directoryInfo.FullName);

        if (!options.RespectGitIgnore)
        {
            return parentContext;
        }

        var ignoreFilePath = Path.Combine(directoryInfo.FullName, GitIgnoreFileName);
        if (!File.Exists(ignoreFilePath))
        {
            return parentContext;
        }

        try
        {
            additionalRules.AddRange(IgnoreFileParser.Parse(ignoreFilePath, directoryRelativePath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(CreateDiagnostic(
                message: $"TokenMap could not read '{ignoreFilePath}'.",
                context: AppIssueContext.Create(("IgnoreFilePath", ignoreFilePath))));
            _logger.LogWarning(
                exception,
                "Reading an ignore file during scanning failed.",
                eventCode: "scanner.ignore_file_read_failed",
                context: AppIssueContext.Create(("IgnoreFilePath", ignoreFilePath)));
        }

        return additionalRules.Count == 0
            ? parentContext
            : parentContext.AppendBetween(additionalRules);
    }

    private static AnalysisIssue CreateDiagnostic(
        string message,
        IReadOnlyDictionary<string, string> context) =>
        new()
        {
            Message = message,
            Context = context,
        };

    private static IgnoreDirectoryContext CreateInitialIgnoreContext(ScanOptions options)
    {
        var globalRules = options.UseGlobalExcludes
            ? IgnoreFileParser.ParseLines(options.GlobalExcludes, baseRelativePath: string.Empty)
            : [];
        var folderRules = options.UseFolderExcludes
            ? IgnoreFileParser.ParseLines(options.FolderExcludes, baseRelativePath: string.Empty)
            : [];

        return new IgnoreDirectoryContext(globalRules, folderRules);
    }
}
