# TokenMap - Current Status

## Current State
- Project status: **MVP complete**
- Primary MVP platform: **Windows**
- Secondary MVP platform: **macOS smoke/publish baseline**

## What Already Exists In MVP
- Folder selection, re-analysis, cancellation, and progress feedback.
- Project structure scanning with `.gitignore`, `.ignore`, default excludes, and user excludes.
- Local token counting through `Microsoft.ML.Tokenizers` with `o200k_base`, `cl100k_base`, and `p50k_base` profiles.
- `tokei` integration with merged language/code/comments/blanks metrics and fallback partial LOC metrics.
- In-memory cache, batched progress reporting, and resilience to partial file system/tool errors.
- Tree view, summary strip, and treemap in one window.
- Tooltip is the current detailed inspection surface for nodes.
- Hover tooltip, persistent selection highlight, and `Tree <-> Treemap` synchronization.
- Shared accent alignment is in place for selection, focus, progress, and action emphasis without changing treemap fill colors.
- Toolbar settings are collapsed behind an on-demand right-side settings drawer that does not consume workspace width while closed.
- Treemap hover now uses a custom structured tooltip layout instead of a raw multiline text tooltip.
- Treemap scope navigation now uses breadcrumbs instead of the old `Scope + Back to overview` pair.
- Tree table now shows a baseline Material Icon Theme subset for common folders and developer file types, explicit sort-state in headers, right-aligned numeric columns, and stronger hover/selection emphasis.
- Automatic path expansion in the tree when a node is selected from the treemap.
- Treemap drill-down on directory double-click now pairs with breadcrumb-based scope return.
- Verified `win-x64` publish and configured secondary target `osx-arm64`.

## Fixed Decisions
- MVP stack: `.NET 10 + Avalonia stable + CommunityToolkit.Mvvm`.
- `Clever.TokenMap.Core` does not depend on Avalonia.
- `Clever.TokenMap.App` works with the file system only through services/contracts.
- The treemap is implemented as one custom control with no visual/control per rectangle.
- `tokei` is used as a local sidecar and source of truth for language / code / comments / blanks when statistics are available.
- Stage 1 of the active UX/UI plan is complete: one shared accent family now drives selection, focus, progress, and action emphasis.
- Stage 2 of the active UX/UI plan is complete: toolbar settings moved into a toggleable right-side drawer overlay.
- Stage 3 of the active UX/UI plan is complete: treemap hover details render through a custom tooltip layout.
- Stage 4 of the active UX/UI plan is complete: breadcrumb navigation replaced `Scope + Back to overview` for treemap scope changes.
- Stage 5 of the active UX/UI plan is complete: the tree now has developer-oriented icons, explicit sort-state visibility, right-aligned metrics, and clearer row emphasis.
- Primary publish target: `win-x64`.
- `single-file`, `Native AOT`, installer/signing, and Linux polish are post-MVP.

## Documentation
- Documentation is synchronized with the code as of 2026-03-22.
- The documentation policy is defined in [../AGENTS.md](../AGENTS.md): documents reflect only the current state or planned work; history stays in git.
- The active ordered UX/UI follow-up plan is tracked in [plan.md](plan.md).

## Last Verification
- Date: 2026-03-22
- Verified:
  - `dotnet restore`
  - `dotnet build Clever.TokenMap.sln`
  - `dotnet test Clever.TokenMap.sln --no-build`
  - headless scenarios for treemap drill-down, breadcrumb return, and tree-table icon/sort layout expectations

## How To Run
- Development run:
  - `dotnet run --project src/Clever.TokenMap.App/Clever.TokenMap.App.csproj`
- Windows publish:
  - `dotnet publish src/Clever.TokenMap.App/Clever.TokenMap.App.csproj -c Release -r win-x64 --self-contained false`
- Published app:
  - run `artifacts/publish/win-x64/Clever.TokenMap.App.exe`
- Sidecar locations:
  - Windows executable: `third_party/tokei/win-x64/tokei.exe`
  - macOS ARM64 slot: `third_party/tokei/osx-arm64/`
  - publish output already copies `third_party/` next to the app, so `ProcessTokeiRunner` on Windows picks up the sidecar without `PATH` changes.

## Current Limitations
- The ignore parser covers the MVP subset of rules, not the full range of Git ignore edge cases.
- The cache is still in-memory only and lives within a single process.
- The `tokei` sidecar is physically included only for `win-x64`; `osx-arm64` has only a prepared placement slot.
- Linux support, installer/signing, single-file publish, and Native AOT are outside MVP.

## Handoff Summary
- The Windows-first MVP is built, tests are green, and `win-x64` publish plus launch smoke have passed.
- Continue development using:
  - [status.md](status.md) for the current state, run commands, and real limitations;
  - [plan.md](plan.md) for the active ordered UX/UI checklist;
  - [post-mvp.md](post-mvp.md) for the broader backlog;
  - [spec.md](spec.md) and [architecture.md](architecture.md) for product and technical invariants.
