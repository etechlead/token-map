using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Skia;

[assembly: AvaloniaTestApplication(typeof(Clever.TokenMap.HeadlessTests.TestAppBuilder))]

namespace Clever.TokenMap.HeadlessTests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Clever.TokenMap.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false,
            });
}
