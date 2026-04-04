using System.ComponentModel;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.App.Views.Sections;
using Clever.TokenMap.Tests.Headless.Support;
using Clever.TokenMap.Tests.Support;
using static Clever.TokenMap.Tests.Headless.Support.HeadlessTestSupport;

namespace Clever.TokenMap.Tests.Headless.MainWindow;

public sealed class ProjectTreePaneViewInteractionTests
{
    [AvaloniaFact]
    public async Task ProjectTreePaneView_HeaderClick_SortsByNameAndTogglesDirection()
    {
        var viewModel = CreateMainWindowViewModel();
        viewModel.Tree.LoadRoot(CreateRootWithChildren(
            ("Zulu.cs", 20, 2, 2),
            ("Alpha.cs", 10, 1, 1)));

        var window = new Window
        {
            Content = new ProjectTreePaneView
            {
                DataContext = viewModel,
            },
        };

        await ShowAndRenderAsync(window);

        var nameHeader = FindProjectTreeHeader(window, "Name");
        Assert.NotNull(nameHeader);

        await ClickAsync(window, nameHeader);

        Assert.Equal(ProjectTreeSortColumn.Name, viewModel.Tree.CurrentSortColumn);
        Assert.Equal(ListSortDirection.Ascending, viewModel.Tree.CurrentSortDirection);
        Assert.Equal(
            ["Alpha.cs", "Zulu.cs"],
            viewModel.Tree.VisibleNodes.Skip(1).Select(node => node.Name).ToArray());

        await Task.Delay(TimeSpan.FromMilliseconds(700));
        nameHeader = FindProjectTreeHeader(window, "Name");
        Assert.NotNull(nameHeader);

        await ClickAsync(window, nameHeader);

        Assert.Equal(ProjectTreeSortColumn.Name, viewModel.Tree.CurrentSortColumn);
        Assert.Equal(ListSortDirection.Descending, viewModel.Tree.CurrentSortDirection);
        Assert.Equal(
            ["Zulu.cs", "Alpha.cs"],
            viewModel.Tree.VisibleNodes.Skip(1).Select(node => node.Name).ToArray());
    }

    [AvaloniaFact]
    public async Task ProjectTreePaneView_DoubleClickOnDirectory_TogglesExpansion()
    {
        var viewModel = CreateMainWindowViewModel();
        viewModel.Tree.LoadRoot(CreateNestedSnapshot().Root);

        var window = new Window
        {
            Content = new ProjectTreePaneView
            {
                DataContext = viewModel,
            },
        };

        await ShowAndRenderAsync(window);

        var srcRow = FindProjectTreeRow(window, "src");
        Assert.NotNull(srcRow);
        Assert.DoesNotContain(viewModel.Tree.VisibleNodes, node => node.Node.Id == "src/Program.cs");

        await DoubleClickAsync(window, srcRow);

        Assert.Equal("src", viewModel.Tree.SelectedNode?.Node.Id);
        Assert.Contains(viewModel.Tree.VisibleNodes, node => node.Node.Id == "src/Program.cs");

        srcRow = FindProjectTreeRow(window, "src");
        Assert.NotNull(srcRow);

        await DoubleClickAsync(window, srcRow);

        Assert.Equal("src", viewModel.Tree.SelectedNode?.Node.Id);
        Assert.DoesNotContain(viewModel.Tree.VisibleNodes, node => node.Node.Id == "src/Program.cs");
    }

    [AvaloniaFact]
    public async Task ProjectTreePaneView_DoubleClickOnFile_OpensPreview()
    {
        var viewModel = CreateMainWindowViewModel();
        viewModel.Tree.LoadRoot(CreateRootWithChildren(("Program.cs", 20, 2, 2)));

        var window = new Window
        {
            Content = new ProjectTreePaneView
            {
                DataContext = viewModel,
            },
        };

        await ShowAndRenderAsync(window);

        var fileRow = FindProjectTreeRow(window, "Program.cs");
        Assert.NotNull(fileRow);

        await DoubleClickAsync(window, fileRow);

        Assert.True(viewModel.IsFilePreviewOpen);
        Assert.Equal("Program.cs", viewModel.FilePreview.DisplayName);
        Assert.Equal("Program.cs", viewModel.Tree.SelectedNode?.Node.Id);
    }

