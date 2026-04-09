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
        var promptLanguageTag = ResolvePromptLanguageTag();
        var promptCulture = GetPromptCulture(promptLanguageTag);
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{relative_path}}"] = FormatRelativePath(node.RelativePath),
            ["{{tokens}}"] = FormatMetricValue(MetricIds.Tokens, metrics, promptCulture),
            ["{{non_empty_lines}}"] = FormatMetricValue(MetricIds.NonEmptyLines, metrics, promptCulture),
            ["{{file_size}}"] = FormatMetricValue(MetricIds.FileSizeBytes, metrics, promptCulture),
            ["{{refactor_priority}}"] = FormatMetricValue(MetricIds.RefactorPriorityPoints, metrics, promptCulture),
            ["{{refactor_priority_breakdown}}"] = BuildRefactorPrioritySection(metrics, promptLanguageTag, promptCulture),
        };

        var template = ResolveTemplate(promptLanguageTag);
        foreach (var replacement in replacements)
        {
            template = template.Replace(replacement.Key, replacement.Value, StringComparison.Ordinal);
        }

        return template.Trim();
    }

    private string ResolvePromptLanguageTag() =>
        _settingsState is null
            ? ApplicationLanguageTags.Default
            : AppSettingsCanonicalizer.NormalizePromptLanguageTag(_settingsState.SelectedPromptLanguageTag)
              ?? ApplicationLanguageTags.Default;

    private string ResolveTemplate(string promptLanguageTag) =>
        RefactorPromptTemplateCatalog.ResolveTemplate(
            promptLanguageTag,
            _settingsState?.GetRefactorPromptTemplate(promptLanguageTag));

    private static string FormatMetricValue(MetricId metricId, MetricSet metrics, CultureInfo culture) =>
        MetricValueFormatter.Format(metricId, metrics.GetOrDefault(metricId), culture);

    private static string BuildRefactorPrioritySection(MetricSet metrics, string promptLanguageTag, CultureInfo culture)
    {
        var isRussian = IsRussianLanguage(promptLanguageTag);
        if (!ProductMetricFormulas.TryComputeRefactorPriority(metrics, out var refactorBreakdown))
        {
            return isRussian
                ? "- Приоритет рефакторинга недоступен, потому что отсутствует одна или несколько обязательных продуктовых метрик."
                : "- Refactor Priority is unavailable because one or more prerequisite product metrics are unavailable.";
        }

        var hasStructuralBreakdown = ProductMetricFormulas.TryComputeStructuralRisk(metrics, out var structuralBreakdown);
        var structuralBasePoints = metrics.TryGetNumber(MetricIds.ComplexityPoints) ?? structuralBreakdown.TotalPoints;
        var builder = new StringBuilder();
        var hasGitContext = HasGitContext(metrics);
        var gitUpliftPoints = Math.Max(0d, refactorBreakdown.TotalPoints - structuralBasePoints);

        builder.AppendLine(
            isRussian
                ? $"- Приоритет рефакторинга строится на структурной базе в {FormatPoints(structuralBasePoints, culture, promptLanguageTag)}."
                : $"- Refactor Priority is built from a structural base of {FormatPoints(structuralBasePoints, culture, promptLanguageTag)}.");

        if (hasStructuralBreakdown)
        {
            builder.AppendLine(isRussian
                ? "- Драйверы структурной базы:"
                : "- Structural base drivers:");
            foreach (var contributor in structuralBreakdown.Components)
            {
                AppendContributorLine(
                    builder,
                    promptLanguageTag,
                    culture,
                    contributor.Label,
                    contributor.RawValue,
                    contributor.ContributionPoints,
                    GetStructuralDescription(contributor.Key, promptLanguageTag));
            }
        }

        if (!hasGitContext)
        {
            builder.Append(isRussian
                ? "- Git-надбавка недоступна, потому что для этого файла не были рассчитаны git-метрики изменений и ко-изменений."
                : "- Git uplift: unavailable because git-derived change and co-change inputs were not produced for this file.");
            return builder.ToString().TrimEnd();
        }

        if (gitUpliftPoints <= 0d)
        {
            builder.Append(isRussian
                ? "- Git-надбавка: +0 баллов, потому что все git-сигналы остались ниже порогов надбавки."
                : "- Git uplift: +0 pts because all git signals stayed below the uplift thresholds.");
            return builder.ToString().TrimEnd();
        }

        builder.Append(isRussian ? "- Git-надбавка: " : "- Git uplift: ");
        builder.Append(FormatContribution(gitUpliftPoints, culture, promptLanguageTag));
        builder.AppendLine(".");
        builder.AppendLine(isRussian ? "- Git-драйверы:" : "- Git drivers:");
        foreach (var contributor in refactorBreakdown.Components
                     .Where(component => !string.Equals(component.Category, "Structural", StringComparison.Ordinal))
                     .Where(component => component.ContributionPoints > 0d))
        {
            AppendContributorLine(
                builder,
                promptLanguageTag,
                culture,
                contributor.Label,
                contributor.RawValue,
                contributor.ContributionPoints,
                GetGitDescription(contributor.Key, promptLanguageTag));
        }

        return builder.ToString();
    }

    private static void AppendContributorLine(
        StringBuilder builder,
        string promptLanguageTag,
        CultureInfo culture,
        string label,
        double rawValue,
        double contributionPoints,
        string description)
    {
        builder.Append("  - ");
        builder.Append(label);
        builder.Append(": ");
        builder.Append(FormatRawValue(rawValue, culture));
        builder.Append(" (");
        builder.Append(FormatContribution(contributionPoints, culture, promptLanguageTag));
        builder.Append(") ");
        builder.AppendLine(description);
    }

    private static string FormatRelativePath(string relativePath) =>
        string.IsNullOrWhiteSpace(relativePath)
            ? "."
            : relativePath;

    private static string FormatRawValue(double value, CultureInfo culture) =>
        IsWholeNumber(value)
            ? value.ToString("N0", culture)
            : value.ToString("N1", culture);

    private static string FormatContribution(double contributionPoints, CultureInfo culture, string promptLanguageTag) =>
        IsWholeNumber(contributionPoints)
            ? IsRussianLanguage(promptLanguageTag)
                ? $"+{contributionPoints.ToString("N0", culture)} баллов"
                : $"+{contributionPoints.ToString("N0", culture)} pts"
            : IsRussianLanguage(promptLanguageTag)
                ? $"+{contributionPoints.ToString("N1", culture)} балла"
                : $"+{contributionPoints.ToString("N1", culture)} pts";

    private static string FormatPoints(double value, CultureInfo culture, string promptLanguageTag) =>
        IsWholeNumber(value)
            ? IsRussianLanguage(promptLanguageTag)
                ? $"{value.ToString("N0", culture)} баллов"
                : $"{value.ToString("N0", culture)} pts"
            : IsRussianLanguage(promptLanguageTag)
                ? $"{value.ToString("N1", culture)} балла"
                : $"{value.ToString("N1", culture)} pts";

    private static string GetStructuralDescription(string key, string promptLanguageTag) =>
        IsRussianLanguage(promptLanguageTag)
            ? key switch
            {
                "code_lines" => "Объём кода в файле. Более крупные файлы обычно накапливают больше подвижных частей ещё до оценки риска на уровне методов.",
                "total_callable_burden_points" => "Суммарная нагрузка по всем callable после мягких порогов для длины метода, цикломатической сложности, глубины вложенности и числа параметров.",
                "top_callable_burden_points" => "Нагрузка самого тяжёлого callable. Это ловит один доминирующий метод, даже если остальная часть файла выглядит умеренно.",
                "affected_callable_ratio" => "Доля callable, которые превышают мягкие пороги. Чем выше доля, тем сильнее проблема распределена по файлу, а не изолирована локально.",
                "top_three_callable_burden_share" => "Доля нагрузки callable, сосредоточенная в трёх самых тяжёлых callable. Высокая концентрация означает, что риск определяется небольшим числом методов.",
                _ => "Структурный вход, который формирует базовый внутренний риск рефакторинга.",
            }
            : key switch
            {
                "code_lines" => "Code volume in the file. Larger files tend to accumulate more moving parts before method-level risk is considered.",
                "total_callable_burden_points" => "Sum of per-callable burden after soft thresholds for method length, cyclomatic complexity, nesting depth, and parameter count.",
                "top_callable_burden_points" => "Burden of the single heaviest callable. This catches one dominant method even when the rest of the file looks moderate.",
                "affected_callable_ratio" => "Share of callables that exceed the soft thresholds. Higher ratios mean the problem is spread across the file rather than isolated.",
                "top_three_callable_burden_share" => "Share of callable burden concentrated in the top three callables. High concentration means a small number of methods dominate the risk.",
                _ => "Structural input that feeds the intrinsic refactor-risk base score.",
            };

    private static string GetGitDescription(string key, string promptLanguageTag) =>
        IsRussianLanguage(promptLanguageTag)
            ? key switch
            {
                "churn_lines_90d" => "Объём недавно переписанных строк. Частые переписывания повышают срочность, но только как ограниченная надбавка поверх структурной базы.",
                "touch_count_90d" => "Количество недавних коммитов, которые затрагивали этот файл. Повторяющиеся касания указывают на постоянное трение вокруг кода.",
                "author_count_90d" => "Количество недавних авторов, меняющих этот файл. Большее число участников обычно повышает координационную нагрузку вокруг рискованного кода.",
                "strong_cochanged_file_count_90d" => "Файлы, которые регулярно меняются вместе с этим. Это указывает на более плотный blast radius при изменении файла.",
                "unique_cochanged_file_count_90d" => "Ширина множества разных файлов, которые менялись вместе с этим файлом в недавнем временном окне.",
                "avg_cochange_set_size_90d" => "Типичная ширина change set'ов, включающих этот файл. Более широкие наборы указывают, что изменения склонны распространяться.",
                _ => "Git-производное давление, которое может усиливать срочность, не доминируя над структурной базой.",
            }
            : key switch
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

    private static CultureInfo GetPromptCulture(string promptLanguageTag)
    {
        try
        {
            return CultureInfo.GetCultureInfo(promptLanguageTag);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo(ApplicationLanguageTags.Default);
        }
    }

    private static bool IsRussianLanguage(string? promptLanguageTag)
    {
        if (string.IsNullOrWhiteSpace(promptLanguageTag))
        {
            return false;
        }

        try
        {
            return string.Equals(
                CultureInfo.GetCultureInfo(promptLanguageTag.Trim()).TwoLetterISOLanguageName,
                "ru",
                StringComparison.OrdinalIgnoreCase);
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }

    private static bool IsWholeNumber(double value) =>
        Math.Abs(value - Math.Round(value, MidpointRounding.AwayFromZero)) < 0.0001d;
}
