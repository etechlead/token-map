using static Clever.TokenMap.Tests.Headless.Support.HeadlessTestSupport;
using Clever.TokenMap.Tests.Headless.Support;
using Clever.TokenMap.Tests.Support;

namespace Clever.TokenMap.Tests.Headless.ViewModels;

public sealed class RecentFoldersViewModelTests
{
    [Fact]
    public void ProvidesFlyoutPlaceholder_WhenNoRecentFoldersExist()
    {
        var viewModel = CreateMainWindowViewModel();

        Assert.Single(viewModel.RecentFolders.FlyoutItems);
        Assert.False(viewModel.RecentFolders.FlyoutItems[0].CanOpen);
        Assert.Equal("No previous folders yet", viewModel.RecentFolders.FlyoutItems[0].DisplayName);
    }

    [Fact]
    public void RemoveRecentFolderCommand_RemovesOneEntry()
    {
        var repoAPath = TestPaths.Folder("RepoA");
        var repoBPath = TestPaths.Folder("RepoB");
        var viewModel = CreateMainWindowViewModel(
            new StubProjectAnalyzer(CreateSnapshot()),
            recentFolderPaths:
            [
                repoAPath,
                repoBPath,
            ]);

        var folderToRemove = Assert.Single(viewModel.RecentFolders.Items, folder => folder.DisplayName == "RepoB");

        viewModel.RecentFolders.RemoveRecentFolderCommand.Execute(folderToRemove);

        Assert.Single(viewModel.RecentFolders.Items);
        Assert.Equal("RepoA", viewModel.RecentFolders.Items[0].DisplayName);
    }

    [Fact]
    public void ClearRecentFoldersCommand_ClearsListAndRestoresFlyoutPlaceholder()
    {
        var repoAPath = TestPaths.Folder("RepoA");
        var repoBPath = TestPaths.Folder("RepoB");
        var viewModel = CreateMainWindowViewModel(
            new StubProjectAnalyzer(CreateSnapshot()),
            recentFolderPaths:
            [
                repoAPath,
                repoBPath,
            ]);

        viewModel.RecentFolders.ClearRecentFoldersCommand.Execute(null);

        Assert.Empty(viewModel.RecentFolders.Items);
        Assert.Single(viewModel.RecentFolders.FlyoutItems);
        Assert.Equal("No previous folders yet", viewModel.RecentFolders.FlyoutItems[0].DisplayName);
    }
}
