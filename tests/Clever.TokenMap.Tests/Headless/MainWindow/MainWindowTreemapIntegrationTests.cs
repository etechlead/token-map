using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Globalization;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.App.Views.Sections;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Tests.Headless.Support;
using Clever.TokenMap.Tests.Support;
using Clever.TokenMap.Treemap;
using AppMainWindow = Clever.TokenMap.App.Views.MainWindow;
using static Clever.TokenMap.Tests.Headless.Support.HeadlessTestSupport;

namespace Clever.TokenMap.Tests.Headless.MainWindow;

public sealed class MainWindowTreemapIntegrationTests
{
    [AvaloniaFact]
    public async Task MainWindow_TreemapSelection_ExpandsAncestorChainInProjectTree()
    {
        var (window, viewModel) = await CreateOpenWindowAsync(CreateNestedSnapshot());

        var control = FindNamedDescendant<TreemapControl>(window, "ProjectTreemapControl");
        Assert.NotNull(control);

        var visual = Assert.Single(control.NodeVisuals, item => item.Node.RelativePath == "src/Program.cs");
        control.SelectNodeAt(GetCenter(visual));

        var rootNode = Assert.Single(viewModel.Tree.RootNodes);
        var directoryNode = Assert.Single(viewModel.Tree.VisibleNodes, node => node.Node.Id == "src");
        var fileNode = Assert.Single(viewModel.Tree.VisibleNodes, node => node.Node.Id == "src/Program.cs");

        Assert.True(rootNode.IsExpanded);
        Assert.True(directoryNode.IsExpanded);
        Assert.Equal(fileNode, viewModel.Tree.SelectedNode);
        Assert.Equal("src/Program.cs", viewModel.SelectedNode?.RelativePath);
    }

    [AvaloniaFact]
    public async Task MainWindow_TreemapDirectoryDrillDown_ScopesTreemapAndSynchronizesTree()
    {
        var (window, viewModel) = await CreateOpenWindowAsync(CreateNestedSnapshot());

        var control = FindNamedDescendant<TreemapControl>(window, "ProjectTreemapControl");
        var breadcrumbs = FindNamedDescendant<ItemsControl>(window, "TreemapBreadcrumbsItemsControl");

        Assert.NotNull(control);
        Assert.NotNull(breadcrumbs);
        Assert.Single(viewModel.TreemapBreadcrumbs);

        var directoryVisual = Assert.Single(control.NodeVisuals, item => item.Node.RelativePath == "src");
        var handled = control.RequestDrillDownAt(GetInteriorPoint(directoryVisual));

        Assert.True(handled);
        Assert.Equal("src", viewModel.TreemapRootNode?.RelativePath);
        Assert.Equal("src", viewModel.Tree.SelectedNode?.Node.RelativePath);
        Assert.Equal("src", viewModel.SelectedNode?.RelativePath);
        Assert.Equal(2, viewModel.TreemapBreadcrumbs.Count);
        Assert.Equal("Demo", viewModel.TreemapBreadcrumbs[0].Label);
        Assert.Equal("/ src", viewModel.TreemapBreadcrumbs[1].Label);
        Assert.All(control.NodeVisuals, item => Assert.StartsWith("src", item.Node.RelativePath));
    }

    [AvaloniaFact]
    public async Task MainWindow_TreemapBreadcrumbNavigation_RestoresGlobalTreemap()
    {
        var (window, viewModel) = await CreateOpenWindowAsync(CreateNestedSnapshot());

        var control = FindNamedDescendant<TreemapControl>(window, "ProjectTreemapControl");
        var breadcrumbs = FindNamedDescendant<ItemsControl>(window, "TreemapBreadcrumbsItemsControl");

        Assert.NotNull(control);
        Assert.NotNull(breadcrumbs);
        Assert.Single(viewModel.TreemapBreadcrumbs);

        var directoryVisual = Assert.Single(control.NodeVisuals, item => item.Node.RelativePath == "src");
        control.RequestDrillDownAt(GetInteriorPoint(directoryVisual));

        viewModel.NavigateToTreemapBreadcrumbCommand.Execute(viewModel.TreemapBreadcrumbs[0].Node);

        Assert.Equal("/", viewModel.TreemapRootNode?.Id);
        Assert.Equal("src", viewModel.Tree.SelectedNode?.Node.RelativePath);
        Assert.Single(viewModel.TreemapBreadcrumbs);
        Assert.Equal("Demo", viewModel.TreemapBreadcrumbs[0].Label);
        Assert.Contains(control.NodeVisuals, item => item.Node.RelativePath == "src");
    }

