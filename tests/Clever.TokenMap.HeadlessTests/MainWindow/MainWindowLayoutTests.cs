using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
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
using Clever.TokenMap.Infrastructure.Filtering;
using System.Collections.Specialized;
using System.Reflection;
using static Clever.TokenMap.HeadlessTests.HeadlessTestSupport;
using ShapePath = Avalonia.Controls.Shapes.Path;

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
        Assert.Equal("Treemap - tokens", treemapTitle.Text);
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
        var selectedFolderValue = FindNamedDescendant<TextBlock>(window, "SelectedFolderValueText");

        Assert.NotNull(primaryGroup);
        Assert.NotNull(summaryGroup);
        Assert.NotNull(settingsButton);
        Assert.NotNull(selectedFolderValue);
        Assert.True(
            primaryGroup.Bounds.Right <= summaryGroup.Bounds.Left,
            $"PrimaryGroup.Right={primaryGroup.Bounds.Right}, SummaryGroup.Left={summaryGroup.Bounds.Left}");
        Assert.True(
            summaryGroup.Bounds.Right <= settingsButton.Bounds.Left,
            $"SummaryGroup.Right={summaryGroup.Bounds.Right}, SettingsButton.Left={settingsButton.Bounds.Left}");
        Assert.Equal(Avalonia.Media.TextTrimming.CharacterEllipsis, selectedFolderValue.TextTrimming);
    }

    [AvaloniaFact]
    public void MainWindow_ShowsRecentFoldersEmptyState_WhenNoRecentFoldersAreLoaded()
    {
        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel(),
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
        Assert.True(startSurface.IsVisible);
        Assert.True(emptyState.IsVisible);
        Assert.True(workspaceHost.IsVisible);
        Assert.Equal(0, recentFoldersItems.ItemCount);
    }

    [AvaloniaFact]
    public void MainWindow_ShowsRecentFoldersStartSurface_WhenRecentFoldersAreLoaded()
    {
        var window = new MainWindow
        {
            DataContext = CreateMainWindowViewModel(
                new StubProjectAnalyzer(CreateSnapshot()),
                selectedFolderPath: "C:\\Demo",
                recentFolderPaths:
                [
                    "C:\\RepoA",
                    "C:\\RepoB",
                ]),
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

        Assert.Single(viewModel.RecentFolderFlyoutItems);
        Assert.False(viewModel.RecentFolderFlyoutItems[0].CanOpen);
        Assert.Equal("No previous folders yet", viewModel.RecentFolderFlyoutItems[0].DisplayName);
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

        var folderToRemove = Assert.Single(viewModel.RecentFolders, folder => folder.DisplayName == "RepoB");

        viewModel.RemoveRecentFolderCommand.Execute(folderToRemove);

        Assert.Single(viewModel.RecentFolders);
        Assert.Equal("RepoA", viewModel.RecentFolders[0].DisplayName);
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

        viewModel.ClearRecentFoldersCommand.Execute(null);

        Assert.Empty(viewModel.RecentFolders);
        Assert.Single(viewModel.RecentFolderFlyoutItems);
        Assert.Equal("No previous folders yet", viewModel.RecentFolderFlyoutItems[0].DisplayName);
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
            header => Assert.Equal("Tokens", header),
            header => Assert.Equal("Lines", header),
            header => Assert.Equal("Size", header),
            header => Assert.Equal("Files", header));

        Assert.IsType<DataGridTemplateColumn>(treeTable.Columns[0]);
        Assert.IsType<DataGridTemplateColumn>(treeTable.Columns[1]);
        Assert.IsType<DataGridTemplateColumn>(treeTable.Columns[2]);
        Assert.IsType<DataGridTemplateColumn>(treeTable.Columns[3]);
        Assert.IsType<DataGridTemplateColumn>(treeTable.Columns[4]);
    }

    [AvaloniaFact]
    public void MainWindow_ProjectTreeTable_FirstNumericSort_UsesDescendingOrder()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.Tree.LoadRoot(CreateRootWithChildren(
            ("Small.cs", 10, 20, 10),
            ("Large.cs", 20, 10, 20)));

        var window = new MainWindow
        {
            DataContext = viewModel,
        };

        window.Show();

        var treeTable = FindNamedDescendant<DataGrid>(window, "ProjectTreeTable");
        var projectTreePane = FindDescendant<ProjectTreePaneView>(window);

        Assert.NotNull(treeTable);
        Assert.NotNull(projectTreePane);

        var linesColumn = Assert.Single(treeTable.Columns, column => column.SortMemberPath == "Lines");

        InvokeProjectTreeSort(projectTreePane, viewModel, treeTable, linesColumn, ProjectTreeSortColumn.Lines);

        Assert.Equal(ProjectTreeSortColumn.Lines, viewModel.Tree.CurrentSortColumn);
        Assert.Equal(System.ComponentModel.ListSortDirection.Descending, viewModel.Tree.CurrentSortDirection);
        Assert.Collection(
            viewModel.Tree.VisibleNodes.Select(node => node.Name),
            name => Assert.Equal("Demo", name),
            name => Assert.Equal("Large.cs", name),
            name => Assert.Equal("Small.cs", name));
        window.UpdateLayout();
        var linesHeader = FindProjectTreeHeader(window, "Lines");
        var tokensHeader = FindProjectTreeHeader(window, "Tokens");
        var linesDescendingIcon = FindHeaderElement<ShapePath>(linesHeader, "SortIconDescending");
        var linesAscendingIcon = FindHeaderElement<ShapePath>(linesHeader, "SortIconAscending");
        var tokensDescendingIcon = FindHeaderElement<ShapePath>(tokensHeader, "SortIconDescending");

        Assert.Equal("Lines", linesColumn.Header?.ToString());
        Assert.Equal("Tokens", treeTable.Columns[1].Header?.ToString());
        Assert.NotNull(linesHeader);
        Assert.NotNull(tokensHeader);
        Assert.NotNull(linesDescendingIcon);
        Assert.True(linesDescendingIcon.IsVisible);
        Assert.Null(linesAscendingIcon);
        Assert.Null(tokensDescendingIcon);
    }

    [AvaloniaFact]
    public void MainWindow_ProjectTreeTable_FirstNameSort_UsesAscendingOrder()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.Tree.LoadRoot(CreateRootWithChildren(
            ("Alpha.cs", 10, 1, 1),
            ("Zulu.cs", 20, 2, 2)));

        var window = new MainWindow
        {
            DataContext = viewModel,
        };

        window.Show();

        var treeTable = FindNamedDescendant<DataGrid>(window, "ProjectTreeTable");
        var projectTreePane = FindDescendant<ProjectTreePaneView>(window);

        Assert.NotNull(treeTable);
        Assert.NotNull(projectTreePane);

        var nameColumn = Assert.Single(treeTable.Columns, column => column.SortMemberPath == "Name");

        InvokeProjectTreeSort(projectTreePane, viewModel, treeTable, nameColumn, ProjectTreeSortColumn.Name);

        Assert.Equal(ProjectTreeSortColumn.Name, viewModel.Tree.CurrentSortColumn);
        Assert.Equal(System.ComponentModel.ListSortDirection.Ascending, viewModel.Tree.CurrentSortDirection);
        Assert.Collection(
            viewModel.Tree.VisibleNodes.Select(node => node.Name),
            name => Assert.Equal("Demo", name),
            name => Assert.Equal("Alpha.cs", name),
            name => Assert.Equal("Zulu.cs", name));
        window.UpdateLayout();
        var nameHeader = FindProjectTreeHeader(window, "Name");
        var tokensHeader = FindProjectTreeHeader(window, "Tokens");
        var nameAscendingIcon = FindHeaderElement<ShapePath>(nameHeader, "SortIconAscending");
        var nameDescendingIcon = FindHeaderElement<ShapePath>(nameHeader, "SortIconDescending");
        var tokensDescendingIcon = FindHeaderElement<ShapePath>(tokensHeader, "SortIconDescending");

        Assert.Equal("Name", nameColumn.Header?.ToString());
        Assert.Equal("Tokens", treeTable.Columns[1].Header?.ToString());
        Assert.NotNull(nameHeader);
        Assert.NotNull(tokensHeader);
        Assert.NotNull(nameAscendingIcon);
        Assert.True(nameAscendingIcon.IsVisible);
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
        var filesHeader = headers.First(header => string.Equals(header.Content?.ToString(), "Files", StringComparison.Ordinal));

        var sizeContentPresenter = sizeHeader.GetVisualDescendants()
            .OfType<ContentPresenter>()
            .First(control => string.Equals(control.Name, "PART_ContentPresenter", StringComparison.Ordinal));
        var filesContentPresenter = filesHeader.GetVisualDescendants()
            .OfType<ContentPresenter>()
            .First(control => string.Equals(control.Name, "PART_ContentPresenter", StringComparison.Ordinal));

        Assert.True(
            sizeContentPresenter.Bounds.Width >= 70,
            $"Size header content width should use the available column width. Actual={sizeContentPresenter.Bounds.Width}.");
        Assert.True(
            filesContentPresenter.Bounds.Width >= 44,
            $"Files header content width should use the available column width. Actual={filesContentPresenter.Bounds.Width}.");
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
        var defaultExcludesCheckBox = FindNamedDescendant<CheckBox>(window, "UseDefaultExcludesCheckBox");
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
        Assert.NotNull(defaultExcludesCheckBox);
        Assert.NotNull(rescanButton);
        Assert.NotNull(selectedFolderGroup);
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
        Assert.True(defaultExcludesCheckBox.IsEnabled);
        Assert.False(rescanButton.IsVisible);
        Assert.False(selectedFolderGroup.IsVisible);
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
        var startSurface = FindNamedDescendant<Control>(window, "RecentFoldersStartSurface");
        var viewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);

        Assert.NotNull(drawer);
        Assert.NotNull(drawerHost);
        Assert.NotNull(startSurface);
        Assert.False(drawer.IsVisible);
        Assert.True(drawerHost.ZIndex > startSurface.ZIndex);

        viewModel.ToggleSettingsCommand.Execute(null);
        Assert.True(drawer.IsVisible);

        viewModel.ToggleSettingsCommand.Execute(null);
        Assert.False(drawer.IsVisible);
    }

    [AvaloniaFact]
    public void MainWindow_SettingsDrawer_DefaultExcludesDetails_ShowCanonicalListWhenExpanded()
    {
        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel(),
        };

        window.Show();

        var viewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);
        viewModel.ToggleSettingsCommand.Execute(null);
        window.UpdateLayout();

        var detailsButton = FindNamedDescendant<Button>(window, "DefaultExcludesDetailsButton");
        var detailsContainer = FindNamedDescendant<Control>(window, "DefaultExcludesDetailsContainer");
        var detailsTextBlock = FindNamedDescendant<TextBlock>(window, "DefaultExcludesTextBlock");
        var detailsScrollViewer = FindNamedDescendant<ScrollViewer>(window, "DefaultExcludesScrollViewer");

        Assert.NotNull(detailsButton);
        Assert.NotNull(detailsContainer);
        Assert.NotNull(detailsTextBlock);
        Assert.NotNull(detailsScrollViewer);
        Assert.False(detailsContainer.IsVisible);
        Assert.Equal("View defaults", detailsButton.Content?.ToString());

        viewModel.Toolbar.ToggleDefaultExcludesDetailsCommand.Execute(null);
        window.UpdateLayout();

        var expectedText = string.Join(
            Environment.NewLine,
            DefaultExcludeMatcher.DefaultDirectoryNames);

        Assert.True(detailsContainer.IsVisible);
        Assert.Equal("Hide defaults", detailsButton.Content?.ToString());
        Assert.Equal(expectedText.ReplaceLineEndings("\n"), detailsTextBlock.Text?.ReplaceLineEndings("\n"));
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

        Assert.NotNull(treeTable);
        Assert.NotNull(statusStrip);
        Assert.NotNull(startSurface);
        Assert.NotNull(workspaceHost);
        Assert.NotNull(rescanButton);
        Assert.NotNull(selectedFolderGroup);
        Assert.NotNull(summaryGroup);
        Assert.Single(viewModel.Tree.RootNodes);
        Assert.Equal(2, viewModel.Tree.VisibleNodes.Count);
        Assert.Equal(AnalysisState.Completed, viewModel.AnalysisState);
        Assert.Equal("42", tokenSummaryText?.Text);
        Assert.Equal("11", lineSummaryText?.Text);
        Assert.Equal("1", fileSummaryText?.Text);
        Assert.Null(warningSummaryText);
        Assert.NotNull(metricTokensRadioButton);
        Assert.NotNull(metricLinesRadioButton);
        Assert.NotNull(metricSizeRadioButton);
        Assert.True(metricTokensRadioButton.IsEnabled);
        Assert.True(metricLinesRadioButton.IsEnabled);
        Assert.True(metricSizeRadioButton.IsEnabled);
        Assert.True(metricTokensRadioButton.IsChecked);
        Assert.False(metricLinesRadioButton.IsChecked);
        Assert.False(metricSizeRadioButton.IsChecked);
        Assert.False(statusStrip.IsVisible);
        Assert.False(startSurface.IsVisible);
        Assert.True(workspaceHost.IsVisible);
        Assert.True(rescanButton.IsVisible);
        Assert.True(selectedFolderGroup.IsVisible);
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
                TotalLines: 3,
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
                        TotalLines: 1,
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
                        TotalLines: 2,
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
    public async Task ProjectTreePaneView_SingleClickSelectionSync_IsDelayedUntilDoubleClickWindowExpires()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.Tree.LoadRoot(CreateRootWithChildren(
            ("Alpha.cs", 10, 10, 1),
            ("Beta.cs", 20, 20, 1)));

        var projectTreePane = new ProjectTreePaneView
        {
            DataContext = viewModel,
        };
        var targetNode = Assert.Single(viewModel.Tree.VisibleNodes, node => node.Node.Id == "Alpha.cs");
        var scheduleMethod = typeof(ProjectTreePaneView).GetMethod(
            "ScheduleProjectTreeSelectionSync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(scheduleMethod);

        scheduleMethod.Invoke(projectTreePane, [viewModel, targetNode]);

        Assert.Equal("/", viewModel.SelectedNode?.Id);

        await Task.Delay(350);

        Assert.Equal("Alpha.cs", viewModel.SelectedNode?.Id);
    }

    [AvaloniaFact]
    public async Task ProjectTreePaneView_DoubleTap_CancelsPendingSingleClickSelectionSync()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.Tree.LoadRoot(CreateRootWithChildren(
            ("Alpha.cs", 10, 10, 1),
            ("Beta.cs", 20, 20, 1)));

        var projectTreePane = new ProjectTreePaneView
        {
            DataContext = viewModel,
        };
        var scheduledNode = Assert.Single(viewModel.Tree.VisibleNodes, node => node.Node.Id == "Alpha.cs");
        var doubleTappedNode = Assert.Single(viewModel.Tree.VisibleNodes, node => node.Node.Id == "Beta.cs");
        var scheduleMethod = typeof(ProjectTreePaneView).GetMethod(
            "ScheduleProjectTreeSelectionSync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var doubleTapMethod = typeof(ProjectTreePaneView).GetMethod(
            "HandleProjectTreeRowDoubleTap",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(scheduleMethod);
        Assert.NotNull(doubleTapMethod);

        scheduleMethod.Invoke(projectTreePane, [viewModel, scheduledNode]);
        doubleTapMethod.Invoke(projectTreePane, [viewModel, doubleTappedNode]);

        await Task.Delay(350);

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
                TotalLines: 0,
                FileSizeBytes: 171_801,
                DescendantFileCount: 1,
                DescendantDirectoryCount: 0),
        });

        Assert.Equal("n/a", skippedFileNode.TokensText);
        Assert.Equal("n/a", skippedFileNode.LinesText);
        Assert.Equal("167.8 KB", skippedFileNode.SizeText);
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

    private static void InvokeProjectTreeSort(
        ProjectTreePaneView projectTreePane,
        MainWindowViewModel viewModel,
        DataGrid treeTable,
        DataGridColumn clickedColumn,
        ProjectTreeSortColumn sortColumn)
    {
        var method = typeof(ProjectTreePaneView).GetMethod(
            "ApplyProjectTreeSort",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        method.Invoke(projectTreePane, [viewModel, treeTable, clickedColumn, sortColumn]);
    }

    private static ProjectNode CreateRootWithChildren(params (string Name, long FileSizeBytes, int Tokens, int TotalLines)[] children)
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
                TotalLines: children.Sum(item => item.TotalLines),
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
                    TotalLines: item.TotalLines,
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
}

