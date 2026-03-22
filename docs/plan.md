# TokenMap - Implementation Plan

MVP is complete. This file now captures only the current state and the rules for the next work cycle.

## What Is Already Complete In MVP
- Bootstrap solution and the base Avalonia shell.
- Core contracts, scanner, and path normalization.
- `.gitignore` / `.ignore`, default excludes, and user excludes.
- Token counting through `Microsoft.ML.Tokenizers`.
- `tokei` integration, metric merge, and tree aggregation.
- Analyzer orchestration, progress batching, cancellation, and in-memory cache.
- A working main window with folder picker, tree, summary, and details panel.
- Custom-rendered treemap without child controls for each tile.
- Hover tooltip, selection sync, and persistent highlight.
- Windows-first publish and MVP handoff.

## Rules For The Next Cycle
- New work happens only on explicit user request or for a separately agreed item from [post-mvp.md](post-mvp.md).
- Do not bring completed MVP items back into the plan.
- Do not expand scope unless there is a clear need.
- For new product work, use [post-mvp.md](post-mvp.md) as the backlog and [status.md](status.md) as the source of current state.
- Update [status.md](status.md) after each notable change.

## Basic Verification Commands
Run at minimum after each change:

```bash
dotnet restore
dotnet build Clever.TokenMap.sln
dotnet test Clever.TokenMap.sln --no-build
```

If the change affects publish flows or headless/UI scenarios, run the relevant extra checks as well.
