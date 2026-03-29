#!/usr/bin/env python3
import argparse
import html
import json
import re
import shutil
import subprocess
from pathlib import Path
from typing import Dict, List, Optional
import xml.etree.ElementTree as ET


SAFE_TASKS: Dict[str, str] = {
    "CSOptimizeUsings": "Optimize using directives",
    "CSShortenReferences": "Shorten qualified references",
    "Xaml.RemoveRedundantNamespaceAlias": "Remove redundant XAML namespace aliases",
}

DEFAULT_PROFILE_NAME = "Safe Cleanup"
XML_DECLARATION_PATTERN = re.compile(r"^\s*<\?xml[^>]*\?>\s*")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Run jb cleanupcode with the repository's narrow safe-cleanup profile."
    )
    parser.add_argument(
        "--solution",
        help="Solution path. If omitted, auto-detect a single .sln under the workspace root.",
    )
    parser.add_argument(
        "--workspace-root",
        default=".",
        help="Workspace root used for solution discovery and output paths.",
    )
    parser.add_argument(
        "--settings",
        help="Path to the solution-shared DotSettings file. Defaults to <solution>.sln.DotSettings.",
    )
    parser.add_argument(
        "--profile",
        help=f"Cleanup profile name. Defaults to '{DEFAULT_PROFILE_NAME}' when present in settings.",
    )
    parser.add_argument(
        "--report-dir",
        default=".artifacts/resharper-safe-cleanup",
        help="Directory for cleanup summaries and command logs.",
    )
    parser.add_argument(
        "--include",
        action="append",
        default=[],
        help="Optional Ant-style include mask. May be repeated.",
    )
    parser.add_argument(
        "--exclude",
        action="append",
        default=[],
        help="Optional Ant-style exclude mask. May be repeated.",
    )
    parser.add_argument(
        "--verbosity",
        default="WARN",
        help="cleanupcode verbosity. Default: WARN.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Resolve inputs and write reports without running cleanupcode.",
    )
    return parser.parse_args()


def find_solution(workspace_root: Path) -> Path:
    solutions = sorted(workspace_root.rglob("*.sln"))
    if len(solutions) == 1:
        return solutions[0]
    if not solutions:
        raise RuntimeError("No .sln file found. Pass --solution explicitly.")
    formatted = "\n".join(f"- {path}" for path in solutions)
    raise RuntimeError(f"Multiple .sln files found. Pass --solution explicitly:\n{formatted}")


def detect_dotnet() -> Path:
    dotnet = shutil.which("dotnet")
    if not dotnet:
        raise RuntimeError("dotnet was not found on PATH.")
    return Path(dotnet)


def load_global_json_sdk(workspace_root: Path) -> Optional[str]:
    global_json = workspace_root / "global.json"
    if not global_json.exists():
        return None

    data = json.loads(global_json.read_text(encoding="utf-8"))
    sdk = data.get("sdk") or {}
    version = sdk.get("version")
    return version if isinstance(version, str) and version else None


def list_sdks(dotnet_path: Path) -> List[str]:
    result = subprocess.run(
        [str(dotnet_path), "--list-sdks"],
        check=True,
        capture_output=True,
        text=True,
    )
    versions: List[str] = []
    for line in result.stdout.splitlines():
        version = line.split(" ", 1)[0].strip()
        if version:
            versions.append(version)
    return versions


def choose_sdk(dotnet_path: Path, workspace_root: Path) -> str:
    preferred = load_global_json_sdk(workspace_root)
    versions = list_sdks(dotnet_path)
    if preferred and preferred in versions:
        return preferred
    if not versions:
        raise RuntimeError("No installed dotnet SDKs were found.")
    return versions[-1]


def detect_jb() -> str:
    jb = shutil.which("jb")
    if not jb:
        raise RuntimeError(
            "jb was not found on PATH. Stop and ask the user to install JetBrains ReSharper "
            "Command Line Tools. Suggested commands: 'dotnet tool install -g "
            "JetBrains.ReSharper.GlobalTools' or repo-local install via 'dotnet new "
            "tool-manifest' and 'dotnet tool install JetBrains.ReSharper.GlobalTools'. "
            "Docs: https://www.jetbrains.com/help/resharper/ReSharper_Command_Line_Tools.html"
        )
    return jb


