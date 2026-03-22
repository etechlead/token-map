using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Clever.TokenMap.App.Views;
using Clever.TokenMap.Treemap;
using static Clever.TokenMap.HeadlessTests.HeadlessTestSupport;

namespace Clever.TokenMap.HeadlessTests;

public sealed class MainWindowTreemapIntegrationTests
{
    [AvaloniaFact]
    public async Task MainWindow_TreemapSelection_SynchronizesTree()
    {
        var window = new MainWindow();
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(CreateSnapshot()));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);

        var control = FindNamedDescendant<TreemapControl>(window, "ProjectTreemapControl");
        Assert.NotNull(control);

        var visual = Assert.Single(control.NodeVisuals);
        var point = new Point(
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
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(CreateNestedSnapshot()));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);

        var control = FindNamedDescendant<TreemapControl>(window, "ProjectTreemapControl");
        Assert.NotNull(control);

        var visual = Assert.Single(control.NodeVisuals, item => item.Node.RelativePath == "src/Program.cs");
        var point = new Point(
            visual.Bounds.X + (visual.Bounds.Width / 2),
            visual.Bounds.Y + (visual.Bounds.Height / 2));

        control.SelectNodeAt(point);

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
        var window = new MainWindow();
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(CreateNestedSnapshot()));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);

        var control = FindNamedDescendant<TreemapControl>(window, "ProjectTreemapControl");
        var breadcrumbs = FindNamedDescendant<ItemsControl>(window, "TreemapBreadcrumbsItemsControl");

        Assert.NotNull(control);
        Assert.NotNull(breadcrumbs);
        Assert.Single(viewModel.TreemapBreadcrumbs);

        var directoryVisual = Assert.Single(control.NodeVisuals, item => item.Node.RelativePath == "src");
        var handled = control.RequestDrillDownAt(new Point(
            directoryVisual.Bounds.X + 6,
            directoryVisual.Bounds.Y + 6));

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
        var window = new MainWindow();
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(CreateNestedSnapshot()));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);

        var control = FindNamedDescendant<TreemapControl>(window, "ProjectTreemapControl");
        var breadcrumbs = FindNamedDescendant<ItemsControl>(window, "TreemapBreadcrumbsItemsControl");

        Assert.NotNull(control);
        Assert.NotNull(breadcrumbs);
        Assert.Single(viewModel.TreemapBreadcrumbs);

        var directoryVisual = Assert.Single(control.NodeVisuals, item => item.Node.RelativePath == "src");
        control.RequestDrillDownAt(new Point(
            directoryVisual.Bounds.X + 6,
            directoryVisual.Bounds.Y + 6));

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
        var window = new MainWindow();
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(CreateSnapshot()));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);

        var childNode = Assert.Single(viewModel.Tree.VisibleNodes, node => node.Node.Id == "Program.cs");
        viewModel.Tree.SelectedNode = childNode;

        var control = FindNamedDescendant<TreemapControl>(window, "ProjectTreemapControl");

        Assert.NotNull(control);
        Assert.Equal("Program.cs", control.SelectedNode?.RelativePath);
    }
}
