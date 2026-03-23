using System;
using System.ComponentModel;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Clever.TokenMap.Core.Enums;

namespace Clever.TokenMap.App.ViewModels;

public partial class ToolbarViewModel : ViewModelBase
{
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

    [ObservableProperty]
    private bool canConfigureScanOptions = true;

    [ObservableProperty]
    private bool canChangeMetric;

    [ObservableProperty]
    private bool isStopVisible;

    [ObservableProperty]
    private string selectedFolderDisplay = "No folder selected";

    public bool IsTokensMetricSelected
    {
        get => SelectedMetric == AnalysisMetric.Tokens;
        set
        {
            if (value)
            {
                SelectedMetric = AnalysisMetric.Tokens;
            }
        }
    }

    public bool IsLinesMetricSelected
    {
        get => SelectedMetric is AnalysisMetric.TotalLines or AnalysisMetric.NonEmptyLines;
        set
        {
            if (value)
            {
                SelectedMetric = AnalysisMetric.TotalLines;
            }
        }
    }

    public AnalysisMetric SelectedMetric
    {
        get => _settingsState.SelectedMetric;
        set => _settingsState.SelectedMetric = value;
    }

    public bool RespectGitIgnore
    {
        get => _settingsState.RespectGitIgnore;
        set => _settingsState.RespectGitIgnore = value;
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
                OnPropertyChanged(nameof(IsTokensMetricSelected));
                OnPropertyChanged(nameof(IsLinesMetricSelected));
                break;
            case nameof(SettingsState.RespectGitIgnore):
                OnPropertyChanged(nameof(RespectGitIgnore));
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
            RespectGitIgnore = RespectGitIgnore,
            UseDefaultExcludes = UseDefaultExcludes,
        };
}
