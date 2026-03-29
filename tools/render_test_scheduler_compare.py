#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
OUTPUT_ROOT = ROOT / "output" / "render_test_scheduler_compare"
DEFAULT_SUBTILE_WIDTH = 8


@dataclass(frozen=True)
class Case:
    fixture: str
    mode: str
    scene: str
    extra_args: tuple[str, ...]


CASES: tuple[Case, ...] = (
    Case(
        fixture="curved_minimal",
        mode="baseline",
        scene="res://test-curved-minimal.tscn",
        extra_args=(
            "--tile-metrics=1",
            f"--tile-metrics-subtile-width={DEFAULT_SUBTILE_WIDTH}",
            "--tile-metrics-simulate-reorder=1",
        ),
    ),
    Case(
        fixture="curved_minimal",
        mode="reorder-only",
        scene="res://test-curved-minimal.tscn",
        extra_args=(
            "--tile-metrics=1",
            f"--tile-metrics-subtile-width={DEFAULT_SUBTILE_WIDTH}",
            "--experimental-subtile-scheduler=1",
        ),
    ),
    Case(
        fixture="curved_minimal",
        mode="reorder-only-persistent-priors",
        scene="res://test-curved-minimal.tscn",
        extra_args=(
            "--tile-metrics=1",
            f"--tile-metrics-subtile-width={DEFAULT_SUBTILE_WIDTH}",
            "--experimental-subtile-scheduler=1",
            "--tile-metrics-persistent-priors=1",
        ),
    ),
    Case(
        fixture="curved_minimal_backdrop",
        mode="baseline",
        scene="res://test-curved-minimal-backdrop.tscn",
        extra_args=(
            "--tile-metrics=1",
            f"--tile-metrics-subtile-width={DEFAULT_SUBTILE_WIDTH}",
            "--tile-metrics-simulate-reorder=1",
        ),
    ),
    Case(
        fixture="curved_minimal_backdrop",
        mode="reorder-only",
        scene="res://test-curved-minimal-backdrop.tscn",
        extra_args=(
            "--tile-metrics=1",
            f"--tile-metrics-subtile-width={DEFAULT_SUBTILE_WIDTH}",
            "--experimental-subtile-scheduler=1",
        ),
    ),
    Case(
        fixture="curved_minimal_backdrop",
        mode="reorder-only-persistent-priors",
        scene="res://test-curved-minimal-backdrop.tscn",
        extra_args=(
            "--tile-metrics=1",
            f"--tile-metrics-subtile-width={DEFAULT_SUBTILE_WIDTH}",
            "--experimental-subtile-scheduler=1",
            "--tile-metrics-persistent-priors=1",
        ),
    ),
)


SUMMARY_PREFIXES = ("[TileMetrics][SimSummary]", "[TileMetrics][ExecSummary]")
TOKEN_RE = re.compile(r"([A-Za-z0-9_]+)=([^ ]+)")


def parse_tokens(line: str) -> dict[str, str]:
    return {match.group(1): match.group(2) for match in TOKEN_RE.finditer(line)}


def summarize_scheduler_log(log_path: Path, mode: str) -> dict:
    cold_summary: dict[str, str] | None = None
    warm_summary: dict[str, str] | None = None
    last_summary: dict[str, str] | None = None

    if not log_path.exists():
        return {}

    with log_path.open("r", encoding="utf-8", errors="replace") as handle:
        for raw_line in handle:
            line = raw_line.strip()
            if not line.startswith(SUMMARY_PREFIXES):
                continue
            data = parse_tokens(line)
            last_summary = data
            if line.startswith("[TileMetrics][SimSummary]"):
                warm_summary = data
                continue

            frame_phase = data.get("framePhase", "")
            if frame_phase.startswith("cold_start") and cold_summary is None:
                cold_summary = data
            if data.get("rankActive") == "1":
                warm_summary = data

    return {
        "mode": mode,
        "coldSummary": cold_summary,
        "warmSummary": warm_summary,
        "lastSummary": last_summary,
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Run curved render-test scheduler comparisons and restore per-run PNG artifacts "
            "alongside logs and summary files."
        )
    )
    parser.add_argument(
        "--godot-exe",
        default=os.environ.get("GODOT_EXE", ""),
        help="Path to the Godot executable. Falls back to godot4/godot from PATH.",
    )
    parser.add_argument(
        "--output-root",
        type=Path,
        default=OUTPUT_ROOT,
        help="Root directory for timestamped comparison output folders.",
    )
    parser.add_argument(
        "--project-path",
        default=".",
        help=(
            "Godot project path passed to --path. "
            "Use the synced Windows worktree when driving a Windows Godot binary from WSL."
        ),
    )
    parser.add_argument(
        "--timestamp",
        default=datetime.now().strftime("%Y-%m-%dT%H-%M-%S"),
        help="Timestamp token for the output folder name.",
    )
    return parser.parse_args()


