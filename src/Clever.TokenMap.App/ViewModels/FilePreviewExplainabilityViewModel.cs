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
            CreateRefactorPrioritySection(metrics),
        ]);
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

        var hasStructuralBreakdown = ProductMetricFormulas.TryComputeStructuralRisk(metrics, out var structuralBreakdown);
        var hasGitContext = HasGitContext(metrics);
        var structuralBasePoints = metrics.TryGetNumber(MetricIds.ComplexityPoints) ?? structuralBreakdown.TotalPoints;
        var gitUpliftPoints = Math.Max(0d, breakdown.TotalPoints - structuralBasePoints);

        return MetricExplainabilitySectionViewModel.Available(
            definition.DisplayName,
            MetricValueFormatter.Format(definition.Id, metricValue, CultureInfo.CurrentCulture),
            BuildRefactorPrioritySummary(structuralBreakdown, breakdown, structuralBasePoints, hasGitContext, gitUpliftPoints),
            note: BuildRefactorPriorityNote(hasGitContext, gitUpliftPoints),
            groups: CreateRefactorPriorityGroups(structuralBreakdown, breakdown, hasStructuralBreakdown, hasGitContext, structuralBasePoints, gitUpliftPoints));
    }

    private static string BuildRefactorPrioritySummary(
        MetricFormulaBreakdown structuralBreakdown,
        MetricFormulaBreakdown refactorBreakdown,
        double structuralBasePoints,
        bool hasGitContext,
        double gitUpliftPoints)
    {
        var structuralSummary = structuralBreakdown.Components.Count > 0
            ? $"Structural base: {FormatPoints(structuralBasePoints)}, driven mainly by {JoinLabels(GetTopContributors(structuralBreakdown.Components, 2))}."
            : $"Structural base: {FormatPoints(structuralBasePoints)}.";

        if (!hasGitContext)
        {
            return $"{structuralSummary} Refactor Priority currently matches that structural base because git context is unavailable.";
        }

        if (gitUpliftPoints <= 0d)
        {
            return $"{structuralSummary} Git context is available, but current change pressure does not add extra urgency.";
        }

        var gitLabels = GetPositiveGitContributors(refactorBreakdown);
        var gitSummary = gitLabels.Count > 0
            ? $"Git uplift: {FormatContribution(gitUpliftPoints)}, driven mainly by {JoinLabels(GetTopContributors(gitLabels, 2))}."
            : $"Git uplift: {FormatContribution(gitUpliftPoints)}.";
        return $"{structuralSummary} {gitSummary}";
    }

    private static string? BuildRefactorPriorityNote(bool hasGitContext, double gitUpliftPoints)
    {
        if (!hasGitContext)
        {
            return "Git context unavailable. Refactor Priority currently equals the structural base.";
        }

        return gitUpliftPoints <= 0d
            ? "Git context is available, but all git signals stayed below the uplift thresholds."
            : null;
    }

    private static IReadOnlyList<MetricExplainabilityGroupViewModel> CreateRefactorPriorityGroups(
        MetricFormulaBreakdown structuralBreakdown,
        MetricFormulaBreakdown refactorBreakdown,
        bool hasStructuralBreakdown,
        bool hasGitContext,
        double structuralBasePoints,
        double gitUpliftPoints)
    {
        return
        [
            CreateStructuralGroup(structuralBreakdown, hasStructuralBreakdown, structuralBasePoints),
            CreateGitGroup(refactorBreakdown, hasGitContext, gitUpliftPoints),
        ];
    }

    private static MetricExplainabilityGroupViewModel CreateStructuralGroup(
        MetricFormulaBreakdown structuralBreakdown,
        bool hasStructuralBreakdown,
        double structuralBasePoints)
    {
        var contributors = hasStructuralBreakdown
            ? structuralBreakdown.Components
                .Select(component =>
                    CreateContributorViewModel(
                        component.Label,
                        component.RawValue,
                        component.ContributionPoints,
                        GetStructuralDescription(component.Key)))
                .ToArray()
            : [];

        return MetricExplainabilityGroupViewModel.Create(
            "Structural base",
            FormatPoints(structuralBasePoints),
            note: hasStructuralBreakdown
                ? "Intrinsic score from file scale, callable burden, and risk distribution."
                : "Structural breakdown unavailable for this file.",
            contributors);
    }

    private static MetricExplainabilityGroupViewModel CreateGitGroup(
        MetricFormulaBreakdown refactorBreakdown,
        bool hasGitContext,
        double gitUpliftPoints)
    {
        var contributors = hasGitContext && gitUpliftPoints > 0d
            ? refactorBreakdown.Components
                .Where(component => !string.Equals(component.Category, "Structural", StringComparison.Ordinal))
                .Where(component => component.ContributionPoints > 0d)
                .Select(component =>
                    CreateContributorViewModel(
                        component.Label,
                        component.RawValue,
                        component.ContributionPoints,
                        GetGitDescription(component.Key)))
                .ToArray()
            : [];

        var note = hasGitContext
            ? gitUpliftPoints > 0d
                ? "Bounded urgency boost from recent change and co-change pressure."
                : "Git context is available, but all git signals stayed below the uplift thresholds."
            : "Git context unavailable for this file.";

        return MetricExplainabilityGroupViewModel.Create(
            "Git uplift",
            hasGitContext ? FormatContribution(gitUpliftPoints) : "n/a",
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

    private static IReadOnlyList<MetricComponentContribution> GetPositiveGitContributors(
        MetricFormulaBreakdown breakdown)
    {
        return
        [
            .. breakdown.Components
                .Where(component => !string.Equals(component.Category, "Structural", StringComparison.Ordinal))
                .Where(component => component.ContributionPoints > 0d)
                .OrderByDescending(component => component.ContributionPoints)
        ];
    }

    private static IReadOnlyList<MetricComponentContribution> GetTopContributors(
        IEnumerable<MetricComponentContribution> components,
        int count) =>
        [.. components
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
        string summary,
        string? note,
        IReadOnlyList<MetricExplainabilityGroupViewModel> groups)
    {
        Title = title;
        ScoreText = scoreText;
        Summary = summary;
        Note = note ?? string.Empty;
        Groups = groups;
    }

    public string Title { get; }

    public string ScoreText { get; }

    public string Summary { get; }

    public string Note { get; }

    public IReadOnlyList<MetricExplainabilityGroupViewModel> Groups { get; }

    public bool HasNote => !string.IsNullOrWhiteSpace(Note);

    public bool HasGroups => Groups.Count > 0;

    public static MetricExplainabilitySectionViewModel Available(
        string title,
        string scoreText,
        string summary,
        string? note,
        IReadOnlyList<MetricExplainabilityGroupViewModel> groups) =>
        new(title, scoreText, summary, note, groups);

    public static MetricExplainabilitySectionViewModel Unavailable(
        string title,
        string scoreText,
        string note) =>
        new(title, scoreText, summary: "No explainability data is available.", note, groups: []);
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
