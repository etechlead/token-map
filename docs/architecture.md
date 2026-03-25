# TokenMap - Architecture (MVP)

## Responsibility Split

- `Clever.TokenMap.Core` holds domain models, shared contracts, enums, scan options, and aggregation rules. It stays free of Avalonia and UI types.
- `Clever.TokenMap.Core` also holds the shared persisted settings DTOs used by app-layer coordinators and infrastructure stores.
- `Clever.TokenMap.Infrastructure` holds scanner, ignore handling, token counting, local non-empty line counting, cache, settings storage, logging, and path normalization.
- `Clever.TokenMap.Treemap` holds the treemap control and its rendering/layout logic.
- `Clever.TokenMap.App` holds the desktop shell, section views, view models, app-layer coordinators, and binding to analysis/settings services.

## App-Layer State

- `MainWindowViewModel` is the shell coordinator for the desktop window.
- `AnalysisSessionController` owns the committed selected-folder state, the current snapshot, analysis state, progress, and open/rescan/cancel flow. A newly picked folder is only committed when its analysis succeeds; failed or cancelled opens keep the previous committed folder/snapshot pair intact.
- `SettingsState` is the app-layer source of truth for persisted app-wide analysis and appearance preferences.
- `CurrentFolderSettingsState` is the app-layer source of truth for the committed root folder's folder-specific scan preferences.
- `SettingsCoordinator` owns app-wide and current-folder settings load/save behavior, maps persisted settings onto app-layer state, debounces persistence, resolves scan options for a target root path, and applies theme changes.
- `RecentFoldersViewModel` owns recent-folder projection, the start-surface empty-state workflow, and recent-folder open/remove/clear commands.
- `ProjectTreeViewModel` owns tree sort mode, expansion state, selection, and the visible-row projection built from the scanned `ProjectNode` tree.
- `TreemapNavigationState` owns selected node state, treemap root scope, and breadcrumb rebuilding.
- `MainWindow` composes section `UserControl`s for toolbar/summary, project tree, treemap, and settings drawer. Per-section UI behavior stays in section code-behind when it is strictly view-specific, such as `DataGrid` sorting headers and treemap drill-down event wiring.

## Sources Of Truth

- The scanner defines the tree structure and the included path set.
- `ProjectSnapshotMetricsEnricher` clones the scanned tree into a separate enriched snapshot instead of mutating the scanner output in place.
- `ProjectNode.Metrics` and `ProjectNode.SkippedReason` are construction-time state; enrichment produces new nodes rather than rewriting existing ones.
- Global excludes, directory `.gitignore` files, and folder-specific excludes all run through the same gitignore-style ignore-rule engine with this precedence order: global excludes, then `.gitignore`, then folder excludes.
- Token counts come from the local `o200k_base` tokenizer pipeline.
- Line metrics come from local file analysis for included text files and count only non-empty lines.
- File details show extensions rather than inferred languages.

## Path Handling

- Nodes carry both `FullPath` and `RelativePath`.
- Internal merge keys use normalized relative paths in `/` form.
- Windows-oriented comparisons use case-insensitive behavior where needed.
- Scanner output, cache keys, and analysis results meet on the same normalized relative-path key space, partitioned by normalized root path.

## Treemap Model

- The treemap is one custom-rendered control, not a control tree of rectangles.
- Treemap weighting can switch between tokens, non-empty lines, and file size.
- Layout, rendering, and hit testing are handled on the control's own computed rectangle data.

## User Settings

- Per-user app-wide settings are stored in one lightweight `settings.json` file under the user data directory.
- Folder-specific settings are stored separately under a sibling `folders/<folder-key>/settings.json` layout, one small file per committed root folder.
- The app starts from defaults first, then best-effort applies values from `settings.json` and any requested folder settings file.
- Analysis preferences including global excludes, appearance preferences including theme mode, and recent folder history are stored in the app-wide settings file; folder-specific excludes live only in the per-folder settings files.
- `Clever.TokenMap.App` works against app-layer settings state and the shared settings DTOs from `Clever.TokenMap.Core`; infrastructure stores handle JSON persistence details behind the store boundary.
- Settings use typed enum-backed values and persist as JSON strings.
- Unknown or legacy persisted enum values fall back to defaults instead of keeping compatibility aliases forever.
- Missing, unreadable, malformed, or unwritable app-wide or folder settings files must not block startup, scanning, or UI interaction; the app falls back to defaults.
- `Clever.TokenMap.App` uses a settings service/store rather than reading the settings file directly.
- Logs and future cache data live next to that settings file in separate files/directories rather than inside the settings document.

