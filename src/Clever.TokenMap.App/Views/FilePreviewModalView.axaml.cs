using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using Clever.TokenMap.App.State;
using Clever.TokenMap.App.ViewModels;
using TextMateSharp.Grammars;

namespace Clever.TokenMap.App.Views;

public partial class FilePreviewModalView : UserControl
{
    private readonly RegistryOptions _darkRegistryOptions = new(ThemeName.DarkPlus);
    private readonly RegistryOptions _lightRegistryOptions = new(ThemeName.LightPlus);
    private dynamic? _textMateInstallation;
    private MainWindowViewModel? _viewModel;

    public FilePreviewModalView()
    {
        InitializeComponent();
        DataContextChanged += FilePreviewModalView_OnDataContextChanged;
    }

    private async void OpenInDefaultAppButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        await _viewModel.OpenNodeAsync(_viewModel.FilePreview.Node);
    }

    private async void RevealButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        await _viewModel.RevealNodeAsync(_viewModel.FilePreview.Node);
    }

    private void OpenRefactorPromptButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.OpenRefactorPrompt(_viewModel.FilePreview.Node);
    }

    private async void CopyFullPathButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        await CopyTextToClipboardAsync(_viewModel.FilePreview.FullPath);
    }

    private async void CopyRelativePathButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var relativePath = string.IsNullOrWhiteSpace(_viewModel.FilePreview.RelativePath)
            ? "."
            : _viewModel.FilePreview.RelativePath;
        await CopyTextToClipboardAsync(relativePath);
    }

    private void SetAsTreemapRootButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.SetTreemapRoot(_viewModel.FilePreview.Node);
    }

    private void ExcludeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var node = _viewModel.FilePreview.Node;
        _viewModel.CloseFilePreview();
        _viewModel.ExcludeNodeFromFolderCommand.Execute(node);
    }

    private void FilePreviewModalView_OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.FilePreview.PropertyChanged -= FilePreviewOnPropertyChanged;
            _viewModel.Toolbar.PropertyChanged -= ToolbarOnPropertyChanged;
        }

        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FilePreview.PropertyChanged += FilePreviewOnPropertyChanged;
            _viewModel.Toolbar.PropertyChanged += ToolbarOnPropertyChanged;
        }

        RefreshActionButtons();
        RefreshEditor();
    }

    private void FilePreviewOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName) ||
            e.PropertyName == nameof(FilePreviewState.Node) ||
            e.PropertyName == nameof(FilePreviewState.IsOpen))
        {
            RefreshActionButtons();
            RefreshEditor();
            return;
        }

        if (e.PropertyName == nameof(FilePreviewState.Content))
        {
            UpdateEditorContent();
        }
    }

    private void ToolbarOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName) ||
            e.PropertyName == nameof(ToolbarViewModel.SelectedThemePreference) ||
            e.PropertyName == nameof(ToolbarViewModel.IsSystemThemeSelected) ||
            e.PropertyName == nameof(ToolbarViewModel.IsLightThemeSelected) ||
            e.PropertyName == nameof(ToolbarViewModel.IsDarkThemeSelected))
        {
            RefreshEditor();
        }
    }

    private async Task CopyTextToClipboardAsync(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        await clipboard.SetTextAsync(text);
    }

    private void RefreshActionButtons()
    {
        ConfigureActionButton(
            "FilePreviewOpenDefaultAppButton",
            ProjectNodeActionPresentation.OpenHeader,
            ProjectNodeActionPresentation.OpenIconResourceKey,
            isVisible: true,
            isEnabled: _viewModel?.FilePreview.IsPathActionsEnabled == true);
        ConfigureActionButton(
            "FilePreviewRevealButton",
            _viewModel?.RevealMenuHeader ?? "Reveal",
            ProjectNodeActionPresentation.RevealIconResourceKey,
            isVisible: true,
            isEnabled: _viewModel?.FilePreview.IsPathActionsEnabled == true);
        ConfigureActionButton(
            "FilePreviewOpenRefactorPromptButton",
            ProjectNodeActionPresentation.RefactorPromptHeader,
            ProjectNodeActionPresentation.RefactorPromptIconResourceKey,
            isVisible: _viewModel?.CanOpenRefactorPrompt(_viewModel.FilePreview.Node) == true,
            isEnabled: _viewModel?.CanOpenRefactorPrompt(_viewModel.FilePreview.Node) == true);
        ConfigureActionButton(
            "FilePreviewSetTreemapRootButton",
            ProjectNodeActionPresentation.SetAsTreemapRootHeader,
            ProjectNodeActionPresentation.SetAsTreemapRootIconResourceKey,
            isVisible: _viewModel?.CanSetTreemapRoot(_viewModel.FilePreview.Node) == true,
            isEnabled: _viewModel?.CanSetTreemapRoot(_viewModel.FilePreview.Node) == true);
        ConfigureActionButton(
            "FilePreviewExcludeButton",
            ProjectNodeActionPresentation.ExcludeFromScanHeader,
            ProjectNodeActionPresentation.ExcludeFromScanIconResourceKey,
            isVisible: _viewModel?.CanExcludeNodeFromFolder(_viewModel.FilePreview.Node) == true,
            isEnabled: _viewModel?.CanExcludeNodeFromFolder(_viewModel.FilePreview.Node) == true);
        ConfigureActionButton(
            "FilePreviewCopyFullPathButton",
            ProjectNodeActionPresentation.CopyFullPathHeader,
            ProjectNodeActionPresentation.CopyFullPathIconResourceKey,
            isVisible: true,
            isEnabled: _viewModel?.FilePreview.IsPathActionsEnabled == true);
        ConfigureActionButton(
            "FilePreviewCopyRelativePathButton",
            ProjectNodeActionPresentation.CopyRelativePathHeader,
            iconResourceKey: null,
            isVisible: true,
            isEnabled: _viewModel?.FilePreview.IsPathActionsEnabled == true);
    }

    private void RefreshEditor()
    {
        var editor = this.FindControl<TextEditor>("FilePreviewEditor");
        if (editor is null)
        {
            return;
        }

        (_textMateInstallation as IDisposable)?.Dispose();
        _textMateInstallation = null;

        var registryOptions = GetRegistryOptions();
        _textMateInstallation = editor.InstallTextMate(registryOptions);

        var extension = Path.GetExtension(_viewModel?.FilePreview.FullPath ?? string.Empty);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return;
        }

        var language = registryOptions.GetLanguageByExtension(extension);
        if (language is null)
        {
            return;
        }

        var scopeName = registryOptions.GetScopeByLanguageId(language.Id);
        if (!string.IsNullOrWhiteSpace(scopeName))
        {
            _textMateInstallation?.SetGrammar(scopeName);
        }

        UpdateEditorContent();
    }

    private void UpdateEditorContent()
    {
        var editor = this.FindControl<TextEditor>("FilePreviewEditor");
        if (editor is null)
        {
            return;
        }

        var currentContent = _viewModel?.FilePreview.Content ?? string.Empty;
        if (!string.Equals(editor.Text, currentContent, StringComparison.Ordinal))
        {
            editor.Text = currentContent;
            ResetEditorViewport(editor);
        }
    }

    private static void ResetEditorViewport(TextEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);

        editor.TextArea.Caret.Offset = 0;
        editor.ScrollToLine(1);
    }

    private void ConfigureActionButton(
        string buttonName,
        string header,
        string? iconResourceKey,
        bool isVisible,
        bool isEnabled)
    {
        var button = this.FindControl<Button>(buttonName);
        if (button is null)
        {
            return;
        }

        button.Content = ProjectNodeActionPresentation.CreateActionButtonContent(header, iconResourceKey);
        button.IsVisible = isVisible;
        button.IsEnabled = isEnabled;
    }

    private RegistryOptions GetRegistryOptions()
    {
        var themeVariant = ActualThemeVariant;
        return themeVariant == ThemeVariant.Dark
            ? _darkRegistryOptions
            : _lightRegistryOptions;
    }
}
