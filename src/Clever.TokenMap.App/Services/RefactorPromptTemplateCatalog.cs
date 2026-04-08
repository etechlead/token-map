using System.Collections.Generic;

namespace Clever.TokenMap.App.Services;

public static class RefactorPromptTemplateCatalog
{
    public static IReadOnlyList<RefactorPromptTemplatePlaceholderDefinition> Placeholders { get; } =
    [
        new("{{relative_path}}", "Relative path of the selected file."),
        new("{{tokens}}", "Token count for the file."),
        new("{{non_empty_lines}}", "Non-empty line count."),
        new("{{file_size}}", "File size, formatted for display."),
        new("{{refactor_priority}}", "Composite refactor-priority score."),
        new("{{refactor_priority_breakdown}}", "Multi-line explanation for the structural base and any git-derived uplift behind refactor priority."),
    ];
}

public sealed record RefactorPromptTemplatePlaceholderDefinition(string Token, string Description);
