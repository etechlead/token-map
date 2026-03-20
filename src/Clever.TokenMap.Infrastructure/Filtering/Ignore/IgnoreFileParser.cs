using System.Text;
using System.Text.RegularExpressions;

namespace Clever.TokenMap.Infrastructure.Filtering.Ignore;

internal sealed class IgnoreFileParser
{
    private readonly RegexOptions _regexOptions =
        RegexOptions.Compiled |
        RegexOptions.CultureInvariant |
        (OperatingSystem.IsWindows() ? RegexOptions.IgnoreCase : RegexOptions.None);

    public IReadOnlyList<IgnoreRule> Parse(string ignoreFilePath, string baseRelativePath)
    {
        var rules = new List<IgnoreRule>();

        foreach (var rawLine in File.ReadLines(ignoreFilePath))
        {
            var line = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            var isNegated = line.StartsWith('!');
            if (isNegated)
            {
                line = line[1..];
            }

            var startsWithSlash = line.StartsWith('/');
            if (startsWithSlash)
            {
                line = line[1..];
            }

            var directoryOnly = line.EndsWith('/');
            if (directoryOnly)
            {
                line = line[..^1];
            }

            var normalizedPattern = line.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(normalizedPattern))
            {
                continue;
            }

            var hasSlash = normalizedPattern.Contains('/');
            var matchFileNameOnly = !startsWithSlash && !hasSlash;
            var regex = new Regex(ConvertGlobToRegex(normalizedPattern), _regexOptions);

            rules.Add(new IgnoreRule(
                baseRelativePath,
                normalizedPattern,
                regex,
                isNegated,
                directoryOnly,
                matchFileNameOnly));
        }

        return rules;
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
