using System.Globalization;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Tests.Headless.Support;
using Clever.TokenMap.Tests.Support;
using static Clever.TokenMap.Tests.Headless.Support.HeadlessTestSupport;

namespace Clever.TokenMap.Tests.Headless.ViewModels;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void CloseSettingsCommand_ClosesDrawerWithoutRetoggling()
    {
        var viewModel = CreateMainWindowViewModel();
        viewModel.IsSettingsOpen = true;

        viewModel.CloseSettingsCommand.Execute(null);

        Assert.False(viewModel.IsSettingsOpen);
    }

    [Fact]
    public async Task ExcludeNodeFromFolder_AppendsExactEntryAndOpensFolderEditor()
    {
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(CreateSnapshot()));
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);
        viewModel.ExcludeNodeFromFolderCommand.Execute(new ProjectNode
        {
            Id = "src",
            Name = "src",
            FullPath = "C:\\Demo\\src",
            RelativePath = "src",
            Kind = ProjectNodeKind.Directory,
            Summary = NodeSummary.Empty,
            ComputedMetrics = MetricSet.Empty,
        });

        Assert.True(viewModel.ExcludesEditor.IsOpen);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ExcludesEditor.Title));
        Assert.Contains("Demo", viewModel.ExcludesEditor.Title, StringComparison.Ordinal);
        Assert.Equal("/src/", viewModel.ExcludesEditor.Text.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task CanExcludeNodeFromFolder_RequiresCommittedFolderAndRejectsRoot()
    {
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(CreateNestedSnapshot()));
        var rootNode = CreateNestedSnapshot().Root;
        var childNode = Assert.Single(rootNode.Children);

        Assert.False(viewModel.CanExcludeNodeFromFolder(childNode));
        Assert.False(viewModel.CanExcludeNodeFromFolder(rootNode));

        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);

        Assert.True(viewModel.CanExcludeNodeFromFolder(childNode));
        Assert.False(viewModel.CanExcludeNodeFromFolder(rootNode));
    }

    [Fact]
    public async Task ExcludeNodeFromFolderCommand_IgnoresRootNode()
    {
        var snapshot = CreateNestedSnapshot();
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(snapshot));
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);

        viewModel.ExcludeNodeFromFolderCommand.Execute(snapshot.Root);

        Assert.False(viewModel.ExcludesEditor.IsOpen);
    }

    [Fact]
    public async Task SelectedMetric_UpdatesParentShareWithoutReanalysis()
    {
        var demoRootPath = TestPaths.Folder("Demo");
        var decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        var snapshot = new ProjectSnapshot
        {
            RootPath = demoRootPath,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Options = ScanOptions.Default,
            Root = CreateRootWithChildren(
                ("Alpha.cs", 10, 20, 10),
                ("Beta.cs", 20, 10, 20)),
        };
        var analyzer = new CountingProjectAnalyzer(snapshot);
        var viewModel = CreateMainWindowViewModel(analyzer);

        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);

        var alphaByTokens = Assert.Single(viewModel.Tree.VisibleNodes, node => node.Name == "Alpha.cs");
        Assert.Equal($"66{decimalSeparator}7%", alphaByTokens.ParentShareText);
        Assert.Equal(1, analyzer.CallCount);

        viewModel.Toolbar.IsSizeMetricSelected = true;

        var alphaBySize = Assert.Single(viewModel.Tree.VisibleNodes, node => node.Name == "Alpha.cs");
        Assert.Equal($"33{decimalSeparator}3%", alphaBySize.ParentShareText);
        Assert.Equal(1, analyzer.CallCount);
    }

    [Fact]
    public async Task About_OpenRepositoryCommand_UsesPathShellService()
    {
        var shellService = new TrackingPathShellService();
        var viewModel = CreateMainWindowViewModel(pathShellService: shellService);

        await viewModel.About.OpenRepositoryCommand.ExecuteAsync(null);

        Assert.Equal("https://github.com/etechlead/token-map", shellService.LastOpenedPath);
    }

    private sealed class TrackingPathShellService : IPathShellService
    {
        public string RevealMenuHeader => "Reveal";

        public string? LastOpenedPath { get; private set; }

        public Task<bool> TryOpenAsync(string fullPath, CancellationToken cancellationToken = default)
        {
            LastOpenedPath = fullPath;
            return Task.FromResult(true);
        }

        public Task<bool> TryRevealAsync(string fullPath, bool isDirectory, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}
