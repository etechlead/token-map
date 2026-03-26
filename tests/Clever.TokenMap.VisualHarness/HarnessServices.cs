using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.State;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Logging;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Infrastructure.Analysis;
using Clever.TokenMap.Infrastructure.Caching;
using Clever.TokenMap.Infrastructure.Scanning;
using Clever.TokenMap.Infrastructure.Text;
using Clever.TokenMap.Infrastructure.Tokenization;

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
        IPathShellService pathShellService) =>
        new(
            analysisSessionController,
            new TreemapNavigationState(),
            settingsCoordinator,
            folderPathService,
            pathShellService);
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
    public SettingsState State { get; } = state;

    public CurrentFolderSettingsState CurrentFolderState { get; } = new();

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

    public void SetSelectedMetric(AnalysisMetric metric) => State.SelectedMetric = metric;

    public void SetRespectGitIgnore(bool value) => State.RespectGitIgnore = value;

    public void SetUseGlobalExcludes(bool value) => State.UseGlobalExcludes = value;

    public void ReplaceGlobalExcludes(IEnumerable<string> entries) => State.ReplaceGlobalExcludes(entries);

    public void SetThemePreference(ThemePreference preference) => State.SelectedThemePreference = preference;

    public void SetTreemapPalette(TreemapPalette palette) => State.SelectedTreemapPalette = palette;

    public void RecordRecentFolder(string folderPath) => State.RecordRecentFolder(folderPath);

    public void RemoveRecentFolder(string folderPath) => State.RemoveRecentFolder(folderPath);

    public void ClearRecentFolders() => State.ClearRecentFolders();

    public void SetUseFolderExcludes(bool value) => CurrentFolderState.UseFolderExcludes = value;

    public void ReplaceFolderExcludes(IEnumerable<string> entries) => CurrentFolderState.ReplaceFolderExcludes(entries);

    public void SwitchActiveFolder(string? rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            CurrentFolderState.Reset();
            return;
        }

        CurrentFolderState.Load(rootPath, useFolderExcludes: false, folderExcludes: []);
    }
}
