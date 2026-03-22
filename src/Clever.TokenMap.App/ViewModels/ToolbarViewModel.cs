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
    private static readonly IReadOnlyList<AnalysisMetricOption> MetricOptionItems =
    [
        new(AnalysisMetric.Tokens, "Tokens"),
        new(AnalysisMetric.TotalLines, "Total lines"),
        new(AnalysisMetric.NonEmptyLines, "Non-empty lines"),
    ];

    private static readonly IReadOnlyList<TokenProfileOption> TokenProfileOptionItems =
    [
        new(TokenProfile.O200KBase, "o200k_base"),
        new(TokenProfile.Cl100KBase, "cl100k_base"),
        new(TokenProfile.P50KBase, "p50k_base"),
    ];

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

    public IReadOnlyList<AnalysisMetricOption> MetricOptions { get; } = MetricOptionItems;

    public IReadOnlyList<TokenProfileOption> TokenProfileOptions { get; } = TokenProfileOptionItems;

    [ObservableProperty]
    private bool canConfigureScanOptions = true;

    [ObservableProperty]
    private bool canChangeMetric;

    [ObservableProperty]
    private bool isStopVisible;

    [ObservableProperty]
    private string selectedFolderDisplay = "No folder selected";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedMetric))]
    private AnalysisMetricOption selectedMetricOption = MetricOptionItems[0];

    public AnalysisMetric SelectedMetric
    {
        get => SelectedMetricOption.Value;
        set => SelectedMetricOption = GetMetricOption(value);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedTokenProfile))]
    private TokenProfileOption selectedTokenProfileOption = TokenProfileOptionItems[0];

    public TokenProfile SelectedTokenProfile
    {
        get => SelectedTokenProfileOption.Value;
        set => SelectedTokenProfileOption = GetTokenProfileOption(value);
    }

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

    public void RefreshAvailability(bool isBusy, bool hasSnapshot)
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

    private static AnalysisMetricOption GetMetricOption(AnalysisMetric value) =>
        value switch
        {
            AnalysisMetric.TotalLines => MetricOptionItems[1],
            AnalysisMetric.NonEmptyLines => MetricOptionItems[2],
            _ => MetricOptionItems[0],
        };

    private static TokenProfileOption GetTokenProfileOption(TokenProfile value) =>
        value switch
        {
            TokenProfile.Cl100KBase => TokenProfileOptionItems[1],
            TokenProfile.P50KBase => TokenProfileOptionItems[2],
            _ => TokenProfileOptionItems[0],
        };
}
