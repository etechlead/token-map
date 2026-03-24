#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
from collections import defaultdict
from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterable


STYLE_SELECTOR_RE = re.compile(r"<Style\b[^>]*\bSelector=\"([^\"]+)\"", re.IGNORECASE)
XAML_TAG_RE = re.compile(r"<(?P<tag>(?!/|!|\?)(?:[\w]+:)?[\w.]+)\b(?P<attrs>[^<>]*)>", re.DOTALL)
ATTR_RE = re.compile(r"(?P<name>[\w:.-]+)\s*=\s*\"(?P<value>[^\"]*)\"", re.DOTALL)
VAR_ASSIGN_RE = re.compile(
    r"(?P<indent>\s*)(?:var|[A-Za-z_][\w<>,?. ]*)\s+(?P<var>[A-Za-z_]\w*)\s*=\s*new\s+(?P<type>[A-Za-z_][\w.]*)\b"
)
CLASS_ADD_RE = re.compile(r"(?P<var>[A-Za-z_]\w*)\.Classes\.Add\(\s*\"(?P<value>[^\"]+)\"\s*\)")
NAME_ASSIGN_RE = re.compile(r"(?P<var>[A-Za-z_]\w*)\.Name\s*=\s*\"(?P<value>[^\"]+)\"")
UNSUPPORTED_SELECTOR_TOKENS = ("[", "/template/", ":is(", ":not(", ":nth-", ":has(")
IGNORED_DIRECTORIES = {"bin", "obj", ".git", ".artifacts", ".codex"}


@dataclass
class UsageRecord:
    file_path: str
    line_number: int
    source: str
    type_name: str | None = None
    classes: set[str] = field(default_factory=set)
    names: set[str] = field(default_factory=set)


@dataclass
class SelectorComponent:
    raw: str
    type_name: str | None
    classes: tuple[str, ...]
    name: str | None

    @property
    def anchored(self) -> bool:
        return bool(self.classes or self.name)

    def describe(self) -> str:
        parts: list[str] = []
        if self.type_name:
            parts.append(self.type_name)
        for class_name in self.classes:
            parts.append(f".{class_name}")
        if self.name:
            parts.append(f"#{self.name}")
        return "".join(parts) or self.raw


@dataclass
class SelectorRecord:
    file_path: str
    line_number: int
    selector: str
    components: list[SelectorComponent]
    unsupported_reason: str | None = None


def normalize_type_name(value: str | None) -> str | None:
    if not value:
        return None

    normalized = value.rsplit("|", 1)[-1]
    normalized = normalized.rsplit(":", 1)[-1]
    return normalized or None


def relpath(path: Path, root: Path) -> str:
    return path.resolve().relative_to(root.resolve()).as_posix()


def iter_source_files(workspace_root: Path, suffixes: tuple[str, ...]) -> Iterable[Path]:
    for suffix in suffixes:
        for file_path in sorted(workspace_root.rglob(f"*{suffix}")):
            if any(part in IGNORED_DIRECTORIES for part in file_path.parts):
                continue
            yield file_path


def split_selector_list(selector: str) -> list[str]:
    parts: list[str] = []
    current: list[str] = []
    depth = 0

    for char in selector:
        if char in "([":
            depth += 1
        elif char in ")]" and depth > 0:
            depth -= 1

        if char == "," and depth == 0:
            part = "".join(current).strip()
            if part:
                parts.append(part)
            current.clear()
            continue

        current.append(char)

    tail = "".join(current).strip()
    if tail:
        parts.append(tail)

    return parts or [selector.strip()]


def split_selector_components(selector: str) -> list[str]:
    normalized = selector.replace(">", " ").replace("+", " ").replace("~", " ")
    return [part for part in normalized.split() if part]


def parse_selector_component(raw_component: str) -> SelectorComponent:
    prefix = raw_component
    pseudo_index = raw_component.find(":")
    if pseudo_index >= 0:
        prefix = raw_component[:pseudo_index]

    type_name: str | None = None
    class_names: list[str] = []
    name: str | None = None

    index = 0
    if prefix and prefix[0] not in ".#":
        while index < len(prefix) and prefix[index] not in ".#":
            index += 1
        type_name = normalize_type_name(prefix[:index])

    while index < len(prefix):
        marker = prefix[index]
        index += 1
        start = index
        while index < len(prefix) and prefix[index] not in ".#":
            index += 1
        value = prefix[start:index]
        if not value:
            continue
        if marker == ".":
            class_names.append(value)
        elif marker == "#":
            name = value

    return SelectorComponent(
        raw=raw_component,
        type_name=type_name,
        classes=tuple(class_names),
        name=name,
    )


