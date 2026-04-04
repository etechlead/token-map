using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Diagnostics;

namespace Clever.TokenMap.App.Design;

public sealed class MainWindowDesignViewModel : MainWindowViewModel
{
    public MainWindowDesignViewModel()
        : this(MainWindowViewModelDefaults.Create())
    {
    }

    private MainWindowDesignViewModel(MainWindowViewModelComposition composition)
        : base(
            composition.WorkspacePresenter,
            composition.About,
            composition.Toolbar,
            composition.ExcludesEditor,
            composition.FilePreview,
            composition.RecentFolders,
            composition.Issue,
            composition.Tree,
            composition.Summary,
            composition.PathShellService,
            composition.FilePreviewController,
            NullAppIssueReporter.Instance)
    {
    }
}
