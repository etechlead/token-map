namespace Clever.TokenMap.Core.Models;

public static class GlobalExcludeDefaults
{
    private static readonly string[] Entries =
    [
        ".git/",
        ".hg/",
        ".svn/",
    ];

    public static IReadOnlyList<string> DefaultEntries { get; } = Array.AsReadOnly(Entries);
}
