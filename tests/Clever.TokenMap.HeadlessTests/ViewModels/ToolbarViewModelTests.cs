using Clever.TokenMap.App.State;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Enums;
using CommunityToolkit.Mvvm.Input;

namespace Clever.TokenMap.HeadlessTests;

public sealed class ToolbarViewModelTests
{
    [Fact]
    public void DefaultMetricSelection_UsesTokensRadio()
    {
        var viewModel = CreateViewModel(new SettingsState());

        Assert.True(viewModel.IsTokensMetricSelected);
        Assert.False(viewModel.IsLinesMetricSelected);
        Assert.False(viewModel.IsSizeMetricSelected);
        Assert.False(viewModel.IsClassicTreemapPaletteSelected);
        Assert.True(viewModel.IsWeightedTreemapPaletteSelected);
        Assert.False(viewModel.IsStudioTreemapPaletteSelected);
        Assert.Equal("Treemap - tokens", viewModel.TreemapTitle);
    }

    [Fact]
    public void SelectingLinesRadio_StoresCanonicalLineMetric()
    {
        var state = new SettingsState();
        var viewModel = CreateViewModel(state);

        viewModel.IsLinesMetricSelected = true;

        Assert.Equal(AnalysisMetric.TotalLines, state.SelectedMetric);
        Assert.False(viewModel.IsTokensMetricSelected);
        Assert.True(viewModel.IsLinesMetricSelected);
    }

    [Fact]
    public void LegacyNonEmptyLinesMetric_StillMapsToLinesRadio()
    {
        var state = new SettingsState
        {
            SelectedMetric = AnalysisMetric.NonEmptyLines,
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

        Assert.Equal(AnalysisMetric.Size, state.SelectedMetric);
        Assert.False(viewModel.IsTokensMetricSelected);
        Assert.False(viewModel.IsLinesMetricSelected);
        Assert.True(viewModel.IsSizeMetricSelected);
    }

    [Fact]
    public void TreemapTitle_TracksSelectedMetric()
    {
        var state = new SettingsState();
        var viewModel = CreateViewModel(state);

        state.SelectedMetric = AnalysisMetric.NonEmptyLines;
        Assert.Equal("Treemap - lines", viewModel.TreemapTitle);

        state.SelectedMetric = AnalysisMetric.Size;
        Assert.Equal("Treemap - size", viewModel.TreemapTitle);
    }

    [Fact]
    public void SelectingWeightedPalette_StoresPaletteSelection()
    {
        var state = new SettingsState();
        var viewModel = CreateViewModel(state);

        viewModel.IsWeightedTreemapPaletteSelected = true;

        Assert.Equal(TreemapPalette.Weighted, state.SelectedTreemapPalette);
        Assert.False(viewModel.IsClassicTreemapPaletteSelected);
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
        Assert.False(viewModel.IsClassicTreemapPaletteSelected);
        Assert.False(viewModel.IsWeightedTreemapPaletteSelected);
        Assert.True(viewModel.IsStudioTreemapPaletteSelected);
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
            state,
            new AsyncRelayCommand(() => Task.CompletedTask),
            new AsyncRelayCommand(() => Task.CompletedTask),
            new RelayCommand(() => { }));
    }
}
