using System;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.State;
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

        services.AddSingleton<PathNormalizer>();
        services.AddSingleton<IAppStoragePaths>(sp =>
            new TokenMapAppDataPaths(pathNormalizer: sp.GetRequiredService<PathNormalizer>()));
        services.AddSingleton(sp =>
            new AppLoggerFactory(
                AppSettings.CreateDefault().Logging,
                appStoragePaths: sp.GetRequiredService<IAppStoragePaths>()));
        services.AddSingleton<IAppSettingsStore>(sp =>
            new JsonAppSettingsStore(
                appStoragePaths: sp.GetRequiredService<IAppStoragePaths>(),
                logger: sp.GetRequiredService<AppLoggerFactory>().CreateLogger<JsonAppSettingsStore>()));
        services.AddSingleton<IFolderSettingsStore>(sp =>
            new JsonFolderSettingsStore(
                pathNormalizer: sp.GetRequiredService<PathNormalizer>(),
                appStoragePaths: sp.GetRequiredService<IAppStoragePaths>(),
                logger: sp.GetRequiredService<AppLoggerFactory>().CreateLogger<JsonFolderSettingsStore>()));
        services.AddSingleton(sp => sp.GetRequiredService<IAppSettingsStore>().Load());
        services.AddSingleton<ApplicationThemeService>(_ => new ApplicationThemeService(application));
        services.AddSingleton<IThemeService>(sp => sp.GetRequiredService<ApplicationThemeService>());
        services.AddSingleton<IFolderPathService, FileSystemFolderPathService>();
        services.AddSingleton<IPathShellService>(_ => PathShellService.CreateForCurrentPlatform());
        services.AddSingleton<IAppLoggerFactory>(sp =>
            new AppLoggerFactory(
                sp.GetRequiredService<AppSettings>().Logging,
                appStoragePaths: sp.GetRequiredService<IAppStoragePaths>()));
        services.AddSingleton<IProjectScanner>(sp =>
            new FileSystemProjectScanner(
                pathNormalizer: sp.GetRequiredService<PathNormalizer>(),
                logger: sp.GetRequiredService<IAppLoggerFactory>().CreateLogger<FileSystemProjectScanner>()));
        services.AddSingleton<ITextFileDetector, HeuristicTextFileDetector>();
        services.AddSingleton<ITokenCounter, MicrosoftMlTokenCounter>();
        services.AddSingleton<ICacheStore>(sp =>
            new InMemoryCacheStore(sp.GetRequiredService<PathNormalizer>()));
        services.AddSingleton<IProjectAnalyzer>(sp =>
            new ProjectAnalyzer(
                sp.GetRequiredService<IProjectScanner>(),
                sp.GetRequiredService<ITextFileDetector>(),
                sp.GetRequiredService<ITokenCounter>(),
                sp.GetRequiredService<ICacheStore>(),
                loggerFactory: sp.GetRequiredService<IAppLoggerFactory>()));
        services.AddSingleton<IFolderPickerService>(sp =>
            new WindowFolderPickerService(() => sp.GetRequiredService<MainWindow>()));
        services.AddSingleton<ISettingsCoordinator>(sp =>
            new SettingsCoordinator(
                sp.GetRequiredService<IAppSettingsStore>(),
                sp.GetRequiredService<IFolderSettingsStore>(),
                sp.GetRequiredService<IThemeService>(),
                sp.GetRequiredService<AppSettings>(),
                sp.GetRequiredService<IAppLoggerFactory>().CreateLogger<SettingsCoordinator>(),
                pathNormalizer: sp.GetRequiredService<PathNormalizer>()));
        services.AddSingleton<TreemapNavigationState>();
        services.AddSingleton<IAnalysisSessionController>(sp =>
            new AnalysisSessionController(
                sp.GetRequiredService<IProjectAnalyzer>(),
                sp.GetRequiredService<IFolderPickerService>(),
                sp.GetRequiredService<IFolderPathService>(),
                sp.GetRequiredService<IAppLoggerFactory>().CreateLogger<AnalysisSessionController>(),
                sp.GetRequiredService<ISettingsCoordinator>()));
        services.AddSingleton(sp =>
            new MainWindowViewModel(
                sp.GetRequiredService<IAnalysisSessionController>(),
                sp.GetRequiredService<TreemapNavigationState>(),
                sp.GetRequiredService<ISettingsCoordinator>(),
                sp.GetRequiredService<IFolderPathService>(),
                sp.GetRequiredService<IPathShellService>()));
        services.AddSingleton(sp => new MainWindow(sp.GetRequiredService<MainWindowViewModel>()));
    }
}
