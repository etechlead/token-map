# TokenMap - Architecture (MVP)

## Responsibility Split

- `Clever.TokenMap.Core` holds domain models, shared contracts, settings DTOs, enums, scan options, aggregation rules, and path-normalization rules. It stays free of Avalonia and UI types.
- `Clever.TokenMap.Infrastructure` holds scanning, ignore evaluation, local metrics/token analysis, caching, settings persistence, and concrete file-system/logging implementations. It stays independent from Avalonia and desktop UI framework types.
- `Clever.TokenMap.Treemap` holds the treemap control and its layout/rendering logic.
- `Clever.TokenMap.App` holds the desktop shell, section views, view models, app-layer state, and app-layer services that bind UI workflows to analysis and settings boundaries.
- `Clever.TokenMap.App.AppComposition` is the runtime composition root only. Test, design-time, and harness composition stay outside the app assembly.
- `MainWindowViewModelFactory` is the shared shell-graph builder used across runtime, design-time, headless tests, and harness code.
- The statically enforceable subset of these boundaries lives in [ArchitectureRulesTests.cs](/Z:/Projects/My/tokenmap/src/tests/Clever.TokenMap.Tests/Architecture/ArchitectureRulesTests.cs).

## App-Layer Ownership

- `MainWindowViewModel` stays a shell facade for the desktop window.
- `MainWindowWorkspacePresenter` owns workspace-level projection sync between analysis/session state, tree projection, summary projection, and treemap navigation.
- `AnalysisSessionController` owns committed folder selection, current snapshot, analysis state, progress, and open/rescan/cancel flow.
- `SettingsState` is the source of truth for app-wide analysis and appearance preferences.
- `CurrentFolderSettingsState` is the source of truth for the committed root folder's folder-specific scan preferences.
- `SettingsCoordinator` is the app-layer facade for settings workflows. It exposes read-only settings state views to consumers and keeps writes behind explicit commands.
- `AppIssueState` is the source of truth for the currently displayed user-facing runtime issue.
- `IAppIssueReporter` is the single app-layer entry point for reporting runtime issues. It owns reference-id assignment, structured error logging, and projection of user-visible issues into `AppIssueState`.
- Settings persistence stays infrastructure-owned and logs persistence problems locally; it does not project settings load/save failures into shell-level user-facing issues.
- `Clever.TokenMap.App.ViewModels` and `Clever.TokenMap.App.State` stay independent from infrastructure implementations.
- `Clever.TokenMap.App.Services` stay UI-agnostic except for small platform-adapter services that wrap desktop framework APIs.

## Sources Of Truth

- The scanner defines the included tree structure and analyzed path set.
- Enrichment produces a separate snapshot rather than mutating scanner output in place.
- Global excludes, `.gitignore`, and folder-specific excludes flow through one ignore-rule pipeline.
- Token counting stays local behind `ITokenCounter`.
- Line metrics stay local in the analysis pipeline for included text files.
- `AnalysisIssue` is the canonical snapshot diagnostic model for scanner and metrics-enrichment problems that do not stop analysis.
- `AppIssue` is the canonical runtime issue model for shell actions and global exception handling.
- `AppLogEntry` is the canonical structured log payload; runtime error logging does not use separate ad hoc logger shapes.
- User-facing runtime errors flow through `AppIssueState` and are rendered by the shell as either a non-fatal banner or a fatal modal instead of per-feature bespoke error widgets.
- The treemap remains one custom-rendered control, not a control-per-rectangle surface.
- `Clever.TokenMap.App` works through app-layer services and state; it does not read the file system or settings files directly.
