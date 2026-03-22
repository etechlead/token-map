using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.HeadlessTests;

public sealed class TreemapNavigationStateTests
{
    [Fact]
    public void LoadSnapshot_InitializesSelectionAndBreadcrumbs()
    {
        var snapshot = CreateNestedSnapshot();
        var state = new TreemapNavigationState();

        state.LoadSnapshot(snapshot);

        Assert.Equal(snapshot.Root, state.TreemapRootNode);
        Assert.Equal(snapshot.Root, state.SelectedNode);
        Assert.Single(state.TreemapBreadcrumbs);
        Assert.Equal("Demo", state.TreemapBreadcrumbs[0].Label);
        Assert.False(state.CanResetTreemapRoot);
    }

    [Fact]
    public void DrillInto_UpdatesRootSelectionAndBreadcrumbs()
    {
        var snapshot = CreateNestedSnapshot();
        var directory = Assert.Single(snapshot.Root.Children);
        var state = new TreemapNavigationState();
        state.LoadSnapshot(snapshot);

        var handled = state.DrillInto(directory);

        Assert.True(handled);
        Assert.Equal(directory, state.TreemapRootNode);
        Assert.Equal(directory, state.SelectedNode);
        Assert.Equal(2, state.TreemapBreadcrumbs.Count);
        Assert.True(state.CanResetTreemapRoot);
        Assert.Equal("src", state.TreemapScopeDisplay);
    }

    [Fact]
    public void ResetAndBreadcrumbNavigation_RestoreOverviewWithoutClearingSelection()
    {
        var snapshot = CreateNestedSnapshot();
        var directory = Assert.Single(snapshot.Root.Children);
        var file = Assert.Single(directory.Children);
        var state = new TreemapNavigationState();
        state.LoadSnapshot(snapshot);
        state.DrillInto(directory);
        state.SelectNode(file);

        state.NavigateToBreadcrumb(snapshot.Root);

        Assert.Equal(snapshot.Root, state.TreemapRootNode);
        Assert.Equal(file, state.SelectedNode);
        Assert.Single(state.TreemapBreadcrumbs);

        state.DrillInto(directory);
        state.ResetTreemapRoot();

        Assert.Equal(snapshot.Root, state.TreemapRootNode);
        Assert.Equal(directory, state.SelectedNode);
        Assert.False(state.CanResetTreemapRoot);
    }

    private static ProjectSnapshot CreateNestedSnapshot() =>
        new()
        {
            RootPath = "C:\\Demo",
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Options = ScanOptions.Default,
            Root = new ProjectNode
            {
                Id = "/",
                Name = "Demo",
                FullPath = "C:\\Demo",
                RelativePath = string.Empty,
                Kind = ProjectNodeKind.Root,
                Metrics = new NodeMetrics(
                    Tokens: 42,
                    TotalLines: 12,
                    NonEmptyLines: 11,
                    BlankLines: 1,
                    FileSizeBytes: 128,
                    DescendantFileCount: 1,
                    DescendantDirectoryCount: 1),
                Children =
                {
                    new ProjectNode
                    {
                        Id = "src",
                        Name = "src",
                        FullPath = "C:\\Demo\\src",
                        RelativePath = "src",
                        Kind = ProjectNodeKind.Directory,
                        Metrics = new NodeMetrics(
                            Tokens: 42,
                            TotalLines: 12,
                            NonEmptyLines: 11,
                            BlankLines: 1,
                            FileSizeBytes: 128,
                            DescendantFileCount: 1,
                            DescendantDirectoryCount: 0),
                        Children =
                        {
                            new ProjectNode
                            {
                                Id = "src/Program.cs",
                                Name = "Program.cs",
                                FullPath = "C:\\Demo\\src\\Program.cs",
                                RelativePath = "src/Program.cs",
                                Kind = ProjectNodeKind.File,
                                Metrics = new NodeMetrics(
                                    Tokens: 42,
                                    TotalLines: 12,
                                    NonEmptyLines: 11,
                                    BlankLines: 1,
                                    FileSizeBytes: 128,
                                    DescendantFileCount: 1,
                                    DescendantDirectoryCount: 0),
                            },
                        },
                    },
                },
            },
        };
}
