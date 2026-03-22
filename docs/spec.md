# TokenMap - Product Spec (MVP)

## 1. Purpose

**TokenMap** is a local desktop application for analyzing source code in a selected project folder.

The application must:
- build a directory/file tree;
- count **tokens** for files and folders locally, without the cloud;
- show **LOC** and `code/comments/blanks` breakdown;
- visualize metric distribution with a **treemap** in the style of WinDirStat/WizTree;
- respect `.gitignore`, `.ignore`, default excludes, and user excludes.

## 2. MVP Status

MVP is aimed at:
- **development on Windows**;
- **the first working build for Windows**;
- **secondary verification on macOS** after the Windows MVP is ready.

Linux is not a blocking MVP target, but the architecture must not make it harder to add after MVP.

## 3. MVP Technology Stack

- **.NET 10**
- **Avalonia (stable, non-preview)**
- **CommunityToolkit.Mvvm** for MVVM
- **Microsoft.ML.Tokenizers** for local token counting
- **Tokei** as a local sidecar for language / code / comments / blanks statistics
- **System.Text.Json** for JSON serialization and parsing

## 4. Main User Scenario

1. The user launches the application.
2. The user selects the project root folder.
3. The application scans the structure and applies ignore rules and excludes.
4. The application calculates metrics locally.
5. The user sees:
   - the project tree on the left;
   - the treemap in the center;
   - summary at the top;
   - a progress indicator while analysis is active.
6. The user can:
   - hover a treemap rectangle and see a tooltip;
   - click a rectangle and select a node;
   - select a node in the tree on the left;
   - switch the treemap metric;
   - change the tokenizer profile;
   - restart analysis;
   - cancel analysis.

## 5. Mandatory MVP Functional Requirements

### 5.1 Folder Selection
- The user can select the project root folder through a standard folder picker.
- Selecting a folder again starts a new analysis.

### 5.2 Project Structure Scanning
- The application recursively traverses the directory.
- By default, only **included** files and folders are scanned.
- The application must handle the following robustly:
  - `UnauthorizedAccessException`;
  - files that disappear or are removed during analysis;
  - overly long/invalid paths;
  - read errors on individual files.
- Errors on individual paths must not crash the whole analysis.

### 5.3 Ignore And Excludes
The application must respect:
- `.gitignore`;
- `.ignore`;
- built-in default excludes;
- user excludes entered in settings/input.

#### MVP Default Excludes
Exclude by default:
- `.git`
- `.vs`
- `.idea`
- `.vscode`
- `node_modules`
- `bin`
- `obj`
- `dist`
- `build`
- `out`
- `coverage`
- `target`
- `Debug`
- `Release`

#### Application Rules
- Ignore rules are applied recursively.
- Nested `.gitignore` and `.ignore` files must affect their own subtree.
- User excludes are applied relative to the root.
- If a path is excluded, it must not participate in the tree, tokens, or LOC.

### 5.4 Symlinks And Reparse Points
For MVP:
- do **not** follow symlinks / junctions / reparse points;
- mark such nodes as skipped;
- avoid traversal cycles.

### 5.5 Text File Detection
- Tokens are counted only for files detected as text.
- Binary files are shown in the tree only if they are not excluded, but:
  - tokens are not counted for them;
  - LOC is not counted for them;
  - the node is marked as skipped/binary.
- A simple, reliable text/binary heuristic is acceptable.

### 5.6 Token Counting
- Token counting must happen **locally**.
- At minimum, these profiles must be supported:
  - `o200k_base`
  - `cl100k_base`
  - `p50k_base`
- One active tokenizer profile must be selected.
- Tokens are counted from the **file contents**, without the cloud.
- Before tokenization, content is normalized to `\n` line endings so results are more stable across operating systems.
- For a folder, tokens equal the sum of tokens across all included descendants.

### 5.7 LOC And Language Stats
- For a known Tokei language, the following must be available:
  - `TotalLines`
  - `CodeLines`
  - `CommentLines`
  - `BlankLines`
  - `Language`
- If `tokei` does not provide stats for a file, partial filling is allowed:
  - `TotalLines` may be calculated independently;
  - `CodeLines/CommentLines/BlankLines/Language` may be `null`.
- For folders, metrics are aggregated from descendants.
- Aggregate statistics by language and extension must be available.

### 5.8 Treemap
The treemap is the central visual element of the UI.

