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
        new("{{complexity}}", "Composite complexity score."),
        new("{{hotspots}}", "Composite hotspot score."),
        new("{{refactor_priority}}", "Composite refactor-priority score."),
        new("{{complexity_breakdown}}", "Multi-line explanation for complexity drivers."),
        new("{{hotspots_breakdown}}", "Multi-line explanation for hotspot drivers."),
        new("{{refactor_priority_breakdown}}", "Multi-line explanation for refactor-priority drivers."),
    ];
}

public sealed record RefactorPromptTemplatePlaceholderDefinition(string Token, string Description);
