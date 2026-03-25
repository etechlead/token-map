using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Infrastructure.Analysis;
using Clever.TokenMap.Infrastructure.Caching;
using Clever.TokenMap.Infrastructure.Logging;
using Clever.TokenMap.Infrastructure.Scanning;
using Clever.TokenMap.Infrastructure.Text;
using Clever.TokenMap.Infrastructure.Tokenization;

namespace Clever.TokenMap.App;

public static class AppComposition
{
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
        new(
            analysisSessionController,
            new TreemapNavigationState(),
            settingsCoordinator,
            folderPathService,
            pathShellService);
}
