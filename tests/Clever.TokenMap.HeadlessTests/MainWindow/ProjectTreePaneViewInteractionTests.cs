using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.App.Views.Sections;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using static Clever.TokenMap.HeadlessTests.HeadlessTestSupport;

namespace Clever.TokenMap.HeadlessTests;

public sealed class ProjectTreePaneViewInteractionTests
{
    [AvaloniaFact]
    public async Task ProjectTreePaneView_HeaderClick_SortsByNameAndTogglesDirection()
    {
        var viewModel = new MainWindowViewModel();
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
        Assert.Equal(System.ComponentModel.ListSortDirection.Ascending, viewModel.Tree.CurrentSortDirection);
        Assert.Equal(
            ["Alpha.cs", "Zulu.cs"],
            viewModel.Tree.VisibleNodes.Skip(1).Select(node => node.Name).ToArray());

        await Task.Delay(TimeSpan.FromMilliseconds(700));
        nameHeader = FindProjectTreeHeader(window, "Name");
        Assert.NotNull(nameHeader);

        await ClickAsync(window, nameHeader);

        Assert.Equal(ProjectTreeSortColumn.Name, viewModel.Tree.CurrentSortColumn);
        Assert.Equal(System.ComponentModel.ListSortDirection.Descending, viewModel.Tree.CurrentSortDirection);
        Assert.Equal(
            ["Zulu.cs", "Alpha.cs"],
            viewModel.Tree.VisibleNodes.Skip(1).Select(node => node.Name).ToArray());
    }

    [AvaloniaFact]
    public async Task ProjectTreePaneView_DoubleClickOnDirectory_TogglesExpansion()
    {
        var viewModel = new MainWindowViewModel();
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
    public async Task ProjectTreePaneView_SelectionChanged_WithPointerFlag_DelaysSelectionSync()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.Tree.LoadRoot(CreateRootWithChildren(
            ("Alpha.cs", 10, 10, 1),
            ("Beta.cs", 20, 20, 1)));
        viewModel.Tree.SelectNodeById("Alpha.cs");

        var window = new Window
        {
            Content = new ProjectTreePaneView
            {
                DataContext = viewModel,
            },
        };

        await ShowAndRenderAsync(window);

        var treeTable = FindNamedDescendant<DataGrid>(window, "ProjectTreeTable");
        var targetNode = Assert.Single(viewModel.Tree.VisibleNodes, node => node.Node.Id == "Beta.cs");
        Assert.NotNull(treeTable);
        Assert.Equal("Alpha.cs", viewModel.Tree.SelectedNode?.Node.Id);

        SetPrivateField(window.Content, "_projectTreeSelectionChangeTriggeredByPointer", true);
        treeTable.SelectedItem = targetNode;
        treeTable.RaiseEvent(new SelectionChangedEventArgs(
            SelectingItemsControl.SelectionChangedEvent,
            Array.Empty<object>(),
            new object[] { targetNode }));

        Assert.Equal("Beta.cs", Assert.IsType<ProjectTreeNodeViewModel>(treeTable.SelectedItem).Node.Id);
        Assert.Equal("Alpha.cs", viewModel.Tree.SelectedNode?.Node.Id);

        await Task.Delay(TimeSpan.FromMilliseconds(350));
        await WaitForUiAsync(window);

        Assert.Equal("Beta.cs", viewModel.Tree.SelectedNode?.Node.Id);
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
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        window.UpdateLayout();
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

    private static ProjectNode CreateRootWithChildren(params (string name, int tokens, int lines, int size)[] files)
    {
        var root = new ProjectNode
        {
            Id = "/",
            Name = "Demo",
            FullPath = "C:\\Demo",
            RelativePath = string.Empty,
            Kind = ProjectNodeKind.Root,
            Metrics = NodeMetrics.Empty,
        };

        foreach (var file in files)
        {
            root.Children.Add(new ProjectNode
            {
                Id = file.name,
                Name = file.name,
                FullPath = Path.Combine("C:\\Demo", file.name),
                RelativePath = file.name,
                Kind = ProjectNodeKind.File,
                Metrics = new NodeMetrics(
                    Tokens: file.tokens,
                    NonEmptyLines: file.lines,
                    FileSizeBytes: file.size,
                    DescendantFileCount: 1,
                    DescendantDirectoryCount: 0),
            });
        }

        return root;
    }

    private static void SetPrivateField(object? instance, string fieldName, object value)
    {
        var field = instance?.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(instance, value);
    }
}
