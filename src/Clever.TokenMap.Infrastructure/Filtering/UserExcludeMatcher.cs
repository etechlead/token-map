using System.Text;
using System.Text.RegularExpressions;

namespace Clever.TokenMap.Infrastructure.Filtering;

public sealed class UserExcludeMatcher
{
    private readonly RegexOptions _regexOptions =
        RegexOptions.Compiled |
        RegexOptions.CultureInvariant |
        (OperatingSystem.IsWindows() ? RegexOptions.IgnoreCase : RegexOptions.None);

    public bool IsExcluded(IReadOnlyList<string> userExcludes, string normalizedRelativePath, bool isDirectory)
    {
        if (userExcludes.Count == 0 || string.IsNullOrEmpty(normalizedRelativePath))
        {
            return false;
        }

        return userExcludes.Any(pattern => IsMatch(pattern, normalizedRelativePath, isDirectory));
    }

    private bool IsMatch(string rawPattern, string normalizedRelativePath, bool isDirectory)
    {
        if (string.IsNullOrWhiteSpace(rawPattern))
        {
            return false;
        }

        var normalizedPattern = rawPattern.Trim().Replace('\\', '/').TrimStart('/');
        var directoryOnly = normalizedPattern.EndsWith('/');

        if (directoryOnly)
        {
            normalizedPattern = normalizedPattern[..^1];
        }

        if (directoryOnly && !isDirectory)
        {
            return false;
        }

        var regex = new Regex(ConvertGlobToRegex(normalizedPattern), _regexOptions);
        return regex.IsMatch(normalizedRelativePath);
    }

    private static string ConvertGlobToRegex(string pattern)
    {
        var builder = new StringBuilder("^");

        for (var index = 0; index < pattern.Length; index++)
        {
            var character = pattern[index];

            if (character == '*')
            {
                var nextIsAsterisk = index + 1 < pattern.Length && pattern[index + 1] == '*';
                if (nextIsAsterisk)
                {
                    builder.Append(".*");
                    index++;
                }
                else
                {
                    builder.Append("[^/]*");
                }

                continue;
            }

            if (character == '?')
            {
                builder.Append("[^/]");
                continue;
            }

            if ("+()^$.{}[]|\\".Contains(character))
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        builder.Append('$');
        return builder.ToString();
    }
}
