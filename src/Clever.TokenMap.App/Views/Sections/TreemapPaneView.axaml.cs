using Avalonia.Controls;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Treemap;

namespace Clever.TokenMap.App.Views.Sections;

public partial class TreemapPaneView : UserControl
{
    private readonly ProjectNodeContextMenuController _projectNodeContextMenuController;

    public TreemapPaneView()
    {
        InitializeComponent();

        var treemap = this.FindControl<TreemapControl>("ProjectTreemapControl");
        _projectNodeContextMenuController = new ProjectNodeContextMenuController(
            this,
            () => DataContext as MainWindowViewModel,
            isSuppressed => treemap?.SetTooltipSuppressed(isSuppressed));
        if (treemap is not null)
        {
            treemap.DrillDownRequested += ProjectTreemapControl_OnDrillDownRequested;
            treemap.ContextRequested += ProjectTreemapControl_OnContextRequested;
        }
    }

    private void ProjectTreemapControl_OnDrillDownRequested(object? sender, TreemapDrillDownRequestedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.DrillIntoTreemap(e.Node);
    }

    private void ProjectTreemapControl_OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            sender is not TreemapControl treemap)
        {
            return;
        }

        ProjectNode? targetNode;
        if (e.TryGetPosition(treemap, out var point))
        {
            targetNode = treemap.HitTestNode(point);
            if (targetNode is null)
            {
                return;
            }

            treemap.SelectNodeAt(point);
        }
        else
        {
            targetNode = viewModel.SelectedNode;
        }

        if (targetNode is null)
        {
            return;
        }

        _projectNodeContextMenuController.Show(treemap, targetNode);
        e.Handled = true;
    }
}

