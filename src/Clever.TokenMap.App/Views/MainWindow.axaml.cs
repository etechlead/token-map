using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Enums;

namespace Clever.TokenMap.App.Views;

public partial class MainWindow : Window
{
    private readonly Control? _projectTreePaneHost;
    private readonly GridSplitter? _workspaceSplitter;
    private readonly Control? _treemapPaneHost;
    private readonly Grid? _workspaceHost;
    private ToolbarViewModel? _toolbarViewModel;
    private WorkspaceLayoutMode? _appliedWorkspaceLayoutMode;

    public MainWindow()
    {
        InitializeComponent();
        _workspaceHost = this.FindControl<Grid>("WorkspaceHost");
        _projectTreePaneHost = this.FindControl<Control>("ProjectTreePaneHost");
        _workspaceSplitter = this.FindControl<GridSplitter>("WorkspaceSplitter");
        _treemapPaneHost = this.FindControl<Control>("TreemapPaneHost");
        DataContextChanged += MainWindow_OnDataContextChanged;
        AddHandler(KeyDownEvent, MainWindow_OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    public MainWindow(MainWindowViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }

    private void SettingsBackdrop_OnPointerPressed(object? sender, PointerPressedEventArgs? e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.CloseSettingsCommand.Execute(null);
            if (e is not null)
            {
                e.Handled = true;
            }
        }
    }

    private void FilePreviewBackdrop_OnPointerPressed(object? sender, PointerPressedEventArgs? e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.CloseFilePreviewCommand.Execute(null);
        if (e is not null)
        {
            e.Handled = true;
        }
    }

    private void MainWindow_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not Key.Escape || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.IsFilePreviewOpen)
        {
            viewModel.CloseFilePreviewCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (viewModel.IsShareSnapshotOpen)
        {
            viewModel.CloseShareSnapshotCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (viewModel.ExcludesEditor.IsOpen)
        {
            viewModel.CancelExcludesEditorCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void MainWindow_OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_toolbarViewModel is not null)
        {
            _toolbarViewModel.PropertyChanged -= ToolbarViewModel_OnPropertyChanged;
        }

        _toolbarViewModel = (DataContext as MainWindowViewModel)?.Toolbar;
        if (_toolbarViewModel is not null)
        {
            _toolbarViewModel.PropertyChanged += ToolbarViewModel_OnPropertyChanged;
        }

        ApplyCurrentWorkspaceLayout();
    }

    private void ToolbarViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName) ||
            e.PropertyName == nameof(ToolbarViewModel.SelectedWorkspaceLayoutMode))
        {
            ApplyCurrentWorkspaceLayout();
        }
    }

    private void ApplyCurrentWorkspaceLayout()
    {
        var mode = _toolbarViewModel?.SelectedWorkspaceLayoutMode ?? WorkspaceLayoutMode.SideBySide;
        ApplyWorkspaceLayout(mode);
    }

    private void ApplyWorkspaceLayout(WorkspaceLayoutMode mode)
    {
        if (_appliedWorkspaceLayoutMode == mode ||
            _workspaceHost is null ||
            _projectTreePaneHost is null ||
            _workspaceSplitter is null ||
            _treemapPaneHost is null)
        {
            return;
        }

        switch (mode)
        {
            case WorkspaceLayoutMode.Stacked:
                _workspaceHost.ColumnDefinitions = new ColumnDefinitions("*");
                _workspaceHost.RowDefinitions = new RowDefinitions("2*,10,3*");

                Grid.SetColumn(_projectTreePaneHost, 0);
                Grid.SetRow(_projectTreePaneHost, 0);
                Grid.SetColumn(_workspaceSplitter, 0);
                Grid.SetRow(_workspaceSplitter, 1);
                Grid.SetColumn(_treemapPaneHost, 0);
                Grid.SetRow(_treemapPaneHost, 2);
                _workspaceSplitter.ResizeDirection = GridResizeDirection.Rows;
                break;
            default:
                _workspaceHost.ColumnDefinitions = new ColumnDefinitions("2*,10,3*");
                _workspaceHost.RowDefinitions = new RowDefinitions("*");

                Grid.SetColumn(_projectTreePaneHost, 0);
                Grid.SetRow(_projectTreePaneHost, 0);
                Grid.SetColumn(_workspaceSplitter, 1);
                Grid.SetRow(_workspaceSplitter, 0);
                Grid.SetColumn(_treemapPaneHost, 2);
                Grid.SetRow(_treemapPaneHost, 0);
                _workspaceSplitter.ResizeDirection = GridResizeDirection.Columns;
                break;
        }

        _appliedWorkspaceLayoutMode = mode;
    }
}