def resolve_settings_path(solution_path: Path, provided_path: Optional[str]) -> Path:
    if provided_path:
        settings_path = Path(provided_path).resolve()
    else:
        settings_path = solution_path.with_suffix(solution_path.suffix + ".DotSettings")

    if not settings_path.exists():
        raise RuntimeError(
            "DotSettings file was not found. Create a solution-shared cleanup profile in Rider "
            f"and commit it next to the solution: {settings_path}"
        )

    return settings_path


def parse_cleanup_profiles(settings_path: Path) -> Dict[str, List[str]]:
    try:
        root = ET.parse(settings_path).getroot()
    except ET.ParseError as ex:
        raise RuntimeError(f"Failed to parse DotSettings file {settings_path}: {ex}") from ex

    profiles: Dict[str, List[str]] = {}
    xaml_key = "{http://schemas.microsoft.com/winfx/2006/xaml}Key"

    for element in root.iter():
        key = element.attrib.get(xaml_key)
        if not key or "/Default/CodeStyle/CodeCleanup/Profiles/=" not in key:
            continue

        raw_profile_xml = (element.text or "").strip()
        if not raw_profile_xml:
            continue

        decoded_profile_xml = html.unescape(raw_profile_xml)
        decoded_profile_xml = XML_DECLARATION_PATTERN.sub("", decoded_profile_xml, count=1)
        try:
            profile_root = ET.fromstring(decoded_profile_xml)
        except ET.ParseError as ex:
            raise RuntimeError(
                f"Failed to parse cleanup profile XML from {settings_path}: {ex}"
            ) from ex

        if profile_root.tag != "Profile":
            continue

        name = profile_root.attrib.get("name")
        if not name:
            continue

        enabled_tasks: List[str] = []
        for child in profile_root:
            if child.tag in {"IDEA_SETTINGS", "RIDER_SETTINGS"}:
                continue
            if task_is_enabled(child):
                enabled_tasks.append(child.tag)

        profiles[name] = sorted(enabled_tasks)

    return profiles


def task_is_enabled(node: ET.Element) -> bool:
    texts = [text.strip() for text in node.itertext() if text and text.strip()]
    return any(text.lower() == "true" for text in texts)


def resolve_profile_name(requested_name: Optional[str], profiles: Dict[str, List[str]]) -> str:
    if not profiles:
        raise RuntimeError("No cleanup profiles were found in the supplied DotSettings file.")

    if requested_name:
        if requested_name not in profiles:
            raise RuntimeError(
                f"Cleanup profile '{requested_name}' was not found. Available profiles: "
                + ", ".join(sorted(profiles))
            )
        return requested_name

    if DEFAULT_PROFILE_NAME in profiles:
        return DEFAULT_PROFILE_NAME

    raise RuntimeError(
        f"Profile was not specified and '{DEFAULT_PROFILE_NAME}' was not found. "
        "Available profiles: " + ", ".join(sorted(profiles))
    )


def validate_profile(profile_name: str, enabled_tasks: List[str]) -> None:
    unexpected_tasks = sorted(task for task in enabled_tasks if task not in SAFE_TASKS)
    if unexpected_tasks:
        formatted = ", ".join(unexpected_tasks)
        raise RuntimeError(
            f"Cleanup profile '{profile_name}' is broader than this skill allows. "
            f"Unexpected enabled tasks: {formatted}"
        )

    if not enabled_tasks:
        raise RuntimeError(f"Cleanup profile '{profile_name}' does not enable any safe cleanup tasks.")


def build_cleanup_command(
    jb: str,
    dotnet_path: Path,
    msbuild_path: Path,
    solution_path: Path,
    settings_path: Path,
    profile_name: str,
    includes: List[str],
    excludes: List[str],
    verbosity: str,
) -> List[str]:
    command = [
        jb,
        "cleanupcode",
        f"--settings={settings_path}",
        f"--profile={profile_name}",
        f"--dotnetcore={dotnet_path}",
        f"--toolset-path={msbuild_path}",
        f"--verbosity={verbosity}",
        "--no-updates",
    ]

    for include in includes:
        command.append(f"--include={include}")
    for exclude in excludes:
        command.append(f"--exclude={exclude}")

    command.append(str(solution_path))
    return command


def run_command(command: List[str], cwd: Path) -> None:
    result = subprocess.run(command, cwd=str(cwd))
    if result.returncode != 0:
        raise RuntimeError(f"Command failed with exit code {result.returncode}: {' '.join(command)}")


