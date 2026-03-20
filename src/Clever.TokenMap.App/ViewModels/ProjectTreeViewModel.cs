using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.ViewModels;

public partial class ProjectTreeViewModel : ViewModelBase
{
    private readonly Dictionary<string, ProjectTreeNodeViewModel> _nodesById = new(StringComparer.Ordinal);

    public ObservableCollection<ProjectTreeNodeViewModel> RootNodes { get; } = [];

    [ObservableProperty]
    private ProjectTreeNodeViewModel? selectedNode;

    public event EventHandler<ProjectTreeNodeViewModel?>? SelectedNodeChanged;

    public void LoadRoot(ProjectTreeNodeViewModel rootNode)
    {
        RootNodes.Clear();
        _nodesById.Clear();
        RootNodes.Add(rootNode);
        RegisterNode(rootNode);
        SelectedNode = rootNode;
    }

    public void Clear()
    {
        RootNodes.Clear();
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

        if (_nodesById.TryGetValue(nodeId, out var node) && !ReferenceEquals(SelectedNode, node))
        {
            SelectedNode = node;
        }
    }

    partial void OnSelectedNodeChanged(ProjectTreeNodeViewModel? value)
    {
        SelectedNodeChanged?.Invoke(this, value);
    }

    private void RegisterNode(ProjectTreeNodeViewModel node)
    {
        _nodesById[node.Node.Id] = node;
        foreach (var child in node.Children)
        {
            RegisterNode(child);
        }
    }
}
