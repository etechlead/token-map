namespace Clever.TokenMap.Tests.Support;

internal static class TestPaths
{
    private static readonly string BasePath = Path.Combine(Path.GetTempPath(), "TokenMap.Tests");

    internal static string Folder(string name) => Path.Combine(BasePath, name);

    internal static string CombineUnder(string rootPath, params string[] segments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        if (segments.Length == 0)
        {
            return rootPath;
        }

        var parts = new string[segments.Length + 1];
        parts[0] = rootPath;
        Array.Copy(segments, 0, parts, 1, segments.Length);
        return Path.Combine(parts);
    }

    internal static string Relative(params string[] segments) =>
        segments.Length == 0
            ? string.Empty
            : Path.Combine(segments);
}
