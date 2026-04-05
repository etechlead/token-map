# Tools

## Coverage Summary

- Location: `scripts/run-coverage.ps1`
- Purpose: run the repo verification flow with XPlat coverage collection, merge the per-test-project Cobertura reports, and emit one agent-readable JSON summary without streaming build/test noise to stdout.
- Use it when you want a compact machine-readable view of line, branch, and method coverage, hotspot classes by uncovered lines, and stable artifact paths for follow-up inspection.

```powershell
powershell -File scripts/run-coverage.ps1
```

- Outputs land under `.artifacts/coverage-agent/`.

## Visual Harness

- Location: `tools/Clever.TokenMap.VisualHarness`
- Purpose: internal headless Avalonia harness for rendering app surfaces to PNG, saving machine-readable reports, and diffing screenshots between palettes or revisions.
- Use it when you need visual evidence without manually driving the app: tuning treemap colors, checking layout and styling changes, producing review screenshots, investigating visual regressions, or comparing the same UI state across palettes, branches, or data sources.
- It can capture the main window, the settings-open state, the share modal, and a standalone treemap; it can render from a real repo snapshot or from deterministic demo data; and it can diff two existing images.
- Treat built-in `help` output as the canonical CLI contract and parameter reference.

```powershell
dotnet run --project tools/Clever.TokenMap.VisualHarness -- help
dotnet run --project tools/Clever.TokenMap.VisualHarness -- capture --source repo --project-root . --theme light --surface main
dotnet run --project tools/Clever.TokenMap.VisualHarness -- compare --left .artifacts\visual-harness\example-a.png --right .artifacts\visual-harness\example-b.png
```

- Capture artifacts land under `.artifacts/visual-harness/`, compare artifacts under `.artifacts/visual-compare/`.

## README Screenshot

- Location: `scripts/capture-readme-screenshot.ps1`
- Purpose: capture the current main window through `Visual Harness` directly at the `README` screenshot resolution so the docs asset can be refreshed without manual image editing.
- Use it when you need to refresh `docs/readme/screenshot.png` for releases, documentation updates, or visual checks against the repo's current state.

```powershell
powershell -File scripts/capture-readme-screenshot.ps1
```

- By default the script captures the current repo root, writes raw harness artifacts to `.artifacts/visual-harness/readme/`, and updates `docs/readme/screenshot.png`.
- If `docs/readme/screenshot.png` already exists, its current pixel size is reused automatically as the harness window size so the capture stays in the same native resolution.
