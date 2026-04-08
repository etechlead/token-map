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
            ["{{complexity}}"] = FormatMetricValue(MetricIds.ComplexityPoints, metrics),
            ["{{hotspots}}"] = FormatMetricValue(MetricIds.CallableHotspotPoints, metrics),
            ["{{refactor_priority}}"] = FormatMetricValue(MetricIds.RefactorPriorityPoints, metrics),
            ["{{complexity_breakdown}}"] = BuildFormulaSection(
                title: "Complexity",
                ProductMetricFormulas.TryComputeComplexity(metrics, out var complexityBreakdown),
                complexityBreakdown,
                unavailableReason: "Complexity is unavailable because the required syntax-derived inputs were not produced for this file."),
            ["{{hotspots_breakdown}}"] = BuildFormulaSection(
                title: "Hotspots",
                ProductMetricFormulas.TryComputeHotspots(metrics, out var hotspotsBreakdown),
                hotspotsBreakdown,
                unavailableReason: "Hotspots are unavailable because the required callable-risk inputs were not produced for this file."),
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

    private static string BuildFormulaSection(
        string title,
        bool isAvailable,
        MetricFormulaBreakdown breakdown,
        string unavailableReason)
    {
        if (!isAvailable)
        {
            return "- " + unavailableReason;
        }

        return BuildBreakdownContributors(title, breakdown);
    }

    private static string BuildRefactorPrioritySection(MetricSet metrics)
    {
        if (!ProductMetricFormulas.TryComputeRefactorPriority(metrics, out var refactorBreakdown))
        {
            return "- Refactor Priority is unavailable because one or more prerequisite product metrics are unavailable.";
        }

        var builder = new StringBuilder();
        builder.Append(BuildBreakdownContributors("Refactor Priority", refactorBreakdown));
        if (!HasGitContext(metrics))
        {
            builder.AppendLine();
            builder.Append("- Refactor Priority currently reflects intrinsic pressure only because git-derived change-pressure inputs are unavailable.");
        }

        return builder.ToString();
    }

    private static string BuildBreakdownContributors(
        string title,
        MetricFormulaBreakdown breakdown)
    {
        var builder = new StringBuilder();
        var contributors = breakdown.Components.Any(component => component.ContributionPoints > 0d)
            ? breakdown.Components
                .Where(component => component.ContributionPoints > 0d)
                .OrderByDescending(component => component.ContributionPoints)
                .Take(4)
            : breakdown.Components.Take(4);

        builder.Append("- ");
        builder.Append(title);
        builder.AppendLine(" is driven by:");
        foreach (var contributor in contributors)
        {
            builder.Append("  - ");
            builder.Append(contributor.Label);
            builder.Append(": ");
            builder.Append(FormatRawValue(contributor.RawValue));
            builder.Append(" (");
            builder.Append(FormatContribution(contributor.ContributionPoints));
            builder.AppendLine(")");
        }

        return builder.ToString().TrimEnd();
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
