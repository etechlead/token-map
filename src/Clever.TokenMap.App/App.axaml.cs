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
using Clever.TokenMap.Infrastructure.Scanning;
using Clever.TokenMap.Infrastructure.Settings;
using Clever.TokenMap.Infrastructure.Text;
using Clever.TokenMap.Infrastructure.Tokenization;
using Clever.TokenMap.Infrastructure.Tokei;

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
            var appSettings = appSettingsStore.Load();
            var themeService = new ApplicationThemeService(this);
            themeService.ApplyThemePreference(appSettings.Appearance.ThemePreference);
            var loggerFactory = new AppLoggerFactory(appSettings.Logging);
            desktop.Exit += (_, _) => loggerFactory.Dispose();
            loggerFactory.CreateLogger<App>().LogInformation(
                $"Application starting. OS='{Environment.OSVersion}', framework='{Environment.Version}', logLevel='{appSettings.Logging.MinLevel}', themePreference='{appSettings.Appearance.ThemePreference}', systemTheme='{themeService.CurrentSystemTheme}'.");
            var analyzer = new ProjectAnalyzer(
                new FileSystemProjectScanner(logger: loggerFactory.CreateLogger<FileSystemProjectScanner>()),
                new HeuristicTextFileDetector(),
                new MicrosoftMlTokenCounter(),
                new ProcessTokeiRunner(logger: loggerFactory.CreateLogger<ProcessTokeiRunner>()),
                new InMemoryCacheStore(),
                loggerFactory: loggerFactory);

            mainWindow.DataContext = new MainWindowViewModel(
                analyzer,
                new WindowFolderPickerService(mainWindow),
                appSettingsStore,
                themeService,
                loggerFactory.CreateLogger<MainWindowViewModel>());
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
