using System;
using System.Globalization;
using Avalonia.Media;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
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

    public ShareSnapshotViewModel(ProjectSnapshot snapshot, string? defaultProjectName)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        ProjectName = string.IsNullOrWhiteSpace(defaultProjectName)
            ? FolderDisplayText.GetFolderDisplayName(snapshot.RootPath)
            : defaultProjectName.Trim();
    }

    public ProjectSnapshot Snapshot { get; }

    public ProjectNode PreviewRootNode => Snapshot.Root;

    public AnalysisMetric TreemapMetric { get; } = AnalysisMetric.Tokens;

    public TreemapPalette PreviewTreemapPalette { get; } = TreemapPalette.Plain;

    public string TokenValue => Snapshot.Root.Metrics.Tokens.ToString("N0", CultureInfo.CurrentCulture);

    public string LineValue => Snapshot.Root.Metrics.NonEmptyLines.ToString("N0", CultureInfo.CurrentCulture);

    public string FileValue => Snapshot.Root.Metrics.DescendantFileCount.ToString("N0", CultureInfo.CurrentCulture);

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

    public string CopyButtonText =>
        CopyFeedbackState switch
        {
            ShareCopyFeedbackState.Success => "Copied",
            _ => "Copy",
        };

    public IBrush CopyButtonBackground =>
        CopyFeedbackState switch
        {
            ShareCopyFeedbackState.Success => CopyButtonSuccessBrush,
            _ => CopyButtonDefaultBrush,
        };

    public IBrush CopyButtonBorderBrush => CopyButtonBackground;

    public IBrush CopyButtonForeground { get; } = CopyButtonForegroundBrush;
}
