using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;

[assembly: AvaloniaTestApplication(typeof(Clever.TokenMap.HeadlessTests.TestAppBuilder))]

namespace Clever.TokenMap.HeadlessTests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Clever.TokenMap.App.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
