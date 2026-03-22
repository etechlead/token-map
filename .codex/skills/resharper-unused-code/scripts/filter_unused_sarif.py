#!/usr/bin/env python3
import argparse
import json
import re
from collections import Counter
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Sequence, Tuple
from urllib.parse import unquote, urlparse


GENERATED_SUFFIXES = (
    ".g.cs",
    ".g.i.cs",
    ".designer.cs",
    ".generated.cs",
    ".generated.vb",
    ".AssemblyInfo.cs",
    ".GlobalUsings.g.cs",
)

GENERATED_NAMES = {
    "AssemblyInfo.cs",
    "GlobalUsings.g.cs",
    "GeneratedMSBuildEditorConfig.editorconfig",
}

MARKUP_EXTENSIONS = {
    ".xaml",
    ".axaml",
    ".razor",
    ".cshtml",
    ".aspx",
}

SKIP_DIRS = {
    ".git",
    ".idea",
    ".vs",
    ".vscode",
    "bin",
    "obj",
    "node_modules",
    "dist",
    "build",
    "out",
    ".artifacts",
}

SERIALIZER_MARKERS = (
    "System.Text.Json",
    "Newtonsoft.Json",
    "JsonSerializer",
    "JsonConverter",
    "JsonPropertyName",
    "JsonIgnore",
    "JsonInclude",
    "DataContract",
    "DataMember",
    "XmlSerializer",
    "XmlElement",
    "XmlAttribute",
    "XmlIgnore",
    "ProtoMember",
    "MessagePack",
    "Yaml",
    "Bson",
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Filter ReSharper inspectcode SARIF down to useful unused-code signal."
    )
    parser.add_argument("sarif", help="Path to the SARIF file produced by jb inspectcode.")
    parser.add_argument(
        "--workspace-root",
        default=".",
        help="Repository root used for markup searches and path display.",
    )
    parser.add_argument(
        "--format",
        choices=("markdown", "json"),
        default="markdown",
        help="Output format.",
    )
    parser.add_argument(
        "--output",
        help="Optional output file path. Defaults to stdout.",
    )
    parser.add_argument(
        "--show-noise",
        action="store_true",
        help="Include suppressed noise findings in markdown output.",
    )
    return parser.parse_args()


def uri_to_path(uri: str) -> str:
    if uri.startswith("file://"):
        parsed = urlparse(uri)
        return unquote(parsed.path.lstrip("/"))
    return uri


def is_generated_file(path: Path) -> bool:
    name = path.name
    if name in GENERATED_NAMES:
        return True
    lower_name = name.lower()
    return any(lower_name.endswith(suffix.lower()) for suffix in GENERATED_SUFFIXES)


def extract_symbol(message: str) -> Optional[str]:
    match = re.search(r"'([^']+)'", message)
    return match.group(1) if match else None


def load_markup_files(workspace_root: Path) -> List[Tuple[str, str]]:
    markup_files: List[Tuple[str, str]] = []
    for path in iter_workspace_files(workspace_root):
        if path.suffix.lower() not in MARKUP_EXTENSIONS:
            continue

        try:
            content = path.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            try:
                content = path.read_text(encoding="utf-8-sig")
            except UnicodeDecodeError:
                continue

        relative = str(path.relative_to(workspace_root)).replace("\\", "/")
        markup_files.append((relative, content))

    return markup_files


def find_markup_hits(markup_files: Sequence[Tuple[str, str]], symbol: Optional[str]) -> List[str]:
    if not symbol:
        return []

    token = symbol.split(".", 1)[0]
    if len(token) < 3:
        return []

    hits: List[str] = []
    for path, content in markup_files:
        if token in content:
            hits.append(path)
            if len(hits) >= 5:
                break

    return hits


def iter_workspace_files(workspace_root: Path) -> Iterable[Path]:
    for path in workspace_root.rglob("*"):
        if not path.is_file():
            continue
        if any(part in SKIP_DIRS for part in path.parts):
            continue
        yield path


