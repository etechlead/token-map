using System;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Preview;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.State;

public partial class FilePreviewState : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPathActionsEnabled))]
    [NotifyPropertyChangedFor(nameof(DisplayPath))]
    private ProjectNode? node;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    [NotifyPropertyChangedFor(nameof(ShowEditor))]
    [NotifyPropertyChangedFor(nameof(ShowStatusPanel))]
    private bool isVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEditor))]
    [NotifyPropertyChangedFor(nameof(ShowStatusPanel))]
    private bool isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEditor))]
    [NotifyPropertyChangedFor(nameof(ShowStatusPanel))]
    private FilePreviewReadStatus? status;

    [ObservableProperty]
    private string content = string.Empty;

    [ObservableProperty]
    private string statusTitle = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public bool IsOpen => IsVisible;

    public bool ShowEditor => IsVisible && !IsLoading && Status == FilePreviewReadStatus.Success;

    public bool ShowStatusPanel => IsVisible && !ShowEditor;

    public bool IsPathActionsEnabled => Node is not null;

    public string DisplayName => Node?.Name ?? string.Empty;

    public string FullPath => Node?.FullPath ?? string.Empty;

    public string RelativePath => Node?.RelativePath ?? string.Empty;

    public string DisplayPath => string.IsNullOrWhiteSpace(RelativePath) ? FullPath : RelativePath;

    public void ShowLoading(ProjectNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        Node = node;
        Content = string.Empty;
        Status = null;
        StatusTitle = "Loading preview";
        StatusMessage = "Reading file contents.";
        IsLoading = true;
        IsVisible = true;
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(FullPath));
        OnPropertyChanged(nameof(RelativePath));
    }

    public void ShowResult(ProjectNode node, FilePreviewContentResult result)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(result);

        Node = node;
        IsLoading = false;
        Status = result.Status;
        Content = result.Status == FilePreviewReadStatus.Success ? result.Content ?? string.Empty : string.Empty;
        (StatusTitle, StatusMessage) = BuildStatusText(result);
        IsVisible = true;
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(FullPath));
        OnPropertyChanged(nameof(RelativePath));
    }

    public void Close()
    {
        IsVisible = false;
        IsLoading = false;
        Node = null;
        Status = null;
        Content = string.Empty;
        StatusTitle = string.Empty;
        StatusMessage = string.Empty;
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(FullPath));
        OnPropertyChanged(nameof(RelativePath));
    }

    private static (string Title, string Message) BuildStatusText(FilePreviewContentResult result)
    {
        return result.Status switch
        {
            FilePreviewReadStatus.Success => (string.Empty, string.Empty),
            FilePreviewReadStatus.NotText => (
                "Preview unavailable",
                "TokenMap only shows text files in the built-in preview."),
            FilePreviewReadStatus.TooLarge => (
                "File too large",
                "This file is larger than 2 MiB. Open it in the default app to inspect the full contents."),
            FilePreviewReadStatus.Missing => (
                "File missing",
                "The file is no longer available at its original path."),
            _ => (
                "Preview failed",
                string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "TokenMap could not read this file."
                    : result.ErrorMessage),
        };
    }
}
