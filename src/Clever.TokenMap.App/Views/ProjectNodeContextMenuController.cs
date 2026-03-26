using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.App.Views;

internal sealed class ProjectNodeContextMenuController
{
    private readonly ContextMenu _menu;
    private readonly Control _clipboardHost;
    private readonly Func<IClipboard?>? _clipboardAccessor;
    private readonly Func<MainWindowViewModel?> _viewModelAccessor;
    private readonly Action<bool>? _setSuppressedState;
    private MenuItem? _excludeItem;
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
        menu.Items.Add(CreateMenuItem("Open", FluentIconGeometry.FolderOpen16Regular, OpenItem_OnClick));
        _revealItem = CreateMenuItem(string.Empty, FluentIconGeometry.DesktopMac16Regular, RevealItem_OnClick);
        menu.Items.Add(_revealItem);
        _setAsTreemapRootItem = CreateMenuItem("Set as Treemap Root", FluentIconGeometry.TargetArrow16Regular, SetAsTreemapRootItem_OnClick);
        menu.Items.Add(_setAsTreemapRootItem);
        _excludeItem = CreateMenuItem("Exclude from Scan", FluentIconGeometry.SubtractCircle16Regular, ExcludeItem_OnClick);
        menu.Items.Add(_excludeItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("Copy Full Path", FluentIconGeometry.DocumentCopy16Regular, CopyFullPathItem_OnClick));
        menu.Items.Add(CreateMenuItem("Copy Relative Path", icon: null, CopyRelativePathItem_OnClick));
        return menu;
    }

    private static MenuItem CreateMenuItem(string header, string iconResourceKey, EventHandler<RoutedEventArgs> clickHandler) =>
        CreateMenuItem(header, CreateMenuIcon(iconResourceKey), clickHandler);

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

    private static Path CreateMenuIcon(string iconResourceKey)
    {
        return FluentIconGeometry.CreatePathIcon(iconResourceKey, "context-menu-icon", 18, 18);
    }

    private void UpdateMenuState()
    {
        var viewModel = _viewModelAccessor();
        var canSetTreemapRoot = viewModel?.CanSetTreemapRoot(_currentNode) == true;
        var canExclude = viewModel?.CanExcludeNodeFromFolder(_currentNode) == true;
        if (_revealItem is not null)
        {
            _revealItem.Header = viewModel?.RevealMenuHeader ?? "Reveal";
        }

        if (_setAsTreemapRootItem is null || _excludeItem is null)
        {
            return;
        }

        _setAsTreemapRootItem.IsVisible = canSetTreemapRoot;
        _setAsTreemapRootItem.IsEnabled = canSetTreemapRoot;
        _excludeItem.IsVisible = canExclude;
        _excludeItem.IsEnabled = canExclude;
    }

    private async void OpenItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModelAccessor() is not { } viewModel)
        {
            return;
        }

        await viewModel.OpenNodeAsync(_currentNode);
    }

    private async void RevealItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModelAccessor() is not { } viewModel)
        {
            return;
        }

        await viewModel.RevealNodeAsync(_currentNode);
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
