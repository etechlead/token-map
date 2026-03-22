using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using Avalonia;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.ViewModels;

public partial class ProjectTreeNodeViewModel : ViewModelBase
{
    public ProjectTreeNodeViewModel(ProjectNode node, ProjectTreeNodeViewModel? parent = null)
    {
        Node = node;
        Parent = parent;
        Depth = parent is null ? 0 : parent.Depth + 1;
        Name = node.Name;
        RelativePath = string.IsNullOrEmpty(node.RelativePath) ? "(root)" : node.RelativePath;
        KindText = node.Kind switch
        {
            ProjectNodeKind.Root => "Root",
            ProjectNodeKind.Directory => "Directory",
            _ => "File",
        };
        SecondaryText = node.Kind == ProjectNodeKind.File
            ? $"{node.Metrics.Tokens:N0} tok"
            : $"{node.Metrics.DescendantFileCount:N0} files";
        Children = new ObservableCollection<ProjectTreeNodeViewModel>(
            node.Children.Select(child => new ProjectTreeNodeViewModel(child, this)));
    }

    public ProjectNode Node { get; }

    public ProjectTreeNodeViewModel? Parent { get; }

    public int Depth { get; }

    public string Name { get; }

    public string RelativePath { get; }

    public string KindText { get; }

    public string SecondaryText { get; }

    public ObservableCollection<ProjectTreeNodeViewModel> Children { get; }

    public bool HasChildren => Children.Count > 0;

    public Thickness IndentMargin => new(Depth * 14, 0, 0, 0);

    public string IconPath => $"avares://Clever.TokenMap.App/Assets/FileIcons/{GetIconFileName()}";

    public bool IsCollapsed => !IsExpanded;

    public string SizeText => FormatFileSize(Node.Metrics.FileSizeBytes);

    public string TotalLinesText => $"{Node.Metrics.TotalLines:N0}";

    public string TokensText => $"{Node.Metrics.Tokens:N0}";

    public string FilesText => Node.Kind switch
    {
        ProjectNodeKind.Directory => $"{Node.Metrics.DescendantFileCount:N0}",
        ProjectNodeKind.Root => $"{Node.Metrics.DescendantFileCount:N0}",
        _ => string.Empty,
    };

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
}
