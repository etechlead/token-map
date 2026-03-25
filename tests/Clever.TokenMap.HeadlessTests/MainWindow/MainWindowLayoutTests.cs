using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Clever.TokenMap.App;
using Clever.TokenMap.Treemap;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.App.Views;
using Clever.TokenMap.App.Views.Sections;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;
using FluentIcons.Avalonia;
using System.Collections.Specialized;
using static Clever.TokenMap.HeadlessTests.HeadlessTestSupport;

namespace Clever.TokenMap.HeadlessTests;

public sealed class MainWindowLayoutTests
{
    [AvaloniaFact]
    public void MainWindow_Title_DefaultsToTokenMap_WhenNoFolderIsSelected()
    {
        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel(),
        };

        window.Show();

        Assert.Equal("TokenMap", window.Title);
    }

    [AvaloniaFact]
    public async Task MainWindow_Title_UsesSelectedFolderName_AfterFolderIsOpened()
    {
        var window = new MainWindow();
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(CreateSnapshot()));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);

        Assert.Equal("Demo - TokenMap", window.Title);
    }

    [AvaloniaFact]
    public void MainWindow_ContainsMvpShellSections()
    {
        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel(),
        };

        window.Show();

        Assert.NotNull(FindNamedDescendant<Control>(window, "ToolbarHost"));
        Assert.NotNull(FindNamedDescendant<Grid>(window, "WorkspaceHost"));
        Assert.NotNull(FindNamedDescendant<Control>(window, "ProjectTreePane"));
        Assert.NotNull(FindNamedDescendant<Control>(window, "TreemapPane"));
        var statusStrip = FindNamedDescendant<Control>(window, "StatusStrip");

        Assert.NotNull(statusStrip);
        Assert.NotNull(FindNamedDescendant<TreemapControl>(window, "ProjectTreemapControl"));
        var treemapTitle = FindNamedDescendant<TextBlock>(window, "TreemapTitleText");
        var projectTreeTitle = FindNamedDescendant<TextBlock>(window, "ProjectTreeTitleText");
        var projectTreeSelectedFolder = FindNamedDescendant<TextBlock>(window, "ProjectTreeSelectedFolderText");
        Assert.NotNull(FindNamedDescendant<DataGrid>(window, "ProjectTreeTable"));
        Assert.NotNull(FindNamedDescendant<ItemsControl>(window, "TreemapBreadcrumbsItemsControl"));
        Assert.Null(FindNamedDescendant<Control>(window, "DetailsPane"));
        Assert.NotNull(FindNamedDescendant<Button>(window, "SettingsButton"));
        Assert.NotNull(FindNamedDescendant<SplitButton>(window, "OpenFolderSplitButton"));
        var stopButton = FindNamedDescendant<Button>(window, "StopButton");
        var settingsDrawer = FindNamedDescendant<Control>(window, "SettingsDrawer");
        Assert.Null(FindNamedDescendant<TextBlock>(window, "TreemapScopeText"));
        Assert.Null(FindNamedDescendant<Button>(window, "TreemapBackToOverviewButton"));
        Assert.NotNull(FindNamedDescendant<ProgressBar>(window, "StatusProgressBar"));
        Assert.Null(FindNamedDescendant<TextBlock>(window, "ProgressTextBlock"));
        Assert.Null(FindNamedDescendant<TextBlock>(window, "StatusValueText"));
        Assert.False(statusStrip.IsVisible);
        Assert.NotNull(stopButton);
        Assert.False(stopButton.IsVisible);
        Assert.NotNull(settingsDrawer);
        Assert.False(settingsDrawer.IsVisible);
        Assert.NotNull(treemapTitle);
        Assert.NotNull(projectTreeTitle);
        Assert.NotNull(projectTreeSelectedFolder);
        Assert.Equal("Treemap - tokens", treemapTitle.Text);
        Assert.Equal("Project Tree", projectTreeTitle.Text);
        Assert.False(projectTreeSelectedFolder.IsVisible);
    }

    [AvaloniaFact]
    public void MainWindow_TreemapTitle_ReflectsSelectedMetric()
    {
        var viewModel = new MainWindowViewModel();
        var window = new MainWindow
        {
            DataContext = viewModel,
        };

        window.Show();

        var treemapTitle = FindNamedDescendant<TextBlock>(window, "TreemapTitleText");

        Assert.NotNull(treemapTitle);
        Assert.Equal("Treemap - tokens", treemapTitle.Text);

        viewModel.Toolbar.IsSizeMetricSelected = true;
        window.UpdateLayout();
        Assert.Equal("Treemap - size", treemapTitle.Text);

        viewModel.Toolbar.IsLinesMetricSelected = true;
        window.UpdateLayout();
        Assert.Equal("Treemap - lines", treemapTitle.Text);
    }

    [AvaloniaFact]
    public void MainWindow_WorkspaceHost_UsesFortySixtySplit()
    {
        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel(),
        };

        window.Show();

        var workspaceHost = FindNamedDescendant<Grid>(window, "WorkspaceHost");

        Assert.NotNull(workspaceHost);
        Assert.Equal(3, workspaceHost.ColumnDefinitions.Count);
        Assert.Equal(2, workspaceHost.ColumnDefinitions[0].Width.Value);
        Assert.Equal(GridUnitType.Star, workspaceHost.ColumnDefinitions[0].Width.GridUnitType);
        Assert.Equal(10, workspaceHost.ColumnDefinitions[1].Width.Value);
        Assert.Equal(GridUnitType.Pixel, workspaceHost.ColumnDefinitions[1].Width.GridUnitType);
        Assert.Equal(3, workspaceHost.ColumnDefinitions[2].Width.Value);
        Assert.Equal(GridUnitType.Star, workspaceHost.ColumnDefinitions[2].Width.GridUnitType);
    }

    [AvaloniaFact]
    public async Task MainWindow_ToolbarGroups_DoNotOverlapAtMinimumWidth_AfterSnapshotIsLoaded()
    {
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(CreateSnapshot()));
        var window = new MainWindow
        {
            DataContext = viewModel,
            Width = 1100,
        };

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);
        window.UpdateLayout();

        var primaryGroup = FindNamedDescendant<Control>(window, "ToolbarPrimaryGroup");
        var summaryGroup = FindNamedDescendant<Control>(window, "ToolbarSummaryGroup");
        var settingsButton = FindNamedDescendant<Button>(window, "SettingsButton");

        Assert.NotNull(primaryGroup);
        Assert.NotNull(summaryGroup);
        Assert.NotNull(settingsButton);
        Assert.True(
            primaryGroup.Bounds.Right <= summaryGroup.Bounds.Left,
            $"PrimaryGroup.Right={primaryGroup.Bounds.Right}, SummaryGroup.Left={summaryGroup.Bounds.Left}");
        Assert.True(
            summaryGroup.Bounds.Right <= settingsButton.Bounds.Left,
            $"SummaryGroup.Right={summaryGroup.Bounds.Right}, SettingsButton.Left={settingsButton.Bounds.Left}");
    }

    [AvaloniaFact]
    public void MainWindow_ShowsRecentFoldersEmptyState_WhenNoRecentFoldersAreLoaded()
    {
        var viewModel = new MainWindowViewModel();
        var window = new MainWindow
        {
            DataContext = viewModel,
        };

        window.Show();

        var startSurface = FindNamedDescendant<Control>(window, "RecentFoldersStartSurface");
        var emptyState = FindNamedDescendant<Control>(window, "RecentFoldersEmptyState");
        var emptyStateOpenButton = FindNamedDescendant<Button>(window, "RecentFoldersEmptyStateOpenButton");
        var workspaceHost = FindNamedDescendant<Grid>(window, "WorkspaceHost");
        var recentFoldersItems = FindNamedDescendant<ItemsControl>(window, "RecentFoldersItemsControl");

        Assert.NotNull(startSurface);
        Assert.NotNull(emptyState);
        Assert.NotNull(emptyStateOpenButton);
        Assert.NotNull(workspaceHost);
        Assert.NotNull(recentFoldersItems);
        Assert.True(viewModel.RecentFolders.ShowStartSurface);
        Assert.False(viewModel.RecentFolders.HasRecentFolders);
        Assert.True(viewModel.RecentFolders.ShowEmptyState);
        Assert.True(startSurface.IsVisible);
        Assert.True(emptyState.IsVisible);
        Assert.True(workspaceHost.IsVisible);
        Assert.Equal(0, recentFoldersItems.ItemCount);
    }

    [AvaloniaFact]
    public void MainWindow_ShowsRecentFoldersStartSurface_WhenRecentFoldersAreLoaded()
    {
        var viewModel = CreateMainWindowViewModel(
            new StubProjectAnalyzer(CreateSnapshot()),
            selectedFolderPath: "C:\\Demo",
            recentFolderPaths:
            [
                "C:\\RepoA",
                "C:\\RepoB",
            ]);
        var window = new MainWindow
        {
            DataContext = viewModel,
        };

        window.Show();

        var startSurface = FindNamedDescendant<Control>(window, "RecentFoldersStartSurface");
        var emptyState = FindNamedDescendant<Control>(window, "RecentFoldersEmptyState");
        var workspaceHost = FindNamedDescendant<Grid>(window, "WorkspaceHost");
        var clearButton = FindNamedDescendant<Button>(window, "ClearRecentFoldersButton");
        var recentFoldersItems = FindNamedDescendant<ItemsControl>(window, "RecentFoldersItemsControl");

        Assert.NotNull(startSurface);
        Assert.NotNull(emptyState);
        Assert.NotNull(workspaceHost);
        Assert.NotNull(clearButton);
        Assert.NotNull(recentFoldersItems);
        Assert.True(viewModel.RecentFolders.ShowStartSurface);
        Assert.True(viewModel.RecentFolders.HasRecentFolders);
        Assert.False(viewModel.RecentFolders.ShowEmptyState);
        Assert.True(startSurface.IsVisible);
        Assert.False(emptyState.IsVisible);
        Assert.True(clearButton.IsVisible);
        Assert.True(workspaceHost.IsVisible);
        Assert.Equal(2, recentFoldersItems.ItemCount);
    }

    [AvaloniaFact]
    public void MainWindow_RecentFolderTile_DoesNotLeaveDeadGap_BetweenOpenAndRemoveActions()
    {
        var window = new MainWindow
        {
            DataContext = CreateMainWindowViewModel(
                new StubProjectAnalyzer(CreateSnapshot()),
                selectedFolderPath: "C:\\Demo",
                recentFolderPaths:
                [
                    "C:\\RepoA",
                ]),
        };

        window.Show();
        window.UpdateLayout();

        var tile = window.GetVisualDescendants()
            .OfType<Grid>()
            .FirstOrDefault(control => control.Classes.Contains("recent-folder-tile"));
        var openButton = window.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(control => control.Classes.Contains("recent-folder-button"));
        var removeButton = window.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(control => control.Classes.Contains("recent-folder-remove-button"));

        Assert.NotNull(tile);
        Assert.NotNull(openButton);
        Assert.NotNull(removeButton);
        Assert.True(
            openButton.Bounds.Right >= removeButton.Bounds.Left,
            $"OpenButton.Right={openButton.Bounds.Right}, RemoveButton.Left={removeButton.Bounds.Left}");
        Assert.True(
            openButton.Bounds.Width >= tile.Bounds.Width - 1,
            $"OpenButton.Width={openButton.Bounds.Width}, Tile.Width={tile.Bounds.Width}");
    }

    [AvaloniaFact]
    public void MainWindow_OpenFolderSplitButton_HasRecentFoldersFlyout()
    {
        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel(),
        };

        window.Show();

        var splitButton = FindNamedDescendant<SplitButton>(window, "OpenFolderSplitButton");

        Assert.NotNull(splitButton);
        Assert.NotNull(splitButton.Flyout);
    }

    [Fact]
    public void MainWindowViewModel_ProvidesFlyoutPlaceholder_WhenNoRecentFoldersExist()
    {
        var viewModel = new MainWindowViewModel();

        Assert.Single(viewModel.RecentFolders.FlyoutItems);
        Assert.False(viewModel.RecentFolders.FlyoutItems[0].CanOpen);
        Assert.Equal("No previous folders yet", viewModel.RecentFolders.FlyoutItems[0].DisplayName);
    }

    [Fact]
    public void MainWindowViewModel_RemoveRecentFolderCommand_RemovesOneEntry()
    {
        var viewModel = CreateMainWindowViewModel(
            new StubProjectAnalyzer(CreateSnapshot()),
            recentFolderPaths:
            [
                "C:\\RepoA",
                "C:\\RepoB",
            ]);

        var folderToRemove = Assert.Single(viewModel.RecentFolders.Items, folder => folder.DisplayName == "RepoB");

        viewModel.RecentFolders.RemoveRecentFolderCommand.Execute(folderToRemove);

        Assert.Single(viewModel.RecentFolders.Items);
        Assert.Equal("RepoA", viewModel.RecentFolders.Items[0].DisplayName);
    }

    [Fact]
    public void MainWindowViewModel_ClearRecentFoldersCommand_ClearsListAndRestoresFlyoutPlaceholder()
    {
        var viewModel = CreateMainWindowViewModel(
            new StubProjectAnalyzer(CreateSnapshot()),
            recentFolderPaths:
            [
                "C:\\RepoA",
                "C:\\RepoB",
            ]);

        viewModel.RecentFolders.ClearRecentFoldersCommand.Execute(null);

        Assert.Empty(viewModel.RecentFolders.Items);
        Assert.Single(viewModel.RecentFolders.FlyoutItems);
        Assert.Equal("No previous folders yet", viewModel.RecentFolders.FlyoutItems[0].DisplayName);
    }

    [AvaloniaFact]
    public void MainWindow_ProjectTreeTable_UsesRequestedColumns()
    {
        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel(),
        };

        window.Show();

        var treeTable = FindNamedDescendant<DataGrid>(window, "ProjectTreeTable");

        Assert.NotNull(treeTable);
        Assert.Collection(
            treeTable.Columns.Select(column => column.Header?.ToString()),
            header => Assert.Equal("Name", header),
            header => Assert.Equal("% Parent", header),
            header => Assert.Equal("Tokens", header),
            header => Assert.Equal("Lines", header),
            header => Assert.Equal("Size", header));

        Assert.IsType<DataGridTemplateColumn>(treeTable.Columns[0]);
        Assert.IsType<DataGridTemplateColumn>(treeTable.Columns[1]);
        Assert.IsType<DataGridTemplateColumn>(treeTable.Columns[2]);
        Assert.IsType<DataGridTemplateColumn>(treeTable.Columns[3]);
        Assert.IsType<DataGridTemplateColumn>(treeTable.Columns[4]);
    }

    [AvaloniaFact]
    public async Task MainWindow_ProjectTreeHeaders_ReflectCurrentSortStateFromViewModel()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.Tree.LoadRoot(CreateRootWithChildren(
            ("Zulu.cs", 20, 2, 2),
            ("Alpha.cs", 10, 1, 1)));
        viewModel.Tree.SortBy(ProjectTreeSortColumn.Name, System.ComponentModel.ListSortDirection.Ascending);

        var window = new MainWindow
        {
            DataContext = viewModel,
        };

        window.Show();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        window.UpdateLayout();

        var nameHeader = FindProjectTreeHeader(window, "Name");
        var tokensHeader = FindProjectTreeHeader(window, "Tokens");
        var nameAscendingIcon = FindHeaderElement<FluentIcon>(nameHeader, "SortIconAscending");
        var nameDescendingIcon = FindHeaderElement<FluentIcon>(nameHeader, "SortIconDescending");
        var tokensDescendingIcon = FindHeaderElement<FluentIcon>(tokensHeader, "SortIconDescending");

        Assert.Equal(ProjectTreeSortColumn.Name, viewModel.Tree.CurrentSortColumn);
        Assert.Equal(System.ComponentModel.ListSortDirection.Ascending, viewModel.Tree.CurrentSortDirection);
        Assert.NotNull(nameHeader);
        Assert.NotNull(tokensHeader);
        Assert.NotNull(nameAscendingIcon);
        Assert.True(nameAscendingIcon.IsVisible);
        Assert.True(
            nameAscendingIcon.Bounds.Width >= 16 && nameAscendingIcon.Bounds.Height >= 16,
            $"Sort icon should keep a full 16x16 layout slot. Actual={nameAscendingIcon.Bounds}.");
        Assert.Null(nameDescendingIcon);
        Assert.Null(tokensDescendingIcon);
    }

    [AvaloniaFact]
    public void MainWindow_ProjectTreeHeaders_DoNotReserveHiddenSortIconWidth()
    {
        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel(),
        };

        window.Show();
        window.UpdateLayout();

        var headers = window.GetVisualDescendants()
            .OfType<DataGridColumnHeader>()
            .ToList();

        var sizeHeader = headers.First(header => string.Equals(header.Content?.ToString(), "Size", StringComparison.Ordinal));
        var linesHeader = headers.First(header => string.Equals(header.Content?.ToString(), "Lines", StringComparison.Ordinal));

        var sizeContentPresenter = sizeHeader.GetVisualDescendants()
            .OfType<ContentPresenter>()
            .First(control => string.Equals(control.Name, "PART_ContentPresenter", StringComparison.Ordinal));
        var linesContentPresenter = linesHeader.GetVisualDescendants()
            .OfType<ContentPresenter>()
            .First(control => string.Equals(control.Name, "PART_ContentPresenter", StringComparison.Ordinal));

        Assert.True(
            sizeContentPresenter.Bounds.Width >= 70,
            $"Size header content width should use the available column width. Actual={sizeContentPresenter.Bounds.Width}.");
        Assert.True(
            linesContentPresenter.Bounds.Width >= 56,
            $"Lines header content width should use the available column width. Actual={linesContentPresenter.Bounds.Width}.");
    }

    [AvaloniaFact]
    public void MainWindow_InitialToolbarAvailability_EnablesScanSettingsButDisablesMetric()
    {
        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel(),
        };

        window.Show();

        var metricTokensRadioButton = FindNamedDescendant<RadioButton>(window, "MetricTokensRadioButton");
        var metricLinesRadioButton = FindNamedDescendant<RadioButton>(window, "MetricLinesRadioButton");
        var metricSizeRadioButton = FindNamedDescendant<RadioButton>(window, "MetricSizeRadioButton");
        var themeSystemButton = FindNamedDescendant<ToggleButton>(window, "ThemeSystemButton");
        var themeLightButton = FindNamedDescendant<ToggleButton>(window, "ThemeLightButton");
        var themeDarkButton = FindNamedDescendant<ToggleButton>(window, "ThemeDarkButton");
        var gitIgnoreCheckBox = FindNamedDescendant<CheckBox>(window, "RespectGitIgnoreCheckBox");
        var globalExcludesCheckBox = FindNamedDescendant<CheckBox>(window, "UseGlobalExcludesCheckBox");
        var rescanButton = FindNamedDescendant<Button>(window, "RescanButton");
        var selectedFolderGroup = FindNamedDescendant<Control>(window, "SelectedFolderGroup");
        var summaryGroup = FindNamedDescendant<Control>(window, "ToolbarSummaryGroup");

        Assert.NotNull(metricTokensRadioButton);
        Assert.NotNull(metricLinesRadioButton);
        Assert.NotNull(metricSizeRadioButton);
        Assert.NotNull(themeSystemButton);
        Assert.NotNull(themeLightButton);
        Assert.NotNull(themeDarkButton);
        Assert.NotNull(gitIgnoreCheckBox);
        Assert.NotNull(globalExcludesCheckBox);
        Assert.NotNull(rescanButton);
        Assert.Null(selectedFolderGroup);
        Assert.NotNull(summaryGroup);
        Assert.False(metricTokensRadioButton.IsEnabled);
        Assert.False(metricLinesRadioButton.IsEnabled);
        Assert.False(metricSizeRadioButton.IsEnabled);
        Assert.True(metricTokensRadioButton.IsChecked);
        Assert.False(metricLinesRadioButton.IsChecked);
        Assert.False(metricSizeRadioButton.IsChecked);
        Assert.True(themeSystemButton.IsEnabled);
        Assert.True(themeLightButton.IsEnabled);
        Assert.True(themeDarkButton.IsEnabled);
        Assert.True(themeSystemButton.IsChecked);
        Assert.False(themeLightButton.IsChecked);
        Assert.False(themeDarkButton.IsChecked);
        Assert.True(gitIgnoreCheckBox.IsEnabled);
        Assert.True(globalExcludesCheckBox.IsEnabled);
        Assert.False(rescanButton.IsVisible);
        Assert.False(summaryGroup.IsVisible);
    }

    [AvaloniaFact]
    public void MainWindow_SettingsDrawer_IsHiddenByDefaultAndTogglesFromViewModel()
    {
        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel(),
        };

        window.Show();

        var drawer = FindNamedDescendant<Control>(window, "SettingsDrawer");
        var drawerHost = FindNamedDescendant<Control>(window, "SettingsDrawerHost");
        var backdrop = FindNamedDescendant<Control>(window, "SettingsBackdrop");
        var startSurface = FindNamedDescendant<Control>(window, "RecentFoldersStartSurface");
        var viewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);

        Assert.NotNull(drawer);
        Assert.NotNull(drawerHost);
        Assert.NotNull(backdrop);
        Assert.NotNull(startSurface);
        Assert.False(drawer.IsVisible);
        Assert.False(backdrop.IsVisible);
        Assert.True(drawerHost.ZIndex > startSurface.ZIndex);

        viewModel.ToggleSettingsCommand.Execute(null);
        Assert.True(drawer.IsVisible);
        Assert.True(backdrop.IsVisible);

        viewModel.ToggleSettingsCommand.Execute(null);
        Assert.False(drawer.IsVisible);
        Assert.False(backdrop.IsVisible);
    }

    [Fact]
    public void MainWindowViewModel_CloseSettingsCommand_ClosesDrawerWithoutRetoggling()
    {
        var viewModel = new MainWindowViewModel
        {
            IsSettingsOpen = true,
        };

        viewModel.CloseSettingsCommand.Execute(null);

        Assert.False(viewModel.IsSettingsOpen);
    }

    [AvaloniaFact]
    public void MainWindow_SettingsDrawer_ShowsGlobalExcludesEditorActionInline()
    {
        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel(),
        };

        window.Show();

        var viewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);
        viewModel.ToggleSettingsCommand.Execute(null);
        window.UpdateLayout();

        var globalExcludesCheckBox = FindNamedDescendant<CheckBox>(window, "UseGlobalExcludesCheckBox");
        var editButton = FindNamedDescendant<Button>(window, "EditGlobalExcludesButton");
        var rescanNotice = FindNamedDescendant<Control>(window, "ScanSettingsRescanNotice");
        var folderPanel = FindNamedDescendant<Control>(window, "CurrentFolderSettingsPanel");

        Assert.NotNull(globalExcludesCheckBox);
        Assert.NotNull(editButton);
        Assert.NotNull(rescanNotice);
        Assert.NotNull(folderPanel);
        Assert.True(globalExcludesCheckBox.IsVisible);
        Assert.Equal("Edit", editButton.Content?.ToString());
        Assert.False(rescanNotice.IsVisible);
        Assert.False(folderPanel.IsVisible);
    }

    [AvaloniaFact]
    public void MainWindow_GlobalExcludesEditor_OpensAndCancelsWithoutSaving()
    {
        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel(),
        };

        window.Show();

        var viewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);
        viewModel.OpenGlobalExcludesEditorCommand.Execute(null);
        window.UpdateLayout();

        var modal = FindNamedDescendant<Control>(window, "ExcludesEditorModal");
        var backdrop = FindNamedDescendant<Control>(window, "ExcludesEditorBackdrop");
        var editor = FindNamedDescendant<TextBox>(window, "ExcludesEditorTextBox");
        var saveButton = FindNamedDescendant<Button>(window, "SaveExcludesButton");
        var saveAndRescanButton = FindNamedDescendant<Button>(window, "SaveAndRescanExcludesButton");

        Assert.NotNull(modal);
        Assert.NotNull(backdrop);
        Assert.NotNull(editor);
        Assert.NotNull(saveButton);
        Assert.NotNull(saveAndRescanButton);
        Assert.True(modal.IsVisible);
        Assert.True(backdrop.IsVisible);
        Assert.Equal(
            string.Join(Environment.NewLine, GlobalExcludeDefaults.DefaultEntries).ReplaceLineEndings("\n"),
            editor.Text?.ReplaceLineEndings("\n"));
        Assert.Equal("Save", saveButton.Content?.ToString());
        Assert.Equal("Save and Rescan", saveAndRescanButton.Content?.ToString());

        viewModel.CancelExcludesEditorCommand.Execute(null);
        window.UpdateLayout();

        Assert.False(modal.IsVisible);
        Assert.False(backdrop.IsVisible);
        Assert.Equal(GlobalExcludeDefaults.DefaultEntries, viewModel.Toolbar.BuildScanOptions().GlobalExcludes);
    }

    [AvaloniaFact]
    public async Task MainWindow_GlobalExcludesSave_ShowsAndClearsRescanNotice()
    {
        var window = new MainWindow();
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(CreateSnapshot()));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);

        viewModel.OpenGlobalExcludesEditorCommand.Execute(null);
        viewModel.ExcludesEditor.Text = " node_modules\\\\ \n\n/src//generated/**\n!nested/scripts/";
        viewModel.SaveExcludesEditorCommand.Execute(null);
        window.UpdateLayout();

        var notice = FindNamedDescendant<Control>(window, "ScanSettingsRescanNotice");
        var rescanButton = FindNamedDescendant<Button>(window, "ScanSettingsRescanButton");

        Assert.NotNull(notice);
        Assert.NotNull(rescanButton);
        Assert.True(viewModel.ExcludesEditor.ShowRescanNotice);
        Assert.True(notice.IsVisible);
        Assert.Collection(
            viewModel.Toolbar.BuildScanOptions().GlobalExcludes,
            entry => Assert.Equal("node_modules/", entry),
            entry => Assert.Equal("/src/generated/**", entry),
            entry => Assert.Equal("!nested/scripts/", entry));

        viewModel.OpenGlobalExcludesEditorCommand.Execute(null);
        viewModel.ExcludesEditor.Text += "\nobj/";
        Assert.False(viewModel.ExcludesEditor.ShowRescanNotice);
        viewModel.CancelExcludesEditorCommand.Execute(null);

        viewModel.OpenGlobalExcludesEditorCommand.Execute(null);
        viewModel.ExcludesEditor.Text = "vendor/";
        viewModel.SaveExcludesEditorCommand.Execute(null);
        Assert.True(viewModel.ExcludesEditor.ShowRescanNotice);

        await viewModel.Toolbar.RescanCommand.ExecuteAsync(null);

        Assert.False(viewModel.ExcludesEditor.ShowRescanNotice);
        Assert.False(notice.IsVisible);
    }

    [AvaloniaFact]
    public async Task MainWindow_SettingsDrawer_ShowsCurrentFolderExcludesBlock_WhenFolderIsCommitted()
    {
        var window = new MainWindow();
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(CreateSnapshot()));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);
        viewModel.ToggleSettingsCommand.Execute(null);
        window.UpdateLayout();

        var folderPanel = FindNamedDescendant<Control>(window, "CurrentFolderSettingsPanel");
        var folderTitle = FindNamedDescendant<TextBlock>(window, "CurrentFolderSettingsTitle");
        var folderCheckbox = FindNamedDescendant<CheckBox>(window, "UseFolderExcludesCheckBox");
        var folderEditButton = FindNamedDescendant<Button>(window, "EditFolderExcludesButton");

        Assert.NotNull(folderPanel);
        Assert.NotNull(folderTitle);
        Assert.NotNull(folderCheckbox);
        Assert.NotNull(folderEditButton);
        Assert.True(folderPanel.IsVisible);
        Assert.Equal("Current folder: Demo", folderTitle.Text);
        Assert.Equal("Edit", folderEditButton.Content?.ToString());
    }

    [AvaloniaFact]
    public async Task MainWindow_FolderExcludesSave_UsesSharedEditorAndShowsRescanNotice()
    {
        var window = new MainWindow();
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(CreateSnapshot()));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);

        viewModel.OpenFolderExcludesEditorCommand.Execute(null);
        viewModel.ExcludesEditor.Text = "/generated/\n!generated/keep.txt";
        viewModel.SaveExcludesEditorCommand.Execute(null);
        window.UpdateLayout();

        Assert.True(viewModel.ExcludesEditor.ShowRescanNotice);
        Assert.True(viewModel.Toolbar.BuildScanOptions().UseFolderExcludes);
        Assert.Collection(
            viewModel.Toolbar.BuildScanOptions().FolderExcludes,
            entry => Assert.Equal("/generated/", entry),
            entry => Assert.Equal("!generated/keep.txt", entry));

        await viewModel.Toolbar.RescanCommand.ExecuteAsync(null);

        Assert.False(viewModel.ExcludesEditor.ShowRescanNotice);
    }

    [AvaloniaFact]
    public async Task MainWindow_SaveAndRescanExcludesEditor_RescansImmediately()
    {
        var analyzer = new CountingProjectAnalyzer(
            HeadlessTestSupport.CreateSnapshot(),
            HeadlessTestSupport.CreateSnapshot());
        var viewModel = CreateMainWindowViewModel(analyzer);
        var window = new MainWindow
        {
            DataContext = viewModel,
        };

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);

        viewModel.OpenFolderExcludesEditorCommand.Execute(null);
        viewModel.ExcludesEditor.Text = "/generated/";
        await viewModel.SaveAndRescanExcludesEditorCommand.ExecuteAsync(null);

        Assert.Equal(2, analyzer.CallCount);
        Assert.False(viewModel.ExcludesEditor.IsOpen);
        Assert.False(viewModel.ExcludesEditor.ShowRescanNotice);
    }

    [Fact]
    public async Task MainWindowViewModel_ExcludeNodeFromFolder_AppendsExactEntryAndOpensFolderEditor()
    {
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(CreateSnapshot()));
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);
        viewModel.ExcludeNodeFromFolderCommand.Execute(new ProjectNode
        {
            Id = "src",
            Name = "src",
            FullPath = "C:\\Demo\\src",
            RelativePath = "src",
            Kind = ProjectNodeKind.Directory,
            Metrics = NodeMetrics.Empty,
        });

        Assert.True(viewModel.ExcludesEditor.IsOpen);
        Assert.Equal("Excludes for Demo", viewModel.ExcludesEditor.Title);
        Assert.Equal("/src/", viewModel.ExcludesEditor.Text.ReplaceLineEndings("\n"));
    }

    [AvaloniaFact]
    public async Task MainWindow_OpenFolderFlow_PopulatesTreeAndSummary()
    {
        var window = new MainWindow();
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(CreateSnapshot()));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);

        var treeTable = FindNamedDescendant<DataGrid>(window, "ProjectTreeTable");
        var statusStrip = FindNamedDescendant<Control>(window, "StatusStrip");
        var tokenSummaryText = FindNamedDescendant<TextBlock>(window, "TokenSummaryValueText");
        var lineSummaryText = FindNamedDescendant<TextBlock>(window, "LineSummaryValueText");
        var fileSummaryText = FindNamedDescendant<TextBlock>(window, "FileSummaryValueText");
        var warningSummaryText = FindNamedDescendant<TextBlock>(window, "WarningSummaryValueText");
        var metricTokensRadioButton = FindNamedDescendant<RadioButton>(window, "MetricTokensRadioButton");
        var metricLinesRadioButton = FindNamedDescendant<RadioButton>(window, "MetricLinesRadioButton");
        var metricSizeRadioButton = FindNamedDescendant<RadioButton>(window, "MetricSizeRadioButton");
        var startSurface = FindNamedDescendant<Control>(window, "RecentFoldersStartSurface");
        var workspaceHost = FindNamedDescendant<Grid>(window, "WorkspaceHost");
        var rescanButton = FindNamedDescendant<Button>(window, "RescanButton");
        var selectedFolderGroup = FindNamedDescendant<Control>(window, "SelectedFolderGroup");
        var summaryGroup = FindNamedDescendant<Control>(window, "ToolbarSummaryGroup");
        var projectTreeTitle = FindNamedDescendant<TextBlock>(window, "ProjectTreeTitleText");
        var projectTreeSelectedFolder = FindNamedDescendant<TextBlock>(window, "ProjectTreeSelectedFolderText");

        Assert.NotNull(treeTable);
        Assert.NotNull(statusStrip);
        Assert.NotNull(startSurface);
        Assert.NotNull(workspaceHost);
        Assert.NotNull(rescanButton);
        Assert.Null(selectedFolderGroup);
        Assert.NotNull(summaryGroup);
        Assert.NotNull(projectTreeTitle);
        Assert.NotNull(projectTreeSelectedFolder);
        Assert.Single(viewModel.Tree.RootNodes);
        Assert.Equal(2, viewModel.Tree.VisibleNodes.Count);
        Assert.Equal(AnalysisState.Completed, viewModel.AnalysisState);
        Assert.Equal("42", tokenSummaryText?.Text);
        Assert.Equal("11", lineSummaryText?.Text);
        Assert.Equal("1", fileSummaryText?.Text);
        Assert.Null(warningSummaryText);
        Assert.False(viewModel.RecentFolders.ShowStartSurface);
        Assert.True(viewModel.RecentFolders.HasRecentFolders);
        Assert.Collection(
            viewModel.RecentFolders.Items,
            folder => Assert.Equal("Demo", folder.DisplayName));
        Assert.NotNull(metricTokensRadioButton);
        Assert.NotNull(metricLinesRadioButton);
        Assert.NotNull(metricSizeRadioButton);
        Assert.True(metricTokensRadioButton.IsEnabled);
        Assert.True(metricLinesRadioButton.IsEnabled);
        Assert.True(metricSizeRadioButton.IsEnabled);
        Assert.True(metricTokensRadioButton.IsChecked);
        Assert.Equal("Project Tree", projectTreeTitle.Text);
        Assert.Equal(@"C:\Demo", projectTreeSelectedFolder.Text);
        Assert.False(metricLinesRadioButton.IsChecked);
        Assert.False(metricSizeRadioButton.IsChecked);
        Assert.False(statusStrip.IsVisible);
        Assert.False(startSurface.IsVisible);
        Assert.True(workspaceHost.IsVisible);
        Assert.True(rescanButton.IsVisible);
        Assert.True(projectTreeSelectedFolder.IsVisible);
        Assert.True(summaryGroup.IsVisible);
    }

    [AvaloniaFact]
    public void SummaryViewModel_ShowsProgressOnlyWhileAnalysisIsActive()
    {
        var viewModel = new SummaryViewModel();

        Assert.False(viewModel.IsProgressVisible);

        viewModel.SetState(AnalysisState.Scanning);
        Assert.True(viewModel.IsProgressVisible);
        Assert.True(viewModel.IsProgressIndeterminate);
        Assert.Equal(0, viewModel.ProgressValue);

        viewModel.UpdateProgress(new AnalysisProgress("ScanningTree", 4, null, "src"));
        Assert.True(viewModel.IsProgressVisible);
        Assert.True(viewModel.IsProgressIndeterminate);
        Assert.Equal(0, viewModel.ProgressValue);

        viewModel.UpdateProgress(new AnalysisProgress("AnalyzingFiles", 3, 6, "src/Program.cs"));
        Assert.True(viewModel.IsProgressVisible);
        Assert.False(viewModel.IsProgressIndeterminate);
        Assert.Equal(50, viewModel.ProgressValue);

        viewModel.SetCompleted(CreateSnapshot());
        Assert.False(viewModel.IsProgressVisible);
        Assert.False(viewModel.IsProgressIndeterminate);
        Assert.Equal(0, viewModel.ProgressValue);

        viewModel.SetState(AnalysisState.Cancelled);
        Assert.False(viewModel.IsProgressVisible);
    }

    [AvaloniaFact]
    public void SummaryViewModel_IgnoresLateProgressAfterCompletion()
    {
        var viewModel = new SummaryViewModel();

        viewModel.SetState(AnalysisState.Scanning);
        viewModel.SetCompleted(CreateSnapshot());
        viewModel.UpdateProgress(new AnalysisProgress("AnalyzingFiles", 6, 6, "src/Program.cs"));

        Assert.False(viewModel.IsProgressVisible);
        Assert.False(viewModel.IsProgressIndeterminate);
        Assert.Equal(0, viewModel.ProgressValue);
    }

    [AvaloniaFact]
    public void ProjectTreeViewModel_LoadRoot_DefaultsToTokensDescendingSort()
    {
        var viewModel = new ProjectTreeViewModel();
        var root = CreateRootWithChildren(
            ("Small.cs", 10, 20, 1),
            ("Large.cs", 20, 10, 1));

        viewModel.LoadRoot(root);

        Assert.Equal(ProjectTreeSortColumn.Tokens, viewModel.CurrentSortColumn);
        Assert.Equal(System.ComponentModel.ListSortDirection.Descending, viewModel.CurrentSortDirection);
        Assert.Collection(
            viewModel.VisibleNodes.Select(node => node.Name),
            name => Assert.Equal("Demo", name),
            name => Assert.Equal("Small.cs", name),
            name => Assert.Equal("Large.cs", name));
    }

    [AvaloniaFact]
    public void ProjectTreeViewModel_LoadRoot_PreservesActiveSortAcrossReloads()
    {
        var viewModel = new ProjectTreeViewModel();
        viewModel.LoadRoot(CreateRootWithChildren(
            ("Alpha.cs", 10, 10, 1),
            ("Beta.cs", 20, 20, 1)));

        viewModel.SortBy(ProjectTreeSortColumn.Tokens, System.ComponentModel.ListSortDirection.Ascending);
        viewModel.LoadRoot(CreateRootWithChildren(
            ("Gamma.cs", 30, 30, 1),
            ("Delta.cs", 5, 5, 1)));

        Assert.Equal(ProjectTreeSortColumn.Tokens, viewModel.CurrentSortColumn);
        Assert.Equal(System.ComponentModel.ListSortDirection.Ascending, viewModel.CurrentSortDirection);
        Assert.Collection(
            viewModel.VisibleNodes.Select(node => node.Name),
            name => Assert.Equal("Demo", name),
            name => Assert.Equal("Delta.cs", name),
            name => Assert.Equal("Gamma.cs", name));
    }

    [AvaloniaFact]
    public void ProjectTreeViewModel_SortBy_ReordersVisibleRows()
    {
        var viewModel = new ProjectTreeViewModel();
        var root = new ProjectNode
        {
            Id = "/",
            Name = "Demo",
            FullPath = "C:\\Demo",
            RelativePath = string.Empty,
            Kind = ProjectNodeKind.Root,
            Metrics = new NodeMetrics(
                Tokens: 30,
                NonEmptyLines: 3,
                FileSizeBytes: 30,
                DescendantFileCount: 2,
                DescendantDirectoryCount: 0),
            Children =
            {
                new ProjectNode
                {
                    Id = "A.cs",
                    Name = "A.cs",
                    FullPath = "C:\\Demo\\A.cs",
                    RelativePath = "A.cs",
                    Kind = ProjectNodeKind.File,
                    Metrics = new NodeMetrics(
                        Tokens: 10,
                        NonEmptyLines: 1,
                        FileSizeBytes: 10,
                        DescendantFileCount: 1,
                        DescendantDirectoryCount: 0),
                },
                new ProjectNode
                {
                    Id = "B.cs",
                    Name = "B.cs",
                    FullPath = "C:\\Demo\\B.cs",
                    RelativePath = "B.cs",
                    Kind = ProjectNodeKind.File,
                    Metrics = new NodeMetrics(
                        Tokens: 20,
                        NonEmptyLines: 2,
                        FileSizeBytes: 20,
                        DescendantFileCount: 1,
                        DescendantDirectoryCount: 0),
                },
            },
        };

        viewModel.LoadRoot(root);
        viewModel.SortBy(ProjectTreeSortColumn.Tokens, System.ComponentModel.ListSortDirection.Descending);

        Assert.Collection(
            viewModel.VisibleNodes.Select(node => node.Name),
            name => Assert.Equal("Demo", name),
            name => Assert.Equal("B.cs", name),
            name => Assert.Equal("A.cs", name));
    }

    [AvaloniaFact]
    public void ProjectTreeViewModel_SortByParentShare_KeepsNaRowsLast()
    {
        var viewModel = new ProjectTreeViewModel();
        viewModel.LoadRoot(new ProjectNode
        {
            Id = "/",
            Name = "Demo",
            FullPath = "C:\\Demo",
            RelativePath = string.Empty,
            Kind = ProjectNodeKind.Root,
            Metrics = new NodeMetrics(
                Tokens: 30,
                NonEmptyLines: 20,
                FileSizeBytes: 40,
                DescendantFileCount: 3,
                DescendantDirectoryCount: 0),
            Children =
            {
                new ProjectNode
                {
                    Id = "Alpha.cs",
                    Name = "Alpha.cs",
                    FullPath = "C:\\Demo\\Alpha.cs",
                    RelativePath = "Alpha.cs",
                    Kind = ProjectNodeKind.File,
                    Metrics = new NodeMetrics(
                        Tokens: 20,
                        NonEmptyLines: 10,
                        FileSizeBytes: 10,
                        DescendantFileCount: 1,
                        DescendantDirectoryCount: 0),
                },
                new ProjectNode
                {
                    Id = "Beta.cs",
                    Name = "Beta.cs",
                    FullPath = "C:\\Demo\\Beta.cs",
                    RelativePath = "Beta.cs",
                    Kind = ProjectNodeKind.File,
                    Metrics = new NodeMetrics(
                        Tokens: 10,
                        NonEmptyLines: 10,
                        FileSizeBytes: 20,
                        DescendantFileCount: 1,
                        DescendantDirectoryCount: 0),
                },
                new ProjectNode
                {
                    Id = "binary.dat",
                    Name = "binary.dat",
                    FullPath = "C:\\Demo\\binary.dat",
                    RelativePath = "binary.dat",
                    Kind = ProjectNodeKind.File,
                    SkippedReason = SkippedReason.Binary,
                    Metrics = new NodeMetrics(
                        Tokens: 0,
                        NonEmptyLines: 0,
                        FileSizeBytes: 10,
                        DescendantFileCount: 1,
                        DescendantDirectoryCount: 0),
                },
            },
        });

        viewModel.SortBy(ProjectTreeSortColumn.ParentShare, System.ComponentModel.ListSortDirection.Descending);

        Assert.Equal(ProjectTreeSortColumn.ParentShare, viewModel.CurrentSortColumn);
        Assert.Equal(System.ComponentModel.ListSortDirection.Descending, viewModel.CurrentSortDirection);
        Assert.Collection(
            viewModel.VisibleNodes.Select(node => node.Name),
            name => Assert.Equal("Demo", name),
            name => Assert.Equal("Alpha.cs", name),
            name => Assert.Equal("Beta.cs", name),
            name => Assert.Equal("binary.dat", name));
    }

    [Fact]
    public async Task MainWindowViewModel_SelectedMetric_UpdatesParentShareWithoutReanalysis()
    {
        var snapshot = new ProjectSnapshot
        {
            RootPath = "C:\\Demo",
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Options = ScanOptions.Default,
            Root = CreateRootWithChildren(
                ("Alpha.cs", 10, 20, 10),
                ("Beta.cs", 20, 10, 20)),
        };
        var analyzer = new CountingProjectAnalyzer(snapshot);
        var viewModel = CreateMainWindowViewModel(analyzer);

        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);

        var alphaByTokens = Assert.Single(viewModel.Tree.VisibleNodes, node => node.Name == "Alpha.cs");
        Assert.Equal("66.7%", alphaByTokens.ParentShareText);
        Assert.Equal(1, analyzer.CallCount);

        viewModel.Toolbar.IsSizeMetricSelected = true;

        var alphaBySize = Assert.Single(viewModel.Tree.VisibleNodes, node => node.Name == "Alpha.cs");
        Assert.Equal("33.3%", alphaBySize.ParentShareText);
        Assert.Equal(1, analyzer.CallCount);
    }

    [AvaloniaFact]
    public void ProjectTreeViewModel_ToggleNodeCommand_ShowsAndHidesChildrenInVisibleRows()
    {
        var viewModel = new ProjectTreeViewModel();
        var root = CreateNestedSnapshot().Root;
        viewModel.LoadRoot(root);

        var directoryNode = Assert.Single(viewModel.VisibleNodes, node => node.Node.Id == "src");

        Assert.Equal(2, viewModel.VisibleNodes.Count);

        viewModel.ToggleNodeCommand.Execute(directoryNode);

        Assert.Equal(3, viewModel.VisibleNodes.Count);
        Assert.Contains(viewModel.VisibleNodes, node => node.RelativePath == "src/Program.cs");

        viewModel.ToggleNodeCommand.Execute(directoryNode);

        Assert.Equal(2, viewModel.VisibleNodes.Count);
        Assert.DoesNotContain(viewModel.VisibleNodes, node => node.RelativePath == "src/Program.cs");
    }

    [AvaloniaFact]
    public void ProjectTreeViewModel_MoveSelectionRight_ExpandsSelectedDirectory()
    {
        var viewModel = new ProjectTreeViewModel();
        viewModel.LoadRoot(CreateNestedSnapshot().Root);
        viewModel.SelectNodeById("src");

        var changed = viewModel.MoveSelectionRight();

        Assert.True(changed);
        Assert.Equal("src", viewModel.SelectedNode?.Node.Id);
        Assert.Contains(viewModel.VisibleNodes, node => node.RelativePath == "src/Program.cs");
    }

    [AvaloniaFact]
    public void ProjectTreeViewModel_MoveSelectionRight_OnExpandedDirectory_SelectsFirstChild()
    {
        var viewModel = new ProjectTreeViewModel();
        viewModel.LoadRoot(CreateNestedSnapshot().Root);
        viewModel.SelectNodeById("src");
        viewModel.MoveSelectionRight();

        var changed = viewModel.MoveSelectionRight();

        Assert.True(changed);
        Assert.Equal("src/Program.cs", viewModel.SelectedNode?.Node.Id);
    }

    [AvaloniaFact]
    public void ProjectTreeViewModel_MoveSelectionLeft_CollapsesExpandedDirectory()
    {
        var viewModel = new ProjectTreeViewModel();
        viewModel.LoadRoot(CreateNestedSnapshot().Root);
        viewModel.SelectNodeById("src");
        viewModel.MoveSelectionRight();

        var changed = viewModel.MoveSelectionLeft();

        Assert.True(changed);
        Assert.Equal("src", viewModel.SelectedNode?.Node.Id);
        Assert.DoesNotContain(viewModel.VisibleNodes, node => node.RelativePath == "src/Program.cs");
    }

    [AvaloniaFact]
    public void ProjectTreeViewModel_MoveSelectionLeft_OnCollapsedDirectory_SelectsParent()
    {
        var viewModel = new ProjectTreeViewModel();
        viewModel.LoadRoot(CreateNestedSnapshot().Root);
        viewModel.SelectNodeById("src");

        var changed = viewModel.MoveSelectionLeft();

        Assert.True(changed);
        Assert.Equal("/", viewModel.SelectedNode?.Node.Id);
        Assert.DoesNotContain(viewModel.VisibleNodes, node => node.RelativePath == "src/Program.cs");
    }

    [AvaloniaFact]
    public void ProjectTreeViewModel_MoveSelectionLeft_OnFile_SelectsParent()
    {
        var viewModel = new ProjectTreeViewModel();
        viewModel.LoadRoot(CreateNestedSnapshot().Root);
        viewModel.SelectNodeById("src/Program.cs");

        var changed = viewModel.MoveSelectionLeft();

        Assert.True(changed);
        Assert.Equal("src", viewModel.SelectedNode?.Node.Id);
    }

    [AvaloniaFact]
    public void ProjectTreeViewModel_MoveSelectionRight_OnFile_SelectsNextVisibleNode()
    {
        var viewModel = new ProjectTreeViewModel();
        viewModel.LoadRoot(CreateRootWithChildren(
            ("Alpha.cs", 20, 20, 1),
            ("Beta.cs", 10, 10, 1)));
        viewModel.SelectNodeById("Alpha.cs");

        var changed = viewModel.MoveSelectionRight();

        Assert.True(changed);
        Assert.Equal("Beta.cs", viewModel.SelectedNode?.Node.Id);
    }

    [AvaloniaFact]
    public void ProjectTreePaneView_KeyDownHandler_RespondsEvenWhenDataGridAlreadyHandledArrowKey()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.Tree.LoadRoot(CreateNestedSnapshot().Root);
        viewModel.Tree.SelectNodeById("src");

        var window = new Window
        {
            Content = new ProjectTreePaneView
            {
                DataContext = viewModel,
            },
        };

        window.Show();

        var treeTable = FindNamedDescendant<DataGrid>(window, "ProjectTreeTable");
        Assert.NotNull(treeTable);

        var expandArgs = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Source = treeTable,
            Key = Key.Right,
            Handled = true,
        };

        treeTable.RaiseEvent(expandArgs);

        Assert.Equal("src", viewModel.Tree.SelectedNode?.Node.Id);
        Assert.Contains(viewModel.Tree.VisibleNodes, node => node.RelativePath == "src/Program.cs");

        var selectChildArgs = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Source = treeTable,
            Key = Key.Right,
            Handled = true,
        };

        treeTable.RaiseEvent(selectChildArgs);

        Assert.Equal("src/Program.cs", viewModel.Tree.SelectedNode?.Node.Id);

        viewModel.Tree.SelectNodeById("src");

        var collapseArgs = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Source = treeTable,
            Key = Key.Left,
            Handled = true,
        };

        treeTable.RaiseEvent(collapseArgs);

        Assert.Equal("src", viewModel.Tree.SelectedNode?.Node.Id);
        Assert.DoesNotContain(viewModel.Tree.VisibleNodes, node => node.RelativePath == "src/Program.cs");

        var selectParentArgs = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Source = treeTable,
            Key = Key.Left,
            Handled = true,
        };

        treeTable.RaiseEvent(selectParentArgs);

        Assert.Equal("/", viewModel.Tree.SelectedNode?.Node.Id);

        viewModel.Tree.SelectNodeById("src/Program.cs");

        var fileToParentArgs = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Source = treeTable,
            Key = Key.Left,
            Handled = true,
        };

        treeTable.RaiseEvent(fileToParentArgs);

        Assert.Equal("src", viewModel.Tree.SelectedNode?.Node.Id);
    }

    [AvaloniaFact]
    public void ProjectTreePaneView_SelectionChanged_UpdatesViewModelSelection()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.Tree.LoadRoot(CreateRootWithChildren(
            ("Alpha.cs", 10, 10, 1),
            ("Beta.cs", 20, 20, 1)));

        var window = new Window
        {
            Content = new ProjectTreePaneView
            {
                DataContext = viewModel,
            },
        };

        window.Show();

        var treeTable = FindNamedDescendant<DataGrid>(window, "ProjectTreeTable");
        var targetNode = Assert.Single(viewModel.Tree.VisibleNodes, node => node.Node.Id == "Beta.cs");

        Assert.NotNull(treeTable);

        treeTable.SelectedItem = targetNode;
        treeTable.RaiseEvent(new SelectionChangedEventArgs(
            SelectingItemsControl.SelectionChangedEvent,
            Array.Empty<object>(),
            new object[] { targetNode }));

        Assert.Equal("Beta.cs", viewModel.SelectedNode?.Id);
    }

    [AvaloniaFact]
    public void ProjectTreeViewModel_SelectNodeById_DoesNotRebuildVisibleRowsWhenTargetIsAlreadyVisible()
    {
        var viewModel = new ProjectTreeViewModel();
        var root = CreateRootWithChildren(
            ("Alpha.cs", 10, 10, 1),
            ("Beta.cs", 20, 20, 1));
        viewModel.LoadRoot(root);

        var resetCount = 0;
        viewModel.VisibleNodes.CollectionChanged += (_, args) =>
        {
            if (args.Action == NotifyCollectionChangedAction.Reset)
            {
                resetCount++;
            }
        };

        viewModel.SelectNodeById("Beta.cs");

        Assert.Equal(0, resetCount);
        Assert.Equal("Beta.cs", viewModel.SelectedNode?.Node.Id);
    }

    [AvaloniaFact]
    public void ProjectTreeViewModel_SelectNodeById_RebuildsVisibleRowsWhenAncestorExpansionIsNeeded()
    {
        var viewModel = new ProjectTreeViewModel();
        var root = CreateNestedSnapshot().Root;
        viewModel.LoadRoot(root);

        var resetCount = 0;
        viewModel.VisibleNodes.CollectionChanged += (_, args) =>
        {
            if (args.Action == NotifyCollectionChangedAction.Reset)
            {
                resetCount++;
            }
        };

        viewModel.SelectNodeById("src/Program.cs");

        Assert.True(resetCount > 0);
        Assert.Equal("src/Program.cs", viewModel.SelectedNode?.Node.Id);
        Assert.Contains(viewModel.VisibleNodes, node => node.RelativePath == "src/Program.cs");
    }

    [AvaloniaFact]
    public void ProjectTreeNodeViewModel_MapsBaselineIconsForFoldersAndFiles()
    {
        var folderNode = new ProjectTreeNodeViewModel(new ProjectNode
        {
            Id = "src",
            Name = "src",
            FullPath = "C:\\Demo\\src",
            RelativePath = "src",
            Kind = ProjectNodeKind.Directory,
            Metrics = NodeMetrics.Empty,
            Children =
            {
                new ProjectNode
                {
                    Id = "src/Program.cs",
                    Name = "Program.cs",
                    FullPath = "C:\\Demo\\src\\Program.cs",
                    RelativePath = "src/Program.cs",
                    Kind = ProjectNodeKind.File,
                    Metrics = NodeMetrics.Empty,
                },
            },
        });

        Assert.EndsWith("/Assets/FileIcons/folder-src.svg", folderNode.IconPath, StringComparison.Ordinal);
        Assert.True(folderNode.IsCollapsed);

        folderNode.IsExpanded = true;
        Assert.EndsWith("/Assets/FileIcons/folder-src-open.svg", folderNode.IconPath, StringComparison.Ordinal);
        Assert.False(folderNode.IsCollapsed);

        var csharpFileNode = new ProjectTreeNodeViewModel(new ProjectNode
        {
            Id = "Program.cs",
            Name = "Program.cs",
            FullPath = "C:\\Demo\\Program.cs",
            RelativePath = "Program.cs",
            Kind = ProjectNodeKind.File,
            Metrics = NodeMetrics.Empty,
        });
        Assert.EndsWith("/Assets/FileIcons/csharp.svg", csharpFileNode.IconPath, StringComparison.Ordinal);
        Assert.False(csharpFileNode.HasChildren);

        var tsxFileNode = new ProjectTreeNodeViewModel(new ProjectNode
        {
            Id = "Component.tsx",
            Name = "Component.tsx",
            FullPath = "C:\\Demo\\Component.tsx",
            RelativePath = "Component.tsx",
            Kind = ProjectNodeKind.File,
            Metrics = NodeMetrics.Empty,
        });
        Assert.EndsWith("/Assets/FileIcons/react_ts.svg", tsxFileNode.IconPath, StringComparison.Ordinal);

        var jsonFileNode = new ProjectTreeNodeViewModel(new ProjectNode
        {
            Id = "package-lock.json",
            Name = "package-lock.json",
            FullPath = "C:\\Demo\\package-lock.json",
            RelativePath = "package-lock.json",
            Kind = ProjectNodeKind.File,
            Metrics = NodeMetrics.Empty,
        });
        Assert.EndsWith("/Assets/FileIcons/json.svg", jsonFileNode.IconPath, StringComparison.Ordinal);

        var fallbackFileNode = new ProjectTreeNodeViewModel(new ProjectNode
        {
            Id = "README.unknown",
            Name = "README.unknown",
            FullPath = "C:\\Demo\\README.unknown",
            RelativePath = "README.unknown",
            Kind = ProjectNodeKind.File,
            Metrics = NodeMetrics.Empty,
        });
        Assert.EndsWith("/Assets/FileIcons/document.svg", fallbackFileNode.IconPath, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectTreeNodeViewModel_ShowsNaForSkippedAnalysisMetrics()
    {
        var parentNode = new ProjectNode
        {
            Id = "/",
            Name = "Demo",
            FullPath = "C:\\Demo",
            RelativePath = string.Empty,
            Kind = ProjectNodeKind.Root,
            Metrics = new NodeMetrics(
                Tokens: 100,
                NonEmptyLines: 50,
                FileSizeBytes: 171_801,
                DescendantFileCount: 1,
                DescendantDirectoryCount: 0),
        };
        var skippedFileNode = new ProjectTreeNodeViewModel(new ProjectNode
        {
            Id = "image.ico",
            Name = "image.ico",
            FullPath = "C:\\Demo\\image.ico",
            RelativePath = "image.ico",
            Kind = ProjectNodeKind.File,
            SkippedReason = SkippedReason.Binary,
            Metrics = new NodeMetrics(
                Tokens: 0,
                NonEmptyLines: 0,
                FileSizeBytes: 171_801,
                DescendantFileCount: 1,
                DescendantDirectoryCount: 0),
        },
        parentNode: parentNode,
        parentShareMetric: AnalysisMetric.Tokens);

        Assert.Equal("n/a", skippedFileNode.TokensText);
        Assert.Equal("n/a", skippedFileNode.LinesText);
        Assert.Equal("167.8 KB", skippedFileNode.SizeText);
        Assert.Equal("n/a", skippedFileNode.ParentShareText);
        Assert.Null(skippedFileNode.ParentShareRatio);
    }

    [Fact]
    public void ProjectTreeNodeViewModel_ParentShare_UsesImmediateParentMetric()
    {
        var rootNode = new ProjectNode
        {
            Id = "/",
            Name = "Demo",
            FullPath = "C:\\Demo",
            RelativePath = string.Empty,
            Kind = ProjectNodeKind.Root,
            Metrics = new NodeMetrics(
                Tokens: 30,
                NonEmptyLines: 12,
                FileSizeBytes: 90,
                DescendantFileCount: 1,
                DescendantDirectoryCount: 0),
        };
        var childNode = new ProjectNode
        {
            Id = "Alpha.cs",
            Name = "Alpha.cs",
            FullPath = "C:\\Demo\\Alpha.cs",
            RelativePath = "Alpha.cs",
            Kind = ProjectNodeKind.File,
            Metrics = new NodeMetrics(
                Tokens: 10,
                NonEmptyLines: 4,
                FileSizeBytes: 30,
                DescendantFileCount: 1,
                DescendantDirectoryCount: 0),
        };
        var rootViewModel = new ProjectTreeNodeViewModel(rootNode);
        var childViewModel = new ProjectTreeNodeViewModel(
            childNode,
            depth: 1,
            parentNode: rootNode,
            parentShareMetric: AnalysisMetric.Tokens);
        var zeroMetricParent = new ProjectNode
        {
            Id = "/zero",
            Name = "Zero",
            FullPath = "C:\\Zero",
            RelativePath = string.Empty,
            Kind = ProjectNodeKind.Root,
            Metrics = NodeMetrics.Empty,
        };
        var childWithZeroParent = new ProjectTreeNodeViewModel(
            childNode,
            parentNode: zeroMetricParent,
            parentShareMetric: AnalysisMetric.Tokens);

        Assert.Equal("100.0%", rootViewModel.ParentShareText);
        Assert.NotNull(childViewModel.ParentShareRatio);
        Assert.Equal(1d / 3d, childViewModel.ParentShareRatio.Value, 3);
        Assert.Equal("33.3%", childViewModel.ParentShareText);
        Assert.Equal(0, rootViewModel.IndentOffset);
        Assert.Equal(14, childViewModel.IndentOffset);
        Assert.Equal(104, rootViewModel.ParentShareBlockWidth);
        Assert.Equal(90, childViewModel.ParentShareBlockWidth);
        Assert.Equal(104, rootViewModel.ParentShareFillWidth);
        Assert.Equal(30, childViewModel.ParentShareFillWidth, 3);
        Assert.Equal(0, childWithZeroParent.ParentShareFillWidth);
        Assert.Equal("n/a", childWithZeroParent.ParentShareText);
        Assert.Null(childWithZeroParent.ParentShareRatio);
    }

    [AvaloniaFact]
    public async Task MainWindow_CancelCommand_ShowsProgressOnlyWhileScanIsRunning()
    {
        var window = new MainWindow();
        var viewModel = CreateMainWindowViewModel(new CancelAwareProjectAnalyzer());
        window.DataContext = viewModel;

        window.Show();
        var statusStrip = FindNamedDescendant<Control>(window, "StatusStrip");
        var stopButton = FindNamedDescendant<Button>(window, "StopButton");

        Assert.NotNull(statusStrip);
        Assert.NotNull(stopButton);
        Assert.False(statusStrip.IsVisible);
        Assert.False(stopButton.IsVisible);

        var openTask = viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);
        await Task.Delay(100);
        Assert.True(statusStrip.IsVisible);
        Assert.True(stopButton.IsVisible);
        viewModel.Toolbar.CancelCommand.Execute(null);
        await openTask;

        Assert.Equal(AnalysisState.Cancelled, viewModel.AnalysisState);
        Assert.False(statusStrip.IsVisible);
        Assert.False(stopButton.IsVisible);
    }

    private static T? FindDescendant<T>(Window window)
        where T : class
    {
        return window.GetLogicalDescendants().OfType<T>().FirstOrDefault()
            ?? window.GetVisualDescendants().OfType<T>().FirstOrDefault();
    }

    private static DataGridColumnHeader? FindProjectTreeHeader(Window window, string headerText)
    {
        return window.GetVisualDescendants()
            .OfType<DataGridColumnHeader>()
            .FirstOrDefault(header => string.Equals(header.Content?.ToString(), headerText, StringComparison.Ordinal));
    }

    private static T? FindHeaderElement<T>(DataGridColumnHeader? header, string name)
        where T : Control
    {
        return header?.GetVisualDescendants()
            .OfType<T>()
            .FirstOrDefault(control => string.Equals(control.Name, name, StringComparison.Ordinal));
    }

    private static ProjectNode CreateRootWithChildren(params (string Name, long FileSizeBytes, int Tokens, int NonEmptyLines)[] children)
    {
        var root = new ProjectNode
        {
            Id = "/",
            Name = "Demo",
            FullPath = "C:\\Demo",
            RelativePath = string.Empty,
            Kind = ProjectNodeKind.Root,
            Metrics = new NodeMetrics(
                Tokens: children.Sum(item => item.Tokens),
                NonEmptyLines: children.Sum(item => item.NonEmptyLines),
                FileSizeBytes: children.Sum(item => item.FileSizeBytes),
                DescendantFileCount: children.Length,
                DescendantDirectoryCount: 0),
        };

        foreach (var item in children)
        {
            root.Children.Add(new ProjectNode
            {
                Id = item.Name,
                Name = item.Name,
                FullPath = Path.Combine("C:\\Demo", item.Name),
                RelativePath = item.Name,
                Kind = ProjectNodeKind.File,
                Metrics = new NodeMetrics(
                    Tokens: item.Tokens,
                    NonEmptyLines: item.NonEmptyLines,
                    FileSizeBytes: item.FileSizeBytes,
                    DescendantFileCount: 1,
                    DescendantDirectoryCount: 0),
            });
        }

        return root;
    }

    private sealed class CancelAwareProjectAnalyzer : IProjectAnalyzer
    {
        public async Task<ProjectSnapshot> AnalyzeAsync(
            string rootPath,
            ScanOptions options,
            IProgress<AnalysisProgress>? progress,
            CancellationToken cancellationToken)
        {
            progress?.Report(new AnalysisProgress("ScanningTree", 1, 2, "Program.cs"));
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

            throw new InvalidOperationException("This path should have been cancelled.");
        }
    }

    private sealed class CountingProjectAnalyzer(params ProjectSnapshot[] snapshots) : IProjectAnalyzer
    {
        private readonly Queue<ProjectSnapshot> _snapshots = new(snapshots);

        public int CallCount { get; private set; }

        public Task<ProjectSnapshot> AnalyzeAsync(
            string rootPath,
            ScanOptions options,
            IProgress<AnalysisProgress>? progress,
            CancellationToken cancellationToken)
        {
            CallCount++;
            if (_snapshots.Count == 0)
            {
                throw new InvalidOperationException("No more snapshots configured.");
            }

            return Task.FromResult(_snapshots.Dequeue());
        }
    }
}
