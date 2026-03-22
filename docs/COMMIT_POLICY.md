# Commit Policy

Use this policy for commits in `TokenMap`.

## Format
`<type>: <summary>`

Optional scope is allowed when it adds signal:
`<type>(<scope>): <summary>`

Examples:
- `feat: add project tree table`
- `fix: preserve tree sort after rescan`
- `build: refresh dependency versions`
- `docs: update mvp handoff notes`

## Preferred Types
- `feat`: new user-visible capability or meaningful UX expansion
- `fix`: behavior correction or regression fix
- `build`: NuGet, toolchain, publish, or packaging changes
- `docs`: documentation-only change
- `test`: tests-only change
- `refactor`: code restructuring without intended behavior change
- `perf`: measurable performance improvement
- `chore`: maintenance work that does not fit better elsewhere
- `revert`: explicit rollback commit

## Summary Rules
- Write summaries in imperative mood: `add`, `fix`, `remove`, `refresh`, `preserve`
- Keep the summary short; target about 72 characters or less
- Do not end the summary with a period
- Describe the resulting change, not the implementation detail
- Prefer English commit messages to stay consistent with existing history

## When To Commit
- Commit after each coherent, verified change block that leaves the repo in a shippable state
- Do not batch unrelated work into one commit just because it was done in the same session
- If a user-visible behavior needs coupled UI, viewmodel, test, and docs changes, commit them together
- If a task is still half-done or verification is red, keep working until the block is complete instead of creating a checkpoint commit
- When a follow-up fix is required for the block you just changed, fold it into the same final commit if it has not been committed yet

## Git Safety
- Do not rewrite published history unless the user explicitly asks for it
- Prefer normal commits over amend/rebase for follow-up changes
- Keep commits non-interactive and reproducible from the current worktree

## Repo-Specific Rules
- Keep one coherent concern per commit
- Group together tightly coupled tree/treemap/viewmodel/test changes when they ship one user-facing behavior
- Do not mix unrelated UI work with separate publish or dependency work unless one directly enables the other
- If a change updates repo state or known limitations, include the matching docs update in the same commit
- Before committing, make sure the relevant verification commands are green

Minimum verification for normal code changes:

```bash
dotnet restore
dotnet build Clever.TokenMap.sln
dotnet test Clever.TokenMap.sln --no-build
```

If the change is isolated to docs, tests, or tooling, use judgment and run the smallest meaningful subset.
