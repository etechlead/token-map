# TokenMap - Architecture (MVP)

## Responsibility Split

- `Clever.TokenMap.Core` holds domain models, shared contracts, analysis orchestration, and aggregation. It stays free of Avalonia and UI types.
- `Clever.TokenMap.Infrastructure` holds scanner, ignore handling, token counting, local line counting, cache, and path normalization.
- `Clever.TokenMap.Controls` holds the treemap control and its rendering/layout logic.
- `Clever.TokenMap.App` holds the desktop shell, view models, commands, and binding to core services.

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
- Missing, unreadable, or malformed settings files must not block startup.
- `Clever.TokenMap.App` uses a settings service/store rather than reading the settings file directly.
- Logs and future cache data live next to that settings file in separate files/directories rather than inside the settings document.
