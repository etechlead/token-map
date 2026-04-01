using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.App.Views.Sections;

public partial class ProjectTreePaneView : UserControl
{
    private const int ProjectTreeStaticColumnCount = 2;
    private static readonly TimeSpan ProjectTreeSingleClickSelectionDelay = TimeSpan.FromMilliseconds(250);
    private readonly Dictionary<DataGridColumn, string> _projectTreeTableBaseHeaders = [];
    private readonly ProjectNodeContextMenuController _projectNodeContextMenuController;
    private CancellationTokenSource? _pendingProjectTreeSelectionSync;
    private bool _projectTreeSelectionChangeTriggeredByPointer;
    private ToolbarViewModel? _toolbarViewModel;

    public ProjectTreePaneView()
    {
        InitializeComponent();
        this.FindControl<DataGrid>("ProjectTreeTable")?.AddHandler(
            KeyDownEvent,
            ProjectTreeTable_OnKeyDown,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        _projectNodeContextMenuController = new ProjectNodeContextMenuController(
            this,
            () => DataContext as MainWindowViewModel);
        DataContextChanged += (_, _) => HandleDataContextChanged();
        AttachedToVisualTree += (_, _) =>
        {
            ScheduleRefreshProjectTreeMetricColumns();
            ScheduleApplyProjectTreeTableHeaderStateFromViewModel();
        };
        DetachedFromVisualTree += (_, _) => DetachToolbarSubscription();
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
                !TryMapSortColumn(clickedColumn.SortMemberPath, out var sortColumn, out var metricId))
            {
                return;
            }

            ApplyProjectTreeSort(viewModel, grid, clickedColumn, sortColumn, metricId);
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
            TryMapSortColumn(column.SortMemberPath, out var sortColumn, out var metricId) &&
            sortColumn == viewModel.Tree.CurrentSortColumn &&
            (sortColumn != ProjectTreeSortColumn.Metric || metricId == viewModel.Tree.CurrentMetricSortId));

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

    private void HandleDataContextChanged()
    {
        AttachToolbarSubscription();
        ScheduleRefreshProjectTreeMetricColumns();
        ScheduleApplyProjectTreeTableHeaderStateFromViewModel();
    }

    private void AttachToolbarSubscription()
    {
        var nextToolbar = (DataContext as MainWindowViewModel)?.Toolbar;
        if (ReferenceEquals(_toolbarViewModel, nextToolbar))
        {
            return;
        }

        DetachToolbarSubscription();
        _toolbarViewModel = nextToolbar;
        if (_toolbarViewModel is not null)
        {
            _toolbarViewModel.PropertyChanged += ToolbarViewModel_OnPropertyChanged;
        }
    }

    private void DetachToolbarSubscription()
    {
        if (_toolbarViewModel is null)
        {
            return;
        }

        _toolbarViewModel.PropertyChanged -= ToolbarViewModel_OnPropertyChanged;
        _toolbarViewModel = null;
    }

    private void ToolbarViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ToolbarViewModel.VisibleMetricDefinitions))
        {
            ScheduleRefreshProjectTreeMetricColumns();
        }
    }

    private void ScheduleRefreshProjectTreeMetricColumns()
    {
        Dispatcher.UIThread.Post(
            RefreshProjectTreeMetricColumns,
            DispatcherPriority.Loaded);
    }

    private void RefreshProjectTreeMetricColumns()
    {
        var grid = this.FindControl<DataGrid>("ProjectTreeTable");
        if (grid is null)
        {
            return;
        }

        ClearProjectTreeMetricColumns(grid);

        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        foreach (var definition in viewModel.Toolbar.VisibleMetricDefinitions)
        {
            grid.Columns.Add(CreateMetricColumn(definition));
        }

        EnsureProjectTreeTableBaseHeaders(grid);
        ApplyProjectTreeTableHeaderStateFromViewModel();
    }

    private void ClearProjectTreeMetricColumns(DataGrid grid)
    {
        while (grid.Columns.Count > ProjectTreeStaticColumnCount)
        {
            var removedColumn = grid.Columns[grid.Columns.Count - 1];
            grid.Columns.RemoveAt(grid.Columns.Count - 1);
            _projectTreeTableBaseHeaders.Remove(removedColumn);
        }
    }

    private static DataGridTemplateColumn CreateMetricColumn(MetricDefinition definition)
    {
        return new DataGridTemplateColumn
        {
            Header = definition.ShortName,
            SortMemberPath = GetMetricSortMemberPath(definition.Id),
            Width = GetMetricColumnWidth(definition),
            CellTemplate = new FuncDataTemplate<ProjectTreeNodeViewModel>((node, _) =>
            {
                var textBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = node?.GetMetricText(definition.Id) ?? string.Empty,
                };
                textBlock.Classes.Add("tree-table-number");
                return textBlock;
            }),
        };
    }

    private static DataGridLength GetMetricColumnWidth(MetricDefinition definition)
    {
        if (definition.Unit == MetricUnit.Bytes)
        {
            return new DataGridLength(88);
        }

        return definition.ShortName.Length switch
        {
            <= 5 => new DataGridLength(72),
            <= 8 => new DataGridLength(92),
            <= 12 => new DataGridLength(108),
            _ => new DataGridLength(124),
        };
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
        ProjectTreeSortColumn sortColumn,
        MetricId metricId)
    {
        var direction = GetNextSortDirection(clickedColumn, sortColumn);
        if (sortColumn == ProjectTreeSortColumn.Metric)
        {
            viewModel.Tree.SortByMetric(metricId, direction);
        }
        else
        {
            viewModel.Tree.SortBy(sortColumn, direction);
        }

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

        var sortIcon = FluentIconGeometry.CreatePathIcon(
            direction == ListSortDirection.Ascending
                ? FluentIconGeometry.ArrowSortUp16Regular
                : FluentIconGeometry.ArrowSortDown16Regular,
            "sort-icon",
            14,
            14,
            new Thickness(3, 0, 0, 0));
        sortIcon.Name = direction == ListSortDirection.Ascending
            ? "SortIconAscending"
            : "SortIconDescending";
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

    private static bool TryMapSortColumn(string? sortMemberPath, out ProjectTreeSortColumn column, out MetricId metricId)
    {
        metricId = default;
        switch (sortMemberPath)
        {
            case "ParentShare":
                column = ProjectTreeSortColumn.ParentShare;
                return true;
            case "Name":
                column = ProjectTreeSortColumn.Name;
                return true;
            default:
                if (sortMemberPath is not null &&
                    sortMemberPath.StartsWith("Metric:", StringComparison.Ordinal))
                {
                    column = ProjectTreeSortColumn.Metric;
                    metricId = DefaultMetricCatalog.NormalizeMetricId(new MetricId(sortMemberPath["Metric:".Length..]));
                    return true;
                }

                column = default;
                return false;
        }
    }

    private static string GetMetricSortMemberPath(MetricId metricId) => $"Metric:{metricId.Value}";

}

internal sealed class ProjectTreeColumnHeaderContent(string text) : Grid
{
    public string Text { get; } = text;

    public override string ToString() => Text;
}
