using System;
using Clever.TokenMap.Core.Diagnostics;

namespace Clever.TokenMap.App.State;

public sealed record DisplayedAppIssue(
    AppIssue Issue,
    string ReferenceId,
    DateTimeOffset OccurredAtUtc);
