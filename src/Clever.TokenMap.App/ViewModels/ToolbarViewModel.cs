using System;
using System.Collections.Generic;
using System.ComponentModel;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
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
    private readonly SettingsState _settingsState;

    public ToolbarViewModel(
        SettingsState settingsState,
        IAsyncRelayCommand openFolderCommand,
        IAsyncRelayCommand rescanCommand,
        IRelayCommand cancelCommand)
    {
        _settingsState = settingsState;
        OpenFolderCommand = openFolderCommand;
        RescanCommand = rescanCommand;
        CancelCommand = cancelCommand;
        _settingsState.PropertyChanged += SettingsStateOnPropertyChanged;
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

    public AnalysisMetricOption SelectedMetricOption
    {
        get => GetMetricOption(_settingsState.SelectedMetric);
        set
        {
            if (value is not null)
            {
                SelectedMetric = value.Value;
            }
        }
    }

    public AnalysisMetric SelectedMetric
    {
        get => _settingsState.SelectedMetric;
        set => _settingsState.SelectedMetric = value;
    }

    public TokenProfileOption SelectedTokenProfileOption
    {
        get => GetTokenProfileOption(_settingsState.SelectedTokenProfile);
        set
        {
            if (value is not null)
            {
                SelectedTokenProfile = value.Value;
            }
        }
    }

    public TokenProfile SelectedTokenProfile
    {
        get => _settingsState.SelectedTokenProfile;
        set => _settingsState.SelectedTokenProfile = value;
    }

    public bool RespectGitIgnore
    {
        get => _settingsState.RespectGitIgnore;
        set => _settingsState.RespectGitIgnore = value;
    }

    public bool RespectIgnore
    {
        get => _settingsState.RespectIgnore;
        set => _settingsState.RespectIgnore = value;
    }

    public bool UseDefaultExcludes
    {
        get => _settingsState.UseDefaultExcludes;
        set => _settingsState.UseDefaultExcludes = value;
    }

    public ThemePreference SelectedThemePreference
    {
        get => _settingsState.SelectedThemePreference;
        set => _settingsState.SelectedThemePreference = value;
    }

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

    private void SelectThemePreference(ThemePreference value)
    {
        SelectedThemePreference = value;
    }

    private void SettingsStateOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SettingsState.SelectedMetric):
                OnPropertyChanged(nameof(SelectedMetric));
                OnPropertyChanged(nameof(SelectedMetricOption));
                break;
            case nameof(SettingsState.SelectedTokenProfile):
                OnPropertyChanged(nameof(SelectedTokenProfile));
                OnPropertyChanged(nameof(SelectedTokenProfileOption));
                break;
            case nameof(SettingsState.RespectGitIgnore):
                OnPropertyChanged(nameof(RespectGitIgnore));
                break;
            case nameof(SettingsState.RespectIgnore):
                OnPropertyChanged(nameof(RespectIgnore));
                break;
            case nameof(SettingsState.UseDefaultExcludes):
                OnPropertyChanged(nameof(UseDefaultExcludes));
                break;
            case nameof(SettingsState.SelectedThemePreference):
                OnPropertyChanged(nameof(SelectedThemePreference));
                OnPropertyChanged(nameof(IsSystemThemeSelected));
                OnPropertyChanged(nameof(IsLightThemeSelected));
                OnPropertyChanged(nameof(IsDarkThemeSelected));
                break;
        }
    }

    public ScanOptions BuildScanOptions() =>
        new()
        {
            TokenProfile = SelectedTokenProfile,
            RespectGitIgnore = RespectGitIgnore,
            RespectDotIgnore = RespectIgnore,
            UseDefaultExcludes = UseDefaultExcludes,
        };

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
