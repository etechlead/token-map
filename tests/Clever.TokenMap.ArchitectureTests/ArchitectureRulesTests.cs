using System.IO;
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Infrastructure.Analysis;
using Clever.TokenMap.Treemap;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static ArchUnitNET.Fluent.Slices.SliceRuleDefinition;

namespace Clever.TokenMap.ArchitectureTests;

public sealed class ArchitectureRulesTests
{
    private static readonly Architecture Architecture = new ArchLoader()
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

    private static readonly IObjectProvider<IType> AppStateAndViewModels =
        Types().That().ResideInNamespace("Clever.TokenMap.App.State")
            .Or().ResideInNamespace("Clever.TokenMap.App.ViewModels")
            .As("app state and viewmodels");

    private static readonly IObjectProvider<IType> AppLayer =
        Types().That().ResideInNamespace("Clever.TokenMap.App.State")
            .Or().ResideInNamespace("Clever.TokenMap.App.Services")
            .Or().ResideInNamespace("Clever.TokenMap.App.ViewModels")
            .As("app-layer state, services, and viewmodels");

    private static readonly IObjectProvider<IType> AppViews =
        Types().That().ResideInNamespace("Clever.TokenMap.App.Views")
            .Or().ResideInNamespace("Clever.TokenMap.App.Views.Sections")
            .As("app views");

    private static readonly IObjectProvider<IType> LowLevelInfrastructureDetails =
        Types().That().ResideInNamespace("Clever.TokenMap.Infrastructure.Analysis")
            .Or().ResideInNamespace("Clever.TokenMap.Infrastructure.Caching")
            .Or().ResideInNamespace("Clever.TokenMap.Infrastructure.Filtering")
            .Or().ResideInNamespace("Clever.TokenMap.Infrastructure.Paths")
            .Or().ResideInNamespace("Clever.TokenMap.Infrastructure.Scanning")
            .Or().ResideInNamespace("Clever.TokenMap.Infrastructure.Settings")
            .Or().ResideInNamespace("Clever.TokenMap.Infrastructure.Text")
            .Or().ResideInNamespace("Clever.TokenMap.Infrastructure.Tokenization")
            .As("low-level infrastructure details");

    private static readonly IObjectProvider<IType> DirectFileSystemTypes =
        Types().That().HaveFullName(typeof(File).FullName!)
            .Or().HaveFullName(typeof(Directory).FullName!)
            .Or().HaveFullName(typeof(FileInfo).FullName!)
            .Or().HaveFullName(typeof(DirectoryInfo).FullName!)
            .As("direct file system types");

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
    public void App_State_And_ViewModels_Should_Not_Depend_On_LowLevel_Infrastructure_Details() =>
        Types().That().Are(AppStateAndViewModels).Should()
            .NotDependOnAny(LowLevelInfrastructureDetails)
            .Because("app-facing state and viewmodels should work through app/core contracts")
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
    public void App_Layer_Should_Not_Depend_On_Views() =>
        Types().That().Are(AppLayer).Should()
            .NotDependOnAny(AppViews)
            .Because("the app layer should stay decoupled from XAML view composition")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);

    [Fact]
    public void Infrastructure_Namespaces_Should_Be_Free_Of_Cycles() =>
        Slices().Matching("Clever.TokenMap.Infrastructure.(*)").Should()
            .BeFreeOfCycles()
            .Check(Architecture);
}
