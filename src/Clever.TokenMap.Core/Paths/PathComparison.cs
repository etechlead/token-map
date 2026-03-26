namespace Clever.TokenMap.Core.Paths;

public static class PathComparison
{
    public static bool UsesCaseInsensitivePaths { get; } = OperatingSystem.IsWindows();

    public static StringComparer Comparer { get; } =
        UsesCaseInsensitivePaths ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
