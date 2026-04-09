using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.State;
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

    public static FilePreviewExplainabilityViewModel? Create(
        ProjectNode? node,
        LocalizationState localization,
        MetricPresentationCatalog metricPresentationCatalog)
    {
        if (node is null || node.Kind is not Core.Enums.ProjectNodeKind.File)
        {
            return null;
        }

        var metrics = node.ComputedMetrics;
        return new FilePreviewExplainabilityViewModel(
        [
            CreateRefactorPrioritySection(metrics, localization, metricPresentationCatalog),
        ]);
    }

    private static MetricExplainabilitySectionViewModel CreateRefactorPrioritySection(
        MetricSet metrics,
        LocalizationState localization,
        MetricPresentationCatalog metricPresentationCatalog)
    {
        var metricValue = metrics.GetOrDefault(MetricIds.RefactorPriorityPoints);
        if (!ProductMetricFormulas.TryComputeRefactorPriority(metrics, out var breakdown))
        {
            return MetricExplainabilitySectionViewModel.Unavailable(
                metricPresentationCatalog.GetDisplayName(MetricIds.RefactorPriorityPoints),
                MetricValueFormatter.Format(MetricIds.RefactorPriorityPoints, metricValue, CultureInfo.CurrentCulture),
                localization.ExplainabilityMetricUnavailable);
        }

        var hasStructuralBreakdown = ProductMetricFormulas.TryComputeStructuralRisk(metrics, out var structuralBreakdown);
        var hasGitContext = HasGitContext(metrics);
        var structuralBasePoints = metrics.TryGetNumber(MetricIds.ComplexityPoints) ?? structuralBreakdown.TotalPoints;
        var gitUpliftPoints = Math.Max(0d, breakdown.TotalPoints - structuralBasePoints);

        return MetricExplainabilitySectionViewModel.Available(
            metricPresentationCatalog.GetDisplayName(MetricIds.RefactorPriorityPoints),
            MetricValueFormatter.Format(MetricIds.RefactorPriorityPoints, metricValue, CultureInfo.CurrentCulture),
            note: BuildRefactorPriorityNote(localization, hasGitContext, gitUpliftPoints),
            groups: CreateRefactorPriorityGroups(
                structuralBreakdown,
                breakdown,
                hasStructuralBreakdown,
                hasGitContext,
                structuralBasePoints,
                gitUpliftPoints,
                localization,
                metricPresentationCatalog));
    }

    private static string? BuildRefactorPriorityNote(LocalizationState localization, bool hasGitContext, double gitUpliftPoints)
    {
        if (!hasGitContext)
        {
            return localization.ExplainabilityGitUnavailableNote;
        }

        return gitUpliftPoints <= 0d
            ? localization.ExplainabilityGitBelowThresholdsNote
            : null;
    }

    private static IReadOnlyList<MetricExplainabilityGroupViewModel> CreateRefactorPriorityGroups(
        MetricFormulaBreakdown structuralBreakdown,
        MetricFormulaBreakdown refactorBreakdown,
        bool hasStructuralBreakdown,
        bool hasGitContext,
        double structuralBasePoints,
        double gitUpliftPoints,
        LocalizationState localization,
        MetricPresentationCatalog metricPresentationCatalog)
    {
        return
        [
            CreateStructuralGroup(
                structuralBreakdown,
                hasStructuralBreakdown,
                structuralBasePoints,
                localization,
                metricPresentationCatalog),
            CreateGitGroup(
                refactorBreakdown,
                hasGitContext,
                gitUpliftPoints,
                localization,
                metricPresentationCatalog),
        ];
    }

    private static MetricExplainabilityGroupViewModel CreateStructuralGroup(
        MetricFormulaBreakdown structuralBreakdown,
        bool hasStructuralBreakdown,
        double structuralBasePoints,
        LocalizationState localization,
        MetricPresentationCatalog metricPresentationCatalog)
    {
        var contributors = hasStructuralBreakdown
            ? structuralBreakdown.Components
                .Select(component =>
                    CreateContributorViewModel(
                        GetContributorLabel(component, metricPresentationCatalog),
                        component.RawValue,
                        component.ContributionPoints,
                        localization.GetStructuralDescription(component.Key)))
                .ToArray()
            : [];

        return MetricExplainabilityGroupViewModel.Create(
            localization.ExplainabilityStructuralBaseTitle,
            FormatPoints(structuralBasePoints),
            note: hasStructuralBreakdown
                ? localization.ExplainabilityStructuralBaseNote
                : localization.ExplainabilityStructuralUnavailableNote,
            contributors);
    }

    private static MetricExplainabilityGroupViewModel CreateGitGroup(
        MetricFormulaBreakdown refactorBreakdown,
        bool hasGitContext,
        double gitUpliftPoints,
        LocalizationState localization,
        MetricPresentationCatalog metricPresentationCatalog)
    {
        var contributors = hasGitContext && gitUpliftPoints > 0d
            ? refactorBreakdown.Components
                .Where(component => !string.Equals(component.Category, "Structural", StringComparison.Ordinal))
                .Where(component => component.ContributionPoints > 0d)
                .Select(component =>
                    CreateContributorViewModel(
                        GetContributorLabel(component, metricPresentationCatalog),
                        component.RawValue,
                        component.ContributionPoints,
                        localization.GetGitDescription(component.Key)))
                .ToArray()
            : [];

        var note = hasGitContext
            ? gitUpliftPoints > 0d
                ? localization.ExplainabilityGitUpliftPositiveNote
                : localization.ExplainabilityGitBelowThresholdsNote
            : localization.ExplainabilityGitUnavailableForFileNote;

        return MetricExplainabilityGroupViewModel.Create(
            localization.ExplainabilityGitUpliftTitle,
            hasGitContext ? FormatContribution(gitUpliftPoints) : localization.ExplainabilityNotAvailable,
            note,
            contributors);
    }

    private static MetricExplainabilityContributorViewModel CreateContributorViewModel(
        string label,
        double rawValue,
        double contributionPoints,
        string description) =>
        new(
            label,
            FormatRawValue(rawValue),
            FormatContribution(contributionPoints),
            description);

    private static string GetContributorLabel(
        MetricComponentContribution component,
        MetricPresentationCatalog metricPresentationCatalog)
    {
        var metricId = new MetricId(component.Key);
        return DefaultMetricCatalog.Instance.TryGet(metricId, out _)
            ? metricPresentationCatalog.GetDisplayName(metricId)
            : component.Label;
    }

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
        string? note,
        IReadOnlyList<MetricExplainabilityGroupViewModel> groups)
    {
        Title = title;
        ScoreText = scoreText;
        Note = note ?? string.Empty;
        Groups = groups;
    }

    public string Title { get; }

    public string ScoreText { get; }

    public string Note { get; }

    public IReadOnlyList<MetricExplainabilityGroupViewModel> Groups { get; }

    public bool HasNote => !string.IsNullOrWhiteSpace(Note);

    public bool HasGroups => Groups.Count > 0;

    public static MetricExplainabilitySectionViewModel Available(
        string title,
        string scoreText,
        string? note,
        IReadOnlyList<MetricExplainabilityGroupViewModel> groups) =>
        new(title, scoreText, note, groups);

    public static MetricExplainabilitySectionViewModel Unavailable(
        string title,
        string scoreText,
        string note) =>
        new(title, scoreText, note, groups: []);
}

public sealed class MetricExplainabilityGroupViewModel
{
    private MetricExplainabilityGroupViewModel(
        string title,
        string scoreText,
        string note,
        IReadOnlyList<MetricExplainabilityContributorViewModel> contributors)
    {
        Title = title;
        ScoreText = scoreText;
        Note = note;
        Contributors = contributors;
    }

    public string Title { get; }

    public string ScoreText { get; }

    public string Note { get; }

    public IReadOnlyList<MetricExplainabilityContributorViewModel> Contributors { get; }

    public bool HasNote => !string.IsNullOrWhiteSpace(Note);

    public bool HasContributors => Contributors.Count > 0;

    public static MetricExplainabilityGroupViewModel Create(
        string title,
        string scoreText,
        string? note,
        IReadOnlyList<MetricExplainabilityContributorViewModel> contributors) =>
        new(title, scoreText, note ?? string.Empty, contributors);
}

public sealed record MetricExplainabilityContributorViewModel(
    string Label,
    string ValueText,
    string ContributionText,
    string Description);
