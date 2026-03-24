namespace Clever.TokenMap.Infrastructure.Filtering.Ignore;

internal sealed class IgnoreDirectoryContext
{
    public static IgnoreDirectoryContext Empty { get; } = new([]);

    public IgnoreDirectoryContext(IReadOnlyList<IgnoreRule> rules)
        : this(rules, [])
    {
    }

    public IgnoreDirectoryContext(IReadOnlyList<IgnoreRule> prefixRules, IReadOnlyList<IgnoreRule> finalOverrideRules)
    {
        PrefixRules = prefixRules;
        FinalOverrideRules = finalOverrideRules;
    }

    public IReadOnlyList<IgnoreRule> PrefixRules { get; }

    public IReadOnlyList<IgnoreRule> FinalOverrideRules { get; }

    public IReadOnlyList<IgnoreRule> Rules => [.. PrefixRules, .. FinalOverrideRules];

    public IgnoreDirectoryContext AppendBetween(IEnumerable<IgnoreRule> rules)
    {
        var combinedRules = PrefixRules.Concat(rules).ToArray();
        return new IgnoreDirectoryContext(combinedRules, FinalOverrideRules);
    }

    public IgnoreDirectoryContext AppendFinalOverrides(IEnumerable<IgnoreRule> rules)
    {
        var combinedRules = FinalOverrideRules.Concat(rules).ToArray();
        return new IgnoreDirectoryContext(PrefixRules, combinedRules);
    }
}
