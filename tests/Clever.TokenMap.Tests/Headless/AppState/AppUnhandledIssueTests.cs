using Clever.TokenMap.App;

namespace Clever.TokenMap.Tests.Headless.AppState;

public sealed class AppUnhandledIssueTests
{
    [Fact]
    public void CreateDispatcherUnhandledIssue_UsesBannerForRecoverableExceptions()
    {
        var issue = App.App.CreateDispatcherUnhandledIssue(
            new InvalidOperationException("boom"));

        Assert.False(issue.IsFatal);
        Assert.Equal("app.dispatcher_unhandled", issue.Code);
    }

    [Fact]
    public void BuildStartupFailureMessage_IncludesLogsDirectory()
    {
        var logsDirectoryPath = "C:\\Logs\\TokenMap";

        var message = Program.BuildStartupFailureMessage(logsDirectoryPath);

        Assert.Contains("TokenMap failed to start.", message, StringComparison.Ordinal);
        Assert.Contains(logsDirectoryPath, message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateX11PlatformOptions_DisablesDbusMenu()
    {
        var options = Program.CreateX11PlatformOptions();

        Assert.Equal("tokenmap", options.WmClass);
        Assert.False(options.UseDBusMenu);
    }
}
