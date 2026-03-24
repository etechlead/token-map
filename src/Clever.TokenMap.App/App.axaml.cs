using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.App.Views;
using Clever.TokenMap.Infrastructure.Analysis;
using Clever.TokenMap.Infrastructure.Caching;
using Clever.TokenMap.Infrastructure.Logging;
using Clever.TokenMap.Infrastructure.Paths;
using Clever.TokenMap.Infrastructure.Scanning;
using Clever.TokenMap.Infrastructure.Settings;
using Clever.TokenMap.Infrastructure.Text;
using Clever.TokenMap.Infrastructure.Tokenization;

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
                settingsCoordinator.FlushAsync().GetAwaiter().GetResult();
                loggerFactory.Dispose();
            };
            loggerFactory.CreateLogger<App>().LogInformation(
                $"Application starting. OS='{Environment.OSVersion}', framework='{Environment.Version}', logLevel='{appSettings.Logging.MinLevel}', themePreference='{appSettings.Appearance.ThemePreference}', systemTheme='{themeService.CurrentSystemTheme}'.");
            var analyzer = new ProjectAnalyzer(
                new FileSystemProjectScanner(logger: loggerFactory.CreateLogger<FileSystemProjectScanner>()),
                new HeuristicTextFileDetector(),
                new MicrosoftMlTokenCounter(),
                new InMemoryCacheStore(),
                loggerFactory: loggerFactory);
            var analysisSessionController = new AnalysisSessionController(
                analyzer,
                new WindowFolderPickerService(mainWindow),
                folderPathService,
                loggerFactory.CreateLogger<AnalysisSessionController>(),
                settingsCoordinator);
            mainWindow.DataContext = new MainWindowViewModel(
                analysisSessionController,
                new TreemapNavigationState(),
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
