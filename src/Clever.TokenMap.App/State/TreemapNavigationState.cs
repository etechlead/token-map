using System;
using System.Collections.Generic;
using Clever.TokenMap.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.State;

public sealed partial class TreemapNavigationState : ObservableObject
{
    private ProjectSnapshot? _currentSnapshot;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanResetTreemapRoot))]
    [NotifyPropertyChangedFor(nameof(CanShowTreemapScope))]
    [NotifyPropertyChangedFor(nameof(TreemapScopeDisplay))]
    private ProjectNode? treemapRootNode;

    [ObservableProperty]
    private ProjectNode? selectedNode;

    [ObservableProperty]
    private IReadOnlyList<TreemapBreadcrumbItem> treemapBreadcrumbs = [];

    public bool CanResetTreemapRoot =>
        _currentSnapshot is not null &&
        TreemapRootNode is not null &&
        !string.Equals(TreemapRootNode.Id, _currentSnapshot.Root.Id, StringComparison.Ordinal);

    public bool CanShowTreemapScope => CanResetTreemapRoot;

    public string TreemapScopeDisplay =>
        _currentSnapshot is null || TreemapRootNode is null || !CanResetTreemapRoot
            ? string.Empty
            : TreemapRootNode.RelativePath;

    public void LoadSnapshot(ProjectSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _currentSnapshot = snapshot;
        TreemapRootNode = snapshot.Root;
        SelectedNode = snapshot.Root;
        TreemapBreadcrumbs = BuildTreemapBreadcrumbs(snapshot.Root);
    }

    public void Clear()
    {
        _currentSnapshot = null;
        TreemapRootNode = null;
        SelectedNode = null;
        TreemapBreadcrumbs = [];
    }

    public void SelectNode(ProjectNode? node)
    {
        SelectedNode = node;
    }

    public bool CanSetTreemapRoot(ProjectNode? node)
    {
        if (_currentSnapshot is null || node is null || !CanDrillInto(node))
        {
            return false;
        }

        return !string.Equals(TreemapRootNode?.Id, node.Id, StringComparison.Ordinal);
    }

    public void SetTreemapRoot(ProjectNode? node)
    {
        if (!CanSetTreemapRoot(node))
        {
            return;
        }

        TreemapRootNode = node;
        SelectedNode = node;
    }

    public bool DrillInto(ProjectNode? node)
    {
        if (!CanSetTreemapRoot(node))
        {
            return false;
        }

        SetTreemapRoot(node);
        return true;
    }

    public void ResetTreemapRoot()
    {
        if (_currentSnapshot is null)
        {
            return;
        }

        TreemapRootNode = _currentSnapshot.Root;
    }

    public void NavigateToBreadcrumb(ProjectNode? node)
    {
        if (node is null)
        {
            return;
        }

        TreemapRootNode = node;
    }

    partial void OnTreemapRootNodeChanged(ProjectNode? value)
    {
        TreemapBreadcrumbs = BuildTreemapBreadcrumbs(value);
    }

    private List<TreemapBreadcrumbItem> BuildTreemapBreadcrumbs(ProjectNode? node)
    {
        if (_currentSnapshot is null || node is null)
        {
            return [];
        }

        var path = new List<ProjectNode>();
        if (!TryBuildNodePath(_currentSnapshot.Root, node.Id, path))
        {
            return [];
        }

        var items = new List<TreemapBreadcrumbItem>(path.Count);
        for (var index = 0; index < path.Count; index++)
        {
            var pathNode = path[index];
            var label = index == 0
                ? pathNode.Name
                : $"/ {pathNode.Name}";
            items.Add(new TreemapBreadcrumbItem(
                label,
                pathNode,
                canNavigate: index < path.Count - 1));
        }

        return items;
    }

    private static bool TryBuildNodePath(ProjectNode current, string targetId, List<ProjectNode> path)
    {
        path.Add(current);
        if (string.Equals(current.Id, targetId, StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var child in current.Children)
        {
            if (TryBuildNodePath(child, targetId, path))
            {
                return true;
            }
        }

        path.RemoveAt(path.Count - 1);
        return false;
    }

    private static bool CanDrillInto(ProjectNode? node) =>
        node is not null &&
        node.Kind != Core.Enums.ProjectNodeKind.File &&
        node.Children.Count > 0;
}
