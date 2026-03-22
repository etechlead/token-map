using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.App.ViewModels;

public sealed class TreemapBreadcrumbItemViewModel
{
    public TreemapBreadcrumbItemViewModel(string label, ProjectNode node, bool canNavigate)
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
