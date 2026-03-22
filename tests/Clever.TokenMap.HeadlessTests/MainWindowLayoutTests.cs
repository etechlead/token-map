using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Clever.TokenMap.App.Models;
using Clever.TokenMap.Controls;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.App.Views;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;

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

        Assert.NotNull(window.FindControl<Control>("ToolbarHost"));
        Assert.NotNull(window.FindControl<Grid>("WorkspaceHost"));
        Assert.NotNull(window.FindControl<Control>("ProjectTreePane"));
        Assert.NotNull(window.FindControl<Control>("TreemapPane"));
        var statusStrip = window.FindControl<Control>("StatusStrip");

        Assert.NotNull(statusStrip);
        Assert.NotNull(window.FindControl<TreemapControl>("ProjectTreemapControl"));
        Assert.NotNull(window.FindControl<DataGrid>("ProjectTreeTable"));
        Assert.Null(window.FindControl<Control>("DetailsPane"));
        Assert.NotNull(window.FindControl<ProgressBar>("StatusProgressBar"));
        Assert.Null(window.FindControl<TextBlock>("ProgressTextBlock"));
        Assert.Null(window.FindControl<TextBlock>("StatusValueText"));
        Assert.False(statusStrip.IsVisible);
    }

    [AvaloniaFact]
    public void MainWindow_WorkspaceHost_UsesFortySixtySplit()
    {
        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel(),
        };

        window.Show();

        var workspaceHost = window.FindControl<Grid>("WorkspaceHost");

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
    public void MainWindow_ProjectTreeTable_UsesRequestedColumns()
    {
        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel(),
        };

        window.Show();

        var treeTable = window.FindControl<DataGrid>("ProjectTreeTable");

        Assert.NotNull(treeTable);
        Assert.Collection(
            treeTable.Columns.Select(column => column.Header?.ToString()),
            header => Assert.Equal("Name", header),
            header => Assert.Equal("Size v", header),
            header => Assert.Equal("Lines", header),
            header => Assert.Equal("Tokens", header),
            header => Assert.Equal("Files", header));
    }

    [AvaloniaFact]
    public void MainWindow_InitialToolbarAvailability_EnablesScanSettingsButDisablesMetric()
    {
        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel(),
        };

        window.Show();

        var metricComboBox = window.FindControl<ComboBox>("MetricComboBox");
        var tokenizerComboBox = window.FindControl<ComboBox>("TokenizerComboBox");
        var gitIgnoreCheckBox = window.FindControl<CheckBox>("RespectGitIgnoreCheckBox");
        var ignoreCheckBox = window.FindControl<CheckBox>("RespectIgnoreCheckBox");
        var defaultExcludesCheckBox = window.FindControl<CheckBox>("UseDefaultExcludesCheckBox");

        Assert.NotNull(metricComboBox);
        Assert.NotNull(tokenizerComboBox);
        Assert.NotNull(gitIgnoreCheckBox);
        Assert.NotNull(ignoreCheckBox);
        Assert.NotNull(defaultExcludesCheckBox);
        Assert.False(metricComboBox.IsEnabled);
        Assert.True(tokenizerComboBox.IsEnabled);
        Assert.True(gitIgnoreCheckBox.IsEnabled);
        Assert.True(ignoreCheckBox.IsEnabled);
        Assert.True(defaultExcludesCheckBox.IsEnabled);
    }

    [AvaloniaFact]
    public async Task MainWindow_OpenFolderFlow_PopulatesTreeAndSummary()
    {
        var window = new MainWindow();
        var viewModel = new MainWindowViewModel(
            new StubProjectAnalyzer(CreateSnapshot()),
            new StubFolderPickerService("C:\\Demo"));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);

        var treeTable = window.FindControl<DataGrid>("ProjectTreeTable");
        var statusStrip = window.FindControl<Control>("StatusStrip");
        var tokenSummaryText = window.FindControl<TextBlock>("TokenSummaryValueText");
        var lineSummaryText = window.FindControl<TextBlock>("LineSummaryValueText");
        var fileSummaryText = window.FindControl<TextBlock>("FileSummaryValueText");
        var warningSummaryText = window.FindControl<TextBlock>("WarningSummaryValueText");
        var metricComboBox = window.FindControl<ComboBox>("MetricComboBox");
        var tokenizerComboBox = window.FindControl<ComboBox>("TokenizerComboBox");

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

        viewModel.SetState(AnalysisState.Scanning, "Analyzing C:\\Demo");
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
    public void ProjectTreeViewModel_LoadRoot_DefaultsToSizeDescendingSort()
    {
        var viewModel = new ProjectTreeViewModel();
        var root = CreateRootWithChildren(
            ("Small.cs", 10, 1, 1),
            ("Large.cs", 20, 2, 1));

        viewModel.LoadRoot(root);

        Assert.Equal(ProjectTreeSortColumn.Size, viewModel.CurrentSortColumn);
        Assert.Equal(System.ComponentModel.ListSortDirection.Descending, viewModel.CurrentSortDirection);
        Assert.Collection(
            viewModel.VisibleNodes.Select(node => node.Name),
            name => Assert.Equal("Demo", name),
            name => Assert.Equal("Large.cs", name),
            name => Assert.Equal("Small.cs", name));
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
        var root = new ProjectTreeNodeViewModel(
            new ProjectNode
            {
                Id = "/",
                Name = "Demo",
                FullPath = "C:\\Demo",
                RelativePath = string.Empty,
                Kind = ProjectNodeKind.Root,
                Metrics = new NodeMetrics(
                    Tokens: 30,
                    TotalLines: 3,
                    CodeLines: 3,
                    CommentLines: 0,
                    BlankLines: 0,
                    Language: null,
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
                            CodeLines: 1,
                            CommentLines: 0,
                            BlankLines: 0,
                            Language: "C#",
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
                            CodeLines: 2,
                            CommentLines: 0,
                            BlankLines: 0,
                            Language: "C#",
                            FileSizeBytes: 20,
                            DescendantFileCount: 1,
                            DescendantDirectoryCount: 0),
                    },
                },
            });

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
        var root = new ProjectTreeNodeViewModel(CreateNestedSnapshot().Root);
        viewModel.LoadRoot(root);

        var directoryNode = Assert.Single(root.Children);

        Assert.Equal(2, viewModel.VisibleNodes.Count);

        viewModel.ToggleNodeCommand.Execute(directoryNode);

        Assert.Equal(3, viewModel.VisibleNodes.Count);
        Assert.Contains(viewModel.VisibleNodes, node => node.RelativePath == "src/Program.cs");

        viewModel.ToggleNodeCommand.Execute(directoryNode);

        Assert.Equal(2, viewModel.VisibleNodes.Count);
        Assert.DoesNotContain(viewModel.VisibleNodes, node => node.RelativePath == "src/Program.cs");
    }

    [AvaloniaFact]
    public async Task MainWindow_CancelCommand_ShowsProgressOnlyWhileScanIsRunning()
    {
        var window = new MainWindow();
        var viewModel = new MainWindowViewModel(
            new CancelAwareProjectAnalyzer(),
            new StubFolderPickerService("C:\\Demo"));
        window.DataContext = viewModel;

        window.Show();
        var statusStrip = window.FindControl<Control>("StatusStrip");

        Assert.NotNull(statusStrip);
        Assert.False(statusStrip.IsVisible);

        var openTask = viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);
        await Task.Delay(100);
        Assert.True(statusStrip.IsVisible);
        viewModel.Toolbar.CancelCommand.Execute(null);
        await openTask;

        Assert.Equal(AnalysisState.Cancelled, viewModel.AnalysisState);
        Assert.False(statusStrip.IsVisible);
    }

    [AvaloniaFact]
    public void TreemapControl_RendersSnapshotWithoutChildControls()
    {
        var control = new TreemapControl
        {
            Width = 320,
            Height = 180,
            RootNode = CreateSnapshot().Root,
            Metric = "Tokens",
        };
        var window = new Window
        {
            Content = control,
            Width = 360,
            Height = 240,
        };

        window.Show();

        Assert.NotEmpty(control.NodeVisuals);
    }

    [AvaloniaFact]
    public void TreemapControl_HitTest_ReturnsRenderedNode()
    {
        var control = new TreemapControl
        {
            Width = 320,
            Height = 180,
            RootNode = CreateSnapshot().Root,
            Metric = "Tokens",
        };
        var window = new Window
        {
            Content = control,
            Width = 360,
            Height = 240,
        };

        window.Show();

        var visual = Assert.Single(control.NodeVisuals);
        var point = new Avalonia.Point(
            visual.Bounds.X + (visual.Bounds.Width / 2),
            visual.Bounds.Y + (visual.Bounds.Height / 2));

        var hitNode = control.HitTestNode(point);

        Assert.NotNull(hitNode);
        Assert.Equal(visual.Node.RelativePath, hitNode.RelativePath);
        Assert.Null(control.HitTestNode(new Avalonia.Point(-10, -10)));
    }

    [AvaloniaFact]
    public async Task MainWindow_TreemapSelection_SynchronizesTree()
    {
        var window = new MainWindow();
        var viewModel = new MainWindowViewModel(
            new StubProjectAnalyzer(CreateSnapshot()),
            new StubFolderPickerService("C:\\Demo"));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);

        var control = window.FindControl<TreemapControl>("ProjectTreemapControl");
        Assert.NotNull(control);

        var visual = Assert.Single(control.NodeVisuals);
        var point = new Avalonia.Point(
            visual.Bounds.X + (visual.Bounds.Width / 2),
            visual.Bounds.Y + (visual.Bounds.Height / 2));

        control.SelectNodeAt(point);

        Assert.Equal("Program.cs", viewModel.Tree.SelectedNode?.Node.RelativePath);
        Assert.Equal("Program.cs", viewModel.SelectedNode?.RelativePath);
    }

    [AvaloniaFact]
    public async Task MainWindow_TreemapSelection_ExpandsAncestorChainInProjectTree()
    {
        var window = new MainWindow();
        var viewModel = new MainWindowViewModel(
            new StubProjectAnalyzer(CreateNestedSnapshot()),
            new StubFolderPickerService("C:\\Demo"));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);

        var control = window.FindControl<TreemapControl>("ProjectTreemapControl");
        Assert.NotNull(control);

        var visual = Assert.Single(control.NodeVisuals, item => item.Node.RelativePath == "src/Program.cs");
        var point = new Avalonia.Point(
            visual.Bounds.X + (visual.Bounds.Width / 2),
            visual.Bounds.Y + (visual.Bounds.Height / 2));

        control.SelectNodeAt(point);

        var rootNode = Assert.Single(viewModel.Tree.RootNodes);
        var directoryNode = Assert.Single(rootNode.Children);
        var fileNode = Assert.Single(directoryNode.Children);

        Assert.True(rootNode.IsExpanded);
        Assert.True(directoryNode.IsExpanded);
        Assert.Equal(fileNode, viewModel.Tree.SelectedNode);
        Assert.Equal("src/Program.cs", viewModel.SelectedNode?.RelativePath);
    }

    [AvaloniaFact]
    public async Task MainWindow_TreeSelection_SynchronizesTreemap()
    {
        var window = new MainWindow();
        var viewModel = new MainWindowViewModel(
            new StubProjectAnalyzer(CreateSnapshot()),
            new StubFolderPickerService("C:\\Demo"));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);

        var childNode = Assert.Single(viewModel.Tree.RootNodes[0].Children);
        viewModel.Tree.SelectedNode = childNode;

        var control = window.FindControl<TreemapControl>("ProjectTreemapControl");

        Assert.NotNull(control);
        Assert.Equal("Program.cs", control.SelectedNode?.RelativePath);
    }

    [AvaloniaFact]
    public void TreemapControl_Hover_UpdatesTooltipStateWithoutChangingSelection()
    {
        var control = new TreemapControl
        {
            Width = 320,
            Height = 180,
            RootNode = CreateSnapshot().Root,
            Metric = "Tokens",
        };
        var window = new Window
        {
            Content = control,
            Width = 360,
            Height = 240,
        };

        window.Show();

        var visual = Assert.Single(control.NodeVisuals);
        var point = new Avalonia.Point(
            visual.Bounds.X + (visual.Bounds.Width / 2),
            visual.Bounds.Y + (visual.Bounds.Height / 2));

        control.UpdateHover(point);

        Assert.Equal("Program.cs", control.HoveredNode?.RelativePath);
        Assert.Null(control.SelectedNode);
        Assert.Contains("Program.cs", control.TooltipText);

        control.ClearHover();

        Assert.Null(control.HoveredNode);
        Assert.Null(control.TooltipText);
    }

    private static ProjectSnapshot CreateSnapshot() =>
        new()
        {
            RootPath = "C:\\Demo",
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Options = ScanOptions.Default,
            Root = new ProjectNode
            {
                Id = "/",
                Name = "Demo",
                FullPath = "C:\\Demo",
                RelativePath = string.Empty,
                Kind = ProjectNodeKind.Root,
                Metrics = new NodeMetrics(
                    Tokens: 42,
                    TotalLines: 12,
                    CodeLines: 10,
                    CommentLines: 1,
                    BlankLines: 1,
                    Language: null,
                    FileSizeBytes: 128,
                    DescendantFileCount: 1,
                    DescendantDirectoryCount: 0),
                Children =
                {
                    new ProjectNode
                    {
                        Id = "Program.cs",
                        Name = "Program.cs",
                        FullPath = "C:\\Demo\\Program.cs",
                        RelativePath = "Program.cs",
                        Kind = ProjectNodeKind.File,
                        Metrics = new NodeMetrics(
                            Tokens: 42,
                            TotalLines: 12,
                            CodeLines: 10,
                            CommentLines: 1,
                            BlankLines: 1,
                            Language: "C#",
                            FileSizeBytes: 128,
                            DescendantFileCount: 1,
                            DescendantDirectoryCount: 0),
                    },
                },
            },
        };

    private static ProjectTreeNodeViewModel CreateRootWithChildren(params (string Name, long FileSizeBytes, int Tokens, int TotalLines)[] children)
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
                CodeLines: children.Sum(item => item.TotalLines),
                CommentLines: 0,
                BlankLines: 0,
                Language: null,
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
                    CodeLines: item.TotalLines,
                    CommentLines: 0,
                    BlankLines: 0,
                    Language: "C#",
                    FileSizeBytes: item.FileSizeBytes,
                    DescendantFileCount: 1,
                    DescendantDirectoryCount: 0),
            });
        }

        return new ProjectTreeNodeViewModel(root);
    }

    private static ProjectSnapshot CreateNestedSnapshot() =>
        new()
        {
            RootPath = "C:\\Demo",
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Options = ScanOptions.Default,
            Root = new ProjectNode
            {
                Id = "/",
                Name = "Demo",
                FullPath = "C:\\Demo",
                RelativePath = string.Empty,
                Kind = ProjectNodeKind.Root,
                Metrics = new NodeMetrics(
                    Tokens: 42,
                    TotalLines: 12,
                    CodeLines: 10,
                    CommentLines: 1,
                    BlankLines: 1,
                    Language: null,
                    FileSizeBytes: 128,
                    DescendantFileCount: 1,
                    DescendantDirectoryCount: 1),
                Children =
                {
                    new ProjectNode
                    {
                        Id = "src",
                        Name = "src",
                        FullPath = "C:\\Demo\\src",
                        RelativePath = "src",
                        Kind = ProjectNodeKind.Directory,
                        Metrics = new NodeMetrics(
                            Tokens: 42,
                            TotalLines: 12,
                            CodeLines: 10,
                            CommentLines: 1,
                            BlankLines: 1,
                            Language: null,
                            FileSizeBytes: 128,
                            DescendantFileCount: 1,
                            DescendantDirectoryCount: 0),
                        Children =
                        {
                            new ProjectNode
                            {
                                Id = "src/Program.cs",
                                Name = "Program.cs",
                                FullPath = "C:\\Demo\\src\\Program.cs",
                                RelativePath = "src/Program.cs",
                                Kind = ProjectNodeKind.File,
                                Metrics = new NodeMetrics(
                                    Tokens: 42,
                                    TotalLines: 12,
                                    CodeLines: 10,
                                    CommentLines: 1,
                                    BlankLines: 1,
                                    Language: "C#",
                                    FileSizeBytes: 128,
                                    DescendantFileCount: 1,
                                    DescendantDirectoryCount: 0),
                            },
                        },
                    },
                },
            },
        };

    private sealed class StubFolderPickerService(string? path) : IFolderPickerService
    {
        public Task<string?> PickFolderAsync(CancellationToken cancellationToken) =>
            Task.FromResult(path);
    }

    private sealed class StubProjectAnalyzer(ProjectSnapshot snapshot) : IProjectAnalyzer
    {
        public Task<ProjectSnapshot> AnalyzeAsync(
            string rootPath,
            ScanOptions options,
            IProgress<AnalysisProgress>? progress,
            CancellationToken cancellationToken) =>
            Task.FromResult(snapshot);
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
