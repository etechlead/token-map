using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Clever.TokenMap.App.State;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.App.Views;

internal sealed class ProjectNodeContextMenuController
{
    private readonly ContextMenu _menu;
    private readonly Control _clipboardHost;
    private readonly Func<IClipboard?>? _clipboardAccessor;
    private readonly Func<MainWindowViewModel?> _viewModelAccessor;
    private readonly Action<bool>? _setSuppressedState;
    private MenuItem? _copyFullPathItem;
    private MenuItem? _excludeItem;
    private MenuItem? _openItem;
    private MenuItem? _previewItem;
    private MenuItem? _copyRelativePathItem;
    private MenuItem? _refactorPromptItem;
    private MenuItem? _revealItem;
    private MenuItem? _setAsTreemapRootItem;
    private ProjectNode? _currentNode;

    public ProjectNodeContextMenuController(
        Control clipboardHost,
        Func<MainWindowViewModel?> viewModelAccessor,
        Action<bool>? setSuppressedState = null,
        Func<IClipboard?>? clipboardAccessor = null)
    {
        _clipboardHost = clipboardHost;
        _clipboardAccessor = clipboardAccessor;
        _viewModelAccessor = viewModelAccessor;
        _setSuppressedState = setSuppressedState;
        _menu = CreateMenu();
        _menu.Closed += (_, _) =>
        {
            _currentNode = null;
            _setSuppressedState?.Invoke(false);
        };
    }

    public void Show(Control owner, ProjectNode node)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(node);

