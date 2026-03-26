namespace Clever.TokenMap.Infrastructure.Filtering.Ignore;

internal sealed class IgnoreFileParser
{
    public static IReadOnlyList<IgnoreRule> Parse(string ignoreFilePath, string baseRelativePath)
    {
        return ParseLines(File.ReadLines(ignoreFilePath), baseRelativePath);
    }

    public static IReadOnlyList<IgnoreRule> ParseLines(IEnumerable<string> rawLines, string baseRelativePath)
    {
        ArgumentNullException.ThrowIfNull(rawLines);

        var rules = new List<IgnoreRule>();

        foreach (var rawLine in rawLines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            rules.Add(new IgnoreRule(baseRelativePath, rawLine));
        }

        return rules;
    }
}
