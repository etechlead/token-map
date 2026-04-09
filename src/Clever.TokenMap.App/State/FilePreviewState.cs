using System;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Preview;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.State;

public partial class FilePreviewState : ObservableObject
{
    private readonly LocalizationState _localization;
    private FilePreviewContentResult? _lastResult;

    public FilePreviewState(LocalizationState localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
    }

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
        _lastResult = null;
        Content = string.Empty;
        Status = null;
        StatusTitle = _localization.LoadingPreviewTitle;
        StatusMessage = _localization.LoadingPreviewMessage;
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
        _lastResult = result;
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
        _lastResult = null;
        Content = string.Empty;
        StatusTitle = string.Empty;
        StatusMessage = string.Empty;
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(FullPath));
        OnPropertyChanged(nameof(RelativePath));
    }

    internal void RefreshLocalization()
    {
        if (IsLoading)
        {
            StatusTitle = _localization.LoadingPreviewTitle;
            StatusMessage = _localization.LoadingPreviewMessage;
            return;
        }

        if (_lastResult is not null)
        {
            (StatusTitle, StatusMessage) = BuildStatusText(_lastResult);
        }
    }

    private (string Title, string Message) BuildStatusText(FilePreviewContentResult result)
    {
        return result.Status switch
        {
            FilePreviewReadStatus.Success => (string.Empty, string.Empty),
            FilePreviewReadStatus.NotText => (
                _localization.PreviewUnavailableTitle,
                _localization.PreviewUnavailableMessage),
            FilePreviewReadStatus.TooLarge => (
                _localization.FileTooLargeTitle,
                _localization.FileTooLargeMessage),
            FilePreviewReadStatus.Missing => (
                _localization.FileMissingTitle,
                _localization.FileMissingMessage),
            _ => (
                _localization.PreviewFailedTitle,
                string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? _localization.PreviewFailedMessage
                    : result.ErrorMessage),
        };
    }
}
