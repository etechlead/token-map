using System;
using System.Globalization;
using Avalonia.Media;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.ViewModels;

public enum ShareCopyFeedbackState
{
    Idle = 0,
    Success = 1,
}

public partial class ShareSnapshotViewModel : ViewModelBase
{
    private static readonly IBrush CopyButtonDefaultBrush = new SolidColorBrush(Color.Parse("#0F6CBD"));
    private static readonly IBrush CopyButtonSuccessBrush = new SolidColorBrush(Color.Parse("#2A8A57"));
    private static readonly IBrush CopyButtonForegroundBrush = Brushes.White;
    private readonly LocalizationState _localization;

    public ShareSnapshotViewModel(ProjectSnapshot snapshot, string? defaultProjectName, LocalizationState localization)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        ProjectName = string.IsNullOrWhiteSpace(defaultProjectName)
            ? FolderDisplayText.GetFolderDisplayName(snapshot.RootPath)
            : defaultProjectName.Trim();
    }

    public ProjectSnapshot Snapshot { get; }

    public ProjectNode PreviewRootNode => Snapshot.Root;

    public MetricId TreemapMetric { get; } = MetricIds.Tokens;

    public TreemapPalette PreviewTreemapPalette { get; } = TreemapPalette.Plain;

    public string TokenValue => MetricValueFormatter.Format(
        MetricIds.Tokens,
        Snapshot.Root.ComputedMetrics.GetOrDefault(MetricIds.Tokens),
        CultureInfo.CurrentCulture);

    public string LineValue => MetricValueFormatter.Format(
        MetricIds.NonEmptyLines,
        Snapshot.Root.ComputedMetrics.GetOrDefault(MetricIds.NonEmptyLines),
        CultureInfo.CurrentCulture);

    public string FileValue => Snapshot.Root.Summary.DescendantFileCount.ToString("N0", CultureInfo.CurrentCulture);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowProjectName))]
    [NotifyPropertyChangedFor(nameof(ShowProjectNamePlaceholder))]
    [NotifyPropertyChangedFor(nameof(DisplayProjectName))]
    private bool includeProjectName = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowProjectName))]
    [NotifyPropertyChangedFor(nameof(ShowProjectNamePlaceholder))]
    [NotifyPropertyChangedFor(nameof(DisplayProjectName))]
    private string projectName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCopyFeedbackIdle))]
    [NotifyPropertyChangedFor(nameof(IsCopyFeedbackSuccess))]
    [NotifyPropertyChangedFor(nameof(CopyButtonText))]
    [NotifyPropertyChangedFor(nameof(CopyButtonBackground))]
    [NotifyPropertyChangedFor(nameof(CopyButtonBorderBrush))]
    [NotifyPropertyChangedFor(nameof(CopyButtonForeground))]
    private ShareCopyFeedbackState copyFeedbackState;

    public bool ShowProjectName => IncludeProjectName && !string.IsNullOrWhiteSpace(DisplayProjectName);

    public bool ShowProjectNamePlaceholder => !ShowProjectName;

    public string DisplayProjectName => ProjectName.Trim();

    public bool IsCopyFeedbackIdle => CopyFeedbackState == ShareCopyFeedbackState.Idle;

    public bool IsCopyFeedbackSuccess => CopyFeedbackState == ShareCopyFeedbackState.Success;

    public string TokensLabel => _localization.ShareCardTokens;

    public string LinesLabel => _localization.ShareCardLines;

    public string FilesLabel => _localization.ShareCardFiles;

    public string MadeWithLabel => _localization.ShareCardMadeWith;

    public string IncludeProjectNameLabel => _localization.IncludeProjectName;

    public string ProjectNameWatermark => _localization.ProjectNameWatermark;

    public string CopyButtonText =>
        CopyFeedbackState switch
        {
            ShareCopyFeedbackState.Success => _localization.Copied,
            _ => _localization.Copy,
        };

    public IBrush CopyButtonBackground =>
        CopyFeedbackState switch
        {
            ShareCopyFeedbackState.Success => CopyButtonSuccessBrush,
            _ => CopyButtonDefaultBrush,
        };

    public IBrush CopyButtonBorderBrush => CopyButtonBackground;

    public IBrush CopyButtonForeground { get; } = CopyButtonForegroundBrush;

    internal void RefreshLocalization()
    {
        OnPropertyChanged(nameof(TokensLabel));
        OnPropertyChanged(nameof(LinesLabel));
        OnPropertyChanged(nameof(FilesLabel));
        OnPropertyChanged(nameof(MadeWithLabel));
        OnPropertyChanged(nameof(IncludeProjectNameLabel));
        OnPropertyChanged(nameof(ProjectNameWatermark));
        OnPropertyChanged(nameof(CopyButtonText));
    }
}
