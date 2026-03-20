using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.ViewModels;

public partial class ProjectTreeViewModel : ViewModelBase
{
    public ObservableCollection<ProjectTreeNodeViewModel> RootNodes { get; } = [];

    [ObservableProperty]
    private ProjectTreeNodeViewModel? selectedNode;

    public event EventHandler<ProjectTreeNodeViewModel?>? SelectedNodeChanged;

    public void LoadRoot(ProjectTreeNodeViewModel rootNode)
    {
        RootNodes.Clear();
        RootNodes.Add(rootNode);
        SelectedNode = rootNode;
    }

    public void Clear()
    {
        RootNodes.Clear();
        SelectedNode = null;
    }

    partial void OnSelectedNodeChanged(ProjectTreeNodeViewModel? value)
    {
        SelectedNodeChanged?.Invoke(this, value);
    }
}
