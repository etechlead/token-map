# Localization

## Purpose
This document is the current-state source of truth for UI localization and prompt-language behavior in TokenMap.

## Scope
- Application UI language is resource-driven.
- Prompt language uses the same supported culture list as application language.
- This document covers how to add languages without introducing duplicate registries or hardcoded language lists.

## Sources Of Truth
- UI strings live in `src/Clever.TokenMap.App/Resources/AppStrings*.resx`.
- Supported cultures are discovered by `ApplicationLanguageService` from the app resource assemblies.
- `LocalizationState` builds application-language options and prompt-language options from that same supported-culture list.
- Settings persist language selections as string tags, not enums.

## Adding A New UI Language
1. Add `AppStrings.<culture>.resx` under `src/Clever.TokenMap.App/Resources/`.
2. Build the solution so the satellite resource assembly is produced.
3. Verify the new language appears in both the application-language selector and the prompt-language selector.

No enum, registry, or viewmodel language list should need to change for a new UI language.

## Prompt Language Rules
- Prompt language options reuse the same supported cultures as UI language options.
- Prompt language does not have a separate list of supported languages.
- The prompt selector should not introduce a `System` option; it lists concrete supported cultures only.
- The selected prompt language is persisted as a culture tag.

## Prompt Content Fallback
- Built-in prompt templates and prompt-breakdown copy are currently localized for English and Russian.
- If a newly added UI language has no prompt-specific built-in copy yet, prompt generation falls back to English prompt text.
- This fallback is intentional and is preferable to adding another language registry.

## Contributor Rules
- Do not add a separate prompt-language enum.
- Do not hardcode language lists in XAML, viewmodels, or settings code.
- Do not add parallel sources of truth for language tags.
- Put UI text in `AppStrings*.resx`.
- If prompt-specific text needs localization beyond English fallback, extend the centralized prompt services instead of scattering new language switches through unrelated viewmodels.

## Files To Check
- `src/Clever.TokenMap.App/Resources/AppStrings*.resx`
- `src/Clever.TokenMap.App/Services/ApplicationLanguageService.cs`
- `src/Clever.TokenMap.App/State/LocalizationState.cs`
- `src/Clever.TokenMap.App/Services/RefactorPromptTemplateCatalog.cs`
- `src/Clever.TokenMap.App/Services/RefactorPromptComposer.cs`
