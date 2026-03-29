# TokenMap - Architecture

Dependency boundaries are enforced in [ArchitectureRulesTests.cs](/Z:/Projects/My/tokenmap/src/tests/Clever.TokenMap.Tests/Architecture/ArchitectureRulesTests.cs).
This document covers project purpose, canonical ownership, runtime flow, and non-obvious invariants.

## Projects

- `Clever.TokenMap.Core`: shared domain layer. Holds analysis models, contracts, enums, path rules, and settings DTOs used across the solution.
- `Clever.TokenMap.Infrastructure`: local implementation layer. Holds filesystem scanning, ignore evaluation, metrics enrichment, token counting, caching, logging, and settings persistence.
- `Clever.TokenMap.Treemap`: treemap UI primitive. Holds the custom-rendered treemap control plus layout and rendering rules.
- `Clever.TokenMap.App`: desktop shell. Holds runtime composition, app state, app services, view models, and Avalonia views that project snapshots and settings into UI.
- `Clever.TokenMap.VisualHarness`: repo tooling for visual capture and comparison of UI surfaces and treemap rendering.
- `Clever.TokenMap.Tests`: verification layer. Holds architecture rules, unit tests, headless UI tests, and tool/harness coverage.

## Canonical Owners

- Analysis session, current snapshot, progress, and open/rescan/cancel flow -> `AnalysisSessionController`
- App-wide analysis and appearance preferences -> `SettingsState`
- Folder-specific scan preferences for the active root -> `CurrentFolderSettingsState`
- Treemap root, selection, and breadcrumbs -> `TreemapNavigationState`
- User-visible runtime issue -> `AppIssueState`
- Shell-level projection sync across analysis state, tree, summary, and treemap -> `MainWindowWorkspacePresenter`

## Runtime Flow

- `Program` and `App` bootstrap the desktop process and app lifetime.
- `AppComposition` builds the runtime object graph.
- `AnalysisSessionController` runs analysis and publishes snapshot, state, and progress.
- `MainWindowWorkspacePresenter` projects analysis/session state into tree, summary, and treemap UI state.
- Settings changes flow through `SettingsCoordinator`, then into app/folder settings sessions and persistence.

## Non-Obvious Invariants

- `MainWindowViewModel` is a shell facade, not a long-lived state owner.
- `Clever.TokenMap.App` does not access the filesystem or settings files directly.
- The scanner defines the included tree and analyzed node set.
- Metrics enrichment returns a new snapshot; scanner output is not mutated in place.
- Token counting stays behind `ITokenCounter`.
- The treemap stays one custom-rendered control.
