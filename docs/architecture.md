# TokenMap - Architecture (MVP)

## Responsibility Split

- `Clever.TokenMap.Core` holds domain models, shared contracts, analysis orchestration, and aggregation. It stays free of Avalonia and UI types.
- `Clever.TokenMap.Infrastructure` holds scanner, ignore handling, token counting, `tokei` integration, cache, and path normalization.
- `Clever.TokenMap.Controls` holds the treemap control and its rendering/layout logic.
- `Clever.TokenMap.App` holds the desktop shell, view models, commands, and binding to core services.

## Sources Of Truth

- The scanner defines the tree structure and the included path set.
- Token counts come from the local tokenizer pipeline.
- Language and line metrics come from `tokei` where it recognizes the file.
- Files that have no `tokei` stats still remain in the tree and can carry partial metrics.

## Path Handling

- Nodes carry both `FullPath` and `RelativePath`.
- Internal merge keys use normalized relative paths in `/` form.
- Windows-oriented comparisons use case-insensitive behavior where needed.
- Scanner output and `tokei` output meet on the same normalized relative-path key space.

## Treemap Model

- The treemap is one custom-rendered control, not a control tree of rectangles.
- Layout, rendering, and hit testing are handled on the control's own computed rectangle data.

## User Settings

- Per-user settings are stored in one lightweight `settings.json` file under the user data directory.
- The app starts from defaults first, then best-effort applies values from `settings.json`.
- Missing, unreadable, or malformed settings files must not block startup.
- `Clever.TokenMap.App` uses a settings service/store rather than reading the settings file directly.
- Logs and future cache data live next to that settings file in separate files/directories rather than inside the settings document.