    [AvaloniaFact]
    public async Task MainWindow_TreeSelection_SynchronizesTreemap()
    {
        var (window, viewModel) = await CreateOpenWindowAsync(CreateSnapshot());

        var childNode = Assert.Single(viewModel.Tree.VisibleNodes, node => node.Node.Id == "Program.cs");
        viewModel.Tree.SelectedNode = childNode;

        var control = FindNamedDescendant<TreemapControl>(window, "ProjectTreemapControl");

        Assert.NotNull(control);
        Assert.Equal("Program.cs", control.SelectedNode?.RelativePath);
    }

    [AvaloniaFact]
    public async Task MainWindow_TreemapSelection_ScrollsTreeRowIntoView()
    {
        const string targetRelativePath = "File-079.cs";

        var window = new AppMainWindow
        {
            Height = 650,
        };
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(CreateWideSnapshot(fileCount: 80)));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);
        window.UpdateLayout();

        var treeTable = FindNamedDescendant<DataGrid>(window, "ProjectTreeTable");
        var control = FindNamedDescendant<TreemapControl>(window, "ProjectTreemapControl");

        Assert.NotNull(treeTable);
        Assert.NotNull(control);
        Assert.Null(FindProjectTreeRow(window, targetRelativePath));

        var visual = Assert.Single(control.NodeVisuals, item => item.Node.RelativePath == targetRelativePath);
        control.SelectNodeAt(GetCenter(visual));
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        window.UpdateLayout();

        var row = FindProjectTreeRow(window, targetRelativePath);

        Assert.NotNull(row);
        Assert.Equal(targetRelativePath, viewModel.Tree.SelectedNode?.Node.RelativePath);
        Assert.Equal(targetRelativePath, (row.DataContext as ProjectTreeNodeViewModel)?.Node.RelativePath);
    }

    [AvaloniaFact]
    public async Task MainWindow_TreemapFileDoubleTap_OpensPreview()
    {
        var (window, viewModel) = await CreateOpenWindowAsync(CreateNestedSnapshot());

        var control = FindNamedDescendant<TreemapControl>(window, "ProjectTreemapControl");
        Assert.NotNull(control);

        var pane = window.GetVisualDescendants().OfType<TreemapPaneView>().Single();
        var fileVisual = Assert.Single(control.NodeVisuals, item => item.Node.RelativePath == "src/Program.cs");

        await pane.HandleTreemapNodeDoubleTapAsync(control, GetCenter(fileVisual));
        window.UpdateLayout();

        Assert.True(viewModel.IsFilePreviewOpen);
        Assert.Equal("Program.cs", viewModel.FilePreview.DisplayName);
        Assert.Equal("src/Program.cs", viewModel.SelectedNode?.RelativePath);
    }

    [AvaloniaFact]
    public async Task MainWindow_TreemapShowValuesToggle_UpdatesTreemapControlImmediately()
    {
        var (window, viewModel) = await CreateOpenWindowAsync(CreateSnapshot());

        var control = FindNamedDescendant<TreemapControl>(window, "ProjectTreemapControl");
        var showValuesCheckBox = FindNamedDescendant<CheckBox>(window, "TreemapShowValuesCheckBox");

        Assert.NotNull(control);
        Assert.NotNull(showValuesCheckBox);
        Assert.True(control.ShowMetricValues);
        Assert.True(showValuesCheckBox.IsChecked);

        viewModel.Toolbar.ShowTreemapMetricValues = false;
        window.UpdateLayout();

        Assert.False(control.ShowMetricValues);
        Assert.False(showValuesCheckBox.IsChecked);

        viewModel.Toolbar.ShowTreemapMetricValues = true;
        window.UpdateLayout();

        Assert.True(control.ShowMetricValues);
        Assert.True(showValuesCheckBox.IsChecked);
    }

    [AvaloniaFact]
    public async Task MainWindow_TreemapTooltip_UpdatesWhenVisibleMetricsChange()
    {
        var expectedShareText = (1d).ToString("P1", CultureInfo.CurrentCulture);
        var (window, viewModel) = await CreateOpenWindowAsync(CreateSnapshotWithExtendedMetrics());
        var complexityOption = Assert.Single(
            viewModel.Toolbar.MetricVisibilityOptions,
            option => option.Definition.Id == MetricIds.ComplexityPointsV0);
        complexityOption.IsVisible = false;
        window.UpdateLayout();

        var control = FindNamedDescendant<TreemapControl>(window, "ProjectTreemapControl");
        Assert.NotNull(control);

        var visual = Assert.Single(control.NodeVisuals);
        control.UpdateHover(GetCenter(visual));

        var initialLines = GetTooltipLines(control);
        Assert.True(Array.IndexOf(initialLines, $"Share: {expectedShareText}") < Array.IndexOf(initialLines, "---"));
        Assert.True(Array.IndexOf(initialLines, "---") < Array.IndexOf(initialLines, "Complexity: 18"));

        complexityOption.IsVisible = true;
        window.UpdateLayout();

        Assert.Contains(MetricIds.ComplexityPointsV0, control.VisibleMetricIds);

        var updatedLines = GetTooltipLines(control);
        Assert.True(Array.IndexOf(updatedLines, "Complexity: 18") < Array.IndexOf(updatedLines, $"Share: {expectedShareText}"));
        Assert.Equal(1, updatedLines.Count(line => line == "---"));
    }

    private static async Task<(AppMainWindow Window, MainWindowViewModel ViewModel)> CreateOpenWindowAsync(ProjectSnapshot snapshot)
    {
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(snapshot));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);
        return (window, viewModel);
    }

    private static DataGridRow? FindProjectTreeRow(Window window, string relativePath)
    {
        return window.GetVisualDescendants()
            .OfType<DataGridRow>()
            .FirstOrDefault(row =>
                string.Equals(
                    (row.DataContext as ProjectTreeNodeViewModel)?.Node.RelativePath,
                    relativePath,
                    StringComparison.Ordinal));
    }

    private static Point GetCenter(TreemapNodeVisual visual) =>
        new(
            visual.Bounds.X + (visual.Bounds.Width / 2),
            visual.Bounds.Y + (visual.Bounds.Height / 2));

    private static Point GetInteriorPoint(TreemapNodeVisual visual) =>
        new(
            visual.Bounds.X + 6,
            visual.Bounds.Y + 6);

    private static ProjectSnapshot CreateWideSnapshot(int fileCount)
    {
        var root = CreateRootWithChildren(
            [.. Enumerable.Range(0, fileCount)
                .Select(index => ($"File-{index:D3}.cs", FileSizeBytes: 100L, Tokens: 1, NonEmptyLines: 9))]);

        return new ProjectSnapshot
        {
            RootPath = "C:\\Demo",
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Options = ScanOptions.Default,
            Root = root,
        };
    }

    private static ProjectSnapshot CreateSnapshotWithExtendedMetrics()
    {
        return new ProjectSnapshot
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
                Summary = MetricTestData.CreateDirectorySummary(descendantFileCount: 1, descendantDirectoryCount: 0),
                ComputedMetrics = CreateExtendedMetricSet(tokens: 42, nonEmptyLines: 11, fileSizeBytes: 128, complexity: 17.5, hotspots: 4),
                Children =
                {
                    new ProjectNode
                    {
                        Id = "Program.cs",
                        Name = "Program.cs",
                        FullPath = "C:\\Demo\\Program.cs",
                        RelativePath = "Program.cs",
                        Kind = ProjectNodeKind.File,
                        Summary = MetricTestData.CreateFileSummary(),
                        ComputedMetrics = CreateExtendedMetricSet(tokens: 42, nonEmptyLines: 11, fileSizeBytes: 128, complexity: 17.5, hotspots: 4),
                    },
                },
            },
        };
    }

    private static MetricSet CreateExtendedMetricSet(
        long tokens,
        int nonEmptyLines,
        long fileSizeBytes,
        double complexity,
        int hotspots)
    {
        return MetricSet.From(
            (MetricIds.Tokens, MetricValue.From(tokens)),
            (MetricIds.NonEmptyLines, MetricValue.From(nonEmptyLines)),
            (MetricIds.FileSizeBytes, MetricValue.From(fileSizeBytes)),
            (MetricIds.ComplexityPointsV0, MetricValue.From(complexity)),
            (MetricIds.CallableHotspotPointsV0, MetricValue.From(hotspots)),
            (MetricIds.RefactorPriorityPointsV0, MetricValue.From(22)));
    }

    private static string[] GetTooltipLines(TreemapControl control)
    {
        var tooltipText = control.TooltipText;
        Assert.NotNull(tooltipText);
        return tooltipText.Split(Environment.NewLine, StringSplitOptions.None);
    }
}
