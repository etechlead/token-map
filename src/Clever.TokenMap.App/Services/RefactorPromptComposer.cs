using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Core.Metrics.Formulas;
using Clever.TokenMap.Core.Settings;

namespace Clever.TokenMap.App.Services;

public sealed class RefactorPromptComposer : IRefactorPromptComposer
{
    private readonly IReadOnlySettingsState? _settingsState;

    public RefactorPromptComposer()
    {
    }

    public RefactorPromptComposer(ISettingsCoordinator settingsCoordinator)
        : this(settingsCoordinator?.State)
    {
    }

    internal RefactorPromptComposer(IReadOnlySettingsState? settingsState)
    {
        _settingsState = settingsState;
    }

    public string Compose(ProjectNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node.Kind is not ProjectNodeKind.File)
        {
            throw new ArgumentException("Refactor prompts can only be composed for file nodes.", nameof(node));
        }

        var metrics = node.ComputedMetrics;
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{relative_path}}"] = FormatRelativePath(node.RelativePath),
            ["{{tokens}}"] = FormatMetricValue(MetricIds.Tokens, metrics),
            ["{{non_empty_lines}}"] = FormatMetricValue(MetricIds.NonEmptyLines, metrics),
            ["{{file_size}}"] = FormatMetricValue(MetricIds.FileSizeBytes, metrics),
            ["{{refactor_priority}}"] = FormatMetricValue(MetricIds.RefactorPriorityPoints, metrics),
            ["{{refactor_priority_breakdown}}"] = BuildRefactorPrioritySection(metrics),
        };

        var template = ResolveTemplate();
        foreach (var replacement in replacements)
        {
            template = template.Replace(replacement.Key, replacement.Value, StringComparison.Ordinal);
        }

        return template.Trim();
    }

    private string ResolveTemplate() =>
        AppSettingsCanonicalizer.NormalizeRefactorPromptTemplate(_settingsState?.RefactorPromptTemplate);

    private static string FormatMetricValue(MetricId metricId, MetricSet metrics) =>
        MetricValueFormatter.Format(metricId, metrics.GetOrDefault(metricId), CultureInfo.CurrentCulture);

    private static string BuildRefactorPrioritySection(MetricSet metrics)
    {
        if (!ProductMetricFormulas.TryComputeRefactorPriority(metrics, out var refactorBreakdown))
        {
            return "- Refactor Priority is unavailable because one or more prerequisite product metrics are unavailable.";
        }

        var hasStructuralBreakdown = ProductMetricFormulas.TryComputeStructuralRisk(metrics, out var structuralBreakdown);
        var structuralBasePoints = metrics.TryGetNumber(MetricIds.ComplexityPoints) ?? structuralBreakdown.TotalPoints;
        var builder = new StringBuilder();
        var hasGitContext = HasGitContext(metrics);
        var gitUpliftPoints = Math.Max(0d, refactorBreakdown.TotalPoints - structuralBasePoints);

        builder.Append("- Refactor Priority is built from a structural base of ");
        builder.Append(FormatPoints(structuralBasePoints));
        builder.AppendLine(".");

        if (hasStructuralBreakdown)
        {
            builder.AppendLine("- Structural base drivers:");
            foreach (var contributor in structuralBreakdown.Components)
            {
                AppendContributorLine(
                    builder,
                    contributor.Label,
                    contributor.RawValue,
                    contributor.ContributionPoints,
                    GetStructuralDescription(contributor.Key));
            }
        }

        if (!hasGitContext)
        {
            builder.Append("- Git uplift: unavailable because git-derived change and co-change inputs were not produced for this file.");
            return builder.ToString().TrimEnd();
        }

        if (gitUpliftPoints <= 0d)
        {
            builder.Append("- Git uplift: +0 pts because all git signals stayed below the uplift thresholds.");
            return builder.ToString().TrimEnd();
        }

        builder.Append("- Git uplift: ");
        builder.Append(FormatContribution(gitUpliftPoints));
        builder.AppendLine(".");
        builder.AppendLine("- Git drivers:");
        foreach (var contributor in refactorBreakdown.Components
                     .Where(component => !string.Equals(component.Category, "Structural", StringComparison.Ordinal))
                     .Where(component => component.ContributionPoints > 0d))
        {
            AppendContributorLine(
                builder,
                contributor.Label,
                contributor.RawValue,
                contributor.ContributionPoints,
                GetGitDescription(contributor.Key));
        }

        return builder.ToString();
    }

    private static void AppendContributorLine(
        StringBuilder builder,
        string label,
        double rawValue,
        double contributionPoints,
        string description)
    {
        builder.Append("  - ");
        builder.Append(label);
        builder.Append(": ");
        builder.Append(FormatRawValue(rawValue));
        builder.Append(" (");
        builder.Append(FormatContribution(contributionPoints));
        builder.Append(") ");
        builder.AppendLine(description);
    }

    private static string FormatRelativePath(string relativePath) =>
        string.IsNullOrWhiteSpace(relativePath)
            ? "."
            : relativePath;

    private static string FormatRawValue(double value) =>
        IsWholeNumber(value)
            ? value.ToString("N0", CultureInfo.CurrentCulture)
            : value.ToString("N1", CultureInfo.CurrentCulture);

    private static string FormatContribution(double contributionPoints) =>
        IsWholeNumber(contributionPoints)
            ? $"+{contributionPoints.ToString("N0", CultureInfo.CurrentCulture)} pts"
            : $"+{contributionPoints.ToString("N1", CultureInfo.CurrentCulture)} pts";

    private static string FormatPoints(double value) =>
        IsWholeNumber(value)
            ? $"{value.ToString("N0", CultureInfo.CurrentCulture)} pts"
            : $"{value.ToString("N1", CultureInfo.CurrentCulture)} pts";

    private static string GetStructuralDescription(string key) =>
        key switch
        {
            "code_lines" => "Code volume in the file. Larger files tend to accumulate more moving parts before method-level risk is considered.",
            "total_callable_burden_points" => "Sum of per-callable burden after soft thresholds for method length, cyclomatic complexity, nesting depth, and parameter count.",
            "top_callable_burden_points" => "Burden of the single heaviest callable. This catches one dominant method even when the rest of the file looks moderate.",
            "affected_callable_ratio" => "Share of callables that exceed the soft thresholds. Higher ratios mean the problem is spread across the file rather than isolated.",
            "top_three_callable_burden_share" => "Share of callable burden concentrated in the top three callables. High concentration means a small number of methods dominate the risk.",
            _ => "Structural input that feeds the intrinsic refactor-risk base score.",
        };

    private static string GetGitDescription(string key) =>
        key switch
        {
            "churn_lines_90d" => "Recently rewritten line volume. Frequent rewrites raise urgency, but only as a bounded uplift on top of the structural base.",
            "touch_count_90d" => "Number of recent commits that touched this file. Repeated touches suggest ongoing friction around the code.",
            "author_count_90d" => "Number of recent contributors touching the file. More contributors usually increase coordination pressure around risky code.",
            "strong_cochanged_file_count_90d" => "Files that repeatedly change together with this one. This indicates a tighter blast radius when the file moves.",
            "unique_cochanged_file_count_90d" => "Breadth of different files that changed alongside this one across the recent history window.",
            "avg_cochange_set_size_90d" => "Typical width of change sets that include this file. Wider sets suggest changes tend to propagate.",
            _ => "Git-derived pressure that can amplify urgency without dominating the structural base.",
        };

    private static bool HasGitContext(MetricSet metrics) =>
        metrics.TryGetNumber(MetricIds.ChurnLines90d).HasValue &&
        metrics.TryGetNumber(MetricIds.TouchCount90d).HasValue &&
        metrics.TryGetNumber(MetricIds.AuthorCount90d).HasValue &&
        metrics.TryGetNumber(MetricIds.UniqueCochangedFileCount90d).HasValue &&
        metrics.TryGetNumber(MetricIds.StrongCochangedFileCount90d).HasValue &&
        metrics.TryGetNumber(MetricIds.AverageCochangeSetSize90d).HasValue;

    private static bool IsWholeNumber(double value) =>
        Math.Abs(value - Math.Round(value, MidpointRounding.AwayFromZero)) < 0.0001d;
}
