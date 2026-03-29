using System;
using Avalonia;

namespace Clever.TokenMap.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            // Avoid the WinUI compositor path on Windows. It intermittently crashes during
            // startup on this machine in Avalonia.Win32.WinRT composition setup.
            .With(new Win32PlatformOptions
            {
                CompositionMode =
                [
                    Win32CompositionMode.RedirectionSurface,
                ],
            })
            .With(new X11PlatformOptions
            {
                WmClass = "tokenmap",
            })
            .UsePlatformDetect()
            .LogToTrace();
}
