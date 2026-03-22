---
name: resharper-unused-code
description: Run JetBrains ReSharper CLI `jb inspectcode` to scan .NET solutions for unused code and dead-code signals, then filter noisy `Unused*` findings into actionable removals, contract-review items, markup/binding review items, and likely false positives from generated or serializer-driven code. Use when the user asks to find unused symbols, dead code, or ReSharper `inspectcode` cleanup candidates rather than reading raw SARIF.
---

# ReSharper Unused Code

Use this skill to produce a high-signal dead-code report from `jb inspectcode` output instead of handing the user raw SARIF.

## Preconditions

- Require `jb` on `PATH`.
- Do not install ReSharper CLI tools yourself unless the user explicitly asks.
- If `jb` is missing, stop and tell the user that JetBrains ReSharper Command Line Tools are required. Suggest:
  - `dotnet tool install -g JetBrains.ReSharper.GlobalTools`
  - or repo-local install with `dotnet new tool-manifest`, `dotnet tool install JetBrains.ReSharper.GlobalTools`, `dotnet tool restore`
  - docs: <https://www.jetbrains.com/help/resharper/ReSharper_Command_Line_Tools.html>
  - inspectcode docs: <https://www.jetbrains.com/help/resharper/InspectCode.html>

## Execution

1. Run `python .codex/skills/resharper-unused-code/scripts/run_unused_scan.py` from the repository root.
2. Pass `--solution <path>` when the repo has multiple `.sln` files.
3. Read `.artifacts/resharper-unused/unused-summary.md` first.
4. Use `.artifacts/resharper-unused/unused-summary.json` only when structured post-processing is useful.

## Output Files

The orchestrator writes:

- `.artifacts/resharper-unused/inspectcode-unused.sarif`
- `.artifacts/resharper-unused/unused-summary.md`
- `.artifacts/resharper-unused/unused-summary.json`

## Interpretation

Treat buckets as follows:

- `actionable`: likely removal candidates; still verify source before editing.
- `contract-review`: abstraction is wider than current usage; decide whether to narrow the contract.
- `review-markup`: likely markup/string-based usage; verify before deleting.
- `noise-generated`: generated file noise.
- `noise-serializer`: serializer/binder noise, usually not a deletion task.

## Heuristics

The filter script intentionally applies only coarse heuristics:

- Suppress generated files such as `.g.cs`, `.designer.cs`, `AssemblyInfo.cs`, `GlobalUsings.g.cs`, and generated editorconfig artifacts.
- Map `UnusedMemberInSuper.Global` to `contract-review`.
- Map `UnusedParameter.Global` with implementation-wide unused messages to `contract-review`.
- Map unused setters in serializer-heavy files to `noise-serializer`.
- Search markup files (`.xaml`, `.axaml`, `.razor`, `.cshtml`, `.aspx`) for the symbol name and map matching findings to `review-markup`.

These heuristics reduce noise. They do not prove safety against reflection, source generators, runtime binding, or convention-based access.

## Response Shape

When reporting results:

1. State the exact solution scanned.
2. Link the SARIF and filtered summary paths.
3. Present findings in this order:
   - actionable removals
   - contract-review items
   - suppressed-noise rationale
4. Do not dump raw SARIF into the user answer.

## Manual Fallback

If the orchestrator script is unsuitable, run:

```bash
jb inspectcode --swea --absolute-paths --severity=SUGGESTION --format=Sarif --dotnetcore="<dotnet>" --toolset-path="<sdk>/MSBuild.dll" --output="<sarif>" "<solution>"
python .codex/skills/resharper-unused-code/scripts/filter_unused_sarif.py "<sarif>" --workspace-root .
```

Prefer the SDK from `global.json` when present; otherwise use the newest installed `dotnet` SDK.
