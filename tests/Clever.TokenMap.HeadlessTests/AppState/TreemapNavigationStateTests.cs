using Clever.TokenMap.App.ViewModels;
using static Clever.TokenMap.HeadlessTests.HeadlessTestSupport;

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
    public void CanSetTreemapRoot_RejectsCurrentRootAndFiles()
    {
        var snapshot = CreateNestedSnapshot();
        var directory = Assert.Single(snapshot.Root.Children);
        var file = Assert.Single(directory.Children);
        var state = new TreemapNavigationState();
        state.LoadSnapshot(snapshot);

        Assert.False(state.CanSetTreemapRoot(snapshot.Root));
        Assert.True(state.CanSetTreemapRoot(directory));
        Assert.False(state.CanSetTreemapRoot(file));

        state.SetTreemapRoot(directory);

        Assert.False(state.CanSetTreemapRoot(directory));
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
}
