using Clever.TokenMap.Core.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.State;

public sealed partial class SettingsState : ObservableObject
{
    [ObservableProperty]
    private AnalysisMetric selectedMetric = AnalysisMetric.Tokens;

    [ObservableProperty]
    private TokenProfile selectedTokenProfile = TokenProfile.O200KBase;

    [ObservableProperty]
    private bool respectGitIgnore = true;

    [ObservableProperty]
    private bool respectIgnore = true;

    [ObservableProperty]
    private bool useDefaultExcludes = true;

    [ObservableProperty]
    private ThemePreference selectedThemePreference = ThemePreference.System;
}
