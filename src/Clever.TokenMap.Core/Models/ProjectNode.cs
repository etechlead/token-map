using Clever.TokenMap.Core.Enums;

namespace Clever.TokenMap.Core.Models;

public sealed class ProjectNode
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string FullPath { get; init; }

    public required string RelativePath { get; init; }

    public required ProjectNodeKind Kind { get; init; }

    public NodeMetrics Metrics { get; set; } = NodeMetrics.Empty;

    public SkippedReason? SkippedReason { get; set; }

    public string? DiagnosticMessage { get; set; }

    public List<ProjectNode> Children { get; } = [];
}
