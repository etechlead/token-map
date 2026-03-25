using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Models;
using static Clever.TokenMap.HeadlessTests.HeadlessTestSupport;

namespace Clever.TokenMap.HeadlessTests;

public sealed class ProjectNodeContextMenuControllerTests
{
    [AvaloniaFact]
    public async Task Show_EnablesTreemapRootAndExclude_ForEligibleDirectoryNode()
    {
        var viewModel = await CreateOpenFolderViewModelAsync(CreateNestedSnapshot());
        var node = viewModel.Tree.VisibleNodes.Single(visibleNode => visibleNode.Node.Id == "src").Node;
        var window = CreateHostWindow();
        var controller = CreateController(window, viewModel);

        InvokeControllerMethod(controller, "Show", window, node);

        var menu = GetMenu(controller);
        var setAsTreemapRootItem = GetMenuItem(menu, "Set as Treemap Root");
        var excludeItem = GetMenuItem(menu, "Exclude from Scan");

        Assert.True(setAsTreemapRootItem.IsVisible);
        Assert.True(setAsTreemapRootItem.IsEnabled);
        Assert.True(excludeItem.IsVisible);
        Assert.True(excludeItem.IsEnabled);
    }

    [AvaloniaFact]
    public async Task Show_HidesTreemapRootAndExclude_ForRootNode()
    {
        var viewModel = await CreateOpenFolderViewModelAsync(CreateNestedSnapshot());
        var node = viewModel.Tree.VisibleNodes.Single(visibleNode => visibleNode.Node.Id == "/").Node;
        var window = CreateHostWindow();
        var controller = CreateController(window, viewModel);

        InvokeControllerMethod(controller, "Show", window, node);

        var menu = GetMenu(controller);
        var setAsTreemapRootItem = GetMenuItem(menu, "Set as Treemap Root");
        var excludeItem = GetMenuItem(menu, "Exclude from Scan");

        Assert.False(setAsTreemapRootItem.IsVisible);
        Assert.False(setAsTreemapRootItem.IsEnabled);
        Assert.False(excludeItem.IsVisible);
        Assert.False(excludeItem.IsEnabled);
    }

    [AvaloniaFact]
    public async Task CopyFullPathItem_CopiesCurrentNodeFullPath()
    {
        var viewModel = await CreateOpenFolderViewModelAsync(CreateSnapshot());
        var node = Assert.Single(viewModel.Tree.VisibleNodes, visibleNode => visibleNode.Node.Id == "Program.cs").Node;
        var window = CreateHostWindow();
        var clipboard = CreateClipboardCapture();
        var controller = CreateController(window, viewModel, clipboardAccessor: () => clipboard.Clipboard);

        InvokeControllerMethod(controller, "Show", window, node);
        InvokePrivateClick(controller, "CopyFullPathItem_OnClick");

        await WaitForClipboardTextAsync(clipboard, node.FullPath);
    }

    [AvaloniaFact]
    public async Task CopyRelativePathItem_CopiesCurrentNodeRelativePath()
    {
        var viewModel = await CreateOpenFolderViewModelAsync(CreateSnapshot());
        var node = Assert.Single(viewModel.Tree.VisibleNodes, visibleNode => visibleNode.Node.Id == "Program.cs").Node;
        var window = CreateHostWindow();
        var clipboard = CreateClipboardCapture();
        var controller = CreateController(window, viewModel, clipboardAccessor: () => clipboard.Clipboard);

        InvokeControllerMethod(controller, "Show", window, node);
        InvokePrivateClick(controller, "CopyRelativePathItem_OnClick");

        await WaitForClipboardTextAsync(clipboard, node.RelativePath);
    }

    [AvaloniaFact]
    public async Task CopyRelativePathItem_UsesDot_ForRootNode()
    {
        var viewModel = await CreateOpenFolderViewModelAsync(CreateNestedSnapshot());
        var node = viewModel.Tree.VisibleNodes.Single(visibleNode => visibleNode.Node.Id == "/").Node;
        var window = CreateHostWindow();
        var clipboard = CreateClipboardCapture();
        var controller = CreateController(window, viewModel, clipboardAccessor: () => clipboard.Clipboard);

        InvokeControllerMethod(controller, "Show", window, node);
        InvokePrivateClick(controller, "CopyRelativePathItem_OnClick");

        await WaitForClipboardTextAsync(clipboard, ".");
    }

