# Tools

## Visual Harness

- Location: `tests/Clever.TokenMap.VisualHarness`
- Purpose: internal headless Avalonia harness for rendering app surfaces to PNG, saving machine-readable reports, and diffing screenshots between palettes or branches.
- Use it when tuning treemap colors, checking layout changes, investigating visual regressions, or comparing the same UI state across code revisions.

### Capture Modes

- `capture-palettes`: convenience mode for rendering multiple palettes from the same snapshot and generating diffs against the baseline palette.
- `capture`: generic capture mode for one or more surfaces and palettes without assuming a palette-comparison workflow.
- `compare`: image-to-image diff for already captured PNG files.

### Supported Surfaces

- `main`: full `MainWindow` with the current snapshot loaded.
- `settings`: `MainWindow` with the settings drawer open.
- `treemap`: standalone `TreemapControl` host for focused color and layout inspection.

### Sources

- `repo`: analyze a real folder through the normal project-analysis pipeline before rendering.
- `demo`: use the built-in deterministic snapshot for quick UI experiments.

### Common Commands

```powershell
dotnet run --project tests/Clever.TokenMap.VisualHarness -c Release -- capture-palettes --source repo --project-root . --metric size
dotnet run --project tests/Clever.TokenMap.VisualHarness -c Release -- capture --source repo --surface settings --palette studio --metric size
dotnet run --project tests/Clever.TokenMap.VisualHarness -c Release -- compare --left artifacts\visual-harness\example-a.png --right artifacts\visual-harness\example-b.png
```

### Notes

- Default outputs go to `artifacts/visual-harness/<timestamp>` or `artifacts/visual-compare/<timestamp>`.
- `report.json` records generated image paths and diff statistics.
- The harness auto-excludes its own artifact directories when it analyzes the current repo, so repeated runs do not contaminate the snapshot being rendered.
- Window and treemap sizes can be overridden with `--window-width`, `--window-height`, `--treemap-width`, and `--treemap-height`.
