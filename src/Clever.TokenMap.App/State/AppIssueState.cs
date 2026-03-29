using System;
using Clever.TokenMap.Core.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.State;

public sealed partial class AppIssueState : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveIssue))]
    private DisplayedAppIssue? activeIssue;

    public bool HasActiveIssue => ActiveIssue is not null;

    public void Show(DisplayedAppIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        ActiveIssue = issue;
    }

    public void Dismiss()
    {
        ActiveIssue = null;
    }
}
