using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clever.TokenMap.App.ViewModels;

public partial class ProjectTreeViewModel : ViewModelBase
{
    private readonly Dictionary<string, ProjectTreeNodeViewModel> _nodesById = new(StringComparer.Ordinal);
    private ProjectTreeSortColumn _currentSortColumn = ProjectTreeSortColumn.Size;
    private ListSortDirection _currentSortDirection = ListSortDirection.Descending;

    public ProjectTreeViewModel()
    {
        ToggleNodeCommand = new RelayCommand<ProjectTreeNodeViewModel?>(ToggleNode);
    }

    public ObservableCollection<ProjectTreeNodeViewModel> RootNodes { get; } = [];

    public ObservableCollection<ProjectTreeNodeViewModel> VisibleNodes { get; } = [];

    public IRelayCommand<ProjectTreeNodeViewModel?> ToggleNodeCommand { get; }

    public ProjectTreeSortColumn CurrentSortColumn => _currentSortColumn;

    public ListSortDirection CurrentSortDirection => _currentSortDirection;

    [ObservableProperty]
    private ProjectTreeNodeViewModel? selectedNode;

    public event EventHandler<ProjectTreeNodeViewModel?>? SelectedNodeChanged;

    public void LoadRoot(ProjectTreeNodeViewModel rootNode)
    {
        RootNodes.Clear();
        VisibleNodes.Clear();
        _nodesById.Clear();
        RootNodes.Add(rootNode);
        RegisterNode(rootNode);
        rootNode.IsExpanded = true;
        ApplyCurrentSort();
        RebuildVisibleNodes();
        SelectedNode = rootNode;
    }

    public void Clear()
    {
        RootNodes.Clear();
        VisibleNodes.Clear();
        _nodesById.Clear();
        SelectedNode = null;
    }

    public void SelectNodeById(string? nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            SelectedNode = null;
            return;
        }

        if (_nodesById.TryGetValue(nodeId, out var node))
        {
            var expanded = ExpandToNode(node);
            if (expanded)
            {
                RebuildVisibleNodes();
            }

            if (!ReferenceEquals(SelectedNode, node))
            {
                SelectedNode = node;
            }
        }
    }

    partial void OnSelectedNodeChanged(ProjectTreeNodeViewModel? value)
    {
        SelectedNodeChanged?.Invoke(this, value);
    }

    public void SortBy(ProjectTreeSortColumn column, ListSortDirection direction)
    {
        _currentSortColumn = column;
        _currentSortDirection = direction;
        ApplyCurrentSort();
        RebuildVisibleNodes();
    }

    private void ApplyCurrentSort()
    {
        SortCollection(RootNodes, _currentSortColumn, _currentSortDirection);
        foreach (var root in RootNodes)
        {
            SortChildrenRecursive(root, _currentSortColumn, _currentSortDirection);
        }
    }

    private void RegisterNode(ProjectTreeNodeViewModel node)
    {
        _nodesById[node.Node.Id] = node;
        foreach (var child in node.Children)
        {
            RegisterNode(child);
        }
    }

    private static bool ExpandToNode(ProjectTreeNodeViewModel node)
    {
        var expanded = false;
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (!current.IsExpanded)
            {
                current.IsExpanded = true;
                expanded = true;
            }
        }

        return expanded;
    }

    private void ToggleNode(ProjectTreeNodeViewModel? node)
    {
        if (node is null || !node.HasChildren)
        {
            return;
        }

        node.IsExpanded = !node.IsExpanded;
        RebuildVisibleNodes();
    }

    private void RebuildVisibleNodes()
    {
        VisibleNodes.Clear();

        foreach (var root in RootNodes)
        {
            AddVisibleNodeAndChildren(root);
        }
    }

    private void AddVisibleNodeAndChildren(ProjectTreeNodeViewModel node)
    {
        VisibleNodes.Add(node);

        if (!node.IsExpanded)
        {
            return;
        }

        foreach (var child in node.Children)
        {
            AddVisibleNodeAndChildren(child);
        }
    }

    private static void SortChildrenRecursive(
        ProjectTreeNodeViewModel node,
        ProjectTreeSortColumn column,
        ListSortDirection direction)
    {
        if (node.Children.Count == 0)
        {
            return;
        }

        SortCollection(node.Children, column, direction);

        foreach (var child in node.Children)
        {
            SortChildrenRecursive(child, column, direction);
        }
    }

    private static void SortCollection(
        ObservableCollection<ProjectTreeNodeViewModel> nodes,
        ProjectTreeSortColumn column,
        ListSortDirection direction)
    {
        var ordered = direction == ListSortDirection.Ascending
            ? nodes.OrderBy(node => GetSortValue(node, column)).ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase).ToList()
            : nodes.OrderByDescending(node => GetSortValue(node, column)).ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase).ToList();

        if (ordered.SequenceEqual(nodes))
        {
            return;
        }

        nodes.Clear();
        foreach (var node in ordered)
        {
            nodes.Add(node);
        }
    }

    private static IComparable GetSortValue(ProjectTreeNodeViewModel node, ProjectTreeSortColumn column) =>
        column switch
        {
            ProjectTreeSortColumn.Size => node.Node.Metrics.FileSizeBytes,
            ProjectTreeSortColumn.Lines => node.Node.Metrics.TotalLines,
            ProjectTreeSortColumn.Tokens => node.Node.Metrics.Tokens,
            ProjectTreeSortColumn.Files => node.Node.Kind switch
            {
                Core.Enums.ProjectNodeKind.Directory => node.Node.Metrics.DescendantFileCount,
                Core.Enums.ProjectNodeKind.Root => node.Node.Metrics.DescendantFileCount,
                _ => 0,
            },
            _ => node.Name,
        };
}

public enum ProjectTreeSortColumn
{
    Name,
    Size,
    Lines,
    Tokens,
    Files,
}
