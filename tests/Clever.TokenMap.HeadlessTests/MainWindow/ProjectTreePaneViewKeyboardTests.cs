using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.App.Views.Sections;
using static Clever.TokenMap.HeadlessTests.HeadlessTestSupport;

namespace Clever.TokenMap.HeadlessTests;

public sealed class ProjectTreePaneViewKeyboardTests
{
    [AvaloniaFact]
    public void KeyDownHandler_RespondsEvenWhenDataGridAlreadyHandledArrowKey()
    {
        var viewModel = CreateMainWindowViewModel();
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
    public void SelectionChanged_UpdatesViewModelSelection()
    {
        var viewModel = CreateMainWindowViewModel();
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
}
