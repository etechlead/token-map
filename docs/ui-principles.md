# UI Principles

Use these rules for `TokenMap` UI changes.

## Minimalism

- Prefer absence over placeholder UI.
- Do not show idle states, empty status labels, or decorative helper text when nothing is happening.
- Show progress indicators only while real work is in progress.
- Remove duplicate status signals when the same information is already visible elsewhere.
- Keep labels only when they add decision-making value for the user.
- Default to the smallest visual treatment that still makes the state obvious.

## Icons

- Keep one icon language per UI layer instead of mixing unrelated styles in the same surface.
- Current baseline: app-shell command icons use `Fluent UI System Icons`; project tree file and folder icons use the vendored `Material Icon Theme` subset.

## Themes

- UI changes must work in both light and dark theme without losing hierarchy, affordance, or readability.
- Treat theme support as part of the default UI baseline, not as a later polish pass.

## Practical Rule

If a control is not actionable, not informative, and not needed in the current moment, hide it.
