# TokenMap

Desktop source-tree analysis for local repositories, with a synchronized project tree and treemap for inspecting token weight, line counts, and subtree hotspots.

## Screenshot

TBD: replace this section with a real application screenshot after the current UI pass is finalized.

## Highlights

- Local-only analysis: no cloud services, remote tokenizers, or browser-based rendering.
- Synchronized inspection: selecting nodes in the tree updates the treemap, and treemap drill-down stays aligned with the tree.
- Token-aware treemap: compare repository weight by tokens, lines, or size, where lines count only non-empty lines and size reflects file bytes.
- Configurable scan behavior: adjust ignore handling without leaving the main window.
- Lightweight desktop shell: Avalonia UI with one custom-rendered treemap control instead of a control-per-rectangle surface.

## Architecture

- `src/Clever.TokenMap.Core`: domain models, enums, contracts, scan options, and aggregation rules.
- `src/Clever.TokenMap.Infrastructure`: scanner, ignore handling, local token counting, local non-empty line metrics, cache, settings storage, and logging.
- `src/Clever.TokenMap.Treemap`: treemap control, layout, color rules, and hit testing.
- `src/Clever.TokenMap.App`: desktop shell, section views, view models, analysis session coordination, treemap navigation state, and settings coordination.

The current technical boundaries are documented in [docs/architecture.md](docs/architecture.md).

## MVP Scope

- Open a local folder, scan it, and inspect the resulting tree/treemap snapshot.
- Keep analysis, settings, and theme application local to the desktop app.
- Preserve a maintainable app-layer split where long-lived state lives outside `MainWindowViewModel`.

Current limitations:

- Windows is the primary MVP target. macOS targets Apple Silicon only, and its packaging path is currently manual-only so it does not block Windows-first delivery. Linux polish is intentionally deferred.
- The repository ships source, Windows CI, an unsigned per-user Windows installer release path, and a manual Apple Silicon macOS packaging path. Signing, notarization, and broader multi-platform release automation are still out of scope.
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

Build an Apple Silicon macOS bundle from Windows:

```powershell
.\scripts\publish-macos-arm64.ps1
```

The script writes `.artifacts\macos-arm64\TokenMap.app`. Copy that bundle to a Mac for manual validation. If the direct copy drops execute permissions, run:

```bash
chmod +x "TokenMap.app/Contents/MacOS/Clever.TokenMap.App"
```

Build an Apple Silicon macOS DMG on macOS:

```bash
bash ./scripts/package-macos-dmg.sh
```

The script writes `.artifacts/macos-arm64/TokenMap-macos-arm64-<version>-unsigned.dmg` by default. The workflow in [package-macos-unsigned.yml](.github/workflows/package-macos-unsigned.yml) is manual-only and can build from a selected branch or tag, optionally skip tests for older packaging-only tags, then attach the unsigned DMG to an existing GitHub Release.

The DMG and app remain unsigned. macOS users may need to approve the first launch in `System Settings > Privacy & Security`.
When the DMG opens, drag `TokenMap` into `Applications`, then launch it from `Applications`.

Build a per-user Windows installer:

```powershell
.\scripts\publish-windows-installer.ps1
```

The script writes `.artifacts\windows-installer\installer\TokenMap-win-x64-<version>.exe`. The installer is unsigned and defaults to `%LOCALAPPDATA%\Programs\TokenMap`, so it does not require administrator rights.

Silent uninstall keeps user data by default. To remove `%LOCALAPPDATA%\Clever\TokenMap` during uninstall, pass `/PURGEUSERDATA` to the uninstaller in addition to the normal Inno silent switches.

## Repository Quality

- `README`, `LICENSE`, `.editorconfig`, analyzer policy, and GitHub Actions are part of the repo baseline.
- Build and test CI runs on `windows-latest`.
- A release workflow packages an unsigned per-user Windows installer on `windows-latest` and attaches it to GitHub Releases.
- macOS packaging remains available only as a manual workflow/script path and is not part of the automatic release flow.
- Headless UI tests cover the shell layout and tree/treemap synchronization.
- Unit tests cover settings serialization, debounced settings persistence, analysis session flow, and treemap navigation state.

## License

TokenMap is available under the [MIT License](LICENSE).
