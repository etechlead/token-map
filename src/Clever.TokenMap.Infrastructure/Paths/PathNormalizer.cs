using Clever.TokenMap.Core.Enums;

namespace Clever.TokenMap.Infrastructure.Paths;

public sealed class PathNormalizer
{
    public StringComparer PathComparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public string NormalizeRootPath(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        return NormalizeAbsolutePath(rootPath);
    }

    public string NormalizeFullPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return NormalizeAbsolutePath(path);
    }

    public string NormalizeRelativePath(string rootPath, string path)
    {
        var normalizedRoot = NormalizeRootPath(rootPath);
        var normalizedPath = NormalizeFullPath(path);
        var relativePath = Path.GetRelativePath(normalizedRoot, normalizedPath);

        if (relativePath == ".")
        {
            return string.Empty;
        }

        return relativePath.Replace('\\', '/');
    }

    public string GetNodeId(string normalizedRelativePath) =>
        string.IsNullOrEmpty(normalizedRelativePath)
            ? "/"
            : normalizedRelativePath;

    public ProjectNodeKind GetNodeKind(bool isDirectory, bool isRoot = false) =>
        isRoot
            ? ProjectNodeKind.Root
            : isDirectory
                ? ProjectNodeKind.Directory
                : ProjectNodeKind.File;

    private string NormalizeAbsolutePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);

        if (!string.IsNullOrEmpty(root) && PathComparer.Equals(fullPath, root))
        {
            return root;
        }

        return Path.TrimEndingDirectorySeparator(fullPath);
    }
}
