namespace Clever.TokenMap.Infrastructure.Filtering.Ignore;

internal static class IgnoreFileEvaluator
{
    public static bool IsIncluded(IgnoreDirectoryContext context, string normalizedRelativePath, bool isDirectory)
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
