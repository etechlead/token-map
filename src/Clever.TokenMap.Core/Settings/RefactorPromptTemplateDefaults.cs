namespace Clever.TokenMap.Core.Settings;

public static class RefactorPromptTemplateDefaults
{
    public const string DefaultRefactorPromptTemplate =
        """
        Please assess whether this file is a good refactoring candidate.

        Target file:
        - Relative path: {{relative_path}}

        Observed metrics:
        - Tokens: {{tokens}}
        - Non-empty lines: {{non_empty_lines}}
        - File size: {{file_size}}
        - Complexity: {{complexity}}
        - Hotspots: {{hotspots}}
        - Refactor Priority: {{refactor_priority}}

        Possible reasons these signals look this way:
        {{complexity_breakdown}}
        {{hotspots_breakdown}}
        {{refactor_priority_breakdown}}

        Task:
        - Review this file in the context of the surrounding module and the repository architecture.
        - Do not jump straight to code changes.
        - First decide whether refactoring is justified at all.
        - If refactoring seems warranted, propose several options that would fit the existing architecture and boundaries of this application.
        - Focus on reducing code smells, lowering complexity, and improving maintainability and agent-readability.
        - Include a minimal option and call out if the current design should stay as-is.
        """;
}
