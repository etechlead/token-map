using GitIgnoreRule = Ignore.IgnoreRule;

namespace Clever.TokenMap.Infrastructure.Filtering.Ignore;

internal sealed class IgnoreRule(string baseRelativePath, string rawPattern)
{
    private readonly GitIgnoreRule _rule = new(rawPattern);
    private readonly bool _directoryOnly = IsDirectoryOnly(rawPattern);

    public string BaseRelativePath { get; } = baseRelativePath;

    public bool IsNegated => _rule.Negate;

    public bool IsMatch(string normalizedRelativePath, bool isDirectory)
    {
        var candidatePath = GetCandidatePath(normalizedRelativePath);
        if (candidatePath is null)
        {
            return false;
        }

        var candidateForMatch = isDirectory && candidatePath.Length > 0
            && _directoryOnly
            ? candidatePath + "/"
            : candidatePath;

        return _rule.IsMatch(candidateForMatch);
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

    private static bool IsDirectoryOnly(string rawPattern)
    {
        var pattern = rawPattern.TrimEnd();

        if (pattern.StartsWith(@"\!", StringComparison.Ordinal) ||
            pattern.StartsWith(@"\#", StringComparison.Ordinal))
        {
            pattern = pattern[1..];
        }
        else if (pattern.StartsWith('!'))
        {
            pattern = pattern[1..];
        }

        return pattern.EndsWith('/') &&
               !pattern.EndsWith("\\/", StringComparison.Ordinal);
    }
}
