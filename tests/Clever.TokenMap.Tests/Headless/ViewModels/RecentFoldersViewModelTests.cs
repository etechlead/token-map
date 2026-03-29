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
        var placeholder = Assert.Single(viewModel.RecentFolders.FlyoutItems);

        Assert.False(placeholder.CanOpen);
        Assert.False(placeholder.ShowFolderIcon);
        Assert.False(string.IsNullOrWhiteSpace(placeholder.DisplayName));
        Assert.False(string.IsNullOrWhiteSpace(placeholder.SecondaryText));
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
        var placeholder = Assert.Single(viewModel.RecentFolders.FlyoutItems);
        Assert.False(placeholder.CanOpen);
        Assert.False(placeholder.ShowFolderIcon);
        Assert.False(string.IsNullOrWhiteSpace(placeholder.DisplayName));
        Assert.False(string.IsNullOrWhiteSpace(placeholder.SecondaryText));
    }
}
