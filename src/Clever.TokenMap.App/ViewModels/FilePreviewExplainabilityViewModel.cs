using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Core.Metrics.Formulas;

namespace Clever.TokenMap.App.ViewModels;

public sealed class FilePreviewExplainabilityViewModel
{
    private FilePreviewExplainabilityViewModel(IReadOnlyList<MetricExplainabilitySectionViewModel> sections)
    {
        Sections = sections;
    }

    public IReadOnlyList<MetricExplainabilitySectionViewModel> Sections { get; }

    public static FilePreviewExplainabilityViewModel? Create(ProjectNode? node)
    {
        if (node is null || node.Kind is not Core.Enums.ProjectNodeKind.File)
        {
            return null;
        }

        var metrics = node.ComputedMetrics;
        return new FilePreviewExplainabilityViewModel(
        [
            CreateStructuralRiskSection(metrics),
            CreateRefactorPrioritySection(metrics),
        ]);
    }

    private static MetricExplainabilitySectionViewModel CreateStructuralRiskSection(MetricSet metrics)
    {
        var definition = DefaultMetricCatalog.Instance.GetRequired(MetricIds.ComplexityPoints);
        var metricValue = metrics.GetOrDefault(MetricIds.ComplexityPoints);
        if (!ProductMetricFormulas.TryComputeStructuralRisk(metrics, out var breakdown))
        {
            return MetricExplainabilitySectionViewModel.Unavailable(
                definition.DisplayName,
                MetricValueFormatter.Format(definition.Id, metricValue, CultureInfo.CurrentCulture),
                "This metric is unavailable for this file.");
        }

        return MetricExplainabilitySectionViewModel.Available(
            definition.DisplayName,
            MetricValueFormatter.Format(definition.Id, metricValue, CultureInfo.CurrentCulture),
            breakdown.TotalPoints <= 0d
                ? "Low structural risk."
                : $"Driven mainly by {JoinLabels(GetTopContributors(breakdown, 2))}.",
            note: null,
            contributors: CreateContributorViewModels(breakdown));
    }

    private static MetricExplainabilitySectionViewModel CreateRefactorPrioritySection(MetricSet metrics)
    {
        var definition = DefaultMetricCatalog.Instance.GetRequired(MetricIds.RefactorPriorityPoints);
        var metricValue = metrics.GetOrDefault(MetricIds.RefactorPriorityPoints);
        if (!ProductMetricFormulas.TryComputeRefactorPriority(metrics, out var breakdown))
        {
            return MetricExplainabilitySectionViewModel.Unavailable(
                definition.DisplayName,
                MetricValueFormatter.Format(definition.Id, metricValue, CultureInfo.CurrentCulture),
                "This metric is unavailable for this file.");
        }

        var hasGitContext = HasGitContext(metrics);
        return MetricExplainabilitySectionViewModel.Available(
            definition.DisplayName,
            MetricValueFormatter.Format(definition.Id, metricValue, CultureInfo.CurrentCulture),
            hasGitContext
                ? BuildRefactorPrioritySummary(breakdown)
                : "Matches structural risk because git change context is unavailable.",
            note: hasGitContext ? null : "Git context unavailable.",
            contributors: CreateContributorViewModels(breakdown));
    }

    private static string BuildRefactorPrioritySummary(MetricFormulaBreakdown breakdown)
    {
        var changeContribution = breakdown.Components
            .Where(component => string.Equals(component.Category, "Change pressure", StringComparison.Ordinal))
            .Sum(component => component.ContributionPoints);
        var cochangeContribution = breakdown.Components
            .Where(component => string.Equals(component.Category, "Co-change pressure", StringComparison.Ordinal))
            .Sum(component => component.ContributionPoints);

        return (changeContribution > 0d, cochangeContribution > 0d) switch
        {
            (true, true) => "Driven mainly by structural risk, amplified by recent change and co-change pressure.",
            (true, false) => "Driven mainly by structural risk, amplified by recent change pressure.",
            (false, true) => "Driven mainly by structural risk, amplified by co-change pressure.",
            _ => "Driven mainly by structural risk.",
        };
    }

    private static IReadOnlyList<MetricExplainabilityContributorViewModel> CreateContributorViewModels(
        MetricFormulaBreakdown breakdown)
    {
        var components = breakdown.Components.Any(component => component.ContributionPoints > 0d)
            ? breakdown.Components
                .Where(component => component.ContributionPoints > 0d)
                .OrderByDescending(component => component.ContributionPoints)
                .Take(4)
            : breakdown.Components.Take(4);

        return
        [
            .. components.Select(component =>
                new MetricExplainabilityContributorViewModel(
                    component.Label,
                    FormatRawValue(component.RawValue),
                    FormatContribution(component.ContributionPoints)))
        ];
    }

    private static IReadOnlyList<MetricComponentContribution> GetTopContributors(MetricFormulaBreakdown breakdown, int count) =>
        [.. breakdown.Components
            .Where(component => component.ContributionPoints > 0d)
            .OrderByDescending(component => component.ContributionPoints)
            .Take(count)];

    private static string JoinLabels(IReadOnlyList<MetricComponentContribution> components)
    {
        if (components.Count == 0)
        {
            return "the current inputs";
        }

        return components.Count switch
        {
            1 => components[0].Label.ToLowerInvariant(),
            _ => $"{components[0].Label.ToLowerInvariant()} and {components[1].Label.ToLowerInvariant()}",
        };
    }

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

public sealed class MetricExplainabilitySectionViewModel
{
    private MetricExplainabilitySectionViewModel(
        string title,
        string scoreText,
        string summary,
        string? note,
        IReadOnlyList<MetricExplainabilityContributorViewModel> contributors)
    {
        Title = title;
        ScoreText = scoreText;
        Summary = summary;
        Note = note ?? string.Empty;
        Contributors = contributors;
    }

    public string Title { get; }

    public string ScoreText { get; }

    public string Summary { get; }

    public string Note { get; }

    public IReadOnlyList<MetricExplainabilityContributorViewModel> Contributors { get; }

    public bool HasNote => !string.IsNullOrWhiteSpace(Note);

    public bool HasContributors => Contributors.Count > 0;

    public static MetricExplainabilitySectionViewModel Available(
        string title,
        string scoreText,
        string summary,
        string? note,
        IReadOnlyList<MetricExplainabilityContributorViewModel> contributors) =>
        new(title, scoreText, summary, note, contributors);

    public static MetricExplainabilitySectionViewModel Unavailable(
        string title,
        string scoreText,
        string note) =>
        new(title, scoreText, summary: "No explainability data is available.", note, contributors: []);
}

public sealed record MetricExplainabilityContributorViewModel(
    string Label,
    string ValueText,
    string ContributionText);
