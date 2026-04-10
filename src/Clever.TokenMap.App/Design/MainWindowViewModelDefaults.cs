using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.State;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Diagnostics;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Core.Preview;
using Clever.TokenMap.Core.Settings;

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
        var appIssueState = new AppIssueState();
        var applicationLanguageService = new ApplicationLanguageService();
        var localization = new LocalizationState(applicationLanguageService);
        var metricPresentationCatalog = new MetricPresentationCatalog(localization);

        return MainWindowViewModelFactory.Create(
            new MainWindowViewModelFactoryDependencies(
                analysisSessionController,
                settingsCoordinator,
                folderPathService,
                pathShellService,
                new RefactorPromptComposer(settingsCoordinator),
                new InlineUiDispatcher(),
                new NullFilePreviewContentReader(),
                NullAppIssueReporter.Instance,
                appIssueState,
                new DesignAppStoragePaths(),
                new NullApplicationControlService(),
                localization,
                metricPresentationCatalog));
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

        public void SetSelectedMetric(MetricId metric) => MutableState.SelectedMetric = DefaultMetricCatalog.NormalizeMetricId(metric);

        public void SetMetricVisibility(MetricId metric, bool isVisible) => MutableState.SetMetricVisibility(metric, isVisible);

        public void SetRespectGitIgnore(bool value) => MutableState.RespectGitIgnore = value;

        public void SetUseGlobalExcludes(bool value) => MutableState.UseGlobalExcludes = value;

        public void ReplaceGlobalExcludes(IEnumerable<string> entries) => MutableState.ReplaceGlobalExcludes(entries);

        public void SetThemePreference(ThemePreference preference) => MutableState.SelectedThemePreference = preference;

        public void SetWorkspaceLayoutMode(WorkspaceLayoutMode mode) => MutableState.WorkspaceLayoutMode = mode;

        public void SetTreemapPalette(TreemapPalette palette) => MutableState.SelectedTreemapPalette = palette;

        public void SetShowTreemapMetricValues(bool value) => MutableState.ShowTreemapMetricValues = value;

        public void SetApplicationLanguageTag(string languageTag) =>
            MutableState.ApplicationLanguageTag = ApplicationLanguageTags.Normalize(languageTag);

        public void SetSelectedPromptLanguageTag(string languageTag) =>
            MutableState.SelectedPromptLanguageTag = AppSettingsCanonicalizer.NormalizePromptLanguageTag(languageTag)
                ?? ApplicationLanguageTags.Default;

        public void SetRefactorPromptTemplate(string languageTag, string templateText) =>
            MutableState.SetRefactorPromptTemplate(languageTag, templateText);

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

    private sealed class NullApplicationControlService : IApplicationControlService
    {
        public void RequestShutdown(int exitCode = 0)
        {
        }
    }

    private sealed class NullFilePreviewContentReader : IFilePreviewContentReader
    {
        public Task<FilePreviewContentResult> ReadAsync(string fullPath, CancellationToken cancellationToken = default) =>
            Task.FromResult(new FilePreviewContentResult(FilePreviewReadStatus.ReadFailed));
    }

    private sealed class DesignAppStoragePaths : IAppStoragePaths
    {
        public string GetSettingsFilePath() => string.Empty;

        public string GetFolderSettingsRootPath() => string.Empty;

        public string GetLogsDirectoryPath() => string.Empty;
    }
}
