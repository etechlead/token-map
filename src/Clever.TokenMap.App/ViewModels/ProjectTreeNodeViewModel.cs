using System.Collections.ObjectModel;
using System.Linq;
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

    public Thickness IndentMargin => new(Depth * 16, 0, 0, 0);

    public string ExpanderGlyph => !HasChildren ? string.Empty : IsExpanded ? "-" : "+";

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
    [NotifyPropertyChangedFor(nameof(ExpanderGlyph))]
    private bool isExpanded;

    private static string FormatFileSize(long bytes) =>
        bytes switch
        {
            >= 1024L * 1024L * 1024L => $"{bytes / 1024d / 1024d / 1024d:F1} GB",
            >= 1024 * 1024 => $"{bytes / 1024d / 1024d:F1} MB",
            >= 1024 => $"{bytes / 1024d:F1} KB",
            _ => $"{bytes} B",
        };
}
