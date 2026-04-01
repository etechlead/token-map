using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.Core.Models;

public sealed class ProjectNode
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string FullPath { get; init; }

    public required string RelativePath { get; init; }

    public required ProjectNodeKind Kind { get; init; }

    public NodeSummary Summary { get; init; } = NodeSummary.Empty;

    public MetricSet ComputedMetrics { get; init; } = MetricSet.Empty;

    public SkippedReason? SkippedReason { get; init; }

    public List<ProjectNode> Children { get; } = [];
}
