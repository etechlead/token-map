namespace Clever.TokenMap.Infrastructure.Filtering;

using Clever.TokenMap.Infrastructure.Paths;

public static class DefaultExcludeMatcher
{
    private static readonly string[] ExcludedDirectoryNames =
    [
        ".git",
        ".hg",
        ".svn",
        ".vs",
        "node_modules",
        "target",
        "vendor",
    ];

    private static readonly HashSet<string> DirectoryNameLookup = new(
        ExcludedDirectoryNames,
        PathComparison.Comparer);

    public static IReadOnlyList<string> DefaultDirectoryNames { get; } = Array.AsReadOnly(ExcludedDirectoryNames);

    public static bool IsExcluded(string normalizedRelativePath, bool isDirectory)
    {
        if (!isDirectory || string.IsNullOrEmpty(normalizedRelativePath))
        {
            return false;
        }

        return normalizedRelativePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => DirectoryNameLookup.Contains(segment));
    }
}
