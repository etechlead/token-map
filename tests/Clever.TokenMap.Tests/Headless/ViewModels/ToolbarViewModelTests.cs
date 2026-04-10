using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.State;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Core.Settings;
using CommunityToolkit.Mvvm.Input;

namespace Clever.TokenMap.Tests.Headless.ViewModels;

public sealed class ToolbarViewModelTests
{
    [Fact]
    public void DefaultMetricSelection_UsesTokens()
    {
        var viewModel = CreateViewModel(new SettingsState());

        Assert.Equal(MetricIds.Tokens, viewModel.SelectedMetric);
        Assert.Equal(
            [
                MetricIds.Tokens,
                MetricIds.NonEmptyLines,
                MetricIds.FileSizeBytes,
                MetricIds.RefactorPriorityPoints,
            ],
            viewModel.VisibleTreemapMetricOptions.Select(option => option.Definition.Id).ToArray());
        Assert.Equal(
            MetricIds.Tokens,
            Assert.Single(viewModel.VisibleTreemapMetricOptions, option => option.IsSelected).Definition.Id);
        Assert.True(viewModel.ShowTreemapMetricButtons);
        Assert.False(viewModel.ShowTreemapMetricSelector);
        Assert.Equal(WorkspaceLayoutMode.SideBySide, viewModel.SelectedWorkspaceLayoutMode);
        Assert.True(viewModel.IsSideBySideWorkspaceLayout);
        Assert.False(viewModel.IsStackedWorkspaceLayout);
        Assert.False(viewModel.IsPlainTreemapPaletteSelected);
        Assert.True(viewModel.IsWeightedTreemapPaletteSelected);
        Assert.False(viewModel.IsStudioTreemapPaletteSelected);
        Assert.True(viewModel.ShowTreemapMetricValues);
    }

    [Fact]
    public void SelectingLinesOption_StoresCanonicalLineMetric()
    {
        var state = new SettingsState();
        var viewModel = CreateViewModel(state);

        viewModel.SelectedTreemapMetricOption = Assert.Single(
            viewModel.VisibleTreemapMetricOptions,
            option => option.Definition.Id == MetricIds.NonEmptyLines);

        Assert.Equal(MetricIds.NonEmptyLines, state.SelectedMetric);
        Assert.Equal(MetricIds.NonEmptyLines, viewModel.SelectedMetric);
        Assert.Equal(
            MetricIds.NonEmptyLines,
            Assert.Single(viewModel.VisibleTreemapMetricOptions, option => option.IsSelected).Definition.Id);
    }

    [Fact]
    public void CanonicalLinesMetric_MapsToSelectedOption()
    {
        var state = new SettingsState
        {
            SelectedMetric = MetricIds.NonEmptyLines,
        };
        var viewModel = CreateViewModel(state);

        Assert.Equal(MetricIds.NonEmptyLines, viewModel.SelectedMetric);
        Assert.Equal(MetricIds.NonEmptyLines, viewModel.SelectedTreemapMetricOption?.Definition.Id);
    }

    [Fact]
    public void SelectingSizeOption_StoresCanonicalSizeMetric()
    {
        var state = new SettingsState();
        var viewModel = CreateViewModel(state);

        viewModel.SelectedTreemapMetricOption = Assert.Single(
            viewModel.VisibleTreemapMetricOptions,
            option => option.Definition.Id == MetricIds.FileSizeBytes);

        Assert.Equal(MetricIds.FileSizeBytes, state.SelectedMetric);
        Assert.Equal(MetricIds.FileSizeBytes, viewModel.SelectedMetric);
        Assert.Equal(MetricIds.FileSizeBytes, viewModel.SelectedTreemapMetricOption?.Definition.Id);
    }

    [Fact]
    public void HidingSelectedMetric_FallsBackToFirstVisibleMetric()
    {
        var state = new SettingsState
        {
            SelectedMetric = MetricIds.FileSizeBytes,
        };
        var viewModel = CreateViewModel(state);

        var sizeOption = Assert.Single(
            viewModel.MetricVisibilityOptions,
            option => option.Definition.Id == MetricIds.FileSizeBytes);
        sizeOption.IsVisible = false;

        Assert.Equal(MetricIds.Tokens, state.SelectedMetric);
        Assert.DoesNotContain(state.VisibleMetricIds, metricId => metricId == MetricIds.FileSizeBytes);
        Assert.Equal(MetricIds.Tokens, viewModel.SelectedTreemapMetricOption?.Definition.Id);
    }

    [Fact]
    public void LastVisibleMetric_ToggleIsDisabled()
    {
        var state = new SettingsState();
        state.SetMetricVisibility(MetricIds.NonEmptyLines, isVisible: false);
        state.SetMetricVisibility(MetricIds.FileSizeBytes, isVisible: false);
        state.SetMetricVisibility(MetricIds.RefactorPriorityPoints, isVisible: false);
        var viewModel = CreateViewModel(state);

        var tokensOption = Assert.Single(
            viewModel.MetricVisibilityOptions,
            option => option.Definition.Id == MetricIds.Tokens);

        Assert.True(tokensOption.IsVisible);
        Assert.False(tokensOption.IsToggleEnabled);
    }

    [Fact]
    public void MetricVisibilityOptions_ContainOnlyProductMetrics()
    {
        var viewModel = CreateViewModel(new SettingsState());

        var priorityOption = Assert.Single(
            viewModel.MetricVisibilityOptions,
            option => option.Definition.Id == MetricIds.RefactorPriorityPoints);

        Assert.Equal("Refactor", priorityOption.Label);
        Assert.DoesNotContain(
            viewModel.MetricVisibilityOptions,
            option => option.Definition.Id == MetricIds.ComplexityPoints);
        Assert.Equal(4, viewModel.MetricVisibilityOptions.Count);
        Assert.Empty(viewModel.TreeOnlyMetricVisibilityOptions);
        Assert.False(viewModel.HasTreeOnlyMetricVisibilityOptions);
    }

