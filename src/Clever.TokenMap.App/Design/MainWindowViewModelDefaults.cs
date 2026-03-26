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

internal sealed record MainWindowViewModelDependencies(
    IAnalysisSessionController AnalysisSessionController,
    TreemapNavigationState TreemapNavigationState,
    ISettingsCoordinator SettingsCoordinator,
    IFolderPathService FolderPathService,
    IPathShellService PathShellService);

internal static class MainWindowViewModelDefaults
{
    public static MainWindowViewModelDependencies Create()
    {
        var folderPathService = new NullFolderPathService();
        return new MainWindowViewModelDependencies(
            CreateAnalysisSessionController(
                new NullProjectAnalyzer(),
                new NullFolderPickerService(),
                folderPathService),
            new TreemapNavigationState(),
            new NullSettingsCoordinator(),
            folderPathService,
            new NullPathShellService());
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
        public SettingsState State { get; } = new();

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

        public void SetSelectedMetric(Core.Enums.AnalysisMetric metric) => State.SelectedMetric = metric;

        public void SetRespectGitIgnore(bool value) => State.RespectGitIgnore = value;

        public void SetUseGlobalExcludes(bool value) => State.UseGlobalExcludes = value;

        public void ReplaceGlobalExcludes(IEnumerable<string> entries) => State.ReplaceGlobalExcludes(entries);

        public void SetThemePreference(Core.Enums.ThemePreference preference) => State.SelectedThemePreference = preference;

        public void SetTreemapPalette(Core.Enums.TreemapPalette palette) => State.SelectedTreemapPalette = palette;

        public void RecordRecentFolder(string folderPath) => State.RecordRecentFolder(folderPath);

        public void RemoveRecentFolder(string folderPath) => State.RemoveRecentFolder(folderPath);

        public void ClearRecentFolders() => State.ClearRecentFolders();

        public void SetUseFolderExcludes(bool value) => CurrentFolderState.UseFolderExcludes = value;

        public void ReplaceFolderExcludes(IEnumerable<string> entries) => CurrentFolderState.ReplaceFolderExcludes(entries);

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