def try_git_diff_stat(workspace_root: Path) -> Optional[str]:
    git = shutil.which("git")
    if not git:
        return None

    result = subprocess.run(
        [git, "diff", "--stat", "--relative"],
        cwd=str(workspace_root),
        capture_output=True,
        text=True,
    )
    if result.returncode != 0:
        return None

    diff_stat = result.stdout.strip()
    return diff_stat or None


def write_report(
    report_dir: Path,
    solution_path: Path,
    settings_path: Path,
    profile_name: str,
    enabled_tasks: List[str],
    command: List[str],
    include_masks: List[str],
    exclude_masks: List[str],
    dry_run: bool,
    diff_stat: Optional[str],
) -> None:
    report_dir.mkdir(parents=True, exist_ok=True)

    command_text = subprocess.list2cmdline(command)
    command_path = report_dir / "cleanup-command.txt"
    command_path.write_text(command_text + "\n", encoding="utf-8")

    summary = {
        "solution": str(solution_path),
        "settings": str(settings_path),
        "profile": profile_name,
        "enabled_tasks": enabled_tasks,
        "include_masks": include_masks,
        "exclude_masks": exclude_masks,
        "dry_run": dry_run,
        "command_file": str(command_path),
        "diff_stat": diff_stat,
    }

    summary_json_path = report_dir / "cleanup-summary.json"
    summary_json_path.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")

    lines = [
        "# ReSharper Safe Cleanup",
        "",
        f"- solution: `{solution_path}`",
        f"- settings: `{settings_path}`",
        f"- profile: `{profile_name}`",
        "- enabled tasks:",
    ]
    lines.extend(f"  - `{task}`: {SAFE_TASKS.get(task, 'unknown')}" for task in enabled_tasks)
    lines.append(f"- mode: `{'dry-run' if dry_run else 'applied'}`")
    lines.append(f"- command: `{command_text}`")

    if include_masks:
        lines.append(f"- include masks: `{'; '.join(include_masks)}`")
    if exclude_masks:
        lines.append(f"- exclude masks: `{'; '.join(exclude_masks)}`")

    lines.extend(
        [
            "",
            "Review `git diff` and run the repository verification flow before committing.",
        ]
    )

    if diff_stat:
        lines.extend(
            [
                "",
                "## Current Diff Stat",
                "",
                "```text",
                diff_stat,
                "```",
            ]
        )

    summary_md_path = report_dir / "cleanup-summary.md"
    summary_md_path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    args = parse_args()
    workspace_root = Path(args.workspace_root).resolve()
    solution_path = Path(args.solution).resolve() if args.solution else find_solution(workspace_root)
    settings_path = resolve_settings_path(solution_path, args.settings)
    profiles = parse_cleanup_profiles(settings_path)
    profile_name = resolve_profile_name(args.profile, profiles)
    enabled_tasks = profiles[profile_name]
    validate_profile(profile_name, enabled_tasks)

    dotnet_path = detect_dotnet()
    sdk_version = choose_sdk(dotnet_path, workspace_root)
    msbuild_path = dotnet_path.parent / "sdk" / sdk_version / "MSBuild.dll"
    if not msbuild_path.exists():
        raise RuntimeError(f"MSBuild.dll was not found for SDK {sdk_version}: {msbuild_path}")

    jb = detect_jb()
    report_dir = (workspace_root / args.report_dir).resolve()

    cleanup_command = build_cleanup_command(
        jb=jb,
        dotnet_path=dotnet_path,
        msbuild_path=msbuild_path,
        solution_path=solution_path,
        settings_path=settings_path,
        profile_name=profile_name,
        includes=args.include,
        excludes=args.exclude,
        verbosity=args.verbosity,
    )

    print(f"[safe-cleanup] solution: {solution_path}")
    print(f"[safe-cleanup] settings: {settings_path}")
    print(f"[safe-cleanup] profile:  {profile_name}")
    print(f"[safe-cleanup] tasks:    {', '.join(enabled_tasks)}")
    print(f"[safe-cleanup] command:  {subprocess.list2cmdline(cleanup_command)}")

    if not args.dry_run:
        run_command(cleanup_command, workspace_root)

    diff_stat = try_git_diff_stat(workspace_root)
    write_report(
        report_dir=report_dir,
        solution_path=solution_path,
        settings_path=settings_path,
        profile_name=profile_name,
        enabled_tasks=enabled_tasks,
        command=cleanup_command,
        include_masks=args.include,
        exclude_masks=args.exclude,
        dry_run=args.dry_run,
        diff_stat=diff_stat,
    )

    print(f"[safe-cleanup] summary:  {report_dir / 'cleanup-summary.md'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
