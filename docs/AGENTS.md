# Documentation AGENTS.md

## Documentation Model
- Repository documentation exists for only two purposes: **current-state projection** and **plans**.
- Current-state documents are a compact projection of the current codebase, current constraints, and a router to adjacent docs.
- Plan documents contain only unfinished work. Once work lands, rewrite the relevant current-state docs and remove the completed step from the plan.
- Stable docs keep durable repo truth. Temporary specs, rolling status reports, and other short-lived working artifacts should not live as permanent documents.
- Do not keep historical timelines, handoff journals, migration diaries, or lists of completed stages in working documentation.
- Git history is the only change history.
- When updating docs, rewrite sections in place to the latest truth instead of appending notes.

## Organization Rules
- Each document should have one clear purpose and one natural owner topic.
- Do not duplicate the same rule, command list, or product description across multiple docs; keep one canonical place and link or route to it.
- If a document becomes mostly obvious from the codebase or UI, shrink it or remove it.
- Doc filenames in `docs/` use lowercase with dashes; `AGENTS.md` is the only intentional exception.

## Read Order
- Start with root `AGENTS.md`, then this file.
- For most implementation tasks, read `architecture.md` and `workflow.md`.
- Read `tools.md` when the request touches internal harnesses, screenshot capture, visual diffs, or repo tooling.
- Read `ui-principles.md` only for UI-facing changes.
- Read `versioning.md` when the request touches application version display, release tags, GitHub Releases, or packaged artifact naming.
- Read `commit-policy.md` only when the user asks for a commit or commit message.
- Read `post-mvp.md` only when the request touches deferred scope or backlog questions.

## Documentation Router
- `architecture.md`: current technical boundaries and sources of truth.
- `post-mvp.md`: inactive backlog candidates outside the current MVP scope.
- `tools.md`: internal repo tools and harnesses that are useful for implementation and debugging work.
- `ui-principles.md`: current UI constraints and interaction principles.
- `versioning.md`: canonical versioning, tagging, release, and artifact-naming policy.
- `workflow.md`: the concise workflow for turning a request into code and docs.
- `commit-policy.md`: commit format, grouping, and git safety rules.

## Update Rules
- Rewrite current-state docs in place. Do not append progress journals, dated logs, handoff notes, or lists of completed stages.
- If an active plan file exists, keep it limited to unfinished work only.
- If a planned item lands, remove it from the active plan file and reflect any lasting truth in the relevant current-state docs.
- Keep documents small and non-overlapping. Route to another doc instead of restating the same material.
