#!/usr/bin/env python3
"""Run the v0.0-pre GRIN Observe play-mode verification harness."""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import subprocess
import sys
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
OUTPUT_DIR = ROOT / "output" / "v0.0-pre"
REPORT_PATH = OUTPUT_DIR / "GRIN_OBSERVE_PLAYMODE_VERIFY.md"

CASES = [
    {
        "role": "straight_control",
        "scene": "res://test-straight-basic-visual-offaxis-observe.tscn",
        "json": "playmode_verify_straight_control.json",
    },
    {
        "role": "curved_grin",
        "scene": "res://test-grin-basic-visual-offaxis-observe.tscn",
        "json": "playmode_verify_curved_grin.json",
    },
]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--godot", default=str(ROOT / "scripts" / "godot_local.sh"))
    parser.add_argument("--output-dir", default=str(OUTPUT_DIR.relative_to(ROOT)))
    parser.add_argument("--frames", type=int, default=20)
    parser.add_argument("--timeout", type=int, default=180)
    parser.add_argument(
        "--headless",
		action="store_true",
        help="Run headless scene playback. Visible playback is the default because it verifies the render-mode path used by Godot Play.",
    )
    parser.add_argument(
        "--stop-on-first-fail",
        action="store_true",
        help="Stop after the first failing Godot scene. By default the report includes both canonical scenes.",
    )
    return parser.parse_args()


def run_case(args: argparse.Namespace, case: dict[str, str]) -> dict[str, Any]:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    log_path = OUTPUT_DIR / f"playmode_verify_{case['role']}.log"
    json_path = OUTPUT_DIR / case["json"]
    if json_path.exists():
        json_path.unlink()
    for artifact_name in artifact_names_for_role(case["role"]):
        artifact_path = OUTPUT_DIR / artifact_name
        if artifact_path.exists():
            artifact_path.unlink()

    cmd = [
        args.godot,
        "--path",
        str(ROOT),
        "--scene",
        case["scene"],
        "--",
        "--grin-observe-playmode-verify=1",
        f"--grin-observe-playmode-role={case['role']}",
        f"--grin-observe-playmode-output={args.output_dir}",
        f"--grin-observe-playmode-frames={args.frames}",
    ]
    if args.headless:
        cmd.insert(1, "--headless")

    started = dt.datetime.now(dt.timezone.utc)
    result: dict[str, Any] = {
        "role": case["role"],
        "scene": case["scene"],
        "command": " ".join(cmd),
        "log_path": rel(log_path),
        "json_path": rel(json_path),
        "returncode": None,
        "runner_status": "FAIL",
        "runner_detail": "",
        "data": None,
    }

    with log_path.open("w", encoding="utf-8") as log:
        log.write(f"[playmode-verify] started={started.isoformat()}\n")
        log.write(f"[playmode-verify] command={' '.join(cmd)}\n")
        log.flush()
        try:
            completed = subprocess.run(
                cmd,
                cwd=ROOT,
                stdout=log,
                stderr=subprocess.STDOUT,
                timeout=args.timeout,
                check=False,
            )
            result["returncode"] = completed.returncode
        except subprocess.TimeoutExpired:
            result["runner_detail"] = f"Godot timed out after {args.timeout}s"
            result["returncode"] = "timeout"

    if json_path.exists():
        try:
            data = json.loads(json_path.read_text(encoding="utf-8"))
            result["data"] = data
            summary = data.get("summary_status", "FAIL")
            result["runner_status"] = "PASS" if summary == "PASS" and result["returncode"] == 0 else "FAIL"
            result["runner_detail"] = f"Godot return={result['returncode']}; verifier summary={summary}"
        except json.JSONDecodeError as exc:
            result["runner_detail"] = f"invalid verifier JSON: {exc}"
    elif not result["runner_detail"]:
        result["runner_detail"] = f"verifier JSON was not produced: {rel(json_path)}"

    return result


