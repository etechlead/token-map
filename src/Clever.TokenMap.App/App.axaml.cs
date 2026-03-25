using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.Views;
using Clever.TokenMap.Infrastructure.Logging;
using Clever.TokenMap.Infrastructure.Paths;
using Clever.TokenMap.Infrastructure.Settings;

namespace Clever.TokenMap.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            var mainWindow = new MainWindow();
            var appSettingsStore = new JsonAppSettingsStore();
            var folderSettingsStore = new JsonFolderSettingsStore();
            var appSettings = appSettingsStore.Load();
            var themeService = new ApplicationThemeService(this);
            var folderPathService = new FileSystemFolderPathService();
            var loggerFactory = new AppLoggerFactory(appSettings.Logging);
            var settingsCoordinator = new SettingsCoordinator(
                appSettingsStore,
                folderSettingsStore,
                themeService,
                appSettings,
                loggerFactory.CreateLogger<SettingsCoordinator>());
            desktop.Exit += (_, _) =>
            {
                Task.Run(() => settingsCoordinator.FlushAsync()).GetAwaiter().GetResult();
                loggerFactory.Dispose();
            };
            loggerFactory.CreateLogger<App>().LogInformation(
                $"Application starting. OS='{Environment.OSVersion}', framework='{Environment.Version}', logLevel='{appSettings.Logging.MinLevel}', themePreference='{appSettings.Appearance.ThemePreference}', systemTheme='{themeService.CurrentSystemTheme}'.");
            var analyzer = AppComposition.CreateDefaultProjectAnalyzer(loggerFactory);
            var analysisSessionController = AppComposition.CreateAnalysisSessionController(
                analyzer,
                new WindowFolderPickerService(mainWindow),
                folderPathService,
                settingsCoordinator,
                loggerFactory);
            mainWindow.DataContext = AppComposition.CreateMainWindowViewModel(
                analysisSessionController,
                settingsCoordinator,
                folderPathService,
                new PathShellService());
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
