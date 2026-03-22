# TokenMap

Desktop source-tree analysis for local repositories, with a synchronized project tree and treemap for inspecting token weight, line counts, and subtree hotspots.

## Screenshot

TBD: replace this section with a real application screenshot after the current UI pass is finalized.

## Highlights

- Local-only analysis: no cloud services, remote tokenizers, or browser-based rendering.
- Synchronized inspection: selecting nodes in the tree updates the treemap, and treemap drill-down stays aligned with the tree.
- Token-aware treemap: compare repository weight by tokens, total lines, or non-empty lines.
- Configurable scan behavior: choose token profile and ignore handling without leaving the main window.
- Lightweight desktop shell: Avalonia UI with one custom-rendered treemap control instead of a control-per-rectangle surface.

## Architecture

- `src/Clever.TokenMap.Core`: domain models, enums, contracts, scan options, and aggregation rules.
- `src/Clever.TokenMap.Infrastructure`: scanner, ignore handling, local token counting, local line metrics, cache, settings storage, and logging.
- `src/Clever.TokenMap.Controls`: treemap control, layout, color rules, and hit testing.
- `src/Clever.TokenMap.App`: desktop shell, section views, view models, analysis session coordination, treemap navigation state, and settings coordination.

The current technical boundaries are documented in [docs/architecture.md](docs/architecture.md).

## MVP Scope

- Open a local folder, scan it, and inspect the resulting tree/treemap snapshot.
- Keep analysis, settings, and theme application local to the desktop app.
- Preserve a maintainable app-layer split where long-lived state lives outside `MainWindowViewModel`.

Current limitations:

- Windows is the primary MVP target. macOS is validated as a secondary target. Linux polish is intentionally deferred.
- The repository ships source and CI for the first public release, but not installers, signing, notarization, or release automation.
- Settings currently persist lightweight analysis and appearance preferences, not recent projects or saved scan presets.

## Getting Started

```powershell
dotnet restore Clever.TokenMap.sln
dotnet build Clever.TokenMap.sln
dotnet test Clever.TokenMap.sln --no-build
```

Run the desktop app:

```powershell
dotnet run --project .\src\Clever.TokenMap.App\Clever.TokenMap.App.csproj
```

## Repository Quality

- `README`, `LICENSE`, `.editorconfig`, analyzer policy, and GitHub Actions are part of the repo baseline.
- Build and test CI runs on `windows-latest` and `macos-latest`.
- Headless UI tests cover the shell layout and tree/treemap synchronization.
- Unit tests cover settings serialization, debounced settings persistence, analysis session flow, and treemap navigation state.

## License

TokenMap is available under the [MIT License](LICENSE).
