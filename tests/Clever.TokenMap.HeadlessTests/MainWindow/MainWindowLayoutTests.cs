using Avalonia.Controls;
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
using System.Collections.Specialized;
using System.Reflection;
using static Clever.TokenMap.HeadlessTests.HeadlessTestSupport;

namespace Clever.TokenMap.HeadlessTests;

public sealed class MainWindowLayoutTests
{
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
        Assert.NotNull(FindNamedDescendant<DataGrid>(window, "ProjectTreeTable"));
        Assert.NotNull(FindNamedDescendant<ItemsControl>(window, "TreemapBreadcrumbsItemsControl"));
        Assert.Null(FindNamedDescendant<Control>(window, "DetailsPane"));
        Assert.NotNull(FindNamedDescendant<Button>(window, "SettingsButton"));
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
    public void MainWindow_ToolbarGroups_DoNotOverlapAtMinimumWidth()
    {
        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel(),
            Width = 1100,
        };

        window.Show();

        var primaryGroup = FindNamedDescendant<Control>(window, "ToolbarPrimaryGroup");
        var summaryGroup = FindNamedDescendant<Control>(window, "ToolbarSummaryGroup");
        var settingsButton = FindNamedDescendant<Button>(window, "SettingsButton");
        var selectedFolderValue = FindNamedDescendant<TextBlock>(window, "SelectedFolderValueText");

        Assert.NotNull(primaryGroup);
        Assert.NotNull(summaryGroup);
        Assert.NotNull(settingsButton);
        Assert.NotNull(selectedFolderValue);
        Assert.True(primaryGroup.Bounds.Right <= summaryGroup.Bounds.Left);
        Assert.True(summaryGroup.Bounds.Right <= settingsButton.Bounds.Left);
        Assert.Equal(Avalonia.Media.TextTrimming.CharacterEllipsis, selectedFolderValue.TextTrimming);
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
            header => Assert.Equal("Tokens v", header),
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
        Assert.Equal("Lines v", linesColumn.Header?.ToString());
        Assert.Equal("Tokens", treeTable.Columns[1].Header?.ToString());
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
        Assert.Equal("Name ^", nameColumn.Header?.ToString());
        Assert.Equal("Tokens", treeTable.Columns[1].Header?.ToString());
    }

    [AvaloniaFact]
    public void MainWindow_InitialToolbarAvailability_EnablesScanSettingsButDisablesMetric()
    {
        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel(),
        };

        window.Show();

        var metricComboBox = FindNamedDescendant<ComboBox>(window, "MetricComboBox");
        var themeSystemButton = FindNamedDescendant<ToggleButton>(window, "ThemeSystemButton");
        var themeLightButton = FindNamedDescendant<ToggleButton>(window, "ThemeLightButton");
        var themeDarkButton = FindNamedDescendant<ToggleButton>(window, "ThemeDarkButton");
        var tokenizerComboBox = FindNamedDescendant<ComboBox>(window, "TokenizerComboBox");
        var gitIgnoreCheckBox = FindNamedDescendant<CheckBox>(window, "RespectGitIgnoreCheckBox");
        var ignoreCheckBox = FindNamedDescendant<CheckBox>(window, "RespectIgnoreCheckBox");
        var defaultExcludesCheckBox = FindNamedDescendant<CheckBox>(window, "UseDefaultExcludesCheckBox");

        Assert.NotNull(metricComboBox);
        Assert.NotNull(themeSystemButton);
        Assert.NotNull(themeLightButton);
        Assert.NotNull(themeDarkButton);
        Assert.NotNull(tokenizerComboBox);
        Assert.NotNull(gitIgnoreCheckBox);
        Assert.NotNull(ignoreCheckBox);
        Assert.NotNull(defaultExcludesCheckBox);
        Assert.False(metricComboBox.IsEnabled);
        Assert.True(themeSystemButton.IsEnabled);
        Assert.True(themeLightButton.IsEnabled);
        Assert.True(themeDarkButton.IsEnabled);
        Assert.True(themeSystemButton.IsChecked);
        Assert.False(themeLightButton.IsChecked);
        Assert.False(themeDarkButton.IsChecked);
        Assert.True(tokenizerComboBox.IsEnabled);
        Assert.True(gitIgnoreCheckBox.IsEnabled);
        Assert.True(ignoreCheckBox.IsEnabled);
        Assert.True(defaultExcludesCheckBox.IsEnabled);
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
        var viewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);

        Assert.NotNull(drawer);
        Assert.False(drawer.IsVisible);

        viewModel.ToggleSettingsCommand.Execute(null);
        Assert.True(drawer.IsVisible);

        viewModel.ToggleSettingsCommand.Execute(null);
        Assert.False(drawer.IsVisible);
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
        var metricComboBox = FindNamedDescendant<ComboBox>(window, "MetricComboBox");
        var tokenizerComboBox = FindNamedDescendant<ComboBox>(window, "TokenizerComboBox");

        Assert.NotNull(treeTable);
        Assert.NotNull(statusStrip);
        Assert.Single(viewModel.Tree.RootNodes);
        Assert.Equal(2, viewModel.Tree.VisibleNodes.Count);
        Assert.Equal(AnalysisState.Completed, viewModel.AnalysisState);
        Assert.Equal("42", tokenSummaryText?.Text);
        Assert.Equal("12", lineSummaryText?.Text);
        Assert.Equal("1", fileSummaryText?.Text);
        Assert.Null(warningSummaryText);
        Assert.True(metricComboBox?.IsEnabled);
        Assert.True(tokenizerComboBox?.IsEnabled);
        Assert.False(statusStrip.IsVisible);
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
                NonEmptyLines: 3,
                BlankLines: 0,
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
                        NonEmptyLines: 1,
                        BlankLines: 0,
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
                        NonEmptyLines: 2,
                        BlankLines: 0,
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
                NonEmptyLines: children.Sum(item => item.TotalLines),
                BlankLines: 0,
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
                    NonEmptyLines: item.TotalLines,
                    BlankLines: 0,
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

