using Avalonia;
using Avalonia.Headless;

namespace Clever.TokenMap.VisualHarness;

internal static class VisualHarnessAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false,
            });
}
