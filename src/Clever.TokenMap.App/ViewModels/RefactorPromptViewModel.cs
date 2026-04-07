using System;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.ViewModels;

public enum RefactorPromptCopyFeedbackState
{
    Idle = 0,
    Success = 1,
}

public partial class RefactorPromptViewModel : ViewModelBase
{
    private static readonly IBrush CopyButtonDefaultBrush = new ImmutableSolidColorBrush(Color.Parse("#0F6CBD"));
    private static readonly IBrush CopyButtonSuccessBrush = new ImmutableSolidColorBrush(Color.Parse("#2A8A57"));
    private static readonly IBrush CopyButtonForegroundBrush = Brushes.White;

    public RefactorPromptViewModel(string relativePath, string promptText)
    {
        RelativePath = string.IsNullOrWhiteSpace(relativePath)
            ? "."
            : relativePath.Trim();
        PromptText = promptText ?? throw new ArgumentNullException(nameof(promptText));
    }

    public string RelativePath { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCopyFeedbackIdle))]
    [NotifyPropertyChangedFor(nameof(IsCopyFeedbackSuccess))]
    [NotifyPropertyChangedFor(nameof(CopyButtonText))]
    [NotifyPropertyChangedFor(nameof(CopyButtonBackground))]
    [NotifyPropertyChangedFor(nameof(CopyButtonBorderBrush))]
    [NotifyPropertyChangedFor(nameof(CopyButtonForeground))]
    private RefactorPromptCopyFeedbackState copyFeedbackState;

    [ObservableProperty]
    private string promptText;

    public bool IsCopyFeedbackIdle => CopyFeedbackState == RefactorPromptCopyFeedbackState.Idle;

    public bool IsCopyFeedbackSuccess => CopyFeedbackState == RefactorPromptCopyFeedbackState.Success;

    public string CopyButtonText =>
        CopyFeedbackState switch
        {
            RefactorPromptCopyFeedbackState.Success => "Copied",
            _ => "Copy",
        };

    public IBrush CopyButtonBackground =>
        CopyFeedbackState switch
        {
            RefactorPromptCopyFeedbackState.Success => CopyButtonSuccessBrush,
            _ => CopyButtonDefaultBrush,
        };

    public IBrush CopyButtonBorderBrush => CopyButtonBackground;

    public IBrush CopyButtonForeground { get; } = CopyButtonForegroundBrush;
}
