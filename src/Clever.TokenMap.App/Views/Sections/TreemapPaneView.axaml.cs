using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Treemap;

namespace Clever.TokenMap.App.Views.Sections;

public partial class TreemapPaneView : UserControl
{
    private const int WheelThresholdStepMultiplier = 5;
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
            treemap.DoubleTapped += ProjectTreemapControl_OnDoubleTapped;
            treemap.PointerWheelChanged += ProjectTreemapControl_OnPointerWheelChanged;
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

    private async void ProjectTreemapControl_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not TreemapControl treemap)
        {
            return;
        }

        await HandleTreemapNodeDoubleTapAsync(treemap, e.GetPosition(treemap));
    }

    private void ProjectTreemapControl_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (sender is not TreemapControl treemap)
        {
            return;
        }

        e.Handled = HandleTreemapPointerWheel(
            e.Delta);
    }

    internal async Task HandleTreemapNodeDoubleTapAsync(TreemapControl treemap, Point point, CancellationToken cancellationToken = default)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var targetNode = treemap.LastPressedNode ?? treemap.HitTestNode(point);
        if (targetNode?.Kind != ProjectNodeKind.File)
        {
            return;
        }

        treemap.SelectNodeAt(point);
        await viewModel.PreviewNodeAsync(targetNode, cancellationToken);
    }

    internal bool HandleTreemapPointerWheel(Vector delta)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return false;
        }

        var direction = Math.Sign(delta.Y);
        if (direction == 0)
        {
            return false;
        }

        return viewModel.AdjustTreemapThreshold(direction * WheelThresholdStepMultiplier);
    }
}
