using Avalonia;
using Avalonia.Headless;
using Clever.TokenMap.Tests.Headless.Support;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Clever.TokenMap.Tests.Headless.Support;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false,
            });
}
