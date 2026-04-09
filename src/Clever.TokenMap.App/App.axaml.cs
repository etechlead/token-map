using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.State;
using Clever.TokenMap.App.Views;
using Clever.TokenMap.Core.Diagnostics;
using Clever.TokenMap.Core.Logging;
using Clever.TokenMap.Core.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Clever.TokenMap.App;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    private IAppIssueReporter? _issueReporter;
    private IAppLogger? _logger;
    private LocalizationState? _localization;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            var serviceProvider = AppComposition.CreateServiceProvider(this);
            _serviceProvider = serviceProvider;
            var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
            var appSettings = serviceProvider.GetRequiredService<AppSettings>();
            var themeService = serviceProvider.GetRequiredService<ApplicationThemeService>();
            var settingsCoordinator = serviceProvider.GetRequiredService<ISettingsCoordinator>();
            var loggerFactory = serviceProvider.GetRequiredService<IAppLoggerFactory>();
            _issueReporter = serviceProvider.GetRequiredService<IAppIssueReporter>();
            _localization = serviceProvider.GetRequiredService<LocalizationState>();
            _logger = loggerFactory.CreateLogger<App>();
            RegisterGlobalExceptionHandlers();
            desktop.Exit += (_, _) =>
            {
                Task.Run(() => settingsCoordinator.FlushAsync()).GetAwaiter().GetResult();
                UnregisterGlobalExceptionHandlers();
                (_serviceProvider as IDisposable)?.Dispose();
                _serviceProvider = null;
                _issueReporter = null;
                _logger = null;
                _localization = null;
            };
            _logger.LogInformation(
                "Application starting.",
                eventCode: "app.starting",
                context: AppIssueContext.Create(
                    ("OperatingSystem", Environment.OSVersion),
                    ("FrameworkVersion", Environment.Version),
                    ("LogLevel", appSettings.Logging.MinLevel),
                    ("ThemePreference", appSettings.Appearance.ThemePreference),
                    ("SystemTheme", themeService.CurrentSystemTheme)));
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    private void RegisterGlobalExceptionHandlers()
    {
        Dispatcher.UIThread.UnhandledExceptionFilter += DispatcherOnUnhandledExceptionFilter;
        Dispatcher.UIThread.UnhandledException += DispatcherOnUnhandledException;
        TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
    }

    private void UnregisterGlobalExceptionHandlers()
    {
        Dispatcher.UIThread.UnhandledExceptionFilter -= DispatcherOnUnhandledExceptionFilter;
        Dispatcher.UIThread.UnhandledException -= DispatcherOnUnhandledException;
        TaskScheduler.UnobservedTaskException -= TaskSchedulerOnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException -= CurrentDomainOnUnhandledException;
    }

    private static void DispatcherOnUnhandledExceptionFilter(
        object? sender,
        DispatcherUnhandledExceptionFilterEventArgs e)
    {
        e.RequestCatch = !IsProcessCorruptingException(e.Exception);
    }

    private void DispatcherOnUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (_issueReporter is null)
        {
            return;
        }

        _issueReporter.Report(CreateDispatcherUnhandledIssue(e.Exception, _localization));
        e.Handled = true;
    }

    private void TaskSchedulerOnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _issueReporter?.Report(new AppIssue
        {
            Code = "app.unobserved_task_exception",
            UserMessage = _localization?.UnobservedTaskUserMessage ?? "A background task failed. TokenMap wrote diagnostic details to the log.",
            TechnicalMessage = "An unobserved task exception reached the application boundary.",
            Exception = e.Exception,
            Context = AppIssueContext.Create(("Origin", "TaskScheduler.UnobservedTaskException")),
        });
        e.SetObserved();
    }

    private void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        _issueReporter?.Report(new AppIssue
        {
            Code = "app.domain_unhandled",
            UserMessage = _localization?.DomainUnhandledUserMessage ?? "TokenMap hit an unrecoverable error. Review the log details and restart the app.",
            TechnicalMessage = "An unhandled AppDomain exception reached the process boundary.",
            Exception = exception,
            Context = AppIssueContext.Create(
                ("Origin", "AppDomain.CurrentDomain.UnhandledException"),
                ("IsTerminating", e.IsTerminating)),
            IsFatal = true,
        });
    }

    private static bool IsProcessCorruptingException(Exception exception) =>
        exception is AccessViolationException
            or BadImageFormatException
            or CannotUnloadAppDomainException
            or InvalidProgramException
            or OutOfMemoryException;

    internal static AppIssue CreateDispatcherUnhandledIssue(Exception exception, LocalizationState? localization = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return new AppIssue
        {
            Code = "app.dispatcher_unhandled",
            UserMessage = localization?.DispatcherUnhandledUserMessage ?? "TokenMap hit an unexpected error. Review the log details before continuing.",
            TechnicalMessage = "An unhandled dispatcher exception reached the application boundary.",
            Exception = exception,
            Context = AppIssueContext.Create(("Origin", "Dispatcher.UIThread.UnhandledException")),
        };
    }
}
