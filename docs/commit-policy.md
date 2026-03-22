# Commit Policy

## Message Format

- Use `<type>: <summary>`.
- Optional scope is allowed: `<type>(<scope>): <summary>`.
- Keep the summary short, imperative, and result-focused.
- Prefer English commit messages.

## Commit Boundaries

- Commit one coherent, verified change block at a time.
- Do not batch unrelated work into one commit.
- Keep coupled UI, viewmodel, test, and docs changes together when they ship one behavior.
- Do not commit half-finished work or a red verification state.

## Git Safety

- Do not rewrite published history unless the user explicitly asks for it.
- Prefer normal commits over amend/rebase for follow-up changes.
- Keep git operations non-interactive.

## Repo Notes

- Include matching docs updates when a change affects lasting repo truth or known limitations.
- Before committing, make sure verification is green and follows the standard repo flow from `AGENTS.md`.
- For docs-, tests-, or tooling-only changes, use judgment and run the smallest meaningful verification subset.
