# TokenMap - Post-MVP Plan

This document describes work that is **not part of MVP**, but is planned after a working MVP is complete.

## 1. Nearest Post-MVP

### 1.1 macOS polish
- regular manual validation on macOS;
- improved packaging and launch experience;
- fixes for platform-specific UI/file-system rough edges.

### 1.2 Linux support
- targeted validation on Linux;
- polish for X11/XWayland behavior;
- packaging strategy for Linux;
- verification of the `tokei` sidecar and dependencies in Linux environments.

### 1.3 Publish improvements
- single-file publish;
- smaller artifacts;
- better release artifact structure;
- updated/automated delivery of the `tokei` sidecar for macOS and newer `tokei` versions;
- packaging automation.

### 1.4 Native AOT
- investigation of Native AOT viability;
- trimming/AOT-safe corrections;
- startup and memory-footprint profiling;
- enabling AOT only after a stable non-AOT baseline exists.

## 2. Next Product Wave

### 2.1 Snapshot export / import
- save analysis results to JSON;
- reopen a snapshot without rescanning;
- reproducible comparisons across machines/branches.

### 2.2 Compare / Diff
- compare two snapshots;
- changes in tokens / LOC;
- added/removed/changed treemap areas.

### 2.3 Search / filter UI
- filter by path;
- filter by extension;
- filter by language;
- quick search in the tree.

### 2.4 Treemap navigation improvements
- zoom into folder;
- breadcrumb navigation;
- pan/zoom;
- back/forward through selection history.

### 2.5 Live watch mode
- rescan on file changes;
- event debounce/coalescing;
- incremental snapshot updates.

## 3. More Advanced Features

### 3.1 Additional analysis modes
- bytes as a metric;
- file count as a separate metric;
- top-N reports;
- per-extension/per-language breakdown views.

### 3.2 Settings
- recent-project persistence;
- saved user excludes;
- custom tokenizer profile presets;
- maximum file size limit;
- fine-tuning for text/binary detection.

### 3.3 Tree/tooltip UX improvements
- multi-column tree;
- richer tooltip/popup for the treemap;
- developer-oriented file/folder icons;
- open file/folder in the system explorer;
- quick path copy.

### 3.4 Export and reporting
- CSV/JSON summary export;
- markdown report;
- shareable snapshot bundle.

## 4. Engineering Backlog

### 4.1 CI/CD
- a full publish matrix for multiple operating systems;
- release artifacts;
- smoke tests across multiple runtimes.

### 4.2 Installer / signing
- Windows installer;
- macOS signing/notarization;
- Linux packaging strategy.

### 4.3 Diagnostics
- more convenient debug logging;
- performance trace mode;
- internal dev overlays for treemap/layout profiling.

## 5. What Must Not Leak Into MVP
The items below must not "creep" into MVP without a separate decision:
- Linux polish;
- single-file;
- Native AOT;
- export/import;
- diff;
- live watch;
- treemap zoom/pan;
- installers;
- signing/notarization;
- complex settings system.

## 6. Rule For The Agent
If a feature belongs to this document rather than to `docs/spec.md`, it is **not a mandatory part of the current MVP** and must not be added without an explicit request.
