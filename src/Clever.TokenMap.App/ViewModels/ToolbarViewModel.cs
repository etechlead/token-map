using System;
using System.Collections.Generic;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Infrastructure.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clever.TokenMap.App.ViewModels;

public partial class ToolbarViewModel : ViewModelBase
{
    private readonly RelayCommand _selectDarkThemePreferenceCommand;
    private readonly RelayCommand _selectLightThemePreferenceCommand;
    private readonly RelayCommand _selectSystemThemePreferenceCommand;

    public ToolbarViewModel(
        IAsyncRelayCommand openFolderCommand,
        IAsyncRelayCommand rescanCommand,
        IRelayCommand cancelCommand)
    {
        OpenFolderCommand = openFolderCommand;
        RescanCommand = rescanCommand;
        CancelCommand = cancelCommand;
        _selectSystemThemePreferenceCommand = new RelayCommand(() => SelectThemePreference(ThemePreference.System));
        _selectLightThemePreferenceCommand = new RelayCommand(() => SelectThemePreference(ThemePreference.Light));
        _selectDarkThemePreferenceCommand = new RelayCommand(() => SelectThemePreference(ThemePreference.Dark));
    }

    public IAsyncRelayCommand OpenFolderCommand { get; }

    public IAsyncRelayCommand RescanCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public IRelayCommand SelectSystemThemePreferenceCommand => _selectSystemThemePreferenceCommand;

    public IRelayCommand SelectLightThemePreferenceCommand => _selectLightThemePreferenceCommand;

    public IRelayCommand SelectDarkThemePreferenceCommand => _selectDarkThemePreferenceCommand;

    public IReadOnlyList<AnalysisMetric> MetricOptions { get; } =
    [
        AnalysisMetric.Tokens,
        AnalysisMetric.TotalLines,
        AnalysisMetric.NonEmptyLines,
    ];

    public IReadOnlyList<TokenProfile> TokenProfiles { get; } =
    [
        TokenProfile.O200KBase,
        TokenProfile.Cl100KBase,
        TokenProfile.P50KBase,
    ];

    [ObservableProperty]
    private bool canConfigureScanOptions = true;

    [ObservableProperty]
    private bool canChangeMetric;

    [ObservableProperty]
    private bool isStopVisible;

    [ObservableProperty]
    private string selectedFolderDisplay = "No folder selected";

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
    [NotifyPropertyChangedFor(nameof(IsSystemThemeSelected))]
    [NotifyPropertyChangedFor(nameof(IsLightThemeSelected))]
    [NotifyPropertyChangedFor(nameof(IsDarkThemeSelected))]
    private ThemePreference selectedThemePreference = ThemePreference.System;

    public bool IsSystemThemeSelected => SelectedThemePreference == ThemePreference.System;

    public bool IsLightThemeSelected => SelectedThemePreference == ThemePreference.Light;

    public bool IsDarkThemeSelected => SelectedThemePreference == ThemePreference.Dark;

    public void UpdateFolder(string? folderPath)
    {
        SelectedFolderDisplay = string.IsNullOrWhiteSpace(folderPath)
            ? "No folder selected"
            : folderPath;
    }

    public void RefreshAvailability(bool hasSelectedFolder, bool isBusy, bool hasSnapshot)
    {
        CanConfigureScanOptions = !isBusy;
        CanChangeMetric = hasSnapshot && !isBusy;
        IsStopVisible = isBusy;
        OpenFolderCommand.NotifyCanExecuteChanged();
        RescanCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    public ScanOptions BuildScanOptions() =>
        new()
        {
            TokenProfile = SelectedTokenProfile,
            RespectGitIgnore = RespectGitIgnore,
            RespectDotIgnore = RespectIgnore,
            UseDefaultExcludes = UseDefaultExcludes,
        };

    public AnalysisSettings BuildAnalysisSettings() =>
        new()
        {
            SelectedMetric = SelectedMetric,
            SelectedTokenProfile = SelectedTokenProfile,
            RespectGitIgnore = RespectGitIgnore,
            RespectIgnore = RespectIgnore,
            UseDefaultExcludes = UseDefaultExcludes,
        };

    public AppearanceSettings BuildAppearanceSettings() =>
        new()
        {
            ThemePreference = SelectedThemePreference,
        };

    public void ApplyAnalysisSettings(AnalysisSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        SelectedMetric = settings.SelectedMetric;
        SelectedTokenProfile = settings.SelectedTokenProfile;
        RespectGitIgnore = settings.RespectGitIgnore;
        RespectIgnore = settings.RespectIgnore;
        UseDefaultExcludes = settings.UseDefaultExcludes;
    }

    public void ApplyAppearanceSettings(AppearanceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        SelectedThemePreference = settings.ThemePreference;
    }

    private void SelectThemePreference(ThemePreference value)
    {
        SelectedThemePreference = value;
    }
}
