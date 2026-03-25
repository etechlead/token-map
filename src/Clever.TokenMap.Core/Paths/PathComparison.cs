using System.Text.RegularExpressions;

namespace Clever.TokenMap.Core.Paths;

public static class PathComparison
{
    public static StringComparer Comparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static RegexOptions PathRegexOptions { get; } =
        RegexOptions.Compiled |
        RegexOptions.CultureInvariant |
        (OperatingSystem.IsWindows() ? RegexOptions.IgnoreCase : RegexOptions.None);
}
