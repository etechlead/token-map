namespace Clever.TokenMap.Core.Diagnostics;

public sealed class AppIssue
{
    public required string Code { get; init; }

    public required string UserMessage { get; init; }

    public required string TechnicalMessage { get; init; }

    public Exception? Exception { get; init; }

    public IReadOnlyDictionary<string, string> Context { get; init; } = AppIssueContext.Empty;

    public bool IsFatal { get; init; }
}
