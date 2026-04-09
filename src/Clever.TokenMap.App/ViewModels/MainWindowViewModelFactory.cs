using System;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Diagnostics;
using Clever.TokenMap.Core.Interfaces;
using CommunityToolkit.Mvvm.Input;

namespace Clever.TokenMap.App.ViewModels;

public sealed record MainWindowViewModelFactoryDependencies(
    IAnalysisSessionController AnalysisSessionController,
    ISettingsCoordinator SettingsCoordinator,
    IFolderPathService FolderPathService,
    IPathShellService PathShellService,
    IRefactorPromptComposer RefactorPromptComposer,
    IUiDispatcher UiDispatcher,
    IFilePreviewContentReader FilePreviewContentReader,
    IAppIssueReporter AppIssueReporter,
    AppIssueState AppIssueState,
    IAppStoragePaths AppStoragePaths,
    IApplicationControlService ApplicationControlService,
    LocalizationState Localization,
    MetricPresentationCatalog MetricPresentationCatalog,
    TreemapNavigationState? TreemapNavigationState = null,
    AppAboutInfo? AboutInfo = null);

public sealed record MainWindowViewModelComposition(
    MainWindowViewModel MainWindowViewModel,
    MainWindowWorkspacePresenter WorkspacePresenter,
    AboutViewModel About,
    ToolbarViewModel Toolbar,
    ExcludesEditorViewModel ExcludesEditor,
    FilePreviewState FilePreview,
    IFilePreviewController FilePreviewController,
    RecentFoldersViewModel RecentFolders,
    AppIssueViewModel Issue,
    ProjectTreeViewModel Tree,
    SummaryViewModel Summary,
    RefactorPromptTemplateSettingsViewModel RefactorPromptTemplateSettings,
    LocalizationState Localization,
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
            dependencies.PathShellService,
            dependencies.AppIssueReporter,
            dependencies.Localization);

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
                () => analysisSessionController.IsBusy),
            dependencies.Localization,
            dependencies.MetricPresentationCatalog);
        var tree = new ProjectTreeViewModel();
        var summary = new SummaryViewModel(dependencies.Localization);
        var filePreview = new FilePreviewState(dependencies.Localization);
        var filePreviewController = new FilePreviewController(
            dependencies.FilePreviewContentReader,
            dependencies.UiDispatcher,
            filePreview);
        var excludesEditor = new ExcludesEditorViewModel(
            settingsCoordinator,
            analysisSessionController,
            settingsCoordinator.BuildCurrentScanOptions,
            dependencies.Localization);
        var recentFolders = new RecentFoldersViewModel(
            analysisSessionController,
            settingsCoordinator,
            dependencies.FolderPathService,
            settingsCoordinator.BuildCurrentScanOptions,
            dependencies.Localization);
        var refactorPromptTemplateSettings = new RefactorPromptTemplateSettingsViewModel(
            settingsCoordinator,
            dependencies.Localization);
        var issue = new AppIssueViewModel(
            dependencies.AppIssueState,
            dependencies.PathShellService,
            dependencies.AppStoragePaths,
            dependencies.ApplicationControlService,
            dependencies.AppIssueReporter,
            dependencies.Localization);
        var workspacePresenter = new MainWindowWorkspacePresenter(
            analysisSessionController,
            treemapNavigationState,
            settingsCoordinator,
            toolbar,
            tree,
            summary,
            dependencies.Localization);
        var mainWindowViewModel = new MainWindowViewModel(
            workspacePresenter,
            about,
            toolbar,
            excludesEditor,
            filePreview,
            recentFolders,
            issue,
            tree,
            summary,
            refactorPromptTemplateSettings,
            dependencies.Localization,
            dependencies.PathShellService,
            dependencies.RefactorPromptComposer,
            filePreviewController,
            dependencies.AppIssueReporter);

        return new MainWindowViewModelComposition(
            mainWindowViewModel,
            workspacePresenter,
            about,
            toolbar,
            excludesEditor,
            filePreview,
            filePreviewController,
            recentFolders,
            issue,
            tree,
            summary,
            refactorPromptTemplateSettings,
            dependencies.Localization,
            treemapNavigationState,
            dependencies.PathShellService);
    }
}
