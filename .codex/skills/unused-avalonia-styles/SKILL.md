---
name: unused-avalonia-styles
description: Scan Avalonia `.axaml` style selectors for likely unused or mismatched styles by comparing `Style Selector="..."` patterns against XAML and C# usage evidence. Use when the user asks to find dead Avalonia styles, stale selector branches after control/icon migrations, or likely-unused style classes/names instead of reviewing selectors manually.
---

# Unused Avalonia Styles

Use this skill to produce a high-signal report of likely dead Avalonia selectors. The scanner is heuristic: it finds strong cleanup candidates and ambiguous selectors that need manual review, but it does not prove safety for removal.

## Execution

1. Run:

```bash
python .codex/skills/unused-avalonia-styles/scripts/run_unused_avalonia_styles.py --workspace-root .
```

2. Read `.artifacts/unused-avalonia-styles/unused-summary.md` first.
3. Use `.artifacts/unused-avalonia-styles/unused-summary.json` only when structured post-processing is useful.

## What The Scanner Checks

- Reads `.axaml` files and extracts `Style Selector="..."` values.
- Collects XAML usage evidence from element tags plus `Classes`, `Name`, and `x:Name`.
- Collects simple code-behind evidence from `new ControlType`, `.Classes.Add("...")`, and `.Name = "..."`.
- Compares anchored selector components such as `Path.some-class`, `#SomeName`, or `Button.foo` against that evidence.

## Output Buckets

- `actionable`: anchored selector components have no matching evidence; these are strong cleanup candidates.
- `review`: selectors use syntax the scanner does not model well, or are broad plain-type selectors where generated/template usage may exist.

## Limits

- Treat the report as `likely unused`, not as proof.
- Descendant selectors are checked component-by-component; the scanner does not build a full visual-tree matcher.
- Template-generated controls can make broad type-only selectors look unused, so those are routed to `review`.
- Reflection, runtime class toggles, or highly dynamic control construction can evade the scan.

## Response Shape

When reporting results:

1. State the exact workspace scanned.
2. Link the markdown and JSON report paths.
3. Present `actionable` candidates first, then `review` items.
4. Call out the heuristic nature of the scan when recommending deletions.
