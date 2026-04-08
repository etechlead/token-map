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
    public async Task MainWindow_FilePreviewModal_ShowsExplainabilityPaneBesideEditor()
    {
        const string previewContent = "class Program { }";
        var snapshot = CreateExplainabilitySnapshot(includeGitContext: true);
        var file = Assert.Single(snapshot.Root.Children);
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel(
            new StubProjectAnalyzer(snapshot),
            filePreviewContentReader: new FixedPreviewReader(new FilePreviewContentResult(FilePreviewReadStatus.Success, previewContent)));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.PreviewNodeAsync(file);
        window.UpdateLayout();

        var pane = FindNamedDescendant<Control>(window, "FilePreviewExplainabilityPane");
        var splitter = FindNamedDescendant<GridSplitter>(window, "FilePreviewExplainabilitySplitter");
        var sections = FindNamedDescendant<ItemsControl>(window, "FilePreviewExplainabilitySectionsItemsControl");
        var editor = FindNamedDescendant<TextEditor>(window, "FilePreviewEditor");

        Assert.NotNull(pane);
        Assert.NotNull(splitter);
        Assert.NotNull(sections);
        Assert.NotNull(editor);
        Assert.True(pane.IsVisible);
        Assert.True(splitter.IsVisible);
        Assert.True(editor.IsVisible);
        Assert.Contains(
            sections.GetLogicalDescendants().OfType<TextBlock>().Select(textBlock => textBlock.Text),
            text => text == "Structural Risk");
        Assert.Contains(
            sections.GetLogicalDescendants().OfType<TextBlock>().Select(textBlock => textBlock.Text),
            text => text == "Refactor Priority");
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
        var refactorPromptButton = FindNamedDescendant<Button>(window, "FilePreviewOpenRefactorPromptButton");
        var copyFullPathButton = FindNamedDescendant<Button>(window, "FilePreviewCopyFullPathButton");
        var copyRelativePathButton = FindNamedDescendant<Button>(window, "FilePreviewCopyRelativePathButton");

        Assert.NotNull(openButton);
        Assert.NotNull(revealButton);
        Assert.NotNull(refactorPromptButton);
        Assert.NotNull(copyFullPathButton);
        Assert.NotNull(copyRelativePathButton);
        Assert.Equal("Open", GetButtonText(openButton));
        Assert.Equal(viewModel.RevealMenuHeader, GetButtonText(revealButton));
        Assert.Equal("Refactor Prompt", GetButtonText(refactorPromptButton));
        Assert.Equal("Copy Full Path", GetButtonText(copyFullPathButton));
        Assert.Equal("Copy Relative Path", GetButtonText(copyRelativePathButton));
    }

    [AvaloniaFact]
    public async Task MainWindow_RefactorPromptModal_ShowsEditablePromptForSelectedFile()
    {
        var snapshot = CreateExplainabilitySnapshot(includeGitContext: true);
        var file = Assert.Single(snapshot.Root.Children);
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(snapshot));
        window.DataContext = viewModel;

        window.Show();
        viewModel.OpenRefactorPrompt(file);
        window.UpdateLayout();

        var modal = FindNamedDescendant<Control>(window, "RefactorPromptModal");
        var editor = FindNamedDescendant<TextBox>(window, "RefactorPromptEditor");
        var copyButton = FindNamedDescendant<Button>(window, "CopyRefactorPromptButton");
        var pathText = FindNamedDescendant<TextBlock>(window, "RefactorPromptPathText");

        Assert.NotNull(modal);
        Assert.NotNull(editor);
        Assert.NotNull(copyButton);
        Assert.NotNull(pathText);
        Assert.True(modal.IsVisible);
        Assert.True(editor.IsVisible);
        Assert.True(editor.AcceptsReturn);
        Assert.Equal("Program.cs", pathText.Text);
        Assert.Contains("Relative path: Program.cs", editor.Text);
    }

    [AvaloniaFact]
    public void MainWindow_Escape_ClosesRefactorPromptModal()
    {
        var snapshot = CreateExplainabilitySnapshot(includeGitContext: true);
        var file = Assert.Single(snapshot.Root.Children);
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(snapshot));
        window.DataContext = viewModel;

        window.Show();
        viewModel.OpenRefactorPrompt(file);
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
        Assert.False(viewModel.IsRefactorPromptOpen);
    }

    [AvaloniaFact]
    public void MainWindow_SettingsDrawer_ShowsEditRefactorPromptTemplateButton()
    {
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel();
        window.DataContext = viewModel;
        viewModel.ToggleSettingsCommand.Execute(null);

        window.Show();
        window.UpdateLayout();

        var button = FindNamedDescendant<Button>(window, "EditRefactorPromptTemplateButton");

        Assert.NotNull(button);
        Assert.True(button.IsVisible);
        Assert.Equal("Edit refactor prompt template", GetButtonText(button));
    }

    [AvaloniaFact]
    public void MainWindow_RefactorPromptTemplateEditorModal_ShowsSplitPaneWithPlaceholders()
    {
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel();
        window.DataContext = viewModel;

        window.Show();
        viewModel.RefactorPromptTemplateSettings.OpenEditor();
        window.UpdateLayout();

        var modal = FindNamedDescendant<Control>(window, "RefactorPromptTemplateEditorModal");
        var placeholders = FindNamedDescendant<ItemsControl>(window, "RefactorPromptTemplatePlaceholdersItemsControl");
        var editor = FindNamedDescendant<TextBox>(window, "RefactorPromptTemplateEditorTextBox");

        Assert.NotNull(modal);
        Assert.NotNull(placeholders);
        Assert.NotNull(editor);
        Assert.True(modal.IsVisible);
        Assert.True(editor.AcceptsReturn);
        Assert.Contains(
            placeholders.GetLogicalDescendants().OfType<TextBlock>().Select(textBlock => textBlock.Text),
            text => text == "{{relative_path}}");
    }

    [AvaloniaFact]
    public void MainWindow_RefactorPromptTemplatePlaceholderClick_InsertsTokenAtCaret()
    {
        const string token = "{{tokens}}";
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel();
        window.DataContext = viewModel;

        window.Show();
        viewModel.RefactorPromptTemplateSettings.OpenEditor();
        window.UpdateLayout();

        var editor = FindNamedDescendant<TextBox>(window, "RefactorPromptTemplateEditorTextBox");
        Assert.NotNull(editor);
        editor.Text = "Alpha Omega";
        editor.CaretIndex = "Alpha ".Length;

        var placeholderButton = window.GetLogicalDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => GetButtonText(button) == token);
        Assert.NotNull(placeholderButton);

        placeholderButton.RaiseEvent(new RoutedEventArgs
        {
            RoutedEvent = Button.ClickEvent,
            Source = placeholderButton,
        });
        window.UpdateLayout();

        Assert.Equal("Alpha {{tokens}}Omega", editor.Text);
        Assert.Equal("Alpha ".Length + token.Length, editor.CaretIndex);
    }

    [AvaloniaFact]
    public void MainWindow_Escape_ClosesRefactorPromptTemplateEditorModal()
    {
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel();
        window.DataContext = viewModel;

        window.Show();
        viewModel.RefactorPromptTemplateSettings.OpenEditor();
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
        Assert.False(viewModel.RefactorPromptTemplateSettings.IsEditorOpen);
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

    [AvaloniaFact]
    public void ProjectNodeContextMenuController_RefactorPromptItem_IsVisibleOnlyForFiles()
    {
        var fileSnapshot = CreateSnapshot();
        var fileNode = Assert.Single(fileSnapshot.Root.Children);
        var directorySnapshot = CreateNestedSnapshot();
        var directoryNode = Assert.Single(directorySnapshot.Root.Children);
        var viewModel = CreateMainWindowViewModel();
        var controller = new ProjectNodeContextMenuController(new Border(), () => viewModel);

        var refactorPromptItem = GetRefactorPromptItem(controller);

        SetCurrentNodeAndRefresh(controller, fileNode);
        Assert.True(refactorPromptItem.IsVisible);
        Assert.True(refactorPromptItem.IsEnabled);

        SetCurrentNodeAndRefresh(controller, directoryNode);
        Assert.False(refactorPromptItem.IsVisible);
        Assert.False(refactorPromptItem.IsEnabled);
    }

    private static MenuItem GetPreviewItem(ProjectNodeContextMenuController controller)
    {
        var field = typeof(ProjectNodeContextMenuController).GetField("_previewItem", BindingFlags.Instance | BindingFlags.NonPublic);
        return Assert.IsType<MenuItem>(field?.GetValue(controller));
    }

    private static MenuItem GetRefactorPromptItem(ProjectNodeContextMenuController controller)
    {
        var field = typeof(ProjectNodeContextMenuController).GetField("_refactorPromptItem", BindingFlags.Instance | BindingFlags.NonPublic);
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
