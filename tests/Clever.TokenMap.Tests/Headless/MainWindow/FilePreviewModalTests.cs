using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using AvaloniaEdit;
using Clever.TokenMap.App.Views;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Preview;
using Clever.TokenMap.Tests.Headless.Support;
using AppMainWindow = Clever.TokenMap.App.Views.MainWindow;
using static Clever.TokenMap.Tests.Headless.Support.HeadlessTestSupport;

namespace Clever.TokenMap.Tests.Headless.MainWindow;

public sealed class FilePreviewModalTests
{
    [AvaloniaFact]
    public async Task MainWindow_FilePreviewModal_ShowsEditor_WhenPreviewSucceeds()
    {
        const string previewContent = "class Program { }";
        var snapshot = CreateSnapshot();
        var file = Assert.Single(snapshot.Root.Children);
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel(
            new StubProjectAnalyzer(snapshot),
            filePreviewContentReader: new FixedPreviewReader(new FilePreviewContentResult(FilePreviewReadStatus.Success, previewContent)));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.PreviewNodeAsync(file);
        window.UpdateLayout();

        var modal = FindNamedDescendant<Control>(window, "FilePreviewModal");
        var backdrop = FindNamedDescendant<Control>(window, "FilePreviewBackdrop");
        var editor = FindNamedDescendant<TextEditor>(window, "FilePreviewEditor");
        var statusPanel = FindNamedDescendant<Control>(window, "FilePreviewStatusPanel");

        Assert.NotNull(modal);
        Assert.NotNull(backdrop);
        Assert.NotNull(editor);
        Assert.NotNull(statusPanel);
        Assert.True(modal.IsVisible);
        Assert.True(backdrop.IsVisible);
        Assert.True(editor.IsVisible);
        Assert.Equal(previewContent, editor.Text);
        Assert.False(statusPanel.IsVisible);
    }

    [AvaloniaFact]
    public async Task MainWindow_FilePreviewModal_UsesContextMenuActionLabels()
    {
        var snapshot = CreateSnapshot();
        var file = Assert.Single(snapshot.Root.Children);
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel(
            new StubProjectAnalyzer(snapshot),
            filePreviewContentReader: new FixedPreviewReader(new FilePreviewContentResult(FilePreviewReadStatus.Success, "preview")));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.PreviewNodeAsync(file);
        window.UpdateLayout();

        var openButton = FindNamedDescendant<Button>(window, "FilePreviewOpenDefaultAppButton");
        var revealButton = FindNamedDescendant<Button>(window, "FilePreviewRevealButton");
        var copyFullPathButton = FindNamedDescendant<Button>(window, "FilePreviewCopyFullPathButton");
        var copyRelativePathButton = FindNamedDescendant<Button>(window, "FilePreviewCopyRelativePathButton");

        Assert.NotNull(openButton);
        Assert.NotNull(revealButton);
        Assert.NotNull(copyFullPathButton);
        Assert.NotNull(copyRelativePathButton);
        Assert.Equal("Open", GetButtonText(openButton));
        Assert.Equal(viewModel.RevealMenuHeader, GetButtonText(revealButton));
        Assert.Equal("Copy Full Path", GetButtonText(copyFullPathButton));
        Assert.Equal("Copy Relative Path", GetButtonText(copyRelativePathButton));
    }

    [AvaloniaFact]
    public async Task MainWindow_FilePreviewModal_ExcludeAction_ClosesPreviewAndOpensExcludesEditor()
    {
        var snapshot = CreateSnapshot();
        var file = Assert.Single(snapshot.Root.Children);
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel(
            new StubProjectAnalyzer(snapshot),
            filePreviewContentReader: new FixedPreviewReader(new FilePreviewContentResult(FilePreviewReadStatus.Success, "preview")));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);
        await viewModel.PreviewNodeAsync(file);
        window.UpdateLayout();

        var excludeButton = FindNamedDescendant<Button>(window, "FilePreviewExcludeButton");
        Assert.NotNull(excludeButton);
        Assert.True(excludeButton.IsVisible);

        excludeButton.RaiseEvent(new RoutedEventArgs
        {
            RoutedEvent = Button.ClickEvent,
            Source = excludeButton,
        });
        window.UpdateLayout();

