# TokenMap - Agent Implementation Instructions

## 1. How To Work

Before starting each cycle:
1. Read:
   - `AGENTS.md`
   - `docs/spec.md`
   - `docs/architecture.md`
   - `docs/plan.md`
   - `docs/status.md`
2. Check the current plan and status in `docs/plan.md` and `docs/status.md`.
3. If there is no separate active plan, work only on an explicit user request or on a separately agreed item from `docs/post-mvp.md`.
4. After changes, run the build and tests.
5. Update `docs/status.md`.

## 2. Main Rules
- Build the MVP, not a platform vision.
- Do not add features from `docs/post-mvp.md` to the current task without an explicit request.
- Do not make unrelated refactors.
- Do not change the stack.
- Do not pull in WebView, JS chart libraries, or a browser runtime.
- Do not replace the custom treemap with a heavy ready-made chart control.
- Do not introduce plugins, DI frameworks, event buses, or similar infrastructure without clear need.
- Do not commit without an explicit user request.

## 3. Mandatory Technical Decisions

### 3.1 UI
- Avalonia stable, non-preview.
- MVVM via `CommunityToolkit.Mvvm`.
- Layout:
  - toolbar on top;
  - tree on the left;
  - treemap in the center;
  - progress at the bottom or in the top summary area.
  - settings may stay in the toolbar or move into a temporary drawer.

### 3.2 Treemap
- Use a custom `TreemapControl`.
- One custom-rendered control.
- Hover and click are handled inside it.
- The selected item must have a persistent highlight.
- The hover tooltip must work without a click.

### 3.3 Metrics
- Tokens: local via `Microsoft.ML.Tokenizers`.
- LOC/language breakdown: via `tokei`.
- Tree structure: only through our scanner.

### 3.4 Ignore logic
- Support `.gitignore`, `.ignore`, default excludes, and user excludes.
- Excluded paths must be completely absent from the result model.

## 4. Tokei: How To Integrate
- Do not guess the JSON schema from memory.
- First obtain a **real sample output** of `tokei --files --output json` on a fixture.
- Only after that, write the types/parser.
- Sidecar discovery:
  1. next to the application in `third_party/tokei/<rid>/...`;
  2. fallback to `PATH`.
- On analysis cancellation, terminate the `tokei` process correctly.

## 5. Tokenizer: How To Integrate
- Hide the concrete tokenizer behind `ITokenCounter`.
- Cache tokenizer instances by `TokenProfile`.
- A failure on one file must not break the whole analysis.
- Normalize newlines to `\n` before tokenization.

## 6. File Scanning Policy
- Do not read all files into memory at once.
- Do not use `GetFiles()` for the entire tree in one shot.
- Build a streaming/per-item scan.
- Traversal must be resilient to access errors and race conditions.
- Do not follow symlinks/reparse points.

## 7. UI Sync Policy
There must be a single source of truth for the currently selected node.
At minimum:
- `SelectedNode`
- `HoveredNode` (treemap only)

Rules:
- hover does not change selected;
- click in the treemap changes selected;
- click/select in the tree changes selected;
- tooltip handles hover-time detail inspection.

## 8. Quality Verification After Each Change
Required:

```bash
dotnet restore
dotnet build Clever.TokenMap.sln
dotnet test Clever.TokenMap.sln --no-build
```

If UI/headless tests are added, run them too.

If checks fail:
- fix them;
- do not finish the change with a red build.

## 9. What To Update In docs/status.md
After each notable change, always add:
- date/context;
- what was implemented or changed;
- what was verified;
- what decisions were made;
- what actually remains open.

## 10. What To Do If There Is Ambiguity
If the ambiguity is local and does not change the product goal:
- choose the simplest and most direct option;
- record the decision in `docs/status.md`.

If the ambiguity changes scope or architecture:
- do not invent a new large system;
- choose the smallest solution compatible with the spec.

## 11. Recommended End-Of-Change Report Format
At the end of each cycle, briefly state:
1. what changed;
2. which verification commands were run;
3. which files/modules were touched;
4. what remains open after the change.

## 12. Local Platform Priorities
For MVP:
- Windows first;
- macOS second;
- do not polish Linux or make it a blocker.

Do not spend the current task on:
- Linux-specific polish;
- single-file publish;
- Native AOT;
- signing/notarization.

## 13. Forbidden Anti-Patterns
- a giant god service without interfaces and responsibilities;
- tree <-> treemap sync through random global state;
- one control per treemap tile;
- direct `tokei` calls from UI code;
- mixing file scanning, token counting, and Avalonia views in one class;
- silently swallowing all errors without trace/debug information.
