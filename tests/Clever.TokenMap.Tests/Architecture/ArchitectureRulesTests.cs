using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.State;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Settings;
using Clever.TokenMap.Infrastructure.Analysis;
using Clever.TokenMap.Infrastructure.Settings;
using Clever.TokenMap.Treemap;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static ArchUnitNET.Fluent.Slices.SliceRuleDefinition;

namespace Clever.TokenMap.Tests.Architecture;

public sealed class ArchitectureRulesTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(Clever.TokenMap.App.App).Assembly,
            typeof(ProjectNode).Assembly,
            typeof(ProjectAnalyzer).Assembly,
            typeof(TreemapControl).Assembly)
        .Build();

    private static readonly string AppAssemblyName = typeof(Clever.TokenMap.App.App).Assembly.GetName().Name!;
    private static readonly string CoreAssemblyName = typeof(ProjectNode).Assembly.GetName().Name!;
    private static readonly string InfrastructureAssemblyName = typeof(ProjectAnalyzer).Assembly.GetName().Name!;
    private static readonly string TreemapAssemblyName = typeof(TreemapControl).Assembly.GetName().Name!;

    private static readonly IObjectProvider<IType> AppAssembly =
        Types().That().ResideInAssembly(AppAssemblyName).As("Clever.TokenMap.App");

    private static readonly IObjectProvider<IType> CoreAssembly =
        Types().That().ResideInAssembly(CoreAssemblyName).As("Clever.TokenMap.Core");

    private static readonly IObjectProvider<IType> InfrastructureAssembly =
        Types().That().ResideInAssembly(InfrastructureAssemblyName).As("Clever.TokenMap.Infrastructure");

    private static readonly IObjectProvider<IType> TreemapAssembly =
        Types().That().ResideInAssembly(TreemapAssemblyName).As("Clever.TokenMap.Treemap");

    private static readonly IObjectProvider<IType> NonCoreProductAssemblies =
        Types().That().ResideInAssembly(AppAssemblyName)
            .Or().ResideInAssembly(InfrastructureAssemblyName)
            .Or().ResideInAssembly(TreemapAssemblyName)
            .As("non-core product assemblies");

    private static readonly IObjectProvider<IType> NonInfrastructureUiAssemblies =
        Types().That().ResideInAssembly(AppAssemblyName)
            .Or().ResideInAssembly(TreemapAssemblyName)
            .As("app and treemap assemblies");

    private static readonly IObjectProvider<IType> AppState =
        Types().That().ResideInNamespace("Clever.TokenMap.App.State")
            .As("app state");

    private static readonly IObjectProvider<IType> AppServices =
        Types().That().ResideInNamespace("Clever.TokenMap.App.Services")
            .As("app services");

    private static readonly IObjectProvider<IType> AppViewModels =
        Types().That().ResideInNamespace("Clever.TokenMap.App.ViewModels")
            .As("app viewmodels");

    private static readonly IObjectProvider<IType> AppLayer =
        Types().That().ResideInNamespace("Clever.TokenMap.App.State")
            .Or().ResideInNamespace("Clever.TokenMap.App.Services")
            .Or().ResideInNamespace("Clever.TokenMap.App.ViewModels")
            .As("app-layer state, services, and viewmodels");

    private static readonly IObjectProvider<IType> AppViews =
        Types().That().ResideInNamespace("Clever.TokenMap.App.Views")
            .Or().ResideInNamespace("Clever.TokenMap.App.Views.Sections")
            .As("app views");

    private static readonly IObjectProvider<IType> MainWindowShellViewModel =
        Types().That().Are(typeof(MainWindowViewModel))
            .As("main window shell viewmodel");

    private static readonly IObjectProvider<IType> MainWindowWorkspacePresenterType =
        Types().That().Are(typeof(MainWindowWorkspacePresenter))
            .As("main window workspace presenter");

    private static readonly IObjectProvider<IType> MainWindowShellCompositionHelpers =
        Types().That().Are(typeof(MainWindowViewModelFactory), typeof(MainWindowViewModelComposition), typeof(MainWindowViewModelFactoryDependencies))
            .As("main window shell composition helpers");

    private static readonly IObjectProvider<IType> MutableSettingsStateTypes =
        Types().That().Are(typeof(SettingsState), typeof(CurrentFolderSettingsState))
            .As("mutable settings state types");

    private static readonly IObjectProvider<IType> ConcreteMainWindowProjectionViewModels =
        Types().That().Are(typeof(ToolbarViewModel), typeof(ProjectTreeViewModel), typeof(SummaryViewModel))
            .As("concrete main window projection viewmodels");

    private static readonly IObjectProvider<IType> ConcreteAppServiceImplementations =
        Types().That().Are(
                typeof(AnalysisSessionController),
                typeof(SettingsCoordinator),
                typeof(ApplicationThemeService),
                typeof(WindowFolderPickerService),
                typeof(PathShellService))
            .Or().HaveFullName("Clever.TokenMap.App.Services.AppSettingsSession")
            .Or().HaveFullName("Clever.TokenMap.App.Services.FolderSettingsSession")
            .Or().HaveFullName("Clever.TokenMap.App.Services.SettingsPersistenceQueue")
            .As("concrete app service implementations");

    private static readonly IObjectProvider<IType> NonShellAppViewModels =
        Types().That().Are(AppViewModels)
            .And().AreNot(typeof(MainWindowViewModel))
            .And().AreNot(typeof(MainWindowWorkspacePresenter))
            .And().AreNot(MainWindowShellCompositionHelpers)
            .As("non-shell app viewmodels");

    private static readonly IObjectProvider<IType> MainWindowOrchestrationInternals =
        Types().That().Are(
                typeof(IAnalysisSessionController),
                typeof(ISettingsCoordinator),
                typeof(TreemapNavigationState),
                typeof(SettingsState),
                typeof(CurrentFolderSettingsState))
            .As("main window orchestration internals");

    private static readonly IObjectProvider<IType> InfrastructureSettingsNamespace =
        Types().That().ResideInNamespace("Clever.TokenMap.Infrastructure.Settings")
            .As("infrastructure settings namespace");

    private static readonly IObjectProvider<IType> InfrastructureTypes =
        Types().That().ResideInAssembly(InfrastructureAssemblyName)
            .As("infrastructure types");

    private static readonly IObjectProvider<IType> SettingsStoreTypes =
        Types().That().Are(
                typeof(IAppSettingsStore),
                typeof(IFolderSettingsStore),
                typeof(JsonAppSettingsStore),
                typeof(JsonFolderSettingsStore))
            .As("settings store types");

    private static readonly IObjectProvider<IType> DirectFileSystemTypes =
        Types().That().HaveFullName(typeof(File).FullName!)
            .Or().HaveFullName(typeof(Directory).FullName!)
            .Or().HaveFullName(typeof(FileInfo).FullName!)
            .Or().HaveFullName(typeof(DirectoryInfo).FullName!)
            .As("direct file system types");

    private static readonly IObjectProvider<IType> TraceTypes =
        Types().That().HaveFullName(typeof(System.Diagnostics.Trace).FullName!)
            .As("System.Diagnostics.Trace");

    private static readonly IObjectProvider<IType> AvaloniaTypes =
        Types().That().HaveFullNameContaining("Avalonia.")
            .As("Avalonia UI types");

    private static readonly IObjectProvider<IType> UiEdgeAppServices =
        Types().That().Are(typeof(ApplicationThemeService), typeof(WindowFolderPickerService))
            .As("UI-edge app services");

    private static readonly IObjectProvider<IType> NonUiAppServices =
        Types().That().Are(AppServices)
            .And().AreNot(UiEdgeAppServices)
            .As("non-UI app services");

    private static readonly IObjectProvider<IType> AppCompositionRoots =
        Types().That().Are(typeof(Clever.TokenMap.App.App), typeof(Clever.TokenMap.App.AppComposition))
            .As("app composition roots");

    private static readonly IObjectProvider<IType> NonCompositionRootAppTypes =
        Types().That().Are(AppAssembly)
            .And().AreNot(AppCompositionRoots)
            .As("non-composition-root app types");

    [Fact]
    public void Core_Should_Not_Depend_On_Other_Product_Assemblies() =>
        Types().That().Are(CoreAssembly).Should()
            .NotDependOnAny(NonCoreProductAssemblies)
            .Because("core is the stable shared layer")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);

    [Fact]
    public void Infrastructure_Should_Not_Depend_On_App_Or_Treemap() =>
        Types().That().Are(InfrastructureAssembly).Should()
            .NotDependOnAny(NonInfrastructureUiAssemblies)
            .Because("infrastructure should stay below the app shell and treemap UI")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);

    [Fact]
    public void Infrastructure_Should_Not_Depend_On_Avalonia_Types() =>
        Types().That().Are(InfrastructureAssembly).Should()
            .NotDependOnAny(AvaloniaTypes)
            .Because("infrastructure should stay independent from desktop UI frameworks")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);

    [Fact]
    public void Treemap_Should_Not_Depend_On_App_Or_Infrastructure() =>
        Types().That().Are(TreemapAssembly).Should()
            .NotDependOnAny(
                Types().That().ResideInAssembly(AppAssemblyName)
                    .Or().ResideInAssembly(InfrastructureAssemblyName)
                    .As("app and infrastructure assemblies"))
            .Because("the treemap control should stay reusable and UI-focused")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);

    [Fact]
    public void App_State_Should_Not_Depend_On_Avalonia_Types() =>
        Types().That().Are(AppState).Should()
            .NotDependOnAny(AvaloniaTypes)
            .Because("app state should stay independent from Avalonia UI details")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);

    [Fact]
    public void App_State_Should_Not_Depend_On_Infrastructure_Types() =>
        Types().That().Are(AppState).Should()
            .NotDependOnAny(InfrastructureTypes)
            .Because("app state should stay on the app/core side of the architecture boundary")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);

    [Fact]
    public void App_ViewModels_Should_Not_Depend_On_Infrastructure_Types() =>
        Types().That().Are(AppViewModels).Should()
            .NotDependOnAny(InfrastructureTypes)
            .Because("viewmodels should work through app/core contracts instead of infrastructure details")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);

    [Fact]
    public void App_ViewModels_Should_Not_Depend_On_Avalonia_Types() =>
        Types().That().Are(AppViewModels).Should()
            .NotDependOnAny(AvaloniaTypes)
            .Because("viewmodels should expose neutral state instead of Avalonia-specific UI types")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);

    [Fact]
    public void MainWindow_Shell_ViewModel_Should_Not_Depend_On_Orchestration_Internals() =>
        Types().That().Are(MainWindowShellViewModel).Should()
            .NotDependOnAny(MainWindowOrchestrationInternals)
            .Because("the shell viewmodel should delegate cross-section synchronization to MainWindowWorkspacePresenter")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);

    [Fact]
    public void NonShell_App_ViewModels_Should_Not_Depend_On_MainWindow_WorkspacePresenter() =>
        Types().That().Are(NonShellAppViewModels).Should()
            .NotDependOnAny(MainWindowWorkspacePresenterType)
            .Because("the workspace presenter should remain a private shell composition detail")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);

    [Fact]
    public void App_ViewModels_Should_Not_Depend_On_Mutable_Settings_State_Types() =>
        Types().That().Are(AppViewModels).Should()
            .NotDependOnAny(MutableSettingsStateTypes)
            .Because("viewmodels should observe read-only settings state interfaces instead of mutable settings state implementations")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);

    [Fact]
    public void MainWindow_WorkspacePresenter_Should_Not_Depend_On_Concrete_MainWindow_Projection_ViewModels() =>
        Types().That().Are(MainWindowWorkspacePresenterType).Should()
            .NotDependOnAny(ConcreteMainWindowProjectionViewModels)
            .Because("the workspace presenter should coordinate narrow projection contracts rather than concrete child viewmodel implementations")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);

    [Fact]
    public void App_ViewModels_Should_Not_Depend_On_Concrete_App_Service_Implementations() =>
        Types().That().Are(AppViewModels).Should()
            .NotDependOnAny(ConcreteAppServiceImplementations)
            .Because("viewmodels should depend on app-service interfaces instead of concrete service implementations")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);

    [Fact]
    public void MainWindow_WorkspacePresenter_Should_Not_Depend_On_Shell_Composition_Helpers() =>
        Types().That().Are(MainWindowWorkspacePresenterType).Should()
            .NotDependOnAny(MainWindowShellCompositionHelpers)
            .Because("the workspace presenter should stay unaware of factory-level shell composition details")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);

    [Fact]
    public void App_ViewModels_Should_Not_Depend_On_Settings_Stores() =>
        Types().That().Are(AppViewModels).Should()
            .NotDependOnAny(SettingsStoreTypes)
            .Because("viewmodels should work through the settings coordinator instead of settings stores")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);

    [Fact]
    public void App_Services_Should_Not_Depend_On_ViewModels() =>
        Types().That().Are(AppServices).Should()
            .NotDependOnAny(AppViewModels)
            .Because("app services should stay below presentation-model orchestration")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);

    [Fact]
    public void App_Services_Should_Not_Depend_On_Infrastructure_Types() =>
        Types().That().Are(AppServices).Should()
            .NotDependOnAny(InfrastructureTypes)
            .Because("app services should work through app/core contracts instead of infrastructure details")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);

    [Fact]
    public void Non_Ui_App_Services_Should_Not_Depend_On_Avalonia_Types() =>
        Types().That().Are(NonUiAppServices).Should()
            .NotDependOnAny(AvaloniaTypes)
            .Because("only platform adapter services should talk to Avalonia APIs")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);

    [Fact]
    public void App_Should_Not_Depend_On_Direct_File_System_Types() =>
        Types().That().Are(AppAssembly).Should()
            .NotDependOnAny(DirectFileSystemTypes)
            .Because("the app shell should reach the file system through dedicated services")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);

    [Fact]
    public void Non_Composition_Root_App_Types_Should_Not_Depend_On_Infrastructure_Types() =>
        Types().That().Are(NonCompositionRootAppTypes).Should()
            .NotDependOnAny(InfrastructureTypes)
            .Because("only the app composition root should wire or mention infrastructure types")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);

    [Fact]
    public void App_Layer_Should_Not_Depend_On_Views() =>
        Types().That().Are(AppLayer).Should()
            .NotDependOnAny(AppViews)
            .Because("the app layer should stay decoupled from XAML view composition")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);

    [Fact]
    public void Infrastructure_Settings_Should_Not_Depend_On_Trace() =>
        Types().That().Are(InfrastructureSettingsNamespace).Should()
            .NotDependOnAny(TraceTypes)
            .Because("settings persistence should emit warnings through the app logging abstraction")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);

    [Fact]
    public void Core_Namespaces_Should_Be_Free_Of_Cycles() =>
        Slices().Matching("Clever.TokenMap.Core.(*)").Should()
            .BeFreeOfCycles()
            .Check(Architecture);

    [Fact]
    public void Infrastructure_Namespaces_Should_Be_Free_Of_Cycles() =>
        Slices().Matching("Clever.TokenMap.Infrastructure.(*)").Should()
            .BeFreeOfCycles()
            .Check(Architecture);
}