    [Fact]
    public void VisibleTreemapMetricOptions_ExposeSharedMetricDescriptions()
    {
        var viewModel = CreateViewModel(new SettingsState());
        var refactorOption = Assert.Single(
            viewModel.VisibleTreemapMetricOptions,
            option => option.Definition.Id == MetricIds.RefactorPriorityPoints);

        Assert.Equal(refactorOption.Definition.Description, refactorOption.Description);
        Assert.Equal("Refactoring priority score that combines structural risk with recent change and co-change pressure.", refactorOption.Description);
    }

    [Fact]
    public void SelectingWeightedPalette_StoresPaletteSelection()
    {
        var state = new SettingsState();
        var viewModel = CreateViewModel(state);

        viewModel.IsWeightedTreemapPaletteSelected = true;

        Assert.Equal(TreemapPalette.Weighted, state.SelectedTreemapPalette);
        Assert.False(viewModel.IsPlainTreemapPaletteSelected);
        Assert.True(viewModel.IsWeightedTreemapPaletteSelected);
        Assert.False(viewModel.IsStudioTreemapPaletteSelected);
    }

    [Fact]
    public void SelectingStudioPalette_StoresPaletteSelection()
    {
        var state = new SettingsState();
        var viewModel = CreateViewModel(state);

        viewModel.IsStudioTreemapPaletteSelected = true;

        Assert.Equal(TreemapPalette.Studio, state.SelectedTreemapPalette);
        Assert.False(viewModel.IsPlainTreemapPaletteSelected);
        Assert.False(viewModel.IsWeightedTreemapPaletteSelected);
        Assert.True(viewModel.IsStudioTreemapPaletteSelected);
    }

    [Fact]
    public void TogglingTreemapMetricValues_StoresSetting()
    {
        var state = new SettingsState();
        var viewModel = CreateViewModel(state);

        viewModel.ShowTreemapMetricValues = false;

        Assert.False(state.ShowTreemapMetricValues);
        Assert.False(viewModel.ShowTreemapMetricValues);
    }

    [Fact]
    public void ToggleWorkspaceLayoutCommand_SwitchesModes()
    {
        var state = new SettingsState();
        var viewModel = CreateViewModel(state);

        viewModel.ToggleWorkspaceLayoutCommand.Execute(null);

        Assert.Equal(WorkspaceLayoutMode.Stacked, state.WorkspaceLayoutMode);
        Assert.Equal(WorkspaceLayoutMode.Stacked, viewModel.SelectedWorkspaceLayoutMode);
        Assert.False(viewModel.IsSideBySideWorkspaceLayout);
        Assert.True(viewModel.IsStackedWorkspaceLayout);
        Assert.Equal("Switch to side-by-side layout", viewModel.WorkspaceLayoutToggleToolTip);

        viewModel.ToggleWorkspaceLayoutCommand.Execute(null);

        Assert.Equal(WorkspaceLayoutMode.SideBySide, state.WorkspaceLayoutMode);
        Assert.True(viewModel.IsSideBySideWorkspaceLayout);
        Assert.False(viewModel.IsStackedWorkspaceLayout);
        Assert.Equal("Switch to stacked layout", viewModel.WorkspaceLayoutToggleToolTip);
    }

    [Fact]
    public void BuildScanOptions_UsesGlobalExcludeSettings()
    {
        var state = new SettingsState
        {
            RespectGitIgnore = false,
            UseGlobalExcludes = false,
        };
        state.ReplaceGlobalExcludes(["bin/", "src/generated/**"]);
        var viewModel = CreateViewModel(state);

        var options = viewModel.BuildScanOptions();

        Assert.False(options.RespectGitIgnore);
        Assert.False(options.UseGlobalExcludes);
        Assert.Collection(
            options.GlobalExcludes,
            entry => Assert.Equal("bin/", entry),
            entry => Assert.Equal("src/generated/**", entry));
    }

    [Fact]
    public void LocalizedMetricLabels_UpdateWhenLanguageChanges()
    {
        var state = new SettingsState();
        var languageService = new ApplicationLanguageService();
        var localization = new LocalizationState(languageService);
        var viewModel = new ToolbarViewModel(
            new TestSettingsCoordinator(state),
            new AsyncRelayCommand(() => Task.CompletedTask),
            new AsyncRelayCommand(() => Task.CompletedTask),
            new RelayCommand(() => { }),
            localization,
            new MetricPresentationCatalog(localization));

        Assert.Equal("Tokens", viewModel.VisibleTreemapMetricOptions[0].Label);

        languageService.ApplyPreference("ru");

        Assert.Equal("Токены", viewModel.VisibleTreemapMetricOptions[0].Label);
        Assert.Equal("Рефактор", Assert.Single(
            viewModel.MetricVisibilityOptions,
            option => option.Definition.Id == MetricIds.RefactorPriorityPoints).Label);
    }

    private static ToolbarViewModel CreateViewModel(SettingsState state)
    {
        var localization = new LocalizationState(new ApplicationLanguageService());
        return new ToolbarViewModel(
            new TestSettingsCoordinator(state),
            new AsyncRelayCommand(() => Task.CompletedTask),
            new AsyncRelayCommand(() => Task.CompletedTask),
            new RelayCommand(() => { }),
            localization,
            new MetricPresentationCatalog(localization));
    }

    private sealed class TestSettingsCoordinator(SettingsState state) : ISettingsCoordinator
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
}