def resolve_godot_exe(raw: str) -> str:
    if raw:
        return raw
    for candidate in ("godot4", "godot"):
        found = shutil.which(candidate)
        if found:
            return found
    raise FileNotFoundError(
        "No Godot executable found. Pass --godot-exe or set GODOT_EXE."
    )


def safe_token(value: str) -> str:
    chars = []
    for ch in value.strip().lower():
        if ch.isalnum() or ch in ("-", "_", "."):
            chars.append(ch)
        else:
            chars.append("-")
    token = "".join(chars).strip("-")
    while "--" in token:
        token = token.replace("--", "-")
    return token or "na"


def run_case(godot_exe: str, project_path: str, run_root: Path, case: Case) -> dict:
    logs_dir = run_root / "logs"
    images_dir = run_root / "images"
    logs_dir.mkdir(parents=True, exist_ok=True)
    images_dir.mkdir(parents=True, exist_ok=True)

    case_token = f"{safe_token(case.fixture)}__{safe_token(case.mode)}"
    log_path = logs_dir / f"{case_token}.log"

    cmd = [
        godot_exe,
        "--path",
        project_path,
        "--scene",
        case.scene,
        "--",
        "--render-test",
        f"--render-test-fixture={case.fixture}",
        "--lifecycle-stress=0",
        "--smartscale=0",
        "--render-test-capture=1",
        f"--render-test-capture-dir={images_dir}",
        f"--render-test-capture-mode={case.mode}",
        *case.extra_args,
    ]

    with log_path.open("w", encoding="utf-8") as log_handle:
        run_proc = subprocess.run(
            cmd,
            cwd=ROOT,
            stdout=log_handle,
            stderr=subprocess.STDOUT,
            text=True,
            check=False,
        )

    regress_proc = subprocess.run(
        [sys.executable, str(ROOT / "tools" / "renderhealth_regress.py"), str(log_path)],
        cwd=ROOT,
        capture_output=True,
        text=True,
        check=False,
    )

    image_matches = sorted(images_dir.glob(f"{safe_token(case.fixture)}__{safe_token(case.mode)}__*.png"))
    scheduler_summary = summarize_scheduler_log(log_path, case.mode)
    return {
        "fixture": case.fixture,
        "mode": case.mode,
        "scene": case.scene,
        "logPath": str(log_path),
        "capturePaths": [str(path) for path in image_matches],
        "godotExitCode": run_proc.returncode,
        "regressExitCode": regress_proc.returncode,
        "regressStdout": regress_proc.stdout.strip(),
        "regressStderr": regress_proc.stderr.strip(),
        "command": cmd,
        "schedulerSummary": scheduler_summary,
    }


def compare_pairs(results: list[dict], run_root: Path) -> list[dict]:
    try:
        from image_compare import compare_metrics
    except ModuleNotFoundError as exc:
        fixtures = sorted({result["fixture"] for result in results})
        return [
            {
                "fixture": fixture,
                "status": "compare_unavailable",
                "reason": f"missing_python_dependency:{exc.name}",
            }
            for fixture in fixtures
        ]

    pairs = []
    comparisons_dir = run_root / "comparisons"
    comparisons_dir.mkdir(parents=True, exist_ok=True)
    by_fixture: dict[str, dict[str, dict]] = {}
    for result in results:
        by_fixture.setdefault(result["fixture"], {})[result["mode"]] = result

    for fixture, fixture_results in sorted(by_fixture.items()):
        baseline = fixture_results.get("baseline")
        reorder = fixture_results.get("reorder-only")
        priors = fixture_results.get("reorder-only-persistent-priors")
        if not baseline or not reorder:
            continue
        comparison_specs = [
            ("baseline", baseline, "reorder-only", reorder),
        ]
        if priors:
            comparison_specs.extend(
                [
                    ("baseline", baseline, "reorder-only-persistent-priors", priors),
                    ("reorder-only", reorder, "reorder-only-persistent-priors", priors),
                ]
            )

        for left_mode, left_result, right_mode, right_result in comparison_specs:
            left_paths = left_result.get("capturePaths") or []
            right_paths = right_result.get("capturePaths") or []
            if not left_paths or not right_paths:
                pairs.append(
                    {
                        "fixture": fixture,
                        "leftMode": left_mode,
                        "rightMode": right_mode,
                        "status": "missing_capture",
                        "leftPath": left_paths[0] if left_paths else None,
                        "rightPath": right_paths[0] if right_paths else None,
                    }
                )
                continue

            left_path = Path(left_paths[0])
            right_path = Path(right_paths[0])
            ssim_score, mad_score = compare_metrics(str(left_path), str(right_path))
            pair = {
                "fixture": fixture,
                "leftMode": left_mode,
                "rightMode": right_mode,
                "status": "ok",
                "leftPath": str(left_path),
                "rightPath": str(right_path),
                "ssim": ssim_score,
                "meanAbsDiff": mad_score,
            }
            with (
                comparisons_dir
                / f"{safe_token(fixture)}__{safe_token(left_mode)}_vs_{safe_token(right_mode)}.json"
            ).open("w", encoding="utf-8") as handle:
                json.dump(pair, handle, indent=2)
                handle.write("\n")
            pairs.append(pair)
    return pairs


