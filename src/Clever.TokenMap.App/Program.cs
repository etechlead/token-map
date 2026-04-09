using System;
using System.Globalization;
using System.Resources;
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
    private static readonly ResourceManager ResourceManager =
        new("Clever.TokenMap.App.Resources.AppStrings", typeof(Program).Assembly);

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
            .With(CreateX11PlatformOptions())
            .UsePlatformDetect()
            .LogToTrace();

    internal static X11PlatformOptions CreateX11PlatformOptions() =>
        new()
        {
            WmClass = "tokenmap",
            // TokenMap doesn't export a native global menu, and some Linux desktops do not
            // provide the AppMenu registrar service that Avalonia probes by default.
            UseDBusMenu = false,
        };

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
            _ = MessageBoxW(IntPtr.Zero, message, GetResourceString("AppTitle", "TokenMap"), 0x00000010);
            return;
        }

        Console.Error.WriteLine(message);
    }

    internal static string BuildStartupFailureMessage(string logsDirectoryPath) =>
        $"{GetResourceString("StartupFailedToStart", "TokenMap failed to start.")}{Environment.NewLine}{Environment.NewLine}" +
        $"{GetResourceString("StartupDiagnosticDetailsWrittenTo", "Diagnostic details were written to:")}{Environment.NewLine}{logsDirectoryPath}";

    private static string GetResourceString(string key, string fallback)
    {
        try
        {
            var value = ResourceManager.GetString(key, CultureInfo.CurrentUICulture);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
        catch (MissingManifestResourceException)
        {
            return fallback;
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
