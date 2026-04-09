# Localization

## Scope

- UI strings
- application language selection
- prompt language selection
- prompt-language fallback behavior

## Sources Of Truth

- `AppStrings*.resx` is the canonical source for UI text.
- `ApplicationLanguageService` discovers supported cultures from the app resource assemblies.
- `LocalizationState` builds both application-language and prompt-language options from that same supported-culture list.
- Language settings persist as culture tags, not enums.

## Adding A Language

1. Add `AppStrings.<culture>.resx` under `src/Clever.TokenMap.App/Resources/`.
2. Build the solution so the satellite resource assembly is produced.
3. Verify the language appears in both selectors.

## Invariants

- Do not add hardcoded language lists in XAML, viewmodels, or settings code.
- Do not add a separate prompt-language registry or enum.
- Prompt language reuses the same supported cultures as application language.
- The application-language selector may expose `System`; the prompt-language selector should expose concrete cultures only.
- If a supported UI language has no prompt-specific built-in copy yet, prompt generation falls back to English.

## Key Files

- `src/Clever.TokenMap.App/Resources/AppStrings*.resx`
- `src/Clever.TokenMap.App/Services/ApplicationLanguageService.cs`
- `src/Clever.TokenMap.App/State/LocalizationState.cs`
- `src/Clever.TokenMap.App/Services/RefactorPromptTemplateCatalog.cs`
- `src/Clever.TokenMap.App/Services/RefactorPromptComposer.cs`
