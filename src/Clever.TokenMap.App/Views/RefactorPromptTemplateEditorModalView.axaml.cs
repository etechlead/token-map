using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Clever.TokenMap.App.Views;

public partial class RefactorPromptTemplateEditorModalView : UserControl
{
    public RefactorPromptTemplateEditorModalView()
    {
        InitializeComponent();
    }

    private void PlaceholderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string token } ||
            this.FindControl<TextBox>("RefactorPromptTemplateEditorTextBox") is not { } editor)
        {
            return;
        }

        InsertTokenAtCaret(editor, token);
        editor.Focus();
    }

    internal static void InsertTokenAtCaret(TextBox editor, string token)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var text = editor.Text ?? string.Empty;
        var selectionStart = Math.Clamp(Math.Min(editor.SelectionStart, editor.SelectionEnd), 0, text.Length);
        var selectionEnd = Math.Clamp(Math.Max(editor.SelectionStart, editor.SelectionEnd), 0, text.Length);
        var updatedText = string.Concat(
            text[..selectionStart],
            token,
            text[selectionEnd..]);

        editor.Text = updatedText;
        editor.CaretIndex = selectionStart + token.Length;
    }
}
