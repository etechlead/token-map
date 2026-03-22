using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Clever.TokenMap.Controls;
using Clever.TokenMap.App.ViewModels;

namespace Clever.TokenMap.App.Views;

public partial class MainWindow : Window
{
    private readonly Dictionary<DataGridColumn, string> _projectTreeTableBaseHeaders = [];

    public MainWindow()
    {
        InitializeComponent();
        var treemap = this.FindControl<TreemapControl>("ProjectTreemapControl");
        if (treemap is not null)
        {
            treemap.DrillDownRequested += ProjectTreemapControl_OnDrillDownRequested;
        }

        DataContextChanged += (_, _) => ApplyProjectTreeTableHeaderStateFromViewModel();
        Opened += (_, _) => ApplyProjectTreeTableHeaderStateFromViewModel();
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
        if (header is not null && e.ClickCount == 1)
        {
            EnsureProjectTreeTableBaseHeaders(grid);

            var clickedColumn = grid.Columns.FirstOrDefault(
                column => Equals(column.Header, header.Content) ||
                          string.Equals(column.Header?.ToString(), header.Content?.ToString(), StringComparison.Ordinal));

            if (clickedColumn is not null &&
                TryMapSortColumn(clickedColumn.SortMemberPath, out var sortColumn))
            {
                var direction = clickedColumn.Tag is ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;

                viewModel.Tree.SortBy(sortColumn, direction);
                UpdateProjectTreeTableHeaderState(grid, clickedColumn, direction);
                e.Handled = true;
            }

            return;
        }

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
                column.Header = direction == ListSortDirection.Ascending
                    ? $"{baseHeader} ^"
                    : $"{baseHeader} v";
                column.Tag = direction;
            }
            else
            {
                column.Header = baseHeader;
                column.Tag = null;
            }
        }
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

    private void ProjectTreemapControl_OnDrillDownRequested(object? sender, TreemapDrillDownRequestedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.DrillIntoTreemap(e.Node);
    }
}
