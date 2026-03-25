using System;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.Views;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Logging;
using Clever.TokenMap.Core.Paths;
using Clever.TokenMap.Core.Settings;
using Clever.TokenMap.Infrastructure.Analysis;
using Clever.TokenMap.Infrastructure.Caching;
using Clever.TokenMap.Infrastructure.Logging;
using Clever.TokenMap.Infrastructure.Paths;
using Clever.TokenMap.Infrastructure.Scanning;
using Clever.TokenMap.Infrastructure.Settings;
using Clever.TokenMap.Infrastructure.Text;
using Clever.TokenMap.Infrastructure.Tokenization;
using Microsoft.Extensions.DependencyInjection;

namespace Clever.TokenMap.App;

public static class AppComposition
{
    public static IServiceProvider CreateServiceProvider(App application)
    {
        ArgumentNullException.ThrowIfNull(application);

        var services = new ServiceCollection();
        ConfigureServices(services, application);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    internal static void ConfigureServices(IServiceCollection services, App application)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(application);

        services.AddSingleton<IAppSettingsStore, JsonAppSettingsStore>();
        services.AddSingleton<IFolderSettingsStore, JsonFolderSettingsStore>();
        services.AddSingleton(sp => sp.GetRequiredService<IAppSettingsStore>().Load());
        services.AddSingleton<ApplicationThemeService>(_ => new ApplicationThemeService(application));
        services.AddSingleton<IThemeService>(sp => sp.GetRequiredService<ApplicationThemeService>());
        services.AddSingleton<IFolderPathService, FileSystemFolderPathService>();
        services.AddSingleton<IPathNormalizer, PathNormalizer>();
        services.AddSingleton<IPathShellService, PathShellService>();
        services.AddSingleton<IAppLoggerFactory>(sp =>
            new AppLoggerFactory(sp.GetRequiredService<AppSettings>().Logging));
        services.AddSingleton<IProjectAnalyzer>(sp =>
            CreateDefaultProjectAnalyzer(sp.GetRequiredService<IAppLoggerFactory>()));
        services.AddSingleton<IFolderPickerService>(sp =>
            new WindowFolderPickerService(() => sp.GetRequiredService<MainWindow>()));
        services.AddSingleton<ISettingsCoordinator>(sp =>
            new SettingsCoordinator(
                sp.GetRequiredService<IAppSettingsStore>(),
                sp.GetRequiredService<IFolderSettingsStore>(),
                sp.GetRequiredService<IThemeService>(),
                sp.GetRequiredService<AppSettings>(),
                sp.GetRequiredService<IAppLoggerFactory>().CreateLogger<SettingsCoordinator>(),
                pathNormalizer: sp.GetRequiredService<IPathNormalizer>()));
        services.AddSingleton<TreemapNavigationState>();
        services.AddSingleton<IAnalysisSessionController>(sp =>
            CreateAnalysisSessionController(
                sp.GetRequiredService<IProjectAnalyzer>(),
                sp.GetRequiredService<IFolderPickerService>(),
                sp.GetRequiredService<IFolderPathService>(),
                sp.GetRequiredService<ISettingsCoordinator>(),
                sp.GetRequiredService<IAppLoggerFactory>()));
        services.AddSingleton(sp =>
            CreateMainWindowViewModel(
                sp.GetRequiredService<IAnalysisSessionController>(),
                sp.GetRequiredService<TreemapNavigationState>(),
                sp.GetRequiredService<ISettingsCoordinator>(),
                sp.GetRequiredService<IFolderPathService>(),
                sp.GetRequiredService<IPathShellService>()));
        services.AddSingleton(sp => new MainWindow(sp.GetRequiredService<MainWindowViewModel>()));
    }

    public static IProjectAnalyzer CreateDefaultProjectAnalyzer(IAppLoggerFactory? loggerFactory = null) =>
        new ProjectAnalyzer(
            new FileSystemProjectScanner(logger: loggerFactory?.CreateLogger<FileSystemProjectScanner>()),
            new HeuristicTextFileDetector(),
            new MicrosoftMlTokenCounter(),
            new InMemoryCacheStore(),
            loggerFactory: loggerFactory);

    public static AnalysisSessionController CreateAnalysisSessionController(
        IProjectAnalyzer projectAnalyzer,
        IFolderPickerService folderPickerService,
        IFolderPathService folderPathService,
        ISettingsCoordinator? settingsCoordinator = null,
        IAppLoggerFactory? loggerFactory = null) =>
        new(
            projectAnalyzer,
            folderPickerService,
            folderPathService,
            loggerFactory?.CreateLogger<AnalysisSessionController>(),
            settingsCoordinator);

    public static MainWindowViewModel CreateMainWindowViewModel(
        IAnalysisSessionController analysisSessionController,
        ISettingsCoordinator settingsCoordinator,
        IFolderPathService folderPathService,
        IPathShellService pathShellService) =>
        CreateMainWindowViewModel(
            analysisSessionController,
            treemapNavigationState: null,
            settingsCoordinator,
            folderPathService,
            pathShellService);

    public static MainWindowViewModel CreateMainWindowViewModel(
        IAnalysisSessionController analysisSessionController,
        TreemapNavigationState? treemapNavigationState,
        ISettingsCoordinator settingsCoordinator,
        IFolderPathService folderPathService,
        IPathShellService pathShellService) =>
        new(
            analysisSessionController,
            treemapNavigationState ?? new TreemapNavigationState(),
            settingsCoordinator,
            folderPathService,
            pathShellService);
}