def build_scheduler_metric_line(label: str, summary: dict[str, str] | None) -> str:
    if not summary:
        return f"- {label}: scheduler_summary=missing"

    metric_keys = (
        "framePhase",
        "totalHits",
        "segmentsTraced",
        "hitsPerSegmentTraced",
        "actualAvgFirstHitOrdinal",
        "actualAvgHit50Ordinal",
        "avgFirstHitOrdinal",
        "avgHit50Ordinal",
        "actualTop1Share",
        "actualTop2Share",
        "actualTop3Share",
        "execTop1Share",
        "execTop2Share",
        "execTop3Share",
        "priorBandsWithHits",
        "priorOnlyBandsWithHits",
        "priorContribBandsWithHits",
    )
    parts = [f"{key}={summary[key]}" for key in metric_keys if key in summary]
    return f"- {label}: " + " ".join(parts)


def build_summary_markdown(timestamp: str, run_root: Path, results: list[dict], pairs: list[dict]) -> str:
    lines = [
        f"# Render Test Scheduler Compare ({timestamp})",
        "",
        f"Run root: `{run_root}`",
        f"Images: `{run_root / 'images'}`",
        f"Logs: `{run_root / 'logs'}`",
        "",
        "## Cases",
        "",
    ]
    for result in results:
        capture_paths = result.get("capturePaths") or []
        lines.append(
            "- "
            f"{result['fixture']} {result['mode']}: "
            f"godot_exit={result['godotExitCode']} "
            f"regress_exit={result['regressExitCode']} "
            f"captures={len(capture_paths)} "
            f"log=`{result['logPath']}`"
        )
        for capture_path in capture_paths[:1]:
            lines.append(f"  capture=`{capture_path}`")
        if result.get("regressStdout"):
            lines.append(f"  regress=`{result['regressStdout']}`")
        scheduler_summary = result.get("schedulerSummary") or {}
        lines.append(
            f"  scheduler_warm=`{json.dumps(scheduler_summary.get('warmSummary') or {}, sort_keys=True)}`"
        )
    if pairs:
        lines.extend(["", "## Comparisons", ""])
        for pair in pairs:
            if pair["status"] != "ok":
                lines.append(
                    f"- {pair['fixture']} {pair['leftMode']} vs {pair['rightMode']}: missing capture"
                )
                continue
            lines.append(
                f"- {pair['fixture']} {pair['leftMode']} vs {pair['rightMode']}: "
                f"SSIM={pair['ssim']:.6f} MeanAbsDiff={pair['meanAbsDiff']:.4f}"
            )
    lines.extend(["", "## Scheduler Metrics", ""])
    by_fixture: dict[str, dict[str, dict]] = {}
    for result in results:
        by_fixture.setdefault(result["fixture"], {})[result["mode"]] = result
    for fixture, fixture_results in sorted(by_fixture.items()):
        lines.extend(["", f"### {fixture}", ""])
        for mode in ("baseline", "reorder-only", "reorder-only-persistent-priors"):
            if mode not in fixture_results:
                continue
            summary = fixture_results[mode].get("schedulerSummary", {})
            lines.append(build_scheduler_metric_line(f"{mode} warm", summary.get("warmSummary")))
            if summary.get("coldSummary"):
                lines.append(build_scheduler_metric_line(f"{mode} cold", summary.get("coldSummary")))
    lines.append("")
    return "\n".join(lines)


def main() -> int:
    args = parse_args()
    try:
        godot_exe = resolve_godot_exe(args.godot_exe)
    except FileNotFoundError as exc:
        print(str(exc), file=sys.stderr)
        return 1
    run_root = args.output_root / args.timestamp
    run_root.mkdir(parents=True, exist_ok=True)

    results = [run_case(godot_exe, args.project_path, run_root, case) for case in CASES]
    pairs = compare_pairs(results, run_root)

    summary = {
        "timestamp": args.timestamp,
        "runRoot": str(run_root),
        "imagesDir": str(run_root / "images"),
        "logsDir": str(run_root / "logs"),
        "cases": results,
        "comparisons": pairs,
    }
    summary_json_path = run_root / "summary.json"
    summary_md_path = run_root / "summary.md"
    summary_json_path.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    summary_md_path.write_text(
        build_summary_markdown(args.timestamp, run_root, results, pairs),
        encoding="utf-8",
    )

    print(f"run_root={run_root}")
    print(f"images_dir={run_root / 'images'}")
    print(f"summary_json={summary_json_path}")
    print(f"summary_md={summary_md_path}")

    failed = [
        result
        for result in results
        if result["godotExitCode"] != 0
        or result["regressExitCode"] != 0
        or not result["capturePaths"]
    ]
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
