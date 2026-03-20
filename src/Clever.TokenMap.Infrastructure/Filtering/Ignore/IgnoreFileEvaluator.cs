namespace Clever.TokenMap.Infrastructure.Filtering.Ignore;

internal sealed class IgnoreFileEvaluator
{
    public bool IsIncluded(IgnoreDirectoryContext context, string normalizedRelativePath, bool isDirectory)
    {
        var isIncluded = true;

        foreach (var rule in context.Rules)
        {
            if (rule.IsMatch(normalizedRelativePath, isDirectory))
            {
                isIncluded = rule.IsNegated;
            }
        }

        return isIncluded;
    }
}
