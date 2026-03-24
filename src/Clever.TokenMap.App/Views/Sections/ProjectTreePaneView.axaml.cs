using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.App.ViewModels;
using FluentIcons.Avalonia;
using FluentIconGlyph = FluentIcons.Common.Icon;
using FluentIconSize = FluentIcons.Common.IconSize;
using FluentIconVariant = FluentIcons.Common.IconVariant;

namespace Clever.TokenMap.App.Views.Sections;

public partial class ProjectTreePaneView : UserControl
{
    private static readonly TimeSpan ProjectTreeSingleClickSelectionDelay = TimeSpan.FromMilliseconds(250);
    private readonly Dictionary<DataGridColumn, string> _projectTreeTableBaseHeaders = [];
    private readonly ProjectNodeContextMenuController _projectNodeContextMenuController;
    private CancellationTokenSource? _pendingProjectTreeSelectionSync;
    private bool _projectTreeSelectionChangeTriggeredByPointer;

    public ProjectTreePaneView()
    {
        InitializeComponent();
        this.FindControl<DataGrid>("ProjectTreeTable")?.AddHandler(
            InputElement.KeyDownEvent,
            ProjectTreeTable_OnKeyDown,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        _projectNodeContextMenuController = new ProjectNodeContextMenuController(
            this,
            () => DataContext as MainWindowViewModel);
        DataContextChanged += (_, _) => ScheduleApplyProjectTreeTableHeaderStateFromViewModel();
        AttachedToVisualTree += (_, _) => ScheduleApplyProjectTreeTableHeaderStateFromViewModel();
    }

    private void ProjectTreeExpandCollapseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            sender is not Control { DataContext: ProjectTreeNodeViewModel node })
        {
            return;
        }

