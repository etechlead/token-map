using Clever.TokenMap.Core.Diagnostics;

namespace Clever.TokenMap.Core.Models;

public sealed class ProjectSnapshot
{
    public required string RootPath { get; init; }

    public required DateTimeOffset CapturedAtUtc { get; init; }

    public required ScanOptions Options { get; init; }

    public required ProjectNode Root { get; init; }

    public IReadOnlyList<AnalysisIssue> Diagnostics { get; init; } = [];
}
