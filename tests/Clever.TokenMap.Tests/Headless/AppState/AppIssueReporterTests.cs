using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Diagnostics;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Logging;

namespace Clever.TokenMap.Tests.Headless.AppState;

public sealed class AppIssueReporterTests
{
    [AvaloniaFact]
    public async Task Report_FromBackgroundThread_PublishesStateChangeOnUiThread()
    {
        var logger = new RecordingLogger();
        var state = new AppIssueState();
        var reporter = new AppIssueReporter(logger, state, new AvaloniaUiDispatcher());
        bool? propertyChangedOnUiThread = null;
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppIssueState.ActiveIssue))
            {
                propertyChangedOnUiThread = Dispatcher.UIThread.CheckAccess();
            }
        };

        await Task.Run(() => reporter.Report(new AppIssue
        {
            Code = "test.issue",
            UserMessage = "User-visible issue",
            TechnicalMessage = "Technical issue",
        }));
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);

        Assert.NotNull(state.ActiveIssue);
        Assert.True(propertyChangedOnUiThread);
        Assert.Contains(
            logger.Entries,
            entry => entry.EventCode == "test.issue" && entry.Level == AppLogLevel.Error);
    }

    private sealed class RecordingLogger : IAppLogger
    {
        public List<AppLogEntry> Entries { get; } = [];

        public void Log(AppLogEntry entry)
        {
            Entries.Add(entry);
        }
    }
}