        _currentNode = node;
        _setSuppressedState?.Invoke(true);
        UpdateMenuState();
        _menu.PlacementTarget = owner;
        _menu.Open(owner);
    }

    private ContextMenu CreateMenu()
    {
        var menu = new ContextMenu
        {
            Placement = PlacementMode.Pointer,
        };
        _openItem = CreateMenuItem(string.Empty, ProjectNodeActionPresentation.OpenIconResourceKey, OpenItem_OnClick);
        menu.Items.Add(_openItem);
        _previewItem = CreateMenuItem(string.Empty, ProjectNodeActionPresentation.PreviewIconResourceKey, PreviewItem_OnClick);
        menu.Items.Add(_previewItem);
        _refactorPromptItem = CreateMenuItem(string.Empty, ProjectNodeActionPresentation.RefactorPromptIconResourceKey, RefactorPromptItem_OnClick);
        menu.Items.Add(_refactorPromptItem);
        _revealItem = CreateMenuItem(string.Empty, ProjectNodeActionPresentation.RevealIconResourceKey, RevealItem_OnClick);
        menu.Items.Add(_revealItem);
        _setAsTreemapRootItem = CreateMenuItem(string.Empty, ProjectNodeActionPresentation.SetAsTreemapRootIconResourceKey, SetAsTreemapRootItem_OnClick);
        menu.Items.Add(_setAsTreemapRootItem);
        _excludeItem = CreateMenuItem(string.Empty, ProjectNodeActionPresentation.ExcludeFromScanIconResourceKey, ExcludeItem_OnClick);
        menu.Items.Add(_excludeItem);
        menu.Items.Add(new Separator());
        _copyFullPathItem = CreateMenuItem(string.Empty, ProjectNodeActionPresentation.CopyFullPathIconResourceKey, CopyFullPathItem_OnClick);
        menu.Items.Add(_copyFullPathItem);
        _copyRelativePathItem = CreateMenuItem(string.Empty, icon: null, CopyRelativePathItem_OnClick);
        menu.Items.Add(_copyRelativePathItem);
        return menu;
    }

    private static MenuItem CreateMenuItem(string header, string iconResourceKey, EventHandler<RoutedEventArgs> clickHandler) =>
        CreateMenuItem(header, ProjectNodeActionPresentation.CreateContextMenuIcon(iconResourceKey), clickHandler);

    private static MenuItem CreateMenuItem(string header, Control? icon, EventHandler<RoutedEventArgs> clickHandler)
    {
        var item = new MenuItem
        {
            Header = header,
            Icon = icon,
        };

        item.Click += clickHandler;
        return item;
    }

    private void UpdateMenuState()
    {
        var viewModel = _viewModelAccessor();
        var localization = viewModel?.Localization;
        var canPreview = _currentNode?.Kind == ProjectNodeKind.File;
        var canOpenRefactorPrompt = viewModel?.CanOpenRefactorPrompt(_currentNode) == true;
        var canSetTreemapRoot = viewModel?.CanSetTreemapRoot(_currentNode) == true;
        var canExclude = viewModel?.CanExcludeNodeFromFolder(_currentNode) == true;
        if (_openItem is not null)
        {
            _openItem.Header = ProjectNodeActionPresentation.GetOpenHeader(localization);
        }

        if (_previewItem is not null)
        {
            _previewItem.Header = ProjectNodeActionPresentation.GetPreviewHeader(localization);
            _previewItem.IsVisible = canPreview;
            _previewItem.IsEnabled = canPreview;
        }

        if (_refactorPromptItem is not null)
        {
            _refactorPromptItem.Header = ProjectNodeActionPresentation.GetRefactorPromptHeader(localization);
            _refactorPromptItem.IsVisible = canOpenRefactorPrompt;
            _refactorPromptItem.IsEnabled = canOpenRefactorPrompt;
        }

        if (_revealItem is not null)
        {
            _revealItem.Header = viewModel?.LocalizedRevealMenuHeader ?? viewModel?.RevealMenuHeader ?? "Reveal";
        }

        if (_setAsTreemapRootItem is null || _excludeItem is null)
        {
            return;
        }

        _setAsTreemapRootItem.Header = ProjectNodeActionPresentation.GetSetAsTreemapRootHeader(localization);
        _setAsTreemapRootItem.IsVisible = canSetTreemapRoot;
        _setAsTreemapRootItem.IsEnabled = canSetTreemapRoot;
        _excludeItem.Header = ProjectNodeActionPresentation.GetExcludeFromScanHeader(localization);
        _excludeItem.IsVisible = canExclude;
        _excludeItem.IsEnabled = canExclude;

        if (_copyFullPathItem is not null)
        {
            _copyFullPathItem.Header = ProjectNodeActionPresentation.GetCopyFullPathHeader(localization);
        }

        if (_copyRelativePathItem is not null)
        {
            _copyRelativePathItem.Header = ProjectNodeActionPresentation.GetCopyRelativePathHeader(localization);
        }
    }

    private async void OpenItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModelAccessor() is not { } viewModel)
        {
            return;
        }

        await viewModel.OpenNodeAsync(_currentNode);
    }

    private async void PreviewItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModelAccessor() is not { } viewModel)
        {
            return;
        }

        await viewModel.PreviewNodeAsync(_currentNode);
    }

    private async void RevealItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModelAccessor() is not { } viewModel)
        {
            return;
        }

        await viewModel.RevealNodeAsync(_currentNode);
    }

    private void RefactorPromptItem_OnClick(object? sender, RoutedEventArgs e)
    {
        _viewModelAccessor()?.OpenRefactorPrompt(_currentNode);
    }

    private void SetAsTreemapRootItem_OnClick(object? sender, RoutedEventArgs e)
    {
        _viewModelAccessor()?.SetTreemapRoot(_currentNode);
    }

    private void ExcludeItem_OnClick(object? sender, RoutedEventArgs e)
    {
        _viewModelAccessor()?.ExcludeNodeFromFolderCommand.Execute(_currentNode);
    }

    private async void CopyFullPathItem_OnClick(object? sender, RoutedEventArgs e)
    {
        await CopyTextToClipboardAsync(_currentNode?.FullPath);
    }

    private async void CopyRelativePathItem_OnClick(object? sender, RoutedEventArgs e)
    {
        var relativePath = _currentNode is null
            ? null
            : string.IsNullOrWhiteSpace(_currentNode.RelativePath)
                ? "."
                : _currentNode.RelativePath;
        await CopyTextToClipboardAsync(relativePath);
    }

    private async Task CopyTextToClipboardAsync(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var clipboard = _clipboardAccessor?.Invoke() ?? TopLevel.GetTopLevel(_clipboardHost)?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        await clipboard.SetTextAsync(text);
    }
}
