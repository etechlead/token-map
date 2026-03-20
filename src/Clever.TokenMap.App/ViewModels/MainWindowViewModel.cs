using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public string WindowTitle => "TokenMap";

    public string SelectedFolderDisplay => "No folder selected";

    public string SummaryText => "MVP bootstrap shell is ready. Folder scanning and metrics will be wired in the next stages.";

    public string StatusText => "Idle";

    public IReadOnlyList<string> MetricOptions { get; } =
    [
        "Tokens",
        "Total lines",
        "Code lines",
    ];

    public IReadOnlyList<string> TokenProfiles { get; } =
    [
        "o200k_base",
        "cl100k_base",
        "p50k_base",
    ];

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
}
