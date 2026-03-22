#!/usr/bin/env python3
import argparse
import json
import shutil
import subprocess
import sys
from pathlib import Path
from typing import List, Optional


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Run jb inspectcode for unused-code analysis and emit filtered summaries."
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
        "--report-dir",
        default=".artifacts/resharper-unused",
        help="Directory for SARIF and filtered reports.",
    )
    parser.add_argument(
        "--severity",
        default="SUGGESTION",
        help="Minimal ReSharper severity to report. Default: SUGGESTION.",
    )
    parser.add_argument(
        "--no-swea",
        action="store_true",
        help="Disable solution-wide analysis.",
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


def load_global_json_sdk(workspace_root: Path) -> Optional[str]:
    global_json = workspace_root / "global.json"
    if not global_json.exists():
        return None
    data = json.loads(global_json.read_text(encoding="utf-8"))
    sdk = data.get("sdk") or {}
    version = sdk.get("version")
    return version if isinstance(version, str) and version else None


def detect_dotnet() -> Path:
    dotnet = shutil.which("dotnet")
    if not dotnet:
        raise RuntimeError("dotnet was not found on PATH.")
    return Path(dotnet)


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


def filter_script_path() -> Path:
    return Path(__file__).resolve().with_name("filter_unused_sarif.py")


def run_command(command: List[str], cwd: Path) -> None:
    result = subprocess.run(command, cwd=str(cwd))
    if result.returncode != 0:
        raise RuntimeError(f"Command failed with exit code {result.returncode}: {' '.join(command)}")


def main() -> int:
    args = parse_args()
    workspace_root = Path(args.workspace_root).resolve()
    solution_path = Path(args.solution).resolve() if args.solution else find_solution(workspace_root)
    report_dir = (workspace_root / args.report_dir).resolve()
    report_dir.mkdir(parents=True, exist_ok=True)

    dotnet_path = detect_dotnet()
    sdk_version = choose_sdk(dotnet_path, workspace_root)
    msbuild_path = dotnet_path.parent / "sdk" / sdk_version / "MSBuild.dll"
    if not msbuild_path.exists():
        raise RuntimeError(f"MSBuild.dll was not found for SDK {sdk_version}: {msbuild_path}")

    jb = shutil.which("jb")
    if not jb:
        raise RuntimeError(
            "jb was not found on PATH. Stop and ask the user to install JetBrains ReSharper "
            "Command Line Tools. Suggested commands: 'dotnet tool install -g "
            "JetBrains.ReSharper.GlobalTools' or repo-local install via 'dotnet new "
            "tool-manifest' and 'dotnet tool install JetBrains.ReSharper.GlobalTools'. "
            "Docs: https://www.jetbrains.com/help/resharper/ReSharper_Command_Line_Tools.html"
        )

    raw_sarif = report_dir / "inspectcode-unused.sarif"
    summary_md = report_dir / "unused-summary.md"
    summary_json = report_dir / "unused-summary.json"

    inspectcode_command = [
        jb,
        "inspectcode",
        "--absolute-paths",
        f"--severity={args.severity}",
        "--format=Sarif",
        f"--dotnetcore={dotnet_path}",
        f"--toolset-path={msbuild_path}",
        f"--output={raw_sarif}",
    ]
    if args.no_swea:
        inspectcode_command.append("--no-swea")
    else:
        inspectcode_command.append("--swea")
    inspectcode_command.append(str(solution_path))

    print(f"[unused-scan] solution: {solution_path}")
    print(f"[unused-scan] dotnet:   {dotnet_path}")
    print(f"[unused-scan] sdk:      {sdk_version}")
    print(f"[unused-scan] sarif:    {raw_sarif}")
    run_command(inspectcode_command, workspace_root)

    filter_script = filter_script_path()
    common_filter = [
        sys.executable,
        str(filter_script),
        str(raw_sarif),
        "--workspace-root",
        str(workspace_root),
    ]
    run_command(common_filter + ["--format", "markdown", "--output", str(summary_md)], workspace_root)
    run_command(common_filter + ["--format", "json", "--output", str(summary_json)], workspace_root)

    print(f"[unused-scan] summary:  {summary_md}")
    print(f"[unused-scan] json:     {summary_json}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
