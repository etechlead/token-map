using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Diagnostics;

namespace Clever.TokenMap.App.Views;

public partial class RefactorPromptModalView : UserControl
{
    private int _copyFeedbackVersion;

    public RefactorPromptModalView()
    {
        InitializeComponent();
    }

    private async void CopyRefactorPromptButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { RefactorPrompt: { } refactorPrompt } viewModel ||
            sender is not Button copyButton)
        {
            return;
        }

        copyButton.IsEnabled = false;
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
            {
                ReportCopyFailure(viewModel, exception: null);
                return;
            }

            await clipboard.SetTextAsync(refactorPrompt.PromptText);
            refactorPrompt.CopyFeedbackState = RefactorPromptCopyFeedbackState.Success;
            var version = ++_copyFeedbackVersion;
            _ = ResetCopyFeedbackAsync(refactorPrompt, version);
        }
        catch (Exception exception)
        {
            ReportCopyFailure(viewModel, exception);
        }
        finally
        {
            copyButton.IsEnabled = true;
        }
    }

    private async Task ResetCopyFeedbackAsync(RefactorPromptViewModel refactorPrompt, int version)
    {
        await Task.Delay(TimeSpan.FromSeconds(1.8));
        await Dispatcher.UIThread.InvokeAsync(
            () =>
            {
                if (version == _copyFeedbackVersion)
                {
                    refactorPrompt.CopyFeedbackState = RefactorPromptCopyFeedbackState.Idle;
                }
            },
            DispatcherPriority.Background);
    }

    private static void ReportCopyFailure(MainWindowViewModel viewModel, Exception? exception)
    {
        viewModel.ReportIssue(new AppIssue
        {
            Code = "refactor_prompt.copy_failed",
            UserMessage = "TokenMap could not copy the refactor prompt to the clipboard.",
            TechnicalMessage = "Copying the refactor prompt text to the clipboard failed.",
            Exception = exception,
        });
    }
}
