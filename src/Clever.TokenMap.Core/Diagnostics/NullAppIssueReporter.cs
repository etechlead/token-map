namespace Clever.TokenMap.Core.Diagnostics;

public sealed class NullAppIssueReporter : IAppIssueReporter
{
    public static NullAppIssueReporter Instance { get; } = new();

    private NullAppIssueReporter()
    {
    }

    public void Report(AppIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
    }
}
