using System;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Controls;

public sealed class TreemapDrillDownRequestedEventArgs(ProjectNode node) : EventArgs
{
    public ProjectNode Node { get; } = node;
}
