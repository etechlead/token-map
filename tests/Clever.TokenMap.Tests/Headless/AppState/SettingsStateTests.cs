using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Tests.Headless.AppState;

public sealed class SettingsStateTests
{
    [Fact]
    public void Defaults_UseWeightedTreemapPalette()
    {
        var state = new SettingsState();

        Assert.Equal(WorkspaceLayoutMode.SideBySide, state.WorkspaceLayoutMode);
        Assert.Equal(TreemapPalette.Weighted, state.SelectedTreemapPalette);
        Assert.True(state.ShowTreemapMetricValues);
        Assert.Equal(GlobalExcludeDefaults.DefaultEntries, state.GlobalExcludes);
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

    [Fact]
    public void ReplaceGlobalExcludes_PreservesGitIgnoreStyleRuleOrder()
    {
        var state = new SettingsState();

        state.ReplaceGlobalExcludes(["  /src//generated/**  ", "", "# generated", "!nested/scripts/"]);

        Assert.Collection(
            state.GlobalExcludes,
            entry => Assert.Equal("/src/generated/**", entry),
            entry => Assert.Equal("# generated", entry),
            entry => Assert.Equal("!nested/scripts/", entry));
    }
}