        viewModel.Tree.ToggleNodeCommand.Execute(node);
    }

    private void ProjectTreeTable_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            sender is not DataGrid grid)
        {
            return;
        }

        var sourceElement = e.Source as StyledElement;
        var header = FindAncestor<DataGridColumnHeader>(sourceElement);
        if (header is not null)
        {
            if (e.ClickCount != 1)
            {
                return;
            }

            EnsureProjectTreeTableBaseHeaders(grid);

            var clickedColumn = grid.Columns.FirstOrDefault(
                column => Equals(column.Header, header.Content) ||
                          string.Equals(column.Header?.ToString(), header.Content?.ToString(), StringComparison.Ordinal));

            if (clickedColumn is null ||
                !TryMapSortColumn(clickedColumn.SortMemberPath, out var sortColumn))
            {
                return;
            }

            ApplyProjectTreeSort(viewModel, grid, clickedColumn, sortColumn);
            e.Handled = true;
            return;
        }

        if (FindAncestor<DataGridRow>(sourceElement) is not null)
        {
            _projectTreeSelectionChangeTriggeredByPointer = true;
        }
    }

    private void ProjectTreeTable_OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            sender is not DataGrid grid)
        {
            return;
        }

        if (FindAncestor<DataGridColumnHeader>(e.Source as StyledElement) is not null)
        {
            return;
        }

        var targetNode = FindAncestor<DataGridRow>(e.Source as StyledElement)?.DataContext as ProjectTreeNodeViewModel;
        if (targetNode is null)
        {
            if (e.TryGetPosition(grid, out _))
            {
                return;
            }

            targetNode = viewModel.Tree.SelectedNode;
        }

        if (targetNode is null)
        {
            return;
        }

        viewModel.SelectedNode = targetNode.Node;
        _projectNodeContextMenuController.Show(grid, targetNode.Node);
        e.Handled = true;
    }

    private void ProjectTreeTable_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            e.Source is not StyledElement sourceElement)
        {
            return;
        }

        if (FindAncestor<DataGridColumnHeader>(sourceElement) is not null)
        {
            return;
        }

        if (FindAncestor<DataGridRow>(sourceElement) is { DataContext: ProjectTreeNodeViewModel node })
        {
            HandleProjectTreeRowDoubleTap(viewModel, node);
            if (node.HasChildren)
            {
                e.Handled = true;
            }
        }
    }

    private void ProjectTreeTable_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        var handled = e.Key switch
        {
            Key.Left => viewModel.Tree.MoveSelectionLeft(),
            Key.Right => viewModel.Tree.MoveSelectionRight(),
            _ => false,
        };

        if (handled)
        {
            e.Handled = true;
        }
    }

    private void ProjectTreeTable_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            sender is not DataGrid grid)
        {
            return;
        }

        if (grid.SelectedItem is not ProjectTreeNodeViewModel selectedNode)
        {
            _projectTreeSelectionChangeTriggeredByPointer = false;
            CancelPendingProjectTreeSelectionSync();
            return;
        }

        ScheduleScrollSelectedProjectTreeRowIntoView(grid);

        if (ReferenceEquals(viewModel.Tree.SelectedNode, selectedNode))
        {
            _projectTreeSelectionChangeTriggeredByPointer = false;
            return;
        }

        if (_projectTreeSelectionChangeTriggeredByPointer)
        {
            _projectTreeSelectionChangeTriggeredByPointer = false;
            ScheduleProjectTreeSelectionSync(viewModel, selectedNode);
            return;
        }

        ApplyProjectTreeSelection(viewModel, selectedNode);
    }

    private void HandleProjectTreeRowDoubleTap(MainWindowViewModel viewModel, ProjectTreeNodeViewModel node)
    {
        CancelPendingProjectTreeSelectionSync();
        ApplyProjectTreeSelection(viewModel, node);

        if (node.HasChildren)
        {
            viewModel.Tree.ToggleNodeCommand.Execute(node);
        }
    }

    private void ApplyProjectTreeSelection(MainWindowViewModel viewModel, ProjectTreeNodeViewModel node)
    {
        CancelPendingProjectTreeSelectionSync();

        if (!ReferenceEquals(viewModel.Tree.SelectedNode, node))
        {
            viewModel.Tree.SelectedNode = node;
        }
    }

    private void ScheduleProjectTreeSelectionSync(MainWindowViewModel viewModel, ProjectTreeNodeViewModel node)
    {
        CancelPendingProjectTreeSelectionSync();

        var cancellationTokenSource = new CancellationTokenSource();
        _pendingProjectTreeSelectionSync = cancellationTokenSource;
        _ = ApplyProjectTreeSelectionAfterDelayAsync(viewModel, node, cancellationTokenSource);
    }

    private async Task ApplyProjectTreeSelectionAfterDelayAsync(
        MainWindowViewModel viewModel,
        ProjectTreeNodeViewModel node,
        CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            await Task.Delay(ProjectTreeSingleClickSelectionDelay, cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (cancellationTokenSource.IsCancellationRequested ||
                !ReferenceEquals(_pendingProjectTreeSelectionSync, cancellationTokenSource))
            {
                return;
            }

            _pendingProjectTreeSelectionSync = null;
            ApplyProjectTreeSelection(viewModel, node);
            cancellationTokenSource.Dispose();
        });
    }

    private void CancelPendingProjectTreeSelectionSync()
    {
        if (_pendingProjectTreeSelectionSync is null)
        {
            return;
        }

        _pendingProjectTreeSelectionSync.Cancel();
        _pendingProjectTreeSelectionSync.Dispose();
        _pendingProjectTreeSelectionSync = null;
    }

    private void EnsureProjectTreeTableBaseHeaders(DataGrid grid)
    {
        foreach (var column in grid.Columns)
        {
            if (!_projectTreeTableBaseHeaders.ContainsKey(column))
            {
                _projectTreeTableBaseHeaders[column] = column.Header?.ToString() ?? string.Empty;
            }
        }
    }

    private void ApplyProjectTreeTableHeaderStateFromViewModel()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var grid = this.FindControl<DataGrid>("ProjectTreeTable");
        if (grid is null)
        {
            return;
        }

        EnsureProjectTreeTableBaseHeaders(grid);

        var sortedColumn = grid.Columns.FirstOrDefault(column =>
            TryMapSortColumn(column.SortMemberPath, out var sortColumn) &&
            sortColumn == viewModel.Tree.CurrentSortColumn);

        if (sortedColumn is null)
        {
            return;
        }

        UpdateProjectTreeTableHeaderState(grid, sortedColumn, viewModel.Tree.CurrentSortDirection);
    }

    private void ScheduleApplyProjectTreeTableHeaderStateFromViewModel()
    {
        Dispatcher.UIThread.Post(
            ApplyProjectTreeTableHeaderStateFromViewModel,
            DispatcherPriority.Loaded);
    }

    private static void ScheduleScrollSelectedProjectTreeRowIntoView(DataGrid grid)
    {
        Dispatcher.UIThread.Post(
            () => ScrollSelectedProjectTreeRowIntoView(grid),
            DispatcherPriority.Loaded);
    }

    private static void ScrollSelectedProjectTreeRowIntoView(DataGrid grid)
    {
        if (grid.SelectedItem is null || TopLevel.GetTopLevel(grid) is null)
        {
            return;
        }

        grid.ScrollIntoView(grid.SelectedItem, null);
    }

    private void UpdateProjectTreeTableHeaderState(
        DataGrid grid,
        DataGridColumn sortedColumn,
        ListSortDirection direction)
    {
        foreach (var column in grid.Columns)
        {
            var baseHeader = _projectTreeTableBaseHeaders.TryGetValue(column, out var header)
                ? header
                : column.Header?.ToString() ?? string.Empty;

            if (ReferenceEquals(column, sortedColumn))
            {
                column.Header = CreateProjectTreeHeaderContent(baseHeader, direction);
                column.Tag = direction;
            }
            else
            {
                column.Header = CreateProjectTreeHeaderContent(baseHeader, null);
                column.Tag = null;
            }
        }
    }

    private void ApplyProjectTreeSort(
        MainWindowViewModel viewModel,
        DataGrid grid,
        DataGridColumn clickedColumn,
        ProjectTreeSortColumn sortColumn)
    {
        var direction = GetNextSortDirection(clickedColumn, sortColumn);
        viewModel.Tree.SortBy(sortColumn, direction);
        UpdateProjectTreeTableHeaderState(grid, clickedColumn, direction);
    }

    private static ListSortDirection GetNextSortDirection(
        DataGridColumn clickedColumn,
        ProjectTreeSortColumn sortColumn)
    {
        if (clickedColumn.Tag is ListSortDirection currentDirection)
        {
            return currentDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        }

        return GetDefaultSortDirection(sortColumn);
    }

    private static ListSortDirection GetDefaultSortDirection(ProjectTreeSortColumn sortColumn) =>
        sortColumn == ProjectTreeSortColumn.Name
            ? ListSortDirection.Ascending
            : ListSortDirection.Descending;

    private static ProjectTreeColumnHeaderContent CreateProjectTreeHeaderContent(string text, ListSortDirection? direction)
    {
        var panel = new ProjectTreeColumnHeaderContent(text)
        {
            VerticalAlignment = VerticalAlignment.Center,
        };
        panel.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        panel.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        var headerText = new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(headerText, 0);
        panel.Children.Add(headerText);

        if (direction is null)
        {
            return panel;
        }

        var sortIcon = new FluentIcon
        {
            Name = direction == ListSortDirection.Ascending
                ? "SortIconAscending"
                : "SortIconDescending",
            Icon = direction == ListSortDirection.Ascending
                ? FluentIconGlyph.ArrowSortUp
                : FluentIconGlyph.ArrowSortDown,
            IconVariant = FluentIconVariant.Regular,
            IconSize = FluentIconSize.Size16,
            Width = 10,
            Height = 10,
            Margin = new Thickness(4, 3, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };
        sortIcon.Classes.Add("sort-icon");
        Grid.SetColumn(sortIcon, 1);

        panel.Children.Add(sortIcon);
        return panel;
    }

    private static T? FindAncestor<T>(StyledElement? element)
        where T : StyledElement
    {
        for (var current = element; current is not null; current = current.Parent)
        {
            if (current is T typed)
            {
                return typed;
            }
        }

        return null;
    }

    private static bool TryMapSortColumn(string? sortMemberPath, out ProjectTreeSortColumn column)
    {
        switch (sortMemberPath)
        {
            case "Size":
                column = ProjectTreeSortColumn.Size;
                return true;
            case "Lines":
                column = ProjectTreeSortColumn.Lines;
                return true;
            case "Tokens":
                column = ProjectTreeSortColumn.Tokens;
                return true;
            case "Files":
                column = ProjectTreeSortColumn.Files;
                return true;
            case "Name":
                column = ProjectTreeSortColumn.Name;
                return true;
            default:
                column = default;
                return false;
        }
    }

}

internal sealed class ProjectTreeColumnHeaderContent(string text) : Grid
{
    public string Text { get; } = text;

    public override string ToString() => Text;
}