def parse_selector_record(file_path: Path, root: Path, selector: str, line_number: int) -> list[SelectorRecord]:
    records: list[SelectorRecord] = []

    for selector_part in split_selector_list(selector):
        unsupported_reason = next((token for token in UNSUPPORTED_SELECTOR_TOKENS if token in selector_part), None)
        components = [parse_selector_component(component) for component in split_selector_components(selector_part)]
        records.append(
            SelectorRecord(
                file_path=relpath(file_path, root),
                line_number=line_number,
                selector=selector_part,
                components=components,
                unsupported_reason=unsupported_reason,
            )
        )

    return records


def scan_style_selectors(workspace_root: Path) -> list[SelectorRecord]:
    records: list[SelectorRecord] = []

    for file_path in iter_source_files(workspace_root, (".axaml", ".xaml")):
        text = file_path.read_text(encoding="utf-8")
        for match in STYLE_SELECTOR_RE.finditer(text):
            selector = match.group(1).strip()
            line_number = text.count("\n", 0, match.start()) + 1
            records.extend(parse_selector_record(file_path, workspace_root, selector, line_number))

    return records


def scan_xaml_usage(workspace_root: Path) -> list[UsageRecord]:
    records: list[UsageRecord] = []

    for file_path in iter_source_files(workspace_root, (".axaml", ".xaml")):
        text = file_path.read_text(encoding="utf-8")
        for match in XAML_TAG_RE.finditer(text):
            tag = match.group("tag")
            attrs = match.group("attrs")
            type_name = normalize_type_name(tag)
            line_number = text.count("\n", 0, match.start()) + 1

            classes: set[str] = set()
            names: set[str] = set()
            for attr_match in ATTR_RE.finditer(attrs):
                attr_name = attr_match.group("name")
                attr_value = attr_match.group("value").strip()
                if attr_name == "Classes":
                    classes.update(value for value in attr_value.split() if value)
                elif attr_name in ("Name", "x:Name"):
                    if attr_value:
                        names.add(attr_value)

            records.append(
                UsageRecord(
                    file_path=relpath(file_path, workspace_root),
                    line_number=line_number,
                    source="xaml",
                    type_name=type_name,
                    classes=classes,
                    names=names,
                )
            )

    return records


def scan_csharp_usage(workspace_root: Path) -> list[UsageRecord]:
    records: list[UsageRecord] = []

    for file_path in iter_source_files(workspace_root, (".cs",)):
        lines = file_path.read_text(encoding="utf-8").splitlines()
        variables: dict[str, UsageRecord] = {}

        def flush_variable(variable_name: str) -> None:
            record = variables.pop(variable_name, None)
            if record is not None:
                records.append(record)

        for line_number, line in enumerate(lines, start=1):
            assign_match = VAR_ASSIGN_RE.search(line)
            if assign_match:
                variable_name = assign_match.group("var")
                flush_variable(variable_name)
                variables[variable_name] = UsageRecord(
                    file_path=relpath(file_path, workspace_root),
                    line_number=line_number,
                    source="cs",
                    type_name=normalize_type_name(assign_match.group("type")),
                )
                continue

            class_match = CLASS_ADD_RE.search(line)
            if class_match:
                variable_name = class_match.group("var")
                if variable_name in variables:
                    variables[variable_name].classes.add(class_match.group("value"))
                continue

            name_match = NAME_ASSIGN_RE.search(line)
            if name_match:
                variable_name = name_match.group("var")
                if variable_name in variables:
                    variables[variable_name].names.add(name_match.group("value"))

        for variable_name in list(variables):
            flush_variable(variable_name)

    return records


def matches_component(record: UsageRecord, component: SelectorComponent) -> bool:
    if component.type_name and record.type_name != component.type_name:
        return False
    if component.name and component.name not in record.names:
        return False
    if component.classes and not set(component.classes).issubset(record.classes):
        return False
    return True


def related_evidence(records: Iterable[UsageRecord], component: SelectorComponent) -> dict[str, list[UsageRecord]]:
    related: dict[str, list[UsageRecord]] = {}

    if component.classes:
        class_matches = [
            record
            for record in records
            if set(component.classes).issubset(record.classes)
        ]
        if class_matches:
            related["class_matches"] = class_matches

    if component.name:
        name_matches = [
            record
            for record in records
            if component.name in record.names
        ]
        if name_matches:
            related["name_matches"] = name_matches

    if component.type_name:
        type_matches = [
            record
            for record in records
            if record.type_name == component.type_name
        ]
        if type_matches:
            related["type_matches"] = type_matches

    return related


def selector_has_plain_type_only(record: SelectorRecord) -> bool:
    return bool(record.components) and all(not component.anchored for component in record.components)


def describe_related(related: dict[str, list[UsageRecord]], component: SelectorComponent) -> str:
    fragments: list[str] = []
    if "class_matches" in related and component.type_name:
        fragments.append("class exists, but only on other control types")
    elif "class_matches" in related:
        fragments.append("class exists elsewhere")

    if "name_matches" in related and component.type_name:
        fragments.append("name exists, but not on the expected control type")
    elif "name_matches" in related:
        fragments.append("name exists elsewhere")

    if not fragments and "type_matches" in related:
        fragments.append("control type exists, but the anchored selector evidence is missing")

    return "; ".join(fragments) if fragments else "no matching usage evidence found"


