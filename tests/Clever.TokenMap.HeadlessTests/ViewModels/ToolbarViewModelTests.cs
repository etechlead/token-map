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
