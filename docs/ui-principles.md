# UI Principles

Use these rules for `TokenMap` UI changes.

## Minimalism

- Prefer absence over placeholder UI.
- Do not show idle states, empty status labels, or decorative helper text when nothing is happening.
- Show progress indicators only while real work is in progress.
- Remove duplicate status signals when the same information is already visible elsewhere.
- Keep labels only when they add decision-making value for the user.
- Default to the smallest visual treatment that still makes the state obvious.

## Practical Rule

If a control is not actionable, not informative, and not needed in the current moment, hide it.
