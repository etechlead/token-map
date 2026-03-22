# AGENTS.md

## Goal
Build the MVP of the local desktop application **TokenMap**: analyze source code in a selected folder, count tokens and LOC, visualize the project structure with a treemap, show the directory tree on the left, and a details panel on the right.

## Key Decisions
- MVP stack: **.NET 10 + Avalonia (stable, non-preview)**.
- UI: top toolbar, tree on the left, treemap in the center, details panel on the right.
- Tokens are counted locally with `Microsoft.ML.Tokenizers` through the `ITokenCounter` adapter.
- Language / code / comments / blanks metrics use local **Tokei** through `ITokeiRunner`.
- The source of truth for tree structure and the set of included files is our .NET scanner.
- `Tokei` is the source of truth for language / code / comments / blanks where statistics are available.
- The treemap is rendered by **one custom control**, without creating a visual/control per rectangle.

## MVP Priorities
1. **Windows** is the main development environment and the first target platform.
2. **macOS** is the secondary validation target after the Windows MVP works.
3. **Linux** is not an MVP blocker; do not break portability, but do not polish it separately.

## Mandatory Architecture Rules
- `Clever.TokenMap.Core` must not depend on Avalonia.
- `Clever.TokenMap.App` must not read the file system directly; only through services/contracts.
- Do not use WebView, browser embedding, or JS charting.
- Do not use cloud services, network APIs, or remote tokenizers.
- Do not pull in heavy dependencies without explicit need.
- Do not build the treemap out of thousands of child controls.
- Every new functionality must end with build and test verification.

## MVP Scope
Required in MVP:
- folder selection;
- project structure scanning;
- support for `.gitignore`, `.ignore`, default excludes, and user excludes;
- tokens for files and folders;
- LOC and `code/comments/blanks` breakdown when `tokei` recognizes the language;
- treemap by the selected metric;
- hover tooltip for a treemap node;
- click selection with persistent highlight;
- synchronization between `TreeView`, treemap, and details panel;
- analysis progress and cancellation.

Not required in MVP:
- live watch;
- diff of two snapshots;
- single-file publish;
- Native AOT;
- installer/signing/notarization;
- Linux polish;
- search/filter UI;
- snapshot export.

## Change Rules
- Work against the current active plan in `docs/plan.md`; if there is no active stage or separate plan, work only on an explicit user request.
- Do not make unrelated refactors.
- Do not change architecture decisions without a strong reason and a note in `docs/status.md`.
- If there is local ambiguity, choose the simplest option that stays compatible with `docs/spec.md`.
- If the change needs tests, add them as part of the same change.
- In PowerShell, do not use `&&`; separate commands with compatible syntax such as `;`.
- Follow `docs/COMMIT_POLICY.md` for commit strategy, message format, and git safety.

## Documentation Policy
- Documentation must reflect either the **current state** or **planned work**.
- Do not keep historical lists of completed stages, tasks, or temporary transition notes in working documents.
- Change history belongs in git history, not duplicated in working documentation.

## Verification After Each Change
Minimum:
- `dotnet restore`
- `dotnet build Clever.TokenMap.sln`
- `dotnet test Clever.TokenMap.sln --no-build`

If the change adds headless/UI tests, run them too.

## Documentation Updates
After each notable change:
- update `docs/status.md`;
- briefly record what was done, what was verified, and what decisions were made;
- if the change leaves real open limitations, record them in their current form without historical logs.

## What To Do When Blocked
If you hit a problem:
1. Record it in `docs/status.md`.
2. Choose the smallest workaround that does not break the architecture.
3. Do not expand scope unless necessary.
