using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.App.State;

public sealed class TreemapBreadcrumbItem
{
    public TreemapBreadcrumbItem(string label, ProjectNode node, bool canNavigate)
    {
        Label = label;
        Node = node;
        CanNavigate = canNavigate;
    }

    public string Label { get; }

    public ProjectNode Node { get; }

    public bool CanNavigate { get; }

    public bool IsCurrent => !CanNavigate;
}
