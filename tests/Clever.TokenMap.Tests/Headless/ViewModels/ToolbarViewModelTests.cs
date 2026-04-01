using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.State;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;
using CommunityToolkit.Mvvm.Input;

namespace Clever.TokenMap.Tests.Headless.ViewModels;

public sealed class ToolbarViewModelTests
{
    [Fact]
    public void DefaultMetricSelection_UsesTokens()
    {
        var viewModel = CreateViewModel(new SettingsState());

        Assert.True(viewModel.IsTokensMetricSelected);
        Assert.False(viewModel.IsLinesMetricSelected);
        Assert.False(viewModel.IsSizeMetricSelected);
        Assert.False(viewModel.IsPlainTreemapPaletteSelected);
        Assert.True(viewModel.IsWeightedTreemapPaletteSelected);
        Assert.False(viewModel.IsStudioTreemapPaletteSelected);
        Assert.True(viewModel.ShowTreemapMetricValues);
    }

    [Fact]
    public void SelectingLinesRadio_StoresCanonicalLineMetric()
    {
        var state = new SettingsState();
        var viewModel = CreateViewModel(state);

        viewModel.IsLinesMetricSelected = true;

        Assert.Equal(MetricIds.NonEmptyLines, state.SelectedMetric);
        Assert.False(viewModel.IsTokensMetricSelected);
        Assert.True(viewModel.IsLinesMetricSelected);
    }

    [Fact]
    public void CanonicalLinesMetric_MapsToLinesRadio()
    {
        var state = new SettingsState
        {
            SelectedMetric = MetricIds.NonEmptyLines,
        };
        var viewModel = CreateViewModel(state);

        Assert.False(viewModel.IsTokensMetricSelected);
        Assert.True(viewModel.IsLinesMetricSelected);
        Assert.False(viewModel.IsSizeMetricSelected);
    }

    [Fact]
    public void SelectingSizeRadio_StoresCanonicalSizeMetric()
    {
        var state = new SettingsState();
        var viewModel = CreateViewModel(state);

        viewModel.IsSizeMetricSelected = true;

        Assert.Equal(MetricIds.FileSizeBytes, state.SelectedMetric);
        Assert.False(viewModel.IsTokensMetricSelected);
        Assert.False(viewModel.IsLinesMetricSelected);
        Assert.True(viewModel.IsSizeMetricSelected);
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

    private static ToolbarViewModel CreateViewModel(SettingsState state)
    {
        return new ToolbarViewModel(
            new TestSettingsCoordinator(state),
            new AsyncRelayCommand(() => Task.CompletedTask),
            new AsyncRelayCommand(() => Task.CompletedTask),
            new RelayCommand(() => { }));
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
        }
    }
}
