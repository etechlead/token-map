# TokenMap - Architecture (MVP)

## 1. Solution Layout

```text
Clever.TokenMap.sln
Directory.Packages.props
global.json
AGENTS.md
.codex/
  config.toml
docs/
  spec.md
  architecture.md
  plan.md
  implement.md
  status.md
  post-mvp.md
src/
  Clever.TokenMap.App/
  Clever.TokenMap.Controls/
  Clever.TokenMap.Core/
  Clever.TokenMap.Infrastructure/
tests/
  Clever.TokenMap.Core.Tests/
  Clever.TokenMap.HeadlessTests/
  Fixtures/
third_party/
  tokei/
    README.md
    win-x64/
      tokei.exe
    osx-arm64/
      README.md
```

## 2. Responsibility Split

### `Clever.TokenMap.Core`
Contains:
- domain models;
- interfaces;
- analysis orchestration;
- metric aggregation;
- progress model;
- cache contracts.

Does not contain:
- Avalonia;
- concrete UI types;
- direct work with `Process`, `FileSystemWatcher`, `Window`, and so on.

### `Clever.TokenMap.Infrastructure`
Contains:
- file system traversal implementation;
- ignore/exclude rule application;
- text/binary detection;
- tokenizer adapter;
- `TokeiRunner`;
- cache;
- path normalization.

### `Clever.TokenMap.Controls`
Contains:
- `TreemapControl`;
- treemap layout engine;
- helper render/hit-test models.

### `Clever.TokenMap.App`
Contains:
- windows, view models, commands;
- user-scenario orchestration;
- binding to `Clever.TokenMap.Core`;
- summary/data presentation;
- tree <-> treemap <-> details interaction.

## 3. Recommended Project-Level Contracts

### Core Models
Minimum model set:

- `ProjectSnapshot`
- `ProjectNode`
- `NodeMetrics`
- `ScanOptions`
- `TokenProfile`
- `AnalysisProgress`
- `SkippedReason`
- `LanguageSummary`
- `ExtensionSummary`

### Core Interfaces
Minimum interface set:

- `IProjectAnalyzer`
- `IProjectScanner`
- `IPathFilter`
- `ITextFileDetector`
- `ITokenCounter`
- `ITokeiRunner`
- `ICacheStore`
- `IClock` (optional, if convenient testable cache timestamping is needed)

## 4. Main Data Flow

1. The UI calls `IProjectAnalyzer.AnalyzeAsync(root, options, progress, cancellationToken)`.
2. The scanner builds a tree of included paths.
3. For each included file:
   - detect text/binary;
   - for text files, read contents;
   - count tokens;
   - prepare a record for merge.
4. In parallel or afterward, call `TokeiRunner`.
5. Map `tokei` results to the same relative paths.
6. Inside `ProjectAnalyzer`, merge:
   - tree structure;
   - tokens;
   - LOC/language breakdown;
   - skipped/errors.
7. Aggregate from leaves to root.
8. Produce a `ProjectSnapshot`.
9. The UI updates the tree, treemap, details, and summary.

## 5. Truth Boundaries

To avoid divergence, always follow these rules:

- Tree structure is defined **only** by our scanner.
- Excluded files must not suddenly appear because of `tokei`.
- `tokei` does not control the tree; it only enriches already included nodes with statistics.
- If `tokei` does not know a file, the node still stays in the tree.
- For unknown files, partially populated LOC metrics are allowed.

## 6. Path Normalization Rules

Paths must be normalized consistently:

- store `FullPath`;
- store `RelativePath` relative to the root;
- use `RelativePath` in unified `/` form for internal keys;
- path comparison must respect Windows case-insensitive behavior where needed;
- merge with `tokei` by normalized relative path.

## 7. Text Vs Binary Policy

MVP does not require perfect language-aware binary detection.

Sufficient approach:
- quickly inspect a small file prefix;
- if the file looks binary, mark it as skipped/binary;
- do not read the full binary file into memory just for detection.

## 8. Token Counting Policy

- Count tokens only for text files.
- Normalize content to `\n` before tokenization.
- Wrap the tokenizer behind `ITokenCounter`.
- Cache tokenizer instances by `TokenProfile`.
- A tokenization error for one file must not fail the whole analysis.

## 9. Tokei Integration Policy

- `TokeiRunner` must be able to:
  - find the sidecar `tokei` next to the application;
  - or use `PATH` if the sidecar is not found;
  - start the process without a shell;
  - read stdout/stderr;
  - terminate the process correctly on cancellation.
- The `tokei` parser must rely on real sample JSON, not guesses.

## 10. UI Interaction Model

### Selection
`SelectedNodeId`/`SelectedPath` is the single source of truth for the selected node for:
- tree;
- treemap.

### Hover
`HoveredNodeId` exists only for treemap hover state and tooltip.

### Sync Rules
- click in the treemap updates the selected node;
- selection in the tree updates the treemap;
- the tooltip reflects hover only and does not replace selection;
- hover does not change the selected node.

## 11. Treemap Rendering Requirements

The treemap must:
- keep a flat list of computed rectangles;
- do hit testing on its own data;
- render as a single control;
- not create a child control per node;
- be able to recalculate on:
  - snapshot change;
  - metric change;
  - resize;
  - selected/hovered node change.

## 12. UI Composition

Recommended root layout:
- `Grid`:
  - row 0: toolbar + summary
  - row 1: main content
  - row 2: status/progress
- main content:
  - column 0: tree
  - column 1: treemap
- settings may live in the toolbar or in a temporary right-side drawer/overlay, but the main shell stays focused on tree + treemap.

Important:
- do not place virtualized items into infinite height;
- do not put main panels into a `StackPanel` if virtualization is needed.

## 13. Testing Strategy

### Unit Tests
Cover:
- ignore/exclude;
- path normalization;
- aggregation;
- cache key generation;
- merge logic for scanner + tokei;
- text/binary detection at least minimally.

### Headless UI Tests
Cover:
- snapshot opening/binding;
- selection sync;
- hover/click in the treemap;
- treemap scope navigation updates.

## 14. Logging / Diagnostics
MVP does not require a complex telemetry system.

Sufficient:
- neat internal logs;
- human-readable analysis status in the UI;
- accumulation of warnings/errors for debugging.

## 15. Publish Policy For MVP

- Primary publish target: `win-x64`.
- Secondary target after a working Windows MVP: `osx-arm64`.
- `tokei` is shipped as a sidecar file.
- Single-file and Native AOT are not part of MVP.