def file_text(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        try:
            return path.read_text(encoding="utf-8-sig")
        except UnicodeDecodeError:
            return ""


def classify_finding(
    finding: Dict[str, object],
    workspace_root: Path,
    markup_files: Sequence[Tuple[str, str]],
) -> Dict[str, object]:
    path = Path(str(finding["file_path"]))
    message = str(finding["message"])
    rule_id = str(finding["rule_id"])
    symbol = finding.get("symbol")
    content = file_text(path) if path.exists() else ""
    markup_hits = find_markup_hits(markup_files, symbol if isinstance(symbol, str) else None)

    bucket = "actionable"
    reason = "No common noise heuristic matched."

    if is_generated_file(path):
        bucket = "noise-generated"
        reason = "Generated or tool-produced file."
    elif rule_id == "UnusedMemberInSuper.Global":
        bucket = "contract-review"
        reason = "Abstraction member is unused through the contract, but implementations are used."
    elif rule_id == "UnusedParameter.Global" and "implementations" in message.lower():
        bucket = "contract-review"
        reason = "Contract parameter is not used by any current implementation."
    elif rule_id.startswith("UnusedAutoPropertyAccessor") and message.endswith(".set' is never used"):
        if any(marker in content for marker in SERIALIZER_MARKERS):
            bucket = "noise-serializer"
            reason = "Setter likely exists for serializer or binder usage."
    elif markup_hits:
        bucket = "review-markup"
        reason = "Symbol name appears in markup files; verify binding or generated access first."

    finding["bucket"] = bucket
    finding["reason"] = reason
    finding["markup_hits"] = markup_hits
    return finding


def load_findings(sarif_path: Path, workspace_root: Path) -> List[Dict[str, object]]:
    data = json.loads(sarif_path.read_text(encoding="utf-8"))
    runs = data.get("runs") or []
    if not runs:
        return []

    run = runs[0]
    markup_files = load_markup_files(workspace_root)
    findings: List[Dict[str, object]] = []
    for result in run.get("results", []):
        rule_id = result.get("ruleId")
        if not isinstance(rule_id, str) or not rule_id.startswith("Unused"):
            continue

        locations = result.get("locations") or []
        if not locations:
            continue

        physical = locations[0].get("physicalLocation") or {}
        artifact = physical.get("artifactLocation") or {}
        region = physical.get("region") or {}

        file_path = uri_to_path(str(artifact.get("uri", "")))
        message = str((result.get("message") or {}).get("text", ""))
        symbol = extract_symbol(message)

        finding: Dict[str, object] = {
            "rule_id": rule_id,
            "file_path": file_path,
            "line": int(region.get("startLine", 1)),
            "message": message,
            "symbol": symbol,
        }
        findings.append(classify_finding(finding, workspace_root, markup_files))

    findings.sort(key=lambda item: (str(item["bucket"]), str(item["file_path"]), int(item["line"])))
    return findings


def relative_path(path: str, workspace_root: Path) -> str:
    try:
        return str(Path(path).resolve().relative_to(workspace_root.resolve())).replace("\\", "/")
    except Exception:
        return path.replace("\\", "/")


def markdown_report(
    sarif_path: Path,
    workspace_root: Path,
    findings: List[Dict[str, object]],
    show_noise: bool,
) -> str:
    counts = Counter(str(item["bucket"]) for item in findings)
    lines = [
        "# ReSharper Unused Code Summary",
        "",
        f"- SARIF: `{sarif_path}`",
        f"- Workspace: `{workspace_root}`",
        f"- Total `Unused*` findings: {len(findings)}",
        f"- Actionable: {counts.get('actionable', 0)}",
        f"- Contract review: {counts.get('contract-review', 0)}",
        f"- Markup review: {counts.get('review-markup', 0)}",
        f"- Noise suppressed: {counts.get('noise-generated', 0) + counts.get('noise-serializer', 0)}",
        "",
    ]

    for bucket, title in (
        ("actionable", "Actionable"),
        ("contract-review", "Contract Review"),
        ("review-markup", "Markup Review"),
    ):
        bucket_items = [item for item in findings if item["bucket"] == bucket]
        if not bucket_items:
            continue
        lines.append(f"## {title}")
        lines.append("")
        for item in bucket_items:
            path = relative_path(str(item["file_path"]), workspace_root)
            symbol = f" `{item['symbol']}`" if item.get("symbol") else ""
            lines.append(
                f"- `{item['rule_id']}`{symbol} `{path}:{item['line']}` - {item['message']} Reason: {item['reason']}"
            )
            markup_hits = item.get("markup_hits") or []
            if markup_hits:
                lines.append(f"  Markup hits: {', '.join(markup_hits)}")
        lines.append("")

    if show_noise:
        noise_items = [
            item
            for item in findings
            if item["bucket"] in {"noise-generated", "noise-serializer"}
        ]
        if noise_items:
            lines.append("## Suppressed Noise")
            lines.append("")
            for item in noise_items:
                path = relative_path(str(item["file_path"]), workspace_root)
                lines.append(
                    f"- `{item['bucket']}` `{item['rule_id']}` `{path}:{item['line']}` - {item['message']}"
                )
            lines.append("")

    return "\n".join(lines).rstrip() + "\n"


def json_report(
    sarif_path: Path,
    workspace_root: Path,
    findings: List[Dict[str, object]],
) -> str:
    counts = Counter(str(item["bucket"]) for item in findings)
    payload = {
        "sarif": str(sarif_path),
        "workspace_root": str(workspace_root),
        "counts": dict(counts),
        "findings": findings,
    }
    return json.dumps(payload, indent=2)


def main() -> int:
    args = parse_args()
    sarif_path = Path(args.sarif).resolve()
    workspace_root = Path(args.workspace_root).resolve()
    findings = load_findings(sarif_path, workspace_root)

    if args.format == "json":
        output = json_report(sarif_path, workspace_root, findings)
    else:
        output = markdown_report(sarif_path, workspace_root, findings, args.show_noise)

    if args.output:
        output_path = Path(args.output)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(output, encoding="utf-8")
    else:
        print(output, end="")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
