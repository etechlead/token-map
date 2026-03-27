using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.App.State;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.App.ViewModels;

namespace Clever.TokenMap.App.Design;

internal static class MainWindowViewModelDefaults
{
    public static MainWindowViewModelComposition Create()
    {
        var folderPathService = new NullFolderPathService();
        var analysisSessionController = CreateAnalysisSessionController(
            new NullProjectAnalyzer(),
            new NullFolderPickerService(),
            folderPathService);
        var settingsCoordinator = new NullSettingsCoordinator();
        var pathShellService = new NullPathShellService();

        return MainWindowViewModelFactory.Create(
            new MainWindowViewModelFactoryDependencies(
                analysisSessionController,
                settingsCoordinator,
                folderPathService,
                pathShellService));
    }

    private static AnalysisSessionController CreateAnalysisSessionController(
        IProjectAnalyzer projectAnalyzer,
        IFolderPickerService folderPickerService,
        IFolderPathService folderPathService)
    {
        ArgumentNullException.ThrowIfNull(projectAnalyzer);
        ArgumentNullException.ThrowIfNull(folderPickerService);
        ArgumentNullException.ThrowIfNull(folderPathService);

        return new AnalysisSessionController(projectAnalyzer, folderPickerService, folderPathService, logger: null, scanOptionsResolver: null);
    }

    private sealed class NullFolderPickerService : IFolderPickerService
    {
        public Task<string?> PickFolderAsync(CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }

    private sealed class NullProjectAnalyzer : IProjectAnalyzer
    {
        public Task<ProjectSnapshot> AnalyzeAsync(
            string rootPath,
            ScanOptions options,
            IProgress<AnalysisProgress>? progress,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Project analyzer is not configured.");
    }

    private sealed class NullFolderPathService : IFolderPathService
    {
        public bool Exists(string folderPath) => true;
    }

    private sealed class NullSettingsCoordinator : ISettingsCoordinator
    {
        private SettingsState MutableState { get; } = new();

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

        public void SetSelectedMetric(Core.Enums.AnalysisMetric metric) => MutableState.SelectedMetric = metric;

        public void SetRespectGitIgnore(bool value) => MutableState.RespectGitIgnore = value;

        public void SetUseGlobalExcludes(bool value) => MutableState.UseGlobalExcludes = value;

        public void ReplaceGlobalExcludes(IEnumerable<string> entries) => MutableState.ReplaceGlobalExcludes(entries);

        public void SetThemePreference(Core.Enums.ThemePreference preference) => MutableState.SelectedThemePreference = preference;

        public void SetTreemapPalette(Core.Enums.TreemapPalette palette) => MutableState.SelectedTreemapPalette = palette;

        public void SetShowTreemapMetricValues(bool value) => MutableState.ShowTreemapMetricValues = value;

        public void RecordRecentFolder(string folderPath) => MutableState.RecordRecentFolder(folderPath);

        public void RemoveRecentFolder(string folderPath) => MutableState.RemoveRecentFolder(folderPath);

        public void ClearRecentFolders() => MutableState.ClearRecentFolders();

        public void SetUseFolderExcludes(bool value) => MutableCurrentFolderState.UseFolderExcludes = value;

        public void ReplaceFolderExcludes(IEnumerable<string> entries) => MutableCurrentFolderState.ReplaceFolderExcludes(entries);

        public void SwitchActiveFolder(string? rootPath)
        {
        }
    }

    private sealed class NullPathShellService : IPathShellService
    {
        public string RevealMenuHeader => "Reveal";

        public Task<bool> TryOpenAsync(string fullPath, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> TryRevealAsync(string fullPath, bool isDirectory, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
