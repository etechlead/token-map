using System;
using System.Runtime.InteropServices;
using Avalonia;
using Clever.TokenMap.Core.Diagnostics;
using Clever.TokenMap.Core.Logging;
using Clever.TokenMap.Core.Settings;
using Clever.TokenMap.Infrastructure.Logging;
using Clever.TokenMap.Infrastructure.Settings;

namespace Clever.TokenMap.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception exception)
        {
            HandleStartupFailure(exception);
        }
    }

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

    private static void HandleStartupFailure(Exception exception)
    {
        var storagePaths = new TokenMapAppDataPaths();
        using var loggerFactory = new AppLoggerFactory(
            AppSettings.CreateDefault().Logging,
            appStoragePaths: storagePaths);
        var logger = loggerFactory.CreateLogger<App>();
        logger.LogCritical(
            exception,
            "Application startup failed.",
            eventCode: "app.startup_failed",
            context: AppIssueContext.Create(("LogsDirectoryPath", storagePaths.GetLogsDirectoryPath())));

        ShowStartupFailureDialog(storagePaths.GetLogsDirectoryPath());
        Environment.ExitCode = 1;
    }

    private static void ShowStartupFailureDialog(string logsDirectoryPath)
    {
        var message = BuildStartupFailureMessage(logsDirectoryPath);
        if (OperatingSystem.IsWindows())
        {
            _ = MessageBoxW(IntPtr.Zero, message, "TokenMap", 0x00000010);
            return;
        }

        Console.Error.WriteLine(message);
    }

    internal static string BuildStartupFailureMessage(string logsDirectoryPath) =>
        $"TokenMap failed to start.{Environment.NewLine}{Environment.NewLine}" +
        $"Diagnostic details were written to:{Environment.NewLine}{logsDirectoryPath}";

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
