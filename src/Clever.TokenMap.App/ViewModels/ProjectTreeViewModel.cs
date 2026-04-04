using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clever.TokenMap.App.ViewModels;

public partial class ProjectTreeViewModel : ViewModelBase, IProjectTreeWorkspaceView
{
    private readonly Dictionary<string, ProjectNode> _nodesById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _parentNodeIdsById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ProjectTreeNodeViewModel> _visibleNodesById = new(StringComparer.Ordinal);
    private readonly HashSet<string> _expandedNodeIds = new(StringComparer.Ordinal);
    private ProjectTreeSortColumn _currentSortColumn = ProjectTreeSortColumn.Metric;
    private ListSortDirection _currentSortDirection = ListSortDirection.Descending;
    private MetricId _currentMetricSortId = MetricIds.Tokens;
    private MetricId _shareMetric = MetricIds.Tokens;
    private IReadOnlyList<MetricId> _visibleMetricIds = DefaultMetricCatalog.GetDefaultVisibleMetricIds();
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

    public MetricId CurrentMetricSortId => _currentMetricSortId;

    public ListSortDirection CurrentSortDirection => _currentSortDirection;

    [ObservableProperty]
    private ProjectTreeNodeViewModel? selectedNode;

    public event EventHandler<ProjectNode?>? SelectedNodeChanged;

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
        SelectedNodeChanged?.Invoke(this, value?.Node);
    }

    public void SortBy(ProjectTreeSortColumn column, ListSortDirection direction)
    {
        _currentSortColumn = column;
        _currentSortDirection = direction;
        if (_currentSortColumn == ProjectTreeSortColumn.Metric)
        {
            _currentMetricSortId = DefaultMetricCatalog.NormalizeMetricId(_currentMetricSortId);
        }

        RebuildVisibleNodes();
        RestoreSelectedNodeAfterRebuild();
    }

    public void SortByMetric(MetricId metricId, ListSortDirection direction)
    {
        _currentSortColumn = ProjectTreeSortColumn.Metric;
        _currentSortDirection = direction;
        _currentMetricSortId = DefaultMetricCatalog.NormalizeMetricId(metricId);
        RebuildVisibleNodes();
        RestoreSelectedNodeAfterRebuild();
    }

    public void SetShareMetric(MetricId metric)
    {
        var normalizedMetric = DefaultMetricCatalog.NormalizeMetricId(metric);
        if (_shareMetric == normalizedMetric)
        {
            return;
        }

        _shareMetric = normalizedMetric;
        RebuildVisibleNodes();
        RestoreSelectedNodeAfterRebuild();
    }

    public void SetVisibleMetrics(IReadOnlyList<MetricId> metricIds)
    {
        ArgumentNullException.ThrowIfNull(metricIds);

        var normalizedMetricIds = AppSettingsCanonicalizer.NormalizeVisibleMetricIds(metricIds);
        if (_visibleMetricIds.SequenceEqual(normalizedMetricIds))
        {
            return;
        }

        _visibleMetricIds = normalizedMetricIds;
        if (_currentSortColumn == ProjectTreeSortColumn.Metric &&
            !_visibleMetricIds.Contains(_currentMetricSortId))
        {
            _currentMetricSortId = _visibleMetricIds[0];
        }

        RebuildVisibleNodes();
        RestoreSelectedNodeAfterRebuild();
    }

    public bool MoveSelectionLeft()
    {
        if (SelectedNode is null)
        {
            return false;
        }

        if (SelectedNode.HasChildren && _expandedNodeIds.Contains(SelectedNode.Node.Id))
        {
            return SetNodeExpansion(SelectedNode, isExpanded: false);
        }

        if (_parentNodeIdsById.TryGetValue(SelectedNode.Node.Id, out var parentNodeId) &&
            !string.IsNullOrWhiteSpace(parentNodeId))
        {
            SelectNodeById(parentNodeId);
            return true;
        }

        return false;
    }

    public bool MoveSelectionRight()
    {
        if (SelectedNode is null)
        {
            return false;
        }

        if (SelectedNode.HasChildren && !_expandedNodeIds.Contains(SelectedNode.Node.Id))
        {
            return SetNodeExpansion(SelectedNode, isExpanded: true);
        }

        if (SelectedNode.HasChildren)
        {
            var firstChild = GetSortedChildren(SelectedNode.Node).FirstOrDefault();
            if (firstChild is null)
            {
                return false;
            }

            SelectNodeById(firstChild.Id);
            return true;
        }

        var selectedIndex = VisibleNodes.IndexOf(SelectedNode);
        if (selectedIndex < 0 || selectedIndex >= VisibleNodes.Count - 1)
        {
            return false;
        }

        SelectedNode = VisibleNodes[selectedIndex + 1];
        return true;
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

        SetNodeExpansion(node, !_expandedNodeIds.Contains(node.Node.Id));
    }

    private bool SetNodeExpansion(ProjectTreeNodeViewModel? node, bool isExpanded)
    {
        if (node is null || !node.HasChildren)
        {
            return false;
        }

        var changed = isExpanded
            ? _expandedNodeIds.Add(node.Node.Id)
            : _expandedNodeIds.Remove(node.Node.Id);

        if (!changed)
        {
            return false;
        }

        if (!isExpanded && IsSelectedNodeInsideCollapsedBranch(node.Node.Id))
        {
            _selectedNodeId = node.Node.Id;
        }

        RebuildVisibleNodes();
        RestoreSelectedNodeAfterRebuild();

        return true;
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

        AddVisibleNodeAndChildren(_rootNode, parentNode: null, depth: 0);
    }

    private void AddVisibleNodeAndChildren(ProjectNode node, ProjectNode? parentNode, int depth)
    {
        var viewModel = new ProjectTreeNodeViewModel(
            node,
            depth,
            isExpanded: _expandedNodeIds.Contains(node.Id),
            parentNode: parentNode,
            parentShareMetric: _shareMetric);

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
            AddVisibleNodeAndChildren(child, node, depth + 1);
        }
    }

    private IEnumerable<ProjectNode> GetSortedChildren(ProjectNode node)
    {
        if (node.Children.Count == 0)
        {
            return [];
        }

        if (_currentSortColumn == ProjectTreeSortColumn.ParentShare)
        {
            return GetChildrenSortedByParentShare(node);
        }

        return _currentSortDirection == ListSortDirection.Ascending
            ? node.Children
                .OrderBy(child => GetSortValue(child))
                .ThenBy(child => child.Name, StringComparer.OrdinalIgnoreCase)
            : node.Children
                .OrderByDescending(child => GetSortValue(child))
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

    private IEnumerable<ProjectNode> GetChildrenSortedByParentShare(ProjectNode parentNode)
    {
        var childrenWithShare = GetNodeChildrenWithShare(parentNode);
        return _currentSortDirection == ListSortDirection.Ascending
            ? childrenWithShare
                .OrderByDescending(item => item.Share.HasValue)
                .ThenBy(item => item.Share ?? double.PositiveInfinity)
                .ThenBy(item => item.Child.Name, StringComparer.OrdinalIgnoreCase)
                .Select(item => item.Child)
            : childrenWithShare
                .OrderByDescending(item => item.Share.HasValue)
                .ThenByDescending(item => item.Share ?? double.NegativeInfinity)
                .ThenBy(item => item.Child.Name, StringComparer.OrdinalIgnoreCase)
                .Select(item => item.Child);
    }

    private List<(ProjectNode Child, double? Share)> GetNodeChildrenWithShare(ProjectNode parentNode)
    {
        return parentNode.Children
            .Select(child => (Child: child, Share: ProjectTreeNodeViewModel.TryCalculateParentShareRatio(child, parentNode, _shareMetric)))
            .ToList();
    }

    private void RestoreSelectedNodeAfterRebuild()
    {
        if (_selectedNodeId is not { } selectedNodeId ||
            !_visibleNodesById.TryGetValue(selectedNodeId, out var selectedNode))
        {
            return;
        }

        if (!ReferenceEquals(SelectedNode, selectedNode))
        {
            SelectedNode = selectedNode;
        }
    }

    private IComparable GetSortValue(ProjectNode node) =>
        _currentSortColumn switch
        {
            ProjectTreeSortColumn.Metric => node.ComputedMetrics.TryGetNumber(_currentMetricSortId) ?? 0d,
            _ => node.Name,
        };
}

public enum ProjectTreeSortColumn
{
    Name,
    ParentShare,
    Metric,
}
