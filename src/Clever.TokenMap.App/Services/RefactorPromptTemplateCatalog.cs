using System;
using System.Collections.Generic;
using System.Globalization;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Settings;

namespace Clever.TokenMap.App.Services;

public static class RefactorPromptTemplateCatalog
{
    public static IReadOnlyList<RefactorPromptTemplatePlaceholderDefinition> GetPlaceholders(LocalizationState localization) =>
    [
        new("{{relative_path}}", localization.PromptTemplatePlaceholderRelativePath),
        new("{{tokens}}", localization.PromptTemplatePlaceholderTokens),
        new("{{non_empty_lines}}", localization.PromptTemplatePlaceholderNonEmptyLines),
        new("{{file_size}}", localization.PromptTemplatePlaceholderFileSize),
        new("{{refactor_priority}}", localization.PromptTemplatePlaceholderRefactorPriority),
        new("{{refactor_priority_breakdown}}", localization.PromptTemplatePlaceholderRefactorPriorityBreakdown),
    ];

    public static string GetDefaultTemplate(string languageTag) =>
        IsRussianLanguage(languageTag)
            ? RussianDefaultTemplate
            : EnglishDefaultTemplate;

    public static string ResolveTemplate(string languageTag, string? templateText) =>
        string.IsNullOrWhiteSpace(templateText)
            ? GetDefaultTemplate(languageTag)
            : templateText;

    private static bool IsRussianLanguage(string? languageTag)
    {
        if (string.IsNullOrWhiteSpace(languageTag))
        {
            return false;
        }

        try
        {
            return string.Equals(
                CultureInfo.GetCultureInfo(languageTag.Trim()).TwoLetterISOLanguageName,
                "ru",
                StringComparison.OrdinalIgnoreCase);
        }
        catch (CultureNotFoundException)
        {
            return string.Equals(languageTag.Trim(), ApplicationLanguageTags.Default, StringComparison.OrdinalIgnoreCase) is false &&
                   string.Equals(languageTag.Trim(), "ru", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static readonly string EnglishDefaultTemplate = string.Join(
        "\n",
        [
            "Please assess whether this file is a good refactoring candidate.",
            string.Empty,
            "Target file:",
            "- Relative path: {{relative_path}}",
            string.Empty,
            "Observed metrics:",
            "- Tokens: {{tokens}}",
            "- Non-empty lines: {{non_empty_lines}}",
            "- File size: {{file_size}}",
            "- Refactor Priority: {{refactor_priority}}",
            string.Empty,
            "How Refactor Priority was formed:",
            "{{refactor_priority_breakdown}}",
            string.Empty,
            "Task:",
            "- Review this file in the context of the surrounding module and the repository architecture.",
            "- Do not jump straight to code changes.",
            "- First decide whether refactoring is justified at all.",
            "- If refactoring seems warranted, propose several options that would fit the existing architecture and boundaries of this application.",
            "- Focus on reducing structural risk, simplifying the code, and improving maintainability and agent-readability.",
            "- Include a minimal option and call out if the current design should stay as-is.",
        ]);

    private static readonly string RussianDefaultTemplate = string.Join(
        "\n",
        [
            "Оцени, насколько этот файл действительно является хорошим кандидатом на рефакторинг.",
            string.Empty,
            "Целевой файл:",
            "- Относительный путь: {{relative_path}}",
            string.Empty,
            "Наблюдаемые метрики:",
            "- Токены: {{tokens}}",
            "- Непустые строки: {{non_empty_lines}}",
            "- Размер файла: {{file_size}}",
            "- Приоритет рефакторинга: {{refactor_priority}}",
            string.Empty,
            "Как сформировался приоритет рефакторинга:",
            "{{refactor_priority_breakdown}}",
            string.Empty,
            "Задача:",
            "- Рассмотри файл в контексте окружающего модуля и архитектуры репозитория.",
            "- Не переходи сразу к изменениям кода.",
            "- Сначала реши, оправдан ли рефакторинг вообще.",
            "- Если рефакторинг действительно нужен, предложи несколько вариантов, которые укладываются в существующую архитектуру и границы приложения.",
            "- Сфокусируйся на снижении структурного риска, упрощении кода и повышении поддерживаемости и читаемости для агентов.",
            "- Обязательно включи минимальный вариант и явно отметь, если текущий дизайн лучше оставить без изменений.",
        ]);
}

public sealed record RefactorPromptTemplatePlaceholderDefinition(string Token, string Description);
