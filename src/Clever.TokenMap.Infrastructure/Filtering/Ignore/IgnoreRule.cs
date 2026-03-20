namespace Clever.TokenMap.Infrastructure.Filtering.Ignore;

internal sealed class IgnoreRule(
    string baseRelativePath,
    string pattern,
    System.Text.RegularExpressions.Regex regex,
    bool isNegated,
    bool directoryOnly,
    bool matchFileNameOnly)
{
    public string BaseRelativePath { get; } = baseRelativePath;

    public string Pattern { get; } = pattern;

    public System.Text.RegularExpressions.Regex Regex { get; } = regex;

    public bool IsNegated { get; } = isNegated;

    public bool DirectoryOnly { get; } = directoryOnly;

    public bool MatchFileNameOnly { get; } = matchFileNameOnly;

    public bool IsMatch(string normalizedRelativePath, bool isDirectory)
    {
        if (DirectoryOnly && !isDirectory)
        {
            return false;
        }

        var candidatePath = GetCandidatePath(normalizedRelativePath);
        if (candidatePath is null)
        {
            return false;
        }

        if (MatchFileNameOnly)
        {
            var segments = candidatePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Any(segment => Regex.IsMatch(segment));
        }

        return Regex.IsMatch(candidatePath);
    }

    private string? GetCandidatePath(string normalizedRelativePath)
    {
        if (string.IsNullOrEmpty(BaseRelativePath))
        {
            return normalizedRelativePath;
        }

        if (string.Equals(normalizedRelativePath, BaseRelativePath, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var prefix = BaseRelativePath + "/";
        return normalizedRelativePath.StartsWith(prefix, StringComparison.Ordinal)
            ? normalizedRelativePath[prefix.Length..]
            : null;
    }
}
