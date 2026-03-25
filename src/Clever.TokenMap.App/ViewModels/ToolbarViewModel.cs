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
    private readonly CurrentFolderSettingsState _currentFolderSettingsState;
    private readonly SettingsState _settingsState;

    public ToolbarViewModel(
        SettingsState settingsState,
        CurrentFolderSettingsState currentFolderSettingsState,
        IAsyncRelayCommand openFolderCommand,
        IAsyncRelayCommand rescanCommand,
        IRelayCommand cancelCommand)
    {
        _settingsState = settingsState;
        _currentFolderSettingsState = currentFolderSettingsState;
        OpenFolderCommand = openFolderCommand;
        RescanCommand = rescanCommand;
        CancelCommand = cancelCommand;
        _settingsState.PropertyChanged += SettingsStateOnPropertyChanged;
        _currentFolderSettingsState.PropertyChanged += CurrentFolderSettingsStateOnPropertyChanged;
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

    public string TreemapTitle => $"Treemap - {TreemapMetricDisplay}";

    public string TreemapMetricDisplay =>
        SelectedMetric switch
        {
            AnalysisMetric.TotalLines => "lines",
            AnalysisMetric.NonEmptyLines => "lines",
            AnalysisMetric.Size => "size",
            _ => "tokens",
        };

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

    public bool IsSizeMetricSelected
    {
        get => SelectedMetric == AnalysisMetric.Size;
        set
        {
            if (value)
            {
                SelectedMetric = AnalysisMetric.Size;
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

    public bool UseGlobalExcludes
    {
        get => _settingsState.UseGlobalExcludes;
        set => _settingsState.UseGlobalExcludes = value;
    }

    public bool UseFolderExcludes
    {
        get => _currentFolderSettingsState.UseFolderExcludes;
        set => _currentFolderSettingsState.UseFolderExcludes = value;
    }

    public bool HasCurrentFolderSettings => _currentFolderSettingsState.HasActiveFolder;

    public bool CanConfigureFolderExcludes => CanConfigureScanOptions && HasCurrentFolderSettings;

    public string CurrentFolderSettingsTitle => HasCurrentFolderSettings
        ? $"Current folder: {GetFolderDisplayName(_currentFolderSettingsState.ActiveRootPath)}"
        : "Current folder";

    public ThemePreference SelectedThemePreference
    {
        get => _settingsState.SelectedThemePreference;
        set => _settingsState.SelectedThemePreference = value;
    }

    public TreemapPalette SelectedTreemapPalette
    {
        get => _settingsState.SelectedTreemapPalette;
        set => _settingsState.SelectedTreemapPalette = value;
    }

    public bool IsSystemThemeSelected => SelectedThemePreference == ThemePreference.System;

    public bool IsLightThemeSelected => SelectedThemePreference == ThemePreference.Light;

    public bool IsDarkThemeSelected => SelectedThemePreference == ThemePreference.Dark;

    public bool IsClassicTreemapPaletteSelected
    {
        get => SelectedTreemapPalette == TreemapPalette.Classic;
        set
        {
            if (value)
            {
                SelectedTreemapPalette = TreemapPalette.Classic;
            }
        }
    }

    public bool IsWeightedTreemapPaletteSelected
    {
        get => SelectedTreemapPalette == TreemapPalette.Weighted;
        set
        {
            if (value)
            {
                SelectedTreemapPalette = TreemapPalette.Weighted;
            }
        }
    }

    public bool IsStudioTreemapPaletteSelected
    {
        get => SelectedTreemapPalette == TreemapPalette.Studio;
        set
        {
            if (value)
            {
                SelectedTreemapPalette = TreemapPalette.Studio;
            }
        }
    }

    public void RefreshAvailability(bool isBusy, bool hasSnapshot)
    {
        CanConfigureScanOptions = !isBusy;
        CanChangeMetric = hasSnapshot && !isBusy;
        IsStopVisible = isBusy;
        OnPropertyChanged(nameof(CanConfigureFolderExcludes));
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
                OnPropertyChanged(nameof(IsSizeMetricSelected));
                OnPropertyChanged(nameof(TreemapMetricDisplay));
                OnPropertyChanged(nameof(TreemapTitle));
                break;
            case nameof(SettingsState.RespectGitIgnore):
                OnPropertyChanged(nameof(RespectGitIgnore));
                break;
            case nameof(SettingsState.UseGlobalExcludes):
                OnPropertyChanged(nameof(UseGlobalExcludes));
                break;
            case nameof(SettingsState.SelectedThemePreference):
                OnPropertyChanged(nameof(SelectedThemePreference));
                OnPropertyChanged(nameof(IsSystemThemeSelected));
                OnPropertyChanged(nameof(IsLightThemeSelected));
                OnPropertyChanged(nameof(IsDarkThemeSelected));
                break;
            case nameof(SettingsState.SelectedTreemapPalette):
                OnPropertyChanged(nameof(SelectedTreemapPalette));
                OnPropertyChanged(nameof(IsClassicTreemapPaletteSelected));
                OnPropertyChanged(nameof(IsWeightedTreemapPaletteSelected));
                OnPropertyChanged(nameof(IsStudioTreemapPaletteSelected));
                break;
        }
    }

    private void CurrentFolderSettingsStateOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(CurrentFolderSettingsState.ActiveRootPath):
                OnPropertyChanged(nameof(HasCurrentFolderSettings));
                OnPropertyChanged(nameof(CanConfigureFolderExcludes));
                OnPropertyChanged(nameof(CurrentFolderSettingsTitle));
                break;
            case nameof(CurrentFolderSettingsState.UseFolderExcludes):
                OnPropertyChanged(nameof(UseFolderExcludes));
                break;
        }
    }

    public ScanOptions BuildScanOptions() =>
        new()
        {
            RespectGitIgnore = RespectGitIgnore,
            UseGlobalExcludes = UseGlobalExcludes,
            GlobalExcludes = [.. _settingsState.GlobalExcludes],
            UseFolderExcludes = UseFolderExcludes,
            FolderExcludes = [.. _currentFolderSettingsState.FolderExcludes],
        };

    private static string GetFolderDisplayName(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return string.Empty;
        }

        var trimmedPath = folderPath.Trim();
        var displayName = System.IO.Path.GetFileName(trimmedPath.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(displayName)
            ? trimmedPath
            : displayName;
    }
}
