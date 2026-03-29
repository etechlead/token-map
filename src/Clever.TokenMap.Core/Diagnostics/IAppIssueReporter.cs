namespace Clever.TokenMap.Core.Diagnostics;

public interface IAppIssueReporter
{
    void Report(AppIssue issue);
}
