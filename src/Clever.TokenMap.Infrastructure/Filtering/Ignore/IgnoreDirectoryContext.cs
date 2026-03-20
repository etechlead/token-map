namespace Clever.TokenMap.Infrastructure.Filtering.Ignore;

internal sealed class IgnoreDirectoryContext
{
    public static IgnoreDirectoryContext Empty { get; } = new([]);

    public IgnoreDirectoryContext(IReadOnlyList<IgnoreRule> rules)
    {
        Rules = rules;
    }

    public IReadOnlyList<IgnoreRule> Rules { get; }

    public IgnoreDirectoryContext Append(IEnumerable<IgnoreRule> rules)
    {
        var combinedRules = Rules.Concat(rules).ToArray();
        return new IgnoreDirectoryContext(combinedRules);
    }
}
