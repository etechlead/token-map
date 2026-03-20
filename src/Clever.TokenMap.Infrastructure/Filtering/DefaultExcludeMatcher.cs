namespace Clever.TokenMap.Infrastructure.Filtering;

public sealed class DefaultExcludeMatcher
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
    OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    public bool IsExcluded(string normalizedRelativePath, bool isDirectory)
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
