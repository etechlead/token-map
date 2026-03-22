using System;
using System.Collections.Generic;
using System.Linq;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Infrastructure.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clever.TokenMap.App.ViewModels;

public partial class ToolbarViewModel : ViewModelBase
{
    private readonly RelayCommand<string> _selectThemePreferenceCommand;

    public ToolbarViewModel(
        IAsyncRelayCommand openFolderCommand,
        IAsyncRelayCommand rescanCommand,
        IRelayCommand cancelCommand)
    {
        OpenFolderCommand = openFolderCommand;
        RescanCommand = rescanCommand;
        CancelCommand = cancelCommand;
        _selectThemePreferenceCommand = new RelayCommand<string>(SelectThemePreference);
    }

    public IAsyncRelayCommand OpenFolderCommand { get; }

    public IAsyncRelayCommand RescanCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public IRelayCommand<string> SelectThemePreferenceCommand => _selectThemePreferenceCommand;

    public IReadOnlyList<string> MetricOptions { get; } =
    [
        "Tokens",
        "Total lines",
        "Non-empty lines",
    ];

    public IReadOnlyList<string> TokenProfiles { get; } =
    [
        "o200k_base",
        "cl100k_base",
        "p50k_base",
    ];

    public IReadOnlyList<string> ThemeOptions { get; } =
    [
        ThemePreferences.System,
        ThemePreferences.Light,
        ThemePreferences.Dark,
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
    private string selectedMetric = "Tokens";

    [ObservableProperty]
    private string selectedTokenProfile = "o200k_base";

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
    private string selectedThemePreference = ThemePreferences.System;

    public bool IsSystemThemeSelected => string.Equals(SelectedThemePreference, ThemePreferences.System, StringComparison.Ordinal);

    public bool IsLightThemeSelected => string.Equals(SelectedThemePreference, ThemePreferences.Light, StringComparison.Ordinal);

    public bool IsDarkThemeSelected => string.Equals(SelectedThemePreference, ThemePreferences.Dark, StringComparison.Ordinal);

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
            TokenProfile = SelectedTokenProfile switch
            {
                "cl100k_base" => TokenProfile.Cl100KBase,
                "p50k_base" => TokenProfile.P50KBase,
                _ => TokenProfile.O200KBase,
            },
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

        SelectedMetric = IsKnownMetric(settings.SelectedMetric)
            ? settings.SelectedMetric
            : "Tokens";
        SelectedTokenProfile = IsKnownTokenProfile(settings.SelectedTokenProfile)
            ? settings.SelectedTokenProfile
            : "o200k_base";
        RespectGitIgnore = settings.RespectGitIgnore;
        RespectIgnore = settings.RespectIgnore;
        UseDefaultExcludes = settings.UseDefaultExcludes;
    }

    public void ApplyAppearanceSettings(AppearanceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        SelectedThemePreference = IsKnownThemePreference(settings.ThemePreference)
            ? settings.ThemePreference
            : ThemePreferences.System;
    }

    private void SelectThemePreference(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && IsKnownThemePreference(value))
        {
            SelectedThemePreference = value;
        }
    }

    private bool IsKnownMetric(string value) =>
        MetricOptions.Contains(value, StringComparer.Ordinal);

    private bool IsKnownTokenProfile(string value) =>
        TokenProfiles.Contains(value, StringComparer.Ordinal);

    private bool IsKnownThemePreference(string value) =>
        ThemeOptions.Contains(value, StringComparer.Ordinal);
}
