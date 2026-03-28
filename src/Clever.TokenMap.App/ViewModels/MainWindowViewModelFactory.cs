using System;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Interfaces;
using CommunityToolkit.Mvvm.Input;

namespace Clever.TokenMap.App.ViewModels;

public sealed record MainWindowViewModelFactoryDependencies(
    IAnalysisSessionController AnalysisSessionController,
    ISettingsCoordinator SettingsCoordinator,
    IFolderPathService FolderPathService,
    IPathShellService PathShellService,
    TreemapNavigationState? TreemapNavigationState = null,
    AppAboutInfo? AboutInfo = null);

public sealed record MainWindowViewModelComposition(
    MainWindowViewModel MainWindowViewModel,
    MainWindowWorkspacePresenter WorkspacePresenter,
    AboutViewModel About,
    ToolbarViewModel Toolbar,
    ExcludesEditorViewModel ExcludesEditor,
    RecentFoldersViewModel RecentFolders,
    ProjectTreeViewModel Tree,
    SummaryViewModel Summary,
    TreemapNavigationState TreemapNavigationState,
    IPathShellService PathShellService);

public static class MainWindowViewModelFactory
{
    public static MainWindowViewModelComposition Create(MainWindowViewModelFactoryDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);

        var analysisSessionController = dependencies.AnalysisSessionController;
        var settingsCoordinator = dependencies.SettingsCoordinator;
        var treemapNavigationState = dependencies.TreemapNavigationState ?? new TreemapNavigationState();
        var about = new AboutViewModel(
            dependencies.AboutInfo ?? AppAboutInfo.CreateDefault(),
            dependencies.PathShellService);

        var toolbar = new ToolbarViewModel(
            settingsCoordinator,
            new AsyncRelayCommand(
                () => analysisSessionController.OpenFolderAsync(settingsCoordinator.BuildCurrentScanOptions()),
                () => !analysisSessionController.IsBusy),
            new AsyncRelayCommand(
                () => analysisSessionController.RescanAsync(settingsCoordinator.BuildCurrentScanOptions()),
                () => !analysisSessionController.IsBusy && analysisSessionController.HasSelectedFolder),
            new RelayCommand(
                analysisSessionController.Cancel,
                () => analysisSessionController.IsBusy));
        var tree = new ProjectTreeViewModel();
        var summary = new SummaryViewModel();
        var excludesEditor = new ExcludesEditorViewModel(
            settingsCoordinator,
            analysisSessionController,
            settingsCoordinator.BuildCurrentScanOptions);
        var recentFolders = new RecentFoldersViewModel(
            analysisSessionController,
            settingsCoordinator,
            dependencies.FolderPathService,
            settingsCoordinator.BuildCurrentScanOptions);
        var workspacePresenter = new MainWindowWorkspacePresenter(
            analysisSessionController,
            treemapNavigationState,
            settingsCoordinator,
            toolbar,
            tree,
            summary);
        var mainWindowViewModel = new MainWindowViewModel(
            workspacePresenter,
            about,
            toolbar,
            excludesEditor,
            recentFolders,
            tree,
            summary,
            dependencies.PathShellService);

        return new MainWindowViewModelComposition(
            mainWindowViewModel,
            workspacePresenter,
            about,
            toolbar,
            excludesEditor,
            recentFolders,
            tree,
            summary,
            treemapNavigationState,
            dependencies.PathShellService);
    }
}
