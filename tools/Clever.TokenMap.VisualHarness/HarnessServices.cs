using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.State;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Diagnostics;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Logging;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Infrastructure.Analysis;
using Clever.TokenMap.Infrastructure.Caching;
using Clever.TokenMap.Infrastructure.Scanning;
using Clever.TokenMap.Infrastructure.Text;
using Clever.TokenMap.Infrastructure.Tokenization;

namespace Clever.TokenMap.VisualHarness;

internal static class HarnessComposition
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
        IPathShellService pathShellService)
    {
        var appIssueState = new AppIssueState();

        return MainWindowViewModelFactory.Create(
                new MainWindowViewModelFactoryDependencies(
                    analysisSessionController,
                    settingsCoordinator,
                    folderPathService,
                    pathShellService,
                    NullAppIssueReporter.Instance,
                    appIssueState,
                    new HarnessAppStoragePaths(),
                    new HarnessApplicationControlService()))
            .MainWindowViewModel;
    }
}

internal sealed class SnapshotProjectAnalyzer(ProjectSnapshot snapshot) : IProjectAnalyzer
{
    public Task<ProjectSnapshot> AnalyzeAsync(
        string rootPath,
        ScanOptions options,
        IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken) =>
        Task.FromResult(snapshot);
}

internal sealed class FixedFolderPickerService(string path) : IFolderPickerService
{
    public Task<string?> PickFolderAsync(CancellationToken cancellationToken) =>
        Task.FromResult<string?>(path);
}

internal sealed class ExistingFolderPathService : IFolderPathService
{
    public bool Exists(string folderPath) => true;
}

internal sealed class NoOpPathShellService : IPathShellService
{
    public string RevealMenuHeader => "Reveal";

    public Task<bool> TryOpenAsync(string fullPath, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task<bool> TryRevealAsync(string fullPath, bool isDirectory, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}

internal sealed class InlineSettingsCoordinator(SettingsState state) : ISettingsCoordinator
{
    private SettingsState MutableState { get; } = state;

    private CurrentFolderSettingsState MutableCurrentFolderState { get; } = new();

    public IReadOnlySettingsState State => MutableState;

    public IReadOnlyCurrentFolderSettingsState CurrentFolderState => MutableCurrentFolderState;

    public ScanOptions BuildCurrentScanOptions() =>
        new()
        {
            RespectGitIgnore = State.RespectGitIgnore,
            UseGlobalExcludes = State.UseGlobalExcludes,
            GlobalExcludes = [.. State.GlobalExcludes],
            UseFolderExcludes = CurrentFolderState.UseFolderExcludes,
            FolderExcludes = [.. CurrentFolderState.FolderExcludes],
        };

    public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public ScanOptions Resolve(string? rootPath, ScanOptions baseOptions) => baseOptions;

    public void SetSelectedMetric(AnalysisMetric metric) => MutableState.SelectedMetric = metric;

    public void SetRespectGitIgnore(bool value) => MutableState.RespectGitIgnore = value;

    public void SetUseGlobalExcludes(bool value) => MutableState.UseGlobalExcludes = value;

    public void ReplaceGlobalExcludes(IEnumerable<string> entries) => MutableState.ReplaceGlobalExcludes(entries);

    public void SetThemePreference(ThemePreference preference) => MutableState.SelectedThemePreference = preference;

    public void SetTreemapPalette(TreemapPalette palette) => MutableState.SelectedTreemapPalette = palette;

    public void SetShowTreemapMetricValues(bool value) => MutableState.ShowTreemapMetricValues = value;

    public void RecordRecentFolder(string folderPath) => MutableState.RecordRecentFolder(folderPath);

    public void RemoveRecentFolder(string folderPath) => MutableState.RemoveRecentFolder(folderPath);

    public void ClearRecentFolders() => MutableState.ClearRecentFolders();

    public void SetUseFolderExcludes(bool value) => MutableCurrentFolderState.UseFolderExcludes = value;

    public void ReplaceFolderExcludes(IEnumerable<string> entries) => MutableCurrentFolderState.ReplaceFolderExcludes(entries);

    public void SwitchActiveFolder(string? rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            MutableCurrentFolderState.Reset();
            return;
        }

        MutableCurrentFolderState.Load(rootPath, useFolderExcludes: false, folderExcludes: []);
    }
}

internal sealed class HarnessApplicationControlService : IApplicationControlService
{
    public void RequestShutdown(int exitCode = 0)
    {
    }
}

internal sealed class HarnessAppStoragePaths : IAppStoragePaths
{
    public string GetSettingsFilePath() => Path.Combine(Path.GetTempPath(), "tokenmap.settings.json");

    public string GetFolderSettingsRootPath() => Path.Combine(Path.GetTempPath(), "folder-settings");

    public string GetLogsDirectoryPath() => Path.Combine(Path.GetTempPath(), "logs");
}
