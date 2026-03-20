namespace Clever.TokenMap.Core.Models;

public sealed record AnalysisProgress(
    string Phase,
    int ProcessedNodeCount,
    int? TotalNodeCount,
    string? CurrentPath);
