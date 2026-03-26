using Clever.TokenMap.App.ViewModels;

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
            composition.Toolbar,
            composition.ExcludesEditor,
            composition.RecentFolders,
            composition.Tree,
            composition.Summary,
            composition.PathShellService)
    {
    }
}