    [AvaloniaFact]
    public async Task ProjectTreePaneView_NameColumn_UsesInitialVisibleContentWidth_AndDoesNotGrowAfterExpansion()
    {
        var snapshot = new ProjectSnapshot
        {
            RootPath = GetTestFolderPath("Demo"),
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Options = ScanOptions.Default,
            Root = new()
            {
                Id = "/",
                Name = "Demo",
                FullPath = GetTestFolderPath("Demo"),
                RelativePath = string.Empty,
                Kind = ProjectNodeKind.Root,
                Summary = MetricTestData.CreateDirectorySummary(descendantFileCount: 1, descendantDirectoryCount: 1),
                ComputedMetrics = MetricTestData.CreateComputedMetrics(tokens: 42, nonEmptyLines: 11, fileSizeBytes: 128),
                Children =
                {
                    new()
                    {
                        Id = "src",
                        Name = "VisibleDirectoryNameThatShouldSetInitialWidth",
                        FullPath = Path.Combine(GetTestFolderPath("Demo"), "src"),
                        RelativePath = "src",
                        Kind = ProjectNodeKind.Directory,
                        Summary = MetricTestData.CreateDirectorySummary(descendantFileCount: 1, descendantDirectoryCount: 0),
                        ComputedMetrics = MetricTestData.CreateComputedMetrics(tokens: 42, nonEmptyLines: 11, fileSizeBytes: 128),
                        Children =
                        {
                            new()
                            {
                                Id = "src/Program.cs",
                                Name = "NestedChildNameThatShouldNotResizeTheColumnAfterExpansion.cs",
                                FullPath = Path.Combine(GetTestFolderPath("Demo"), "src", "Program.cs"),
                                RelativePath = "src/Program.cs",
                                Kind = ProjectNodeKind.File,
                                Summary = MetricTestData.CreateFileSummary(),
                                ComputedMetrics = MetricTestData.CreateComputedMetrics(tokens: 42, nonEmptyLines: 11, fileSizeBytes: 128),
                            },
                        },
                    },
                },
            },
        };

        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(snapshot));
        foreach (var option in viewModel.Toolbar.MetricVisibilityOptions.Concat(viewModel.Toolbar.TreeOnlyMetricVisibilityOptions))
        {
            option.IsVisible = true;
        }

        var window = new Window
        {
            Width = 900,
            Height = 700,
            Content = new ProjectTreePaneView
            {
                DataContext = viewModel,
            },
        };

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);
        await WaitForUiAsync(window);

        var treeTable = FindNamedDescendant<DataGrid>(window, "ProjectTreeTable");
        Assert.NotNull(treeTable);

        var nameColumn = treeTable.Columns.First(column => string.Equals(column.SortMemberPath, "Name", StringComparison.Ordinal));
        var initialWidth = nameColumn.ActualWidth;

        Assert.True(initialWidth > 200, $"Expected the initial Name column width to fit visible names, but got {initialWidth}.");

        var srcRow = FindProjectTreeRow(window, "src");
        Assert.NotNull(srcRow);

        await DoubleClickAsync(window, srcRow);

        var expandedWidth = nameColumn.ActualWidth;
        Assert.Equal(initialWidth, expandedWidth, precision: 3);
    }

    private static async Task ShowAndRenderAsync(Window window)
    {
        window.Show();
        await WaitForUiAsync(window);
    }

    private static async Task ClickAsync(Window window, Control target)
    {
        var point = GetCenterPoint(window, target);
        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        await WaitForUiAsync(window);
    }

    private static async Task DoubleClickAsync(Window window, Control target)
    {
        await ClickAsync(window, target);
        await ClickAsync(window, target);
    }

    private static async Task WaitForUiAsync(Window window)
    {
        for (var iteration = 0; iteration < 3; iteration++)
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            window.UpdateLayout();
        }
    }

    private static Point GetCenterPoint(Window window, Control target)
    {
        var point = target.TranslatePoint(
            new Point(target.Bounds.Width / 2, target.Bounds.Height / 2),
            window);

        return point ?? throw new InvalidOperationException($"Unable to translate '{target.Name}' to window coordinates.");
    }

    private static DataGridColumnHeader? FindProjectTreeHeader(Window window, string headerText) =>
        window.GetVisualDescendants()
            .OfType<DataGridColumnHeader>()
            .FirstOrDefault(header => string.Equals(header.Content?.ToString(), headerText, StringComparison.Ordinal));

    private static DataGridRow? FindProjectTreeRow(Window window, string nodeId) =>
        window.GetVisualDescendants()
            .OfType<DataGridRow>()
            .FirstOrDefault(row => row.DataContext is ProjectTreeNodeViewModel node && node.Node.Id == nodeId);

}
