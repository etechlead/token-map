# AGENTS.md

## Goal
Maintain `TokenMap` as a local desktop app for source-tree analysis with synchronized tree and treemap inspection.

## Working Baseline
- Windows is the primary MVP target; macOS is secondary validation; do not spend the current task on Linux polish.
- Current technical boundaries live in `docs/architecture.md`.
- Day-to-day execution flow and active-plan handling live in `docs/workflow.md`.
- Inactive backlog lives in `docs/post-mvp.md`.

## Non-Negotiables
- `Clever.TokenMap.Core` stays free of Avalonia.
- `Clever.TokenMap.App` does not read the file system directly; it goes through services and contracts.
- Token counting stays local behind `ITokenCounter`.
- Language and line metrics stay local behind `ITokeiRunner`.
- The scanner defines the included tree; `tokei` only enriches included nodes.
- The treemap stays one custom-rendered control, not a control-per-rectangle surface.
- Do not add WebView, browser embedding, JS charting, cloud services, remote tokenizers, or heavy dependencies without explicit need.

## Change Rules
- Do not make unrelated refactors.
- Do not change architecture decisions without updating the current-state docs.
- In PowerShell, do not use `&&`; use compatible command separation.
- Follow `docs/commit-policy.md` for commit strategy and git safety.

## Documentation Rules
- Keep docs limited to current state and plans.
- Update `docs/AGENTS.md` if the documentation map or policy changes.
- Rewrite docs in place; do not append history, journals, or completed-stage lists.

## Verification
- Standard repo verification flow:
  - `dotnet restore`
  - `dotnet build Clever.TokenMap.sln`
  - `dotnet test Clever.TokenMap.sln --no-build`
- If the change adds headless or UI tests, run them too.

## If Blocked
1. Choose the smallest workaround that does not break the documented architecture.
2. If the blocker creates a lasting repo limitation, reflect it in the relevant current-state doc.
3. Do not expand scope unless necessary.