Mandatory requirements:
- one custom control;
- layout based on a treemap algorithm, preferably squarified;
- rectangle size depends on the selected metric;
- supported MVP metrics:
  - `Tokens`
  - `TotalLines`
  - `CodeLines`

#### Interactions
- hover over a rectangle:
  - visually highlights the node;
  - shows a tooltip with short statistics.
- click on a rectangle:
  - selects the node;
  - gives it a persistent highlight;
  - syncs selection with the tree on the left.
- selection in the tree on the left:
  - syncs the treemap.

### 5.9 Hover Tooltip
The tooltip is the primary detail surface for treemap node inspection in MVP.

The tooltip for a treemap node must show:
- relative path;
- node type (`file`/`folder`);
- tokens;
- share of the total root;
- total lines;
- code/comments/blanks when available;
- language and/or extension;
- for folders, the number of included files in the subtree.

### 5.10 Treemap Scope Navigation
- The current treemap scope must be visible in the treemap header.
- The user must be able to return from a scoped treemap view back to the root.
- The exact control may evolve from a scope label/button pair to breadcrumb navigation without changing the underlying drill-down model.

### 5.11 Tree On The Left
The left side shows a tree of included folders/files.

Requirements:
- folders before files;
- stable sorting;
- mouse selection;
- expansion of the path to the selected treemap node;
- the tree must not lag on large projects.

### 5.12 Toolbar / Summary / Progress
The top panel must contain:
- `Open Folder`
- `Rescan`
- `Cancel`
- treemap metric selector
- tokenizer profile selector
- toggles:
  - `Respect .gitignore`
  - `Respect .ignore`
  - `Use default excludes`

Also required:
- summary cards/summary strip with key totals;
- progress indicator.

### 5.13 Cancellation And Progress
- Analysis must support `CancellationToken`.
- The `Cancel` button must actually interrupt the active analysis.
- The UI must receive progress in batches, not per file.
- After cancellation, the application must stay in a consistent state.

## 6. MVP Non-Functional Requirements

### 6.1 Performance
- The UI must not freeze on typical projects.
- Analysis must run in the background.
- The UI must not update for every single file.
- The treemap must not create a visual/control for every rectangle.

### 6.2 Reliability
- A failure while reading one file must not crash the whole scan.
- Errors must be logged and surfaced softly.
- The application must correctly survive repeated scan/cancel/rescan cycles.

### 6.3 Locality
- Everything works locally.
- No cloud is used.
- No internet connection is required.
- Project data is not sent outside the machine.

### 6.4 UX
- The interface must be clean and tidy.
- Main layout:
  - toolbar on top;
  - tree on the left;
  - treemap in the center.
- The tooltip must work without a click.
- The selected node must be clearly visible.

## 7. MVP Architecture Requirements

## 7.1 Layers
The solution must be split at minimum into:
- `Clever.TokenMap.App` - Avalonia shell, views, viewmodels;
- `Clever.TokenMap.Controls` - custom controls, including the treemap;
- `Clever.TokenMap.Core` - models, interfaces, aggregation, orchestration;
- `Clever.TokenMap.Infrastructure` - file system, ignore parser, tokei runner, token counter, cache.

## 7.2 Sources Of Truth
- Tree structure and the set of included paths come from our scanner.
- `Tokei` is the source of truth for language / code / comments / blanks when statistics are available.
- Tokens come from our token counter.

## 7.3 Cache
In MVP, a simple local cache keyed by the following is acceptable:
- path
- size
- last write time UTC
- tokenizer profile

The cache should be useful, but must not complicate the architecture.

## 8. What Is Not In MVP
Do not build the following as part of MVP:
- live file watching;
- snapshot diff/compare;
- snapshot export/import;
- search/filter UI;
- treemap zoom/pan;
- installers and signing/notarization;
- single-file publish;
- Native AOT;
- Linux polish;
- a complex settings system;
- plugins or extensibility.

## 9. MVP Done Criteria
MVP is done if:
- the application launches and works on Windows;
- the user can select a folder and get an analysis result;
- `.gitignore`, `.ignore`, and excludes actually affect the result;
- tokens are counted locally;
- the treemap works and reacts to hover and click;
- the hover tooltip provides the required node details;
- metric selection and tokenizer profile selection work;
- cancellation and repeated scan are stable;
- the solution builds and tests are green.
