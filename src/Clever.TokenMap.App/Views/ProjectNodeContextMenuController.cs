using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.App.Views;

internal sealed class ProjectNodeContextMenuController
{
    private readonly ContextMenu _menu;
    private readonly Control _clipboardHost;
    private readonly Func<MainWindowViewModel?> _viewModelAccessor;
    private readonly Action<bool>? _setSuppressedState;
    private ProjectNode? _currentNode;

    public ProjectNodeContextMenuController(
        Control clipboardHost,
        Func<MainWindowViewModel?> viewModelAccessor,
        Action<bool>? setSuppressedState = null)
    {
        _clipboardHost = clipboardHost;
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
        _menu.PlacementTarget = owner;
        _menu.Open(owner);
    }

    private ContextMenu CreateMenu()
    {
        var menu = new ContextMenu
        {
            Placement = PlacementMode.Pointer,
        };
        menu.Items.Add(CreateMenuItem("Open", "FluentFolderOpen20Geometry", OpenItem_OnClick));
        menu.Items.Add(CreateMenuItem(GetRevealMenuHeader(), "FluentDesktop20RegularGeometry", RevealItem_OnClick));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("Copy Full Path", "TokenMapCopy20Geometry", CopyFullPathItem_OnClick));
        menu.Items.Add(CreateMenuItem("Copy Relative Path", iconResourceKey: null, CopyRelativePathItem_OnClick));
        return menu;
    }

    private MenuItem CreateMenuItem(string header, string? iconResourceKey, EventHandler<RoutedEventArgs> clickHandler)
    {
        var item = new MenuItem
        {
            Header = header,
        };
        if (!string.IsNullOrWhiteSpace(iconResourceKey))
        {
            item.Icon = CreateIcon(iconResourceKey);
        }

        item.Click += clickHandler;
        return item;
    }

    private Path CreateIcon(string iconResourceKey)
    {
        var icon = new Path
        {
            Width = 16,
            Height = 16,
            Stretch = Stretch.Uniform,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            IsHitTestVisible = false,
        };
        icon.Classes.Add("context-menu-icon");
        if (TryGetGeometryResource(iconResourceKey, out var geometry))
        {
            icon.Data = geometry;
        }

        return icon;
    }

    private bool TryGetGeometryResource(string iconResourceKey, out Geometry geometry)
    {
        if (Application.Current?.TryGetResource(iconResourceKey, _clipboardHost.ActualThemeVariant, out var applicationResource) == true &&
            applicationResource is Geometry applicationGeometry)
        {
            geometry = applicationGeometry;
            return true;
        }

        if (_clipboardHost.TryGetResource(iconResourceKey, out var hostResource) &&
            hostResource is Geometry hostGeometry)
        {
            geometry = hostGeometry;
            return true;
        }

        if (_clipboardHost.TryGetResource(iconResourceKey, _clipboardHost.ActualThemeVariant, out var themedHostResource) &&
            themedHostResource is Geometry themedHostGeometry)
        {
            geometry = themedHostGeometry;
            return true;
        }

        geometry = default!;
        return false;
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

        var clipboard = TopLevel.GetTopLevel(_clipboardHost)?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        await clipboard.SetTextAsync(text);
    }

    private static string GetRevealMenuHeader() =>
        OperatingSystem.IsWindows()
            ? "Reveal in Explorer"
            : OperatingSystem.IsMacOS()
                ? "Reveal in Finder"
                : "Reveal in File Manager";
}