def write_report(results: list[dict[str, Any]]) -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    now = dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    overall = "PASS" if all(result["runner_status"] == "PASS" for result in results) else "FAIL"
    keymap = first_keymap(results)

    lines: list[str] = [
        "# GRIN Observe Play-Mode Verify",
        "",
        f"- Timestamp UTC: `{now}`",
        f"- Summary: `{overall}`",
        "- Demo spine: `GRIN Basic Visual Off-Axis Observe`",
        "- Primary scenes:",
        "  - `test-straight-basic-visual-offaxis-observe.tscn`",
        "  - `test-grin-basic-visual-offaxis-observe.tscn`",
        "- Scope: v0.0-pre coherent transport instrumentation, not exotic physics proof",
        "",
        "## Active Keymap",
        "",
        keymap or "_Keymap unavailable because the HUD verifier did not produce one._",
        "",
        "## Scene Results",
        "",
        "| Role | Scene | Runner | Return | Detail | Log | JSON |",
        "| --- | --- | --- | --- | --- | --- | --- |",
    ]

    for result in results:
        lines.append(
            "| {role} | `{scene}` | `{status}` | `{returncode}` | {detail} | `{log}` | `{json}` |".format(
                role=result["role"],
                scene=result["scene"],
                status=result["runner_status"],
                returncode=result["returncode"],
                detail=escape_md(result["runner_detail"]),
                log=result["log_path"],
                json=result["json_path"],
            )
        )

    lines.extend(
        [
            "",
            "## Control Checks",
            "",
            "| Role | Area | Check | Status | Detail |",
            "| --- | --- | --- | --- | --- |",
        ]
    )
    for result in results:
        data = result.get("data") or {}
        for check in data.get("checks", []):
            lines.append(
                "| {role} | {area} | {name} | `{status}` | {detail} |".format(
                    role=result["role"],
                    area=escape_md(check.get("area", "")),
                    name=escape_md(check.get("name", "")),
                    status=check.get("status", "FAIL"),
                    detail=escape_md(check.get("detail", "")),
                )
            )
        if not data.get("checks"):
            lines.append(f"| {result['role']} | runner | verifier JSON | `FAIL` | no check data produced |")

    lines.extend(
        [
            "",
            "## Pixel Coverage",
            "",
            "| Role | Artifact | Width | Height | Total Pixels | Non-background | Traced/Marked | Coverage | SHA-256 | Status |",
            "| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |",
        ]
    )
    for result in results:
        data = result.get("data") or {}
        for artifact in data.get("artifacts", []):
            lines.append(
                "| {role} | `{path}` | {width} | {height} | {total} | {non_bg} | {traced} | {coverage:.6f}% | `{sha}` | `{status}` |".format(
                    role=result["role"],
                    path=artifact.get("path", ""),
                    width=int(artifact.get("width", 0)),
                    height=int(artifact.get("height", 0)),
                    total=int(artifact.get("total_pixels", 0)),
                    non_bg=int(artifact.get("non_background_pixels", 0)),
                    traced=int(artifact.get("traced_pixels", -1)),
                    coverage=float(artifact.get("coverage_percent", 0.0)),
                    sha=artifact.get("sha256", ""),
                    status=artifact.get("status", "FAIL"),
                )
            )
        if not data.get("artifacts"):
            lines.append(f"| {result['role']} | _missing_ | 0 | 0 | 0 | 0 | -1 | 0.000000% | `` | `FAIL` |")

    lines.extend(
        [
            "",
            "## Acceptance Summary",
            "",
            f"- Both canonical scenes load cleanly: `{status_for(results, 'canonical scene loaded')}`",
            f"- HUD is present and active: `{status_for(results, 'GrinObserveDemoHud active')}`",
            f"- F1-F12 cockpit controls are visible and verified: `{controls_status(results)}`",
            f"- Straight vs curved scene-switch comparison path is reachable: `{status_for(results, 'F2')}`",
            f"- Diagnostics/export path is shown and exercised: `{status_for(results, 'F10')}`",
            f"- Full-pixel artifact pass completed: `{artifact_status(results)}`",
            "- Wormhole/overspace scenes used as primary evidence: `NO`",
            "- Exotic physics claim made: `NO`",
            "",
            "## Known Limitations",
            "",
            "- Automated F2 verification checks matched-scene reachability; in normal Godot Play mode F2 performs the scene switch.",
            "- Pixel coverage scans every exported viewport pixel and records renderer traced-pixel stats when available; it is not an advanced physics validation.",
            "- Movement controls remain Godot/player camera navigation controls. v0.0-pre intentionally does not expose object manipulation.",
            "",
        ]
    )

    REPORT_PATH.write_text("\n".join(lines), encoding="utf-8")


def first_keymap(results: list[dict[str, Any]]) -> str:
    for result in results:
        data = result.get("data") or {}
        keymap = data.get("keymap_markdown")
        if keymap:
            return keymap
    return ""


def artifact_names_for_role(role: str) -> list[str]:
    if role == "straight_control":
        return ["straight_control_verify.png"]
    if role == "curved_grin":
        return ["curved_grin_verify.png", "curved_grin_final_smoke.png"]
    return []


def status_for(results: list[dict[str, Any]], check_name: str) -> str:
    statuses: list[str] = []
    for result in results:
        data = result.get("data") or {}
        for check in data.get("checks", []):
            if check.get("name") == check_name:
                statuses.append(check.get("status", "FAIL"))
    if not statuses:
        return "FAIL"
    if any(status in {"FAIL", "BLOCKED BY KEY CONFLICT"} for status in statuses):
        return "FAIL"
    if any(status == "NOT IMPLEMENTED" for status in statuses):
        return "NOT IMPLEMENTED"
    return "PASS"


def controls_status(results: list[dict[str, Any]]) -> str:
    expected = {f"F{i}" for i in range(1, 13)}
    seen: dict[str, list[str]] = {key: [] for key in expected}
    for result in results:
        data = result.get("data") or {}
        for check in data.get("checks", []):
            name = check.get("name")
            if name in seen:
                seen[name].append(check.get("status", "FAIL"))
    for key in expected:
        if not seen[key]:
            return "FAIL"
        if any(status in {"FAIL", "BLOCKED BY KEY CONFLICT", "NOT IMPLEMENTED"} for status in seen[key]):
            return "FAIL"
    return "PASS"


def artifact_status(results: list[dict[str, Any]]) -> str:
    statuses: list[str] = []
    for result in results:
        data = result.get("data") or {}
        statuses.extend(artifact.get("status", "FAIL") for artifact in data.get("artifacts", []))
    if not statuses:
        return "FAIL"
    return "FAIL" if any(status != "PASS" for status in statuses) else "PASS"


def escape_md(value: Any) -> str:
    return str(value).replace("|", "\\|").replace("\n", " ")


def rel(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return path.as_posix()


def main() -> int:
    args = parse_args()
    results: list[dict[str, Any]] = []
    for case in CASES:
        result = run_case(args, case)
        results.append(result)
        if result["runner_status"] != "PASS" and args.stop_on_first_fail:
            break
    write_report(results)
    print(f"[playmode-verify] report: {rel(REPORT_PATH)}")
    return 0 if all(result["runner_status"] == "PASS" for result in results) and len(results) == len(CASES) else 1


if __name__ == "__main__":
    sys.exit(main())
