using Clever.TokenMap.App.State;

namespace Clever.TokenMap.HeadlessTests;

public sealed class SettingsStateTests
{
    [Fact]
    public void Defaults_UseWeightedTreemapPalette()
    {
        var state = new SettingsState();

        Assert.Equal(Clever.TokenMap.Core.Enums.TreemapPalette.Weighted, state.SelectedTreemapPalette);
    }

    [Fact]
    public void RecordRecentFolder_UsesCurrentPlatformPathComparer()
    {
        var state = new SettingsState();

        state.RecordRecentFolder("/Users/demo/Repo");
        state.RecordRecentFolder("/Users/demo/repo");

        if (OperatingSystem.IsWindows())
        {
            Assert.Collection(
                state.RecentFolderPaths,
                path => Assert.Equal("/Users/demo/Repo", path));
            return;
        }

        Assert.Collection(
            state.RecentFolderPaths,
            path => Assert.Equal("/Users/demo/repo", path),
            path => Assert.Equal("/Users/demo/Repo", path));
    }
}
