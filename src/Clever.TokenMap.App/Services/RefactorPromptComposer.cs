using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Core.Metrics.Formulas;

namespace Clever.TokenMap.App.Services;

public sealed class RefactorPromptComposer : IRefactorPromptComposer
{
    public string Compose(ProjectNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node.Kind is not ProjectNodeKind.File)
        {
            throw new ArgumentException("Refactor prompts can only be composed for file nodes.", nameof(node));
        }

        var metrics = node.ComputedMetrics;
        var builder = new StringBuilder();

        builder.AppendLine("Please assess whether this file is a good refactoring candidate.");
        builder.AppendLine();
        builder.AppendLine("Target file:");
        builder.Append("- Relative path: ");
        builder.AppendLine(FormatRelativePath(node.RelativePath));
        builder.AppendLine();
        builder.AppendLine("Observed metrics:");
        AppendMetricLine(builder, MetricIds.Tokens, metrics);
        AppendMetricLine(builder, MetricIds.NonEmptyLines, metrics);
        AppendMetricLine(builder, MetricIds.FileSizeBytes, metrics);
        AppendMetricLine(builder, MetricIds.ComplexityPoints, metrics);
        AppendMetricLine(builder, MetricIds.CallableHotspotPoints, metrics);
        AppendMetricLine(builder, MetricIds.RefactorPriorityPoints, metrics);
        builder.AppendLine();
        builder.AppendLine("Possible reasons these signals look this way:");
        AppendFormulaSection(
            builder,
            title: "Complexity",
            ProductMetricFormulas.TryComputeComplexity(metrics, out var complexityBreakdown),
            complexityBreakdown,
            unavailableReason: "Complexity is unavailable because the required syntax-derived inputs were not produced for this file.");
        AppendFormulaSection(
            builder,
            title: "Hotspots",
            ProductMetricFormulas.TryComputeHotspots(metrics, out var hotspotsBreakdown),
            hotspotsBreakdown,
            unavailableReason: "Hotspots are unavailable because the required callable-risk inputs were not produced for this file.");

        if (ProductMetricFormulas.TryComputeRefactorPriority(metrics, out var refactorBreakdown))
        {
            AppendBreakdownContributors(builder, "Refactor Priority", refactorBreakdown);
            if (!HasGitContext(metrics))
            {
                builder.AppendLine("- Refactor Priority currently reflects intrinsic pressure only because git-derived change-pressure inputs are unavailable.");
            }
        }
        else
        {
            builder.AppendLine("- Refactor Priority is unavailable because one or more prerequisite product metrics are unavailable.");
        }

        builder.AppendLine();
        builder.AppendLine("Task:");
        builder.AppendLine("- Review this file in the context of the surrounding module and the repository architecture.");
        builder.AppendLine("- Do not jump straight to code changes.");
        builder.AppendLine("- First decide whether refactoring is justified at all.");
        builder.AppendLine("- If refactoring seems warranted, propose several options that would fit the existing architecture and boundaries of this application.");
        builder.AppendLine("- Focus on reducing code smells, lowering complexity, and improving maintainability and agent-readability.");
        builder.AppendLine("- Include a minimal option and call out if the current design should stay as-is.");

        return builder.ToString().TrimEnd();
    }

    private static void AppendMetricLine(StringBuilder builder, MetricId metricId, MetricSet metrics)
    {
        var definition = DefaultMetricCatalog.Instance.GetRequired(metricId);
        builder.Append("- ");
        builder.Append(definition.DisplayName);
        builder.Append(": ");
        builder.AppendLine(MetricValueFormatter.Format(metricId, metrics.GetOrDefault(metricId), CultureInfo.CurrentCulture));
    }

    private static void AppendFormulaSection(
        StringBuilder builder,
        string title,
        bool isAvailable,
        MetricFormulaBreakdown breakdown,
        string unavailableReason)
    {
        if (!isAvailable)
        {
            builder.Append("- ");
            builder.AppendLine(unavailableReason);
            return;
        }

        AppendBreakdownContributors(builder, title, breakdown);
    }

    private static void AppendBreakdownContributors(
        StringBuilder builder,
        string title,
        MetricFormulaBreakdown breakdown)
    {
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