    [AvaloniaFact]
    public async Task Show_ResetsSuppressedState_WhenMenuCloses()
    {
        var viewModel = await CreateOpenFolderViewModelAsync(CreateNestedSnapshot());
        var node = viewModel.Tree.VisibleNodes.Single(visibleNode => visibleNode.Node.Id == "src").Node;
        var window = CreateHostWindow();
        var suppressionStates = new List<bool>();
        var controller = CreateController(window, viewModel, suppressionStates.Add);

        InvokeControllerMethod(controller, "Show", window, node);

        Assert.Single(suppressionStates);
        Assert.True(suppressionStates[0]);

        var menu = GetMenu(controller);
        CloseMenu(menu);

        Assert.Equal(2, suppressionStates.Count);
        Assert.True(suppressionStates[0]);
        Assert.False(suppressionStates[1]);
    }

    private static async Task<MainWindowViewModel> CreateOpenFolderViewModelAsync(ProjectSnapshot snapshot)
    {
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(snapshot));
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);
        return viewModel;
    }

    private static Window CreateHostWindow()
    {
        var window = new Window
        {
            Content = new Border(),
        };

        window.Show();
        return window;
    }

    private static object CreateController(
        Control clipboardHost,
        MainWindowViewModel viewModel,
        Action<bool>? setSuppressedState = null,
        Func<IClipboard?>? clipboardAccessor = null)
    {
        var controllerType = typeof(MainWindowViewModel).Assembly.GetType(
            "Clever.TokenMap.App.Views.ProjectNodeContextMenuController",
            throwOnError: true)!;

        return Activator.CreateInstance(
            controllerType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object?[] { clipboardHost, new Func<MainWindowViewModel?>(() => viewModel), setSuppressedState, clipboardAccessor },
            culture: null)!;
    }

    private static ContextMenu GetMenu(object controller)
    {
        var menuField = controller.GetType().GetField("_menu", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(menuField);
        return Assert.IsType<ContextMenu>(menuField!.GetValue(controller));
    }

    private static MenuItem GetMenuItem(ContextMenu menu, string header)
    {
        return Assert.Single(
            menu.Items.OfType<MenuItem>(),
            item => string.Equals(item.Header?.ToString(), header, StringComparison.Ordinal));
    }

    private static void InvokePrivateClick(object controller, string methodName)
    {
        var method = controller.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(controller, new object?[] { null, new RoutedEventArgs() });
    }

    private static void InvokeControllerMethod(object controller, string methodName, params object?[] args)
    {
        var method = controller.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(controller, args);
    }

    private static void CloseMenu(ContextMenu menu)
    {
        if (TryInvokeMenuMethod(menu, "Close"))
        {
            return;
        }

        if (TryInvokeMenuMethod(menu, "OnClosed", EventArgs.Empty))
        {
            return;
        }

        if (TryInvokeMenuMethod(menu, "OnClosed", new RoutedEventArgs()))
        {
            return;
        }

        var isOpenProperty = menu.GetType().GetProperty("IsOpen", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (isOpenProperty is not null)
        {
            isOpenProperty.SetValue(menu, false);
            return;
        }

        Assert.Fail("Unable to close the menu through reflection.");
    }

    private static bool TryInvokeMenuMethod(ContextMenu menu, string methodName, params object?[] args)
    {
        var method = menu.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method is null)
        {
            return false;
        }

        try
        {
            method.Invoke(menu, args);
            return true;
        }
        catch (TargetInvocationException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static ClipboardCapture CreateClipboardCapture()
    {
        var clipboard = DispatchProxy.Create<IClipboard, ClipboardCaptureProxy>();
        return new ClipboardCapture
        {
            Clipboard = clipboard,
            Proxy = (ClipboardCaptureProxy)(object)clipboard,
        };
    }

    private static async Task WaitForClipboardTextAsync(ClipboardCapture clipboard, string expectedText)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (string.Equals(clipboard.Proxy.Text, expectedText, StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.Equal(expectedText, clipboard.Proxy.Text);
    }

    private sealed class ClipboardCapture
    {
        public required IClipboard Clipboard { get; init; }

        public required ClipboardCaptureProxy Proxy { get; init; }
    }

#pragma warning disable CA1852
    private class ClipboardCaptureProxy : DispatchProxy
    {
        public string? Text { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case "SetTextAsync":
                    Text = args is { Length: > 0 } ? args[0]?.ToString() : null;
                    return Task.CompletedTask;
                case "ClearAsync":
                    Text = null;
                    return Task.CompletedTask;
                case "GetTextAsync":
                    return Task.FromResult(Text);
                default:
                    return targetMethod?.ReturnType == typeof(Task)
                        ? Task.CompletedTask
                        : targetMethod?.ReturnType.IsValueType == true
                            ? Activator.CreateInstance(targetMethod.ReturnType)
                            : null;
            }
        }
    }
#pragma warning restore CA1852
}
