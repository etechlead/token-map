using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Logging;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Core.Paths;
using Clever.TokenMap.Core.Settings;

namespace Clever.TokenMap.App.Services;

public sealed class SettingsCoordinator : ISettingsCoordinator
{
    private static readonly TimeSpan DefaultDebounceDelay = TimeSpan.FromMilliseconds(250);

    private readonly AppSettingsSession _appSettingsSession;
    private readonly FolderSettingsSession _folderSettingsSession;

    public SettingsCoordinator(
        IAppSettingsStore appSettingsStore,
        IFolderSettingsStore folderSettingsStore,
        IThemeService themeService,
        IApplicationLanguageService applicationLanguageService,
        AppSettings? initialSettings = null,
        IAppLogger? logger = null,
        TimeSpan? debounceDelay = null,
        PathNormalizer? pathNormalizer = null)
    {
        var effectiveLogger = logger ?? NullAppLogger.Instance;
        var effectiveDebounceDelay = debounceDelay ?? DefaultDebounceDelay;
        var effectivePathNormalizer = pathNormalizer ?? new PathNormalizer();

        _appSettingsSession = new AppSettingsSession(
            appSettingsStore,
            themeService,
            applicationLanguageService,
            initialSettings,
            effectiveLogger,
            effectiveDebounceDelay);
        _folderSettingsSession = new FolderSettingsSession(
            folderSettingsStore,
            effectivePathNormalizer,
            effectiveLogger,
            effectiveDebounceDelay);
    }

    private SettingsState MutableState => _appSettingsSession.State;

    private CurrentFolderSettingsState MutableCurrentFolderState => _folderSettingsSession.CurrentFolderState;

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

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        await _appSettingsSession.FlushAsync(cancellationToken).ConfigureAwait(false);
        await _folderSettingsSession.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public ScanOptions Resolve(string? rootPath, ScanOptions baseOptions) =>
        _folderSettingsSession.Resolve(rootPath, baseOptions);

    public void SetSelectedMetric(MetricId metric) =>
        MutableState.SelectedMetric = DefaultMetricCatalog.NormalizeMetricId(metric);

    public void SetMetricVisibility(MetricId metric, bool isVisible) =>
        MutableState.SetMetricVisibility(metric, isVisible);

    public void SetRespectGitIgnore(bool value) =>
        MutableState.RespectGitIgnore = value;

    public void SetUseGlobalExcludes(bool value) =>
        MutableState.UseGlobalExcludes = value;

    public void ReplaceGlobalExcludes(IEnumerable<string> entries) =>
        MutableState.ReplaceGlobalExcludes(entries);

    public void SetThemePreference(ThemePreference preference) =>
        MutableState.SelectedThemePreference = preference;

    public void SetWorkspaceLayoutMode(WorkspaceLayoutMode mode) =>
        MutableState.WorkspaceLayoutMode = AppSettingsCanonicalizer.NormalizeWorkspaceLayoutMode(mode);

    public void SetTreemapPalette(TreemapPalette palette) =>
        MutableState.SelectedTreemapPalette = palette;

    public void SetShowTreemapMetricValues(bool value) =>
        MutableState.ShowTreemapMetricValues = value;

    public void SetApplicationLanguageTag(string languageTag) =>
        MutableState.ApplicationLanguageTag = _appSettingsSession.NormalizeApplicationLanguageTag(languageTag);

    public void SetSelectedPromptLanguageTag(string languageTag) =>
        MutableState.SelectedPromptLanguageTag = _appSettingsSession.NormalizePromptLanguageTag(
            languageTag,
            MutableState.ApplicationLanguageTag);

    public void SetRefactorPromptTemplate(string languageTag, string templateText) =>
        MutableState.SetRefactorPromptTemplate(languageTag, templateText);

    public void RecordRecentFolder(string folderPath) =>
        MutableState.RecordRecentFolder(folderPath);

    public void RemoveRecentFolder(string folderPath) =>
        MutableState.RemoveRecentFolder(folderPath);

    public void ClearRecentFolders() =>
        MutableState.ClearRecentFolders();

    public void SetUseFolderExcludes(bool value) =>
        MutableCurrentFolderState.UseFolderExcludes = value;

    public void ReplaceFolderExcludes(IEnumerable<string> entries) =>
        MutableCurrentFolderState.ReplaceFolderExcludes(entries);

    public void SwitchActiveFolder(string? rootPath) =>
        _folderSettingsSession.SwitchActiveFolder(rootPath);
}
