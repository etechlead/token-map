namespace Clever.TokenMap.App.ViewModels;

public sealed class MainWindowDesignViewModel : MainWindowViewModel
{
    public MainWindowDesignViewModel()
        : this(MainWindowViewModelDefaults.Create())
    {
    }

    private MainWindowDesignViewModel(MainWindowViewModelDependencies dependencies)
        : base(
            dependencies.AnalysisSessionController,
            dependencies.TreemapNavigationState,
            dependencies.SettingsCoordinator,
            dependencies.FolderPathService,
            dependencies.PathShellService)
    {
    }
}
