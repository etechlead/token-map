namespace Clever.TokenMap.Core.Diagnostics;

public sealed class AnalysisIssue
{
    public required string Message { get; init; }

    public IReadOnlyDictionary<string, string> Context { get; init; } = AppIssueContext.Empty;
}
