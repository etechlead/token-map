using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Clever.TokenMap.App.ViewModels;

namespace Clever.TokenMap.App.Views.Sections;

public partial class ProjectTreePaneView : UserControl
{
    private readonly Dictionary<DataGridColumn, string> _projectTreeTableBaseHeaders = [];
    private Geometry? _sortIconGeometry;

    public ProjectTreePaneView()
    {
        InitializeComponent();
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
        if (header is null || e.ClickCount != 1)
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

        if (FindAncestor<DataGridRow>(sourceElement) is { DataContext: ProjectTreeNodeViewModel node } &&
            node.HasChildren)
        {
            viewModel.Tree.ToggleNodeCommand.Execute(node);
            e.Handled = true;
        }
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

    private ProjectTreeColumnHeaderContent CreateProjectTreeHeaderContent(string text, ListSortDirection? direction)
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

        var sortIcon = new Path
        {
            Name = direction == ListSortDirection.Ascending
                ? "SortIconAscending"
                : "SortIconDescending",
            Width = 10,
            Height = 10,
            Margin = new Thickness(4, 3, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Stretch = Stretch.Uniform,
            Data = GetProjectTreeSortIconGeometry(direction),
        };
        sortIcon.Classes.Add("sort-icon");
        Grid.SetColumn(sortIcon, 1);

        panel.Children.Add(sortIcon);
        return panel;
    }

    private Geometry GetProjectTreeSortIconGeometry(ListSortDirection? direction)
    {
        if (_sortIconGeometry is not null)
        {
            return direction == ListSortDirection.Ascending
                ? GetProjectTreeSortIconGeometry("FluentSortArrowUp16Geometry")
                : GetProjectTreeSortIconGeometry("FluentSortArrowDown16Geometry");
        }

        _sortIconGeometry = GetProjectTreeSortIconGeometry("FluentSortArrowDown16Geometry");
        return direction == ListSortDirection.Ascending
            ? GetProjectTreeSortIconGeometry("FluentSortArrowUp16Geometry")
            : _sortIconGeometry;
    }

    private Geometry GetProjectTreeSortIconGeometry(string resourceKey)
    {
        if (this.TryGetResource(resourceKey, out var resource) &&
            resource is Geometry geometry)
        {
            return geometry;
        }

        return resourceKey == "FluentSortArrowUp16Geometry"
            ? StreamGeometry.Parse("M7.64645 2.73223C7.84171 2.53697 8.15829 2.53697 8.35355 2.73223L12.5607 6.93934C12.756 7.1346 12.756 7.45118 12.5607 7.64645L11.8536 8.35355C11.6583 8.54882 11.3417 8.54882 11.1464 8.35355L9 6.20711V13.5C9 13.7761 8.77614 14 8.5 14H7.5C7.22386 14 7 13.7761 7 13.5V6.20711L4.85355 8.35355C4.65829 8.54882 4.34171 8.54882 4.14645 8.35355L3.43934 7.64645C3.24408 7.45118 3.24408 7.1346 3.43934 6.93934L7.64645 2.73223Z")
            : StreamGeometry.Parse("M7 2.5C7 2.22386 7.22386 2 7.5 2H8.5C8.77614 2 9 2.22386 9 2.5V9.79289L11.1464 7.64645C11.3417 7.45118 11.6583 7.45118 11.8536 7.64645L12.5607 8.35355C12.756 8.54882 12.756 8.8654 12.5607 9.06066L8.35355 13.2678C8.15829 13.4631 7.84171 13.4631 7.64645 13.2678L3.43934 9.06066C3.24408 8.8654 3.24408 8.54882 3.43934 8.35355L4.14645 7.64645C4.34171 7.45118 4.65829 7.45118 4.85355 7.64645L7 9.79289V2.5Z");
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
