using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Treemap;

public sealed class TreemapDrillDownRequestedEventArgs(ProjectNode node) : EventArgs
{
    public ProjectNode Node { get; } = node;
}

