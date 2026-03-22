using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clever.TokenMap.App.ViewModels;

public partial class ProjectTreeViewModel : ViewModelBase
{
    private readonly Dictionary<string, ProjectNode> _nodesById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _parentNodeIdsById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ProjectTreeNodeViewModel> _visibleNodesById = new(StringComparer.Ordinal);
    private readonly HashSet<string> _expandedNodeIds = new(StringComparer.Ordinal);
    private ProjectTreeSortColumn _currentSortColumn = ProjectTreeSortColumn.Tokens;
    private ListSortDirection _currentSortDirection = ListSortDirection.Descending;
    private ProjectNode? _rootNode;
    private string? _selectedNodeId;

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

    public void LoadRoot(ProjectNode rootNode)
    {
        ArgumentNullException.ThrowIfNull(rootNode);

        _rootNode = rootNode;
        RootNodes.Clear();
        VisibleNodes.Clear();
        _nodesById.Clear();
        _parentNodeIdsById.Clear();
        _visibleNodesById.Clear();
        _expandedNodeIds.Clear();
        RegisterNode(rootNode, parentNodeId: null);
        _expandedNodeIds.Add(rootNode.Id);
        RebuildVisibleNodes();
        SelectNodeById(rootNode.Id);
    }

    public void Clear()
    {
        _rootNode = null;
        RootNodes.Clear();
        VisibleNodes.Clear();
        _nodesById.Clear();
        _parentNodeIdsById.Clear();
        _visibleNodesById.Clear();
        _expandedNodeIds.Clear();
        _selectedNodeId = null;
        SelectedNode = null;
    }

    public void SelectNodeById(string? nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            SelectedNode = null;
            return;
        }

        if (_nodesById.ContainsKey(nodeId))
        {
            var rebuildRequired = ExpandToNode(nodeId) || !_visibleNodesById.ContainsKey(nodeId);
            if (rebuildRequired)
            {
                RebuildVisibleNodes();
            }

            if (_visibleNodesById.TryGetValue(nodeId, out var node) &&
                !ReferenceEquals(SelectedNode, node))
            {
                SelectedNode = node;
            }
        }
    }

    partial void OnSelectedNodeChanged(ProjectTreeNodeViewModel? value)
    {
        _selectedNodeId = value?.Node.Id;
        SelectedNodeChanged?.Invoke(this, value);
    }

    public void SortBy(ProjectTreeSortColumn column, ListSortDirection direction)
    {
        _currentSortColumn = column;
        _currentSortDirection = direction;
        RebuildVisibleNodes();
    }

    private void RegisterNode(ProjectNode node, string? parentNodeId)
    {
        _nodesById[node.Id] = node;
        _parentNodeIdsById[node.Id] = parentNodeId;

        foreach (var child in node.Children)
        {
            RegisterNode(child, node.Id);
        }
    }

    private bool ExpandToNode(string nodeId)
    {
        var expanded = false;
        for (var current = nodeId; _parentNodeIdsById.TryGetValue(current, out var parentNodeId) && parentNodeId is not null; current = parentNodeId)
        {
            if (_expandedNodeIds.Add(parentNodeId))
            {
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

        if (_expandedNodeIds.Contains(node.Node.Id))
        {
            _expandedNodeIds.Remove(node.Node.Id);
            if (IsSelectedNodeInsideCollapsedBranch(node.Node.Id))
            {
                _selectedNodeId = node.Node.Id;
            }
        }
        else
        {
            _expandedNodeIds.Add(node.Node.Id);
        }

        RebuildVisibleNodes();

        if (_selectedNodeId is { } selectedNodeId &&
            _visibleNodesById.TryGetValue(selectedNodeId, out var selectedNode))
        {
            SelectedNode = selectedNode;
        }
    }

    private void RebuildVisibleNodes()
    {
        RootNodes.Clear();
        VisibleNodes.Clear();
        _visibleNodesById.Clear();

        if (_rootNode is null)
        {
            return;
        }

        AddVisibleNodeAndChildren(_rootNode, depth: 0);
    }

    private void AddVisibleNodeAndChildren(ProjectNode node, int depth)
    {
        var viewModel = new ProjectTreeNodeViewModel(
            node,
            depth,
            isExpanded: _expandedNodeIds.Contains(node.Id));

        if (depth == 0)
        {
            RootNodes.Add(viewModel);
        }

        VisibleNodes.Add(viewModel);
        _visibleNodesById[node.Id] = viewModel;

        if (!_expandedNodeIds.Contains(node.Id))
        {
            return;
        }

        foreach (var child in GetSortedChildren(node))
        {
            AddVisibleNodeAndChildren(child, depth + 1);
        }
    }

    private IEnumerable<ProjectNode> GetSortedChildren(ProjectNode node)
    {
        if (node.Children.Count == 0)
        {
            return [];
        }

        return _currentSortDirection == ListSortDirection.Ascending
            ? node.Children
                .OrderBy(child => GetSortValue(child, _currentSortColumn))
                .ThenBy(child => child.Name, StringComparer.OrdinalIgnoreCase)
            : node.Children
                .OrderByDescending(child => GetSortValue(child, _currentSortColumn))
                .ThenBy(child => child.Name, StringComparer.OrdinalIgnoreCase);
    }

    private bool IsSelectedNodeInsideCollapsedBranch(string collapsedNodeId)
    {
        if (_selectedNodeId is null)
        {
            return false;
        }

        for (var current = _selectedNodeId; ; )
        {
            if (string.Equals(current, collapsedNodeId, StringComparison.Ordinal))
            {
                return true;
            }

            if (!_parentNodeIdsById.TryGetValue(current, out var parentNodeId) || parentNodeId is null)
            {
                return false;
            }

            current = parentNodeId;
        }
    }

    private static IComparable GetSortValue(ProjectNode node, ProjectTreeSortColumn column) =>
        column switch
        {
            ProjectTreeSortColumn.Size => node.Metrics.FileSizeBytes,
            ProjectTreeSortColumn.Lines => node.Metrics.TotalLines,
            ProjectTreeSortColumn.Tokens => node.Metrics.Tokens,
            ProjectTreeSortColumn.Files => node.Kind switch
            {
                ProjectNodeKind.Directory => node.Metrics.DescendantFileCount,
                ProjectNodeKind.Root => node.Metrics.DescendantFileCount,
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
