using System.Collections.ObjectModel;
using System.Linq;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.App.ViewModels;

public sealed class ProjectTreeNodeViewModel : ViewModelBase
{
    public ProjectTreeNodeViewModel(ProjectNode node)
    {
        Node = node;
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
            node.Children.Select(child => new ProjectTreeNodeViewModel(child)));
    }

    public ProjectNode Node { get; }

    public string Name { get; }

    public string RelativePath { get; }

    public string KindText { get; }

    public string SecondaryText { get; }

    public ObservableCollection<ProjectTreeNodeViewModel> Children { get; }
}
