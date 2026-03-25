using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Clever.TokenMap.Core.Logging;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.Views;
using Clever.TokenMap.Core.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Clever.TokenMap.App;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

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
            desktop.Exit += (_, _) =>
            {
                Task.Run(() => settingsCoordinator.FlushAsync()).GetAwaiter().GetResult();
                (_serviceProvider as IDisposable)?.Dispose();
                _serviceProvider = null;
            };
            loggerFactory.CreateLogger<App>().LogInformation(
                $"Application starting. OS='{Environment.OSVersion}', framework='{Environment.Version}', logLevel='{appSettings.Logging.MinLevel}', themePreference='{appSettings.Appearance.ThemePreference}', systemTheme='{themeService.CurrentSystemTheme}'.");
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
}
