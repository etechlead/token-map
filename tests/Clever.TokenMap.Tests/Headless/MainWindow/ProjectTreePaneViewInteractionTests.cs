using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.App.Views.Sections;
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

}
