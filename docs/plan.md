# TokenMap - Active UX/UI Plan

This file tracks the current ordered work. Check off a stage only after code, docs, and the required verification for that stage are complete.

## Ordering Rules

1. Start with shared UI changes that affect multiple surfaces.
2. Move settings out of the top toolbar before treemap and tree polish, because the shell composition changes first.
3. Replace the treemap tooltip before lower-priority table polish, because the tooltip is the only detailed inspection surface.
4. Keep the following product constraints unchanged throughout the plan:
   - tooltip is the only detailed inspection surface;
   - no textual analysis status;
   - no reduction of tree-pane priority/width;
   - no change to the existing treemap palette;
   - no extra metric text inside treemap tiles.

## Ordered Stages

- [x] Stage 1. Shared UI foundation and accent alignment
  - Introduce one shared accent color/token set for selection, focus, and progress.
  - Align tree selection, treemap selection/hover, and actionable controls to the same accent.
  - Preserve the current thin progress-bar-only analysis feedback.

- [ ] Stage 2. Toolbar simplification and settings drawer
  - Replace the current in-toolbar settings groups with one `Settings` action.
  - Add a right-side settings drawer/overlay that opens on demand.
  - Ensure the drawer does not consume horizontal workspace width while closed.
  - Move treemap metric, tokenizer, and ignore-related settings into the drawer without losing functionality.

- [ ] Stage 3. Treemap tooltip overhaul
  - Replace the current raw multiline tooltip text with a custom tooltip/popup layout.
  - Keep the tooltip as the only detailed inspection surface for nodes.
  - Show: path, node kind, tokens, total lines, code/comments/blanks, language/ext, share, and subtree file count.

- [ ] Stage 4. Treemap scope navigation
  - Replace `Scope` + `Back to Root` with breadcrumb navigation in the treemap header.
  - Preserve the current drill-down behavior and root reset behavior.
  - Do not add extra metric text into treemap tiles.

- [ ] Stage 5. Tree table polish
  - Add explicit sort-state visibility.
  - Improve numeric alignment and row emphasis.
  - Add developer-oriented icons for folders and common source-file types.

## Verification

Run at minimum after each completed stage:

```bash
dotnet restore
dotnet build Clever.TokenMap.sln
dotnet test Clever.TokenMap.sln --no-build
```

If a stage affects headless/UI scenarios, run the relevant extra checks as well.
