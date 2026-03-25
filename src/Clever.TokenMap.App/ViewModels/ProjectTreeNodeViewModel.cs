using System;
using System.Globalization;
using System.IO;
using Avalonia;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.ViewModels;

public partial class ProjectTreeNodeViewModel : ViewModelBase
{
    private const double TreeIndentStep = 14;
    private const double ParentShareBaseWidth = 104;

    public ProjectTreeNodeViewModel(
        ProjectNode node,
        int depth = 0,
        bool isExpanded = false,
        ProjectNode? parentNode = null,
        AnalysisMetric parentShareMetric = AnalysisMetric.Tokens)
    {
        Node = node;
        Depth = depth;
        Name = node.Name;
        RelativePath = string.IsNullOrEmpty(node.RelativePath) ? "(root)" : node.RelativePath;
        IsExpanded = isExpanded;
        ParentShareRatio = TryCalculateParentShareRatio(node, parentNode, parentShareMetric);
        ParentShareText = FormatParentShare(ParentShareRatio);
    }

    public ProjectNode Node { get; }

    public int Depth { get; }

    public string Name { get; }

    public string RelativePath { get; }

    public bool HasChildren => Node.Children.Count > 0;

    public Thickness IndentMargin => new(Depth * TreeIndentStep, 0, 0, 0);

    public string IconPath => $"avares://Clever.TokenMap.App/Assets/FileIcons/{GetIconFileName()}";

    public bool IsCollapsed => !IsExpanded;

    public string SizeText => FormatFileSize(Node.Metrics.FileSizeBytes);

    public string LinesText => FormatAnalysisMetric(Node.Metrics.NonEmptyLines);

    public string TokensText => FormatAnalysisMetric(Node.Metrics.Tokens);

    public double? ParentShareRatio { get; }

    public double ParentShareDisplayValue => Math.Clamp(ParentShareRatio ?? 0d, 0d, 1d);

    public string ParentShareText { get; }

    public Thickness ParentShareIndentMargin => new(Depth * TreeIndentStep, 0, 0, 0);

    public double ParentShareBlockWidth => Math.Max(0d, ParentShareBaseWidth - ParentShareIndentMargin.Left);

    public double ParentShareFillWidth => ParentShareBlockWidth * ParentShareDisplayValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IconPath))]
    [NotifyPropertyChangedFor(nameof(IsCollapsed))]
    private bool isExpanded;

    private string GetIconFileName()
    {
        return Node.Kind switch
        {
            ProjectNodeKind.Root => IsExpanded ? "folder-project-open.svg" : "folder-project.svg",
            ProjectNodeKind.Directory => GetDirectoryIconFileName(),
            _ => GetFileIconFileName(),
        };
    }

    private string GetDirectoryIconFileName()
    {
        var normalizedName = Node.Name.Trim().ToLowerInvariant();
        return normalizedName switch
        {
            "src" => IsExpanded ? "folder-src-open.svg" : "folder-src.svg",
            "test" or "tests" or "__tests__" => IsExpanded ? "folder-test-open.svg" : "folder-test.svg",
            "docs" or "doc" or "documentation" => IsExpanded ? "folder-markdown-open.svg" : "folder-markdown.svg",
            "scripts" or "script" => IsExpanded ? "folder-scripts-open.svg" : "folder-scripts.svg",
            ".github" or ".git" => IsExpanded ? "folder-github-open.svg" : "folder-github.svg",
            _ => IsExpanded ? "folder-base-open.svg" : "folder-base.svg",
        };
    }

    private string GetFileIconFileName()
    {
        var normalizedName = Node.Name.Trim().ToLowerInvariant();
        if (normalizedName is "dockerfile" or "docker-compose.yml" or "docker-compose.yaml")
        {
            return "docker.svg";
        }

        if (normalizedName is ".editorconfig")
        {
            return "editorconfig.svg";
        }

        if (normalizedName is ".gitignore" or ".gitattributes" or ".gitmodules")
        {
            return "git.svg";
        }

        return Path.GetExtension(normalizedName) switch
        {
            ".cs" or ".csx" or ".csproj" or ".sln" => "csharp.svg",
            ".ts" or ".mts" or ".cts" => "typescript.svg",
            ".tsx" => "react_ts.svg",
            ".js" or ".mjs" or ".cjs" => "javascript.svg",
            ".jsx" => "react.svg",
            ".css" or ".scss" or ".sass" or ".less" => "css.svg",
            ".html" or ".htm" => "html.svg",
            ".json" or ".jsonc" => "json.svg",
            ".md" or ".mdx" => "markdown.svg",
            ".yaml" or ".yml" => "yaml.svg",
            ".xml" or ".props" or ".targets" or ".resx" => "xml.svg",
            ".toml" => "toml.svg",
            ".py" or ".pyi" => "python.svg",
            ".sql" => "database.svg",
            _ => "document.svg",
        };
    }

    private static string FormatFileSize(long bytes) =>
        bytes switch
        {
            >= 1024L * 1024L * 1024L => $"{bytes / 1024d / 1024d / 1024d:F1} GB",
            >= 1024 * 1024 => $"{bytes / 1024d / 1024d:F1} MB",
            >= 1024 => $"{bytes / 1024d:F1} KB",
            _ => $"{bytes} B",
        };

    private string FormatAnalysisMetric(long value) =>
        Node.SkippedReason is not null
            ? "n/a"
            : value.ToString("N0", CultureInfo.CurrentCulture);

    internal static double? TryCalculateParentShareRatio(ProjectNode node, ProjectNode? parentNode, AnalysisMetric metric)
    {
        var normalizedMetric = NormalizeMetric(metric);

        if (node.Kind == ProjectNodeKind.Root && parentNode is null)
        {
            return 1d;
        }

        if (parentNode is null)
        {
            return null;
        }

        var currentValue = TryGetMetricValue(node, normalizedMetric);
        if (currentValue is null)
        {
            return null;
        }

        var parentValue = TryGetMetricValue(parentNode, normalizedMetric);
        if (parentValue is null || parentValue.Value <= 0)
        {
            return null;
        }

        return currentValue.Value / parentValue.Value;
    }

    internal static double? TryGetMetricValue(ProjectNode node, AnalysisMetric metric)
    {
        var normalizedMetric = NormalizeMetric(metric);
        if (normalizedMetric != AnalysisMetric.Size && node.SkippedReason is not null)
        {
            return null;
        }

        return normalizedMetric switch
        {
            AnalysisMetric.TotalLines => node.Metrics.NonEmptyLines,
            AnalysisMetric.Size => node.Metrics.FileSizeBytes,
            _ => node.Metrics.Tokens,
        };
    }

    private static string FormatParentShare(double? ratio) =>
        ratio is null
            ? "n/a"
            : $"{(ratio.Value * 100d).ToString("N1", CultureInfo.CurrentCulture)}%";

    private static AnalysisMetric NormalizeMetric(AnalysisMetric metric) =>
        metric is AnalysisMetric.TotalLines or AnalysisMetric.NonEmptyLines
            ? AnalysisMetric.TotalLines
            : metric;
}