def evaluate_selectors(selectors: list[SelectorRecord], usages: list[UsageRecord]) -> tuple[list[dict], list[dict]]:
    actionable: list[dict] = []
    review: list[dict] = []

    for selector in selectors:
        if selector.unsupported_reason:
            review.append(
                {
                    "file": selector.file_path,
                    "line": selector.line_number,
                    "selector": selector.selector,
                    "reason": f"Unsupported selector syntax contains '{selector.unsupported_reason}'.",
                }
            )
            continue

        if selector_has_plain_type_only(selector):
            review.append(
                {
                    "file": selector.file_path,
                    "line": selector.line_number,
                    "selector": selector.selector,
                    "reason": "Plain type-only selector; scanner cannot distinguish direct usage from generated/template usage.",
                }
            )
            continue

        missing_components: list[dict] = []
        for component in selector.components:
            if not component.anchored:
                continue

            exact_matches = [record for record in usages if matches_component(record, component)]
            if exact_matches:
                continue

            related = related_evidence(usages, component)
            missing_components.append(
                {
                    "component": component.describe(),
                    "reason": describe_related(related, component),
                    "related_samples": [
                        {
                            "file": sample.file_path,
                            "line": sample.line_number,
                            "source": sample.source,
                            "type": sample.type_name,
                            "classes": sorted(sample.classes),
                            "names": sorted(sample.names),
                        }
                        for sample in (
                            related.get("class_matches")
                            or related.get("name_matches")
                            or related.get("type_matches")
                            or []
                        )[:3]
                    ],
                }
            )

        if missing_components:
            actionable.append(
                {
                    "file": selector.file_path,
                    "line": selector.line_number,
                    "selector": selector.selector,
                    "missing_components": missing_components,
                }
            )

    return actionable, review


def write_report(
    output_dir: Path,
    workspace_root: Path,
    selectors: list[SelectorRecord],
    usages: list[UsageRecord],
    actionable: list[dict],
    review: list[dict],
) -> tuple[Path, Path]:
    output_dir.mkdir(parents=True, exist_ok=True)
    json_path = output_dir / "unused-summary.json"
    md_path = output_dir / "unused-summary.md"

    payload = {
        "workspaceRoot": str(workspace_root),
        "selectorCount": len(selectors),
        "usageRecordCount": len(usages),
        "actionable": actionable,
        "review": review,
    }
    json_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")

    lines = [
        "# Unused Avalonia Styles",
        "",
        f"- Workspace: `{workspace_root}`",
        f"- Style selectors scanned: `{len(selectors)}`",
        f"- Usage records scanned: `{len(usages)}`",
        f"- Actionable candidates: `{len(actionable)}`",
        f"- Review candidates: `{len(review)}`",
        "",
        "## Actionable",
        "",
    ]

    if actionable:
        for item in actionable:
            lines.append(f"- `{item['selector']}` at `{item['file']}:{item['line']}`")
            for missing in item["missing_components"]:
                lines.append(f"  - Missing `{missing['component']}`: {missing['reason']}.")
                for sample in missing["related_samples"]:
                    sample_type = sample["type"] or "unknown"
                    class_suffix = f" classes={sample['classes']}" if sample["classes"] else ""
                    name_suffix = f" names={sample['names']}" if sample["names"] else ""
                    lines.append(
                        f"    - related: `{sample['file']}:{sample['line']}` ({sample['source']}, {sample_type}{class_suffix}{name_suffix})"
                    )
    else:
        lines.append("- No high-confidence candidates found.")

    lines.extend(
        [
            "",
            "## Review",
            "",
        ]
    )

    if review:
        for item in review:
            lines.append(f"- `{item['selector']}` at `{item['file']}:{item['line']}`: {item['reason']}")
    else:
        lines.append("- No ambiguous candidates were flagged.")

    md_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return md_path, json_path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Scan a repo for likely unused Avalonia styles.")
    parser.add_argument(
        "--workspace-root",
        default=".",
        help="Repository root to scan. Defaults to the current directory.",
    )
    parser.add_argument(
        "--output-dir",
        default=".artifacts/unused-avalonia-styles",
        help="Directory for markdown/json reports.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    workspace_root = Path(args.workspace_root).resolve()
    output_dir = workspace_root / args.output_dir

    selectors = scan_style_selectors(workspace_root)
    usages = scan_xaml_usage(workspace_root) + scan_csharp_usage(workspace_root)
    actionable, review = evaluate_selectors(selectors, usages)
    md_path, json_path = write_report(output_dir, workspace_root, selectors, usages, actionable, review)

    print(f"Scanned {len(selectors)} selectors and {len(usages)} usage records.")
    print(f"Actionable candidates: {len(actionable)}")
    print(f"Review candidates: {len(review)}")
    print(f"Markdown report: {md_path}")
    print(f"JSON report: {json_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
