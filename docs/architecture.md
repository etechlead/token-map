# TokenMap - Architecture (MVP)

## Responsibility Split

- `Clever.TokenMap.Core` holds domain models, shared contracts, enums, scan options, and aggregation rules. It stays free of Avalonia and UI types.
- `Clever.TokenMap.Infrastructure` holds scanner, ignore handling, token counting, local line counting, cache, settings storage, logging, and path normalization.
- `Clever.TokenMap.Controls` holds the treemap control and its rendering/layout logic.
- `Clever.TokenMap.App` holds the desktop shell, section views, view models, app-layer coordinators, and binding to analysis/settings services.

## App-Layer State

- `MainWindowViewModel` is the shell coordinator for the desktop window.
- `AnalysisSessionController` owns selected folder state, the current snapshot, analysis state, progress, and open/rescan/cancel flow.
- `SettingsCoordinator` owns settings load/apply/save behavior, debounced persistence, and theme application.
- `TreemapNavigationState` owns selected node state, treemap root scope, and breadcrumb rebuilding.
- `MainWindow` composes section `UserControl`s for toolbar/summary, project tree, treemap, and settings drawer. Per-section UI behavior stays in section code-behind when it is strictly view-specific, such as `DataGrid` sorting headers and treemap drill-down event wiring.

## Sources Of Truth

- The scanner defines the tree structure and the included path set.
- Token counts come from the local tokenizer pipeline.
- Line metrics come from local file analysis for included text files.
- File details show extensions rather than inferred languages.

## Path Handling

- Nodes carry both `FullPath` and `RelativePath`.
- Internal merge keys use normalized relative paths in `/` form.
- Windows-oriented comparisons use case-insensitive behavior where needed.
- Scanner output, cache keys, and analysis results meet on the same normalized relative-path key space.

## Treemap Model

- The treemap is one custom-rendered control, not a control tree of rectangles.
- Layout, rendering, and hit testing are handled on the control's own computed rectangle data.

## User Settings

- Per-user settings are stored in one lightweight `settings.json` file under the user data directory.
- The app starts from defaults first, then best-effort applies values from `settings.json`.
- Analysis preferences and appearance preferences, including theme mode, are stored in that settings file.
- Settings use typed enum-backed values and persist as JSON strings.
- Unknown or legacy persisted enum values fall back to defaults instead of keeping compatibility aliases forever.
- Missing, unreadable, or malformed settings files must not block startup.
- `Clever.TokenMap.App` uses a settings service/store rather than reading the settings file directly.
- Logs and future cache data live next to that settings file in separate files/directories rather than inside the settings document.
