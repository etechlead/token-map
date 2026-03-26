using Avalonia;
using Avalonia.Headless;
using Avalonia.Skia;

namespace Clever.TokenMap.VisualHarness;

internal static class VisualHarnessAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Clever.TokenMap.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false,
            });
}
