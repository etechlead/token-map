namespace Clever.TokenMap.Infrastructure.Filtering;

using Clever.TokenMap.Infrastructure.Paths;

public static class DefaultExcludeMatcher
{
    private static readonly HashSet<string> DirectoryNames = new(
    [
        ".git",
        ".vs",
        ".idea",
        ".vscode",
        "node_modules",
        "bin",
        "obj",
        "dist",
        "build",
        "out",
        "coverage",
        "target",
        "Debug",
        "Release",
    ],
    PathComparison.Comparer);

    public static bool IsExcluded(string normalizedRelativePath, bool isDirectory)
    {
        if (!isDirectory || string.IsNullOrEmpty(normalizedRelativePath))
        {
            return false;
        }

        return normalizedRelativePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => DirectoryNames.Contains(segment));
    }
}
