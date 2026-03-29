using System;
using System.Collections.Generic;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Diagnostics;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Logging;

namespace Clever.TokenMap.App.Services;

public sealed class AppIssueReporter : IAppIssueReporter
{
    private readonly IAppLogger _logger;
    private readonly AppIssueState _state;
    private readonly IUiDispatcher _uiDispatcher;

    public AppIssueReporter(IAppLogger logger, AppIssueState state, IUiDispatcher uiDispatcher)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));
    }

    public void Report(AppIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);

        var referenceId = BuildReferenceId();
        var context = BuildContext(issue, referenceId);
        _logger.Log(new AppLogEntry
        {
            Level = issue.IsFatal ? AppLogLevel.Critical : AppLogLevel.Error,
            EventCode = issue.Code,
            Message = issue.TechnicalMessage,
            Exception = issue.Exception,
            Context = context,
        });

        PublishStateChange(() => _state.Show(new DisplayedAppIssue(
            issue,
            referenceId,
            DateTimeOffset.UtcNow)));
    }

    private static string BuildReferenceId() =>
        $"ERR-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..28];

    private static IReadOnlyDictionary<string, string> BuildContext(AppIssue issue, string referenceId)
    {
        if (issue.Context.Count == 0)
        {
            return AppIssueContext.Create(
                ("IssueCode", issue.Code),
                ("ReferenceId", referenceId),
                ("Presentation", issue.IsFatal ? "modal" : "banner"),
                ("IsFatal", issue.IsFatal));
        }

        var merged = new Dictionary<string, string>(issue.Context, StringComparer.Ordinal)
        {
            ["IssueCode"] = issue.Code,
            ["ReferenceId"] = referenceId,
            ["Presentation"] = issue.IsFatal ? "modal" : "banner",
            ["IsFatal"] = issue.IsFatal ? "true" : "false",
        };

        return merged;
    }

    private void PublishStateChange(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_uiDispatcher.CheckAccess())
        {
            action();
            return;
        }

        _uiDispatcher.Post(action);
    }
}
