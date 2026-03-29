using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Clever.TokenMap.Tests.Headless.Support.TestAppBuilder))]

namespace Clever.TokenMap.Tests.Headless.Support;

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
