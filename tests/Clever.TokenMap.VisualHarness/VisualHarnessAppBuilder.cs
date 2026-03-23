using Avalonia;
using Avalonia.Headless;
using Avalonia.Skia;
using Clever.TokenMap.App;

internal static class VisualHarnessAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false,
            });
}
