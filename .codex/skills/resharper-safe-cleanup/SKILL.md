---
name: resharper-safe-cleanup
description: Run JetBrains ReSharper CLI `jb cleanupcode` with the repository's narrow, versioned cleanup profile to apply safe automated cleanup edits such as removing unused `using` directives, shortening redundant qualified references, and removing redundant XAML namespace aliases. Use when the user wants ReSharper-driven cleanup changes rather than a dead-code report or a broad style rewrite.
---

# ReSharper Safe Cleanup

Use this skill to run the repository's committed `cleanupcode` profile while keeping the edit scope intentionally narrow.

## Preconditions

- Require `jb` on `PATH`; verify it with a command lookup, not `jb --version`. On PowerShell use `Get-Command jb -ErrorAction SilentlyContinue`; on other shells use `where jb` or `command -v jb`.
- Do not install ReSharper CLI tools yourself unless the user explicitly asks.
- If `jb` is missing, stop and tell the user that JetBrains ReSharper Command Line Tools are required. Suggest:
  - `dotnet tool install -g JetBrains.ReSharper.GlobalTools`
  - or repo-local install with `dotnet new tool-manifest`, `dotnet tool install JetBrains.ReSharper.GlobalTools`, `dotnet tool restore`
  - docs: <https://www.jetbrains.com/help/resharper/ReSharper_Command_Line_Tools.html>
  - cleanup docs: <https://www.jetbrains.com/help/resharper/CleanupCode.html>
- Require a solution-shared `.DotSettings` file next to the target `.sln`. This skill assumes the cleanup profile is versioned in VCS, not stored only in Rider user settings.
- Build the target solution before cleanup so `cleanupcode` can resolve symbols. For this repo, run `dotnet build Clever.TokenMap.sln`.
- Do not use this skill for broad style normalization, dead-code triage, or semantic refactors. Use `resharper-unused-code` for `inspectcode` dead-code reporting.

## Execution

1. Run `python .codex/skills/resharper-safe-cleanup/scripts/run_safe_cleanup.py` from the repository root.
2. Pass `--solution <path>` when the repo has multiple `.sln` files.
3. Pass `--profile <name>` only when the committed profile is not named `Safe Cleanup`.
4. Use `--include` / `--exclude` to scope cleanup when the user wants only part of the tree.
5. Use `--dry-run` to validate the resolved solution, settings, profile, and command without editing files.
6. Read `.artifacts/resharper-safe-cleanup/cleanup-summary.md` first.
7. Review `git diff`, then run the repo's normal verification and commit only after the cleanup result is acceptable.

## Profile Contract

The orchestrator script intentionally rejects cleanup profiles that enable tasks outside this narrow allowlist:

- `CSOptimizeUsings`
- `CSShortenReferences`
- `Xaml.RemoveRedundantNamespaceAlias`

This keeps the skill aligned with "unused usings + redundant qualifiers" cleanup and prevents accidental full-style rewrites through a broader profile.
Keep the Rider option `Embrace 'using' directives in region` turned off inside that profile. Configure that in the profile itself rather than expecting the script to compensate for it.

## Output Files

The orchestrator writes:

- `.artifacts/resharper-safe-cleanup/cleanup-summary.md`
- `.artifacts/resharper-safe-cleanup/cleanup-summary.json`
- `.artifacts/resharper-safe-cleanup/cleanup-command.txt`

## Response Shape

When reporting results:

1. State the exact solution, settings file, and profile used.
2. Link the summary path.
3. Summarize the cleanup scope and mention whether the profile stayed within the safe allowlist.
4. Tell the user to review `git diff` and run verification before committing.

## Manual Fallback

If the orchestrator script is unsuitable, run:

```bash
jb cleanupcode --settings="<solution>.sln.DotSettings" --profile="Safe Cleanup" "<solution>.sln"
```

Build the solution first, and keep the profile narrow.
