using Clever.TokenMap.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.ViewModels;

public partial class DetailsPanelViewModel : ViewModelBase
{
    [ObservableProperty]
    private string selectionTitle = "No selection";

    [ObservableProperty]
    private string pathText = "Select a node in the project tree to inspect metrics.";

    [ObservableProperty]
    private string kindText = "Kind: n/a";

    [ObservableProperty]
    private string tokensText = "Tokens: n/a";

    [ObservableProperty]
    private string linesText = "Lines: n/a";

    [ObservableProperty]
    private string languageText = "Language: n/a";

    [ObservableProperty]
    private string sizeText = "Size: n/a";

    [ObservableProperty]
    private string diagnosticsText = "Diagnostics: none";

    public void ShowNode(ProjectTreeNodeViewModel? nodeViewModel)
    {
        if (nodeViewModel is null)
        {
            ShowPlaceholder();
            return;
        }

        var node = nodeViewModel.Node;
        SelectionTitle = node.Name;
        PathText = $"Path: {nodeViewModel.RelativePath}";
        KindText = $"Kind: {nodeViewModel.KindText}";
        TokensText = $"Tokens: {node.Metrics.Tokens:N0}";
        LinesText = $"Lines: {node.Metrics.TotalLines:N0}";
        LanguageText = $"Language: {node.Metrics.Language ?? "n/a"}";
        SizeText = $"Size: {FormatFileSize(node.Metrics.FileSizeBytes)}";
        DiagnosticsText = $"Diagnostics: {node.DiagnosticMessage ?? "none"}";
    }

    public void ShowPlaceholder()
    {
        SelectionTitle = "No selection";
        PathText = "Select a node in the project tree to inspect metrics.";
        KindText = "Kind: n/a";
        TokensText = "Tokens: n/a";
        LinesText = "Lines: n/a";
        LanguageText = "Language: n/a";
        SizeText = "Size: n/a";
        DiagnosticsText = "Diagnostics: none";
    }

    private static string FormatFileSize(long bytes) =>
        bytes switch
        {
            >= 1024 * 1024 => $"{bytes / 1024d / 1024d:F1} MB",
            >= 1024 => $"{bytes / 1024d:F1} KB",
            _ => $"{bytes} B",
        };
}
