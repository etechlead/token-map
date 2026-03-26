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
    private int _processedNodeCount;

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

        _processedNodeCount = 0;
        var warnings = new List<string>();
        var rootNode = ScanDirectory(
            directoryInfo,
            normalizedRootPath,
            CreateInitialIgnoreContext(options),
            isRoot: true,
            options,
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
        IgnoreDirectoryContext parentIgnoreContext,
        bool isRoot,
        ScanOptions options,
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
            skippedReason: null);

        ReportProgress(progress, normalizedRelativePath);
        var ignoreContext = LoadIgnoreContext(
            directoryInfo,
            normalizedRootPath,
            parentIgnoreContext,
            options,
            warnings);

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
            _logger.LogWarning(exception, $"Unable to enumerate '{directoryInfo.FullName}'.");
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
                        ignoreContext,
                        isRoot: false,
                        options,
                        warnings,
                        progress,
                        cancellationToken));
                }
                catch (Exception exception) when (IsRecoverableDirectoryException(exception))
                {
                    warnings.Add($"Unable to scan '{entry.FullName}': {exception.Message}");
                    _logger.LogWarning(exception, $"Unable to scan '{entry.FullName}'.");
                    node.Children.Add(CreateNode(
                        fullPath: entry.FullName,
                        normalizedRelativePath: entryRelativePath,
                        isDirectory: true,
                        isRoot: false,
                        skippedReason: SkippedReason.Inaccessible));
                    ReportProgress(progress, entryRelativePath);
                }

                continue;
            }

            node.Children.Add(CreateNode(
                fullPath: entry.FullName,
                normalizedRelativePath: entryRelativePath,
                isDirectory: false,
                isRoot: false,
                skippedReason: null));
            ReportProgress(progress, entryRelativePath);
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

    private IgnoreDirectoryContext LoadIgnoreContext(
        DirectoryInfo directoryInfo,
        string normalizedRootPath,
        IgnoreDirectoryContext parentContext,
        ScanOptions options,
        List<string> warnings)
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
            warnings.Add($"Unable to read '{ignoreFilePath}': {exception.Message}");
            _logger.LogWarning(exception, $"Unable to read ignore file '{ignoreFilePath}'.");
        }

        return additionalRules.Count == 0
            ? parentContext
            : parentContext.AppendBetween(additionalRules);
    }

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
