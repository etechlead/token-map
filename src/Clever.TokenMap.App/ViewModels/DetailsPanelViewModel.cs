using System.IO;
using System.Linq;
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
    private string breakdownText = "Breakdown: n/a";

    [ObservableProperty]
    private string languageText = "Language: n/a";

    [ObservableProperty]
    private string extensionText = "Extension: n/a";

    [ObservableProperty]
    private string sizeText = "Size: n/a";

    [ObservableProperty]
    private string descendantsText = "Descendants: n/a";

    [ObservableProperty]
    private string shareText = "Share: n/a";

    [ObservableProperty]
    private string topChildrenText = "Top children: n/a";

    [ObservableProperty]
    private string diagnosticsText = "Diagnostics: none";

    public void ShowNode(ProjectNode? node, ProjectNode? rootNode, string metric)
    {
        if (node is null)
        {
            ShowPlaceholder();
            return;
        }

        SelectionTitle = node.Name;
        PathText = $"Path: {GetRelativePath(node)}";
        KindText = $"Kind: {GetKindText(node)}";
        TokensText = $"Tokens: {node.Metrics.Tokens:N0}";
        LinesText = $"Lines: {node.Metrics.TotalLines:N0}";
        BreakdownText = BuildBreakdownText(node);
        LanguageText = $"Language: {node.Metrics.Language ?? "n/a"}";
        ExtensionText = $"Extension: {GetExtension(node)}";
        SizeText = $"Size: {FormatFileSize(node.Metrics.FileSizeBytes)}";
        DescendantsText = BuildDescendantsText(node);
        ShareText = BuildShareText(node, rootNode, metric);
        TopChildrenText = BuildTopChildrenText(node, metric);
        DiagnosticsText = $"Diagnostics: {node.DiagnosticMessage ?? "none"}";
    }

    public void ShowPlaceholder()
    {
        SelectionTitle = "No selection";
        PathText = "Select a node in the project tree to inspect metrics.";
        KindText = "Kind: n/a";
        TokensText = "Tokens: n/a";
        LinesText = "Lines: n/a";
        BreakdownText = "Breakdown: n/a";
        LanguageText = "Language: n/a";
        ExtensionText = "Extension: n/a";
        SizeText = "Size: n/a";
        DescendantsText = "Descendants: n/a";
        ShareText = "Share: n/a";
        TopChildrenText = "Top children: n/a";
        DiagnosticsText = "Diagnostics: none";
    }

    private static string BuildBreakdownText(ProjectNode node)
    {
        if (node.Metrics.CodeLines is null && node.Metrics.CommentLines is null && node.Metrics.BlankLines is null)
        {
            return "Breakdown: n/a";
        }

        return $"Breakdown: code {node.Metrics.CodeLines ?? 0:N0}, comments {node.Metrics.CommentLines ?? 0:N0}, blanks {node.Metrics.BlankLines ?? 0:N0}";
    }

    private static string BuildDescendantsText(ProjectNode node) =>
        $"Descendants: {node.Metrics.DescendantFileCount:N0} files, {node.Metrics.DescendantDirectoryCount:N0} dirs";

    private static string BuildShareText(ProjectNode node, ProjectNode? rootNode, string metric)
    {
        var total = GetMetricValue(rootNode, metric);
        var current = GetMetricValue(node, metric);
        if (total <= 0 || current <= 0)
        {
            return $"Share ({metric}): n/a";
        }

        return $"Share ({metric}): {current / total:P1}";
    }

    private static string BuildTopChildrenText(ProjectNode node, string metric)
    {
        if (node.Children.Count == 0)
        {
            return $"Top children ({metric}): n/a";
        }

        var topChildren = node.Children
            .OrderByDescending(child => GetMetricValue(child, metric))
            .ThenBy(child => child.Name, System.StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .Select(child => $"{child.Name} ({FormatMetricValue(GetMetricValue(child, metric))})")
            .ToArray();

        return $"Top children ({metric}): {string.Join(", ", topChildren)}";
    }

    private static string GetRelativePath(ProjectNode node) =>
        string.IsNullOrWhiteSpace(node.RelativePath) ? "(root)" : node.RelativePath;

    private static string GetKindText(ProjectNode node) =>
        node.Kind switch
        {
            Core.Enums.ProjectNodeKind.Root => "Root",
            Core.Enums.ProjectNodeKind.Directory => "Directory",
            _ => "File",
        };

    private static string GetExtension(ProjectNode node)
    {
        if (node.Kind != Core.Enums.ProjectNodeKind.File)
        {
            return "n/a";
        }

        var extension = Path.GetExtension(node.Name);
        return string.IsNullOrWhiteSpace(extension) ? "(none)" : extension;
    }

    private static double GetMetricValue(ProjectNode? node, string metric) =>
        node is null
            ? 0
            : metric switch
            {
                "Total lines" => node.Metrics.TotalLines,
                "Code lines" => node.Metrics.CodeLines ?? 0,
                _ => node.Metrics.Tokens,
            };

    private static string FormatMetricValue(double value) =>
        value >= 1
            ? value.ToString("N0")
            : value.ToString("0.##");

    private static string FormatFileSize(long bytes) =>
        bytes switch
        {
            >= 1024 * 1024 => $"{bytes / 1024d / 1024d:F1} MB",
            >= 1024 => $"{bytes / 1024d:F1} KB",
            _ => $"{bytes} B",
        };
}
