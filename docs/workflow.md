# TokenMap - Workflow

## Before Work

1. Read `AGENTS.md`, `docs/AGENTS.md`, and `docs/architecture.md`.
2. Read the active plan file only when it exists.
3. If there is no active plan file, work only on an explicit user request or on an item explicitly promoted from `docs/post-mvp.md`.

## While Working

- Keep scope tight to the current request.
- Do not pull work from `docs/post-mvp.md` without an explicit request.

## After Changes

- Run the repo's standard verification flow from `AGENTS.md`.
- Update the affected current-state docs in place when the lasting truth changed.
- Update `docs/AGENTS.md` only if the documentation map or policy changed.
- Rewrite docs in place; do not append progress logs or completed-step history.

## If Blocked

- Prefer the smallest workaround that does not break the documented architecture.
- If the blocker creates a lasting repo limitation, reflect it in the relevant current-state doc.
- If checks fail, do not close the change as complete while the build or tests are red.