        Assert.False(viewModel.IsFilePreviewOpen);
        Assert.True(viewModel.ExcludesEditor.IsOpen);
    }

    [AvaloniaFact]
    public async Task MainWindow_FilePreviewModal_ShowsFallbackStatus_WhenPreviewFails()
    {
        var snapshot = CreateSnapshot();
        var file = Assert.Single(snapshot.Root.Children);
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel(
            new StubProjectAnalyzer(snapshot),
            filePreviewContentReader: new FixedPreviewReader(new FilePreviewContentResult(FilePreviewReadStatus.TooLarge)));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.PreviewNodeAsync(file);
        window.UpdateLayout();

        var editor = FindNamedDescendant<Control>(window, "FilePreviewEditor");
        var statusPanel = FindNamedDescendant<Control>(window, "FilePreviewStatusPanel");
        var statusTitle = FindNamedDescendant<TextBlock>(window, "FilePreviewStatusTitleText");

        Assert.NotNull(editor);
        Assert.NotNull(statusPanel);
        Assert.NotNull(statusTitle);
        Assert.False(editor.IsVisible);
        Assert.True(statusPanel.IsVisible);
        Assert.Equal("File too large", statusTitle.Text);
    }

    [AvaloniaFact]
    public async Task MainWindow_Escape_ClosesFilePreviewModal()
    {
        var snapshot = CreateSnapshot();
        var file = Assert.Single(snapshot.Root.Children);
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel(
            new StubProjectAnalyzer(snapshot),
            filePreviewContentReader: new FixedPreviewReader(new FilePreviewContentResult(FilePreviewReadStatus.Success, "preview")));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.PreviewNodeAsync(file);
        window.UpdateLayout();

        var keyArgs = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Source = window,
            Key = Key.Escape,
        };

        window.RaiseEvent(keyArgs);
        window.UpdateLayout();

        Assert.True(keyArgs.Handled);
        Assert.False(viewModel.IsFilePreviewOpen);
    }

    [AvaloniaFact]
    public void ProjectNodeContextMenuController_PreviewItem_IsVisibleOnlyForFiles()
    {
        var fileSnapshot = CreateSnapshot();
        var fileNode = Assert.Single(fileSnapshot.Root.Children);
        var directorySnapshot = CreateNestedSnapshot();
        var directoryNode = Assert.Single(directorySnapshot.Root.Children);
        var viewModel = CreateMainWindowViewModel();
        var controller = new ProjectNodeContextMenuController(new Border(), () => viewModel);

        var previewItem = GetPreviewItem(controller);

        SetCurrentNodeAndRefresh(controller, fileNode);
        Assert.True(previewItem.IsVisible);
        Assert.True(previewItem.IsEnabled);

        SetCurrentNodeAndRefresh(controller, directoryNode);
        Assert.False(previewItem.IsVisible);
        Assert.False(previewItem.IsEnabled);
    }

    private static MenuItem GetPreviewItem(ProjectNodeContextMenuController controller)
    {
        var field = typeof(ProjectNodeContextMenuController).GetField("_previewItem", BindingFlags.Instance | BindingFlags.NonPublic);
        return Assert.IsType<MenuItem>(field?.GetValue(controller));
    }

    private static void SetCurrentNodeAndRefresh(ProjectNodeContextMenuController controller, object node)
    {
        var currentNodeField = typeof(ProjectNodeContextMenuController).GetField("_currentNode", BindingFlags.Instance | BindingFlags.NonPublic);
        currentNodeField?.SetValue(controller, node);

        var updateMethod = typeof(ProjectNodeContextMenuController).GetMethod("UpdateMenuState", BindingFlags.Instance | BindingFlags.NonPublic);
        updateMethod?.Invoke(controller, null);
    }

    private static string GetButtonText(Button button)
    {
        if (button.Content is string text)
        {
            return text;
        }

        return button.GetLogicalDescendants()
            .OfType<TextBlock>()
            .Select(textBlock => textBlock.Text)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text))
            ?? string.Empty;
    }

    private sealed class FixedPreviewReader(FilePreviewContentResult result) : IFilePreviewContentReader
    {
        public Task<FilePreviewContentResult> ReadAsync(string fullPath, CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }
}
