#!/usr/bin/env python3
"""Export paired resolved-film beauty frames for the off-axis disagreement packet.

The beauty frames are observatory context only. This tool uses the existing
GrinBasicVisual capture path in resolved-film mode and does not modify transport,
scheduling, traversal, hit selection, or oracle behavior.
"""

from __future__ import annotations

import argparse
import json
import os
import subprocess
from pathlib import Path
from typing import Any

from observer_disagreement_offaxis_export import (
    CASES,
    DEFAULT_GODOT_EXE,
    DEFAULT_MIN_ROWS,
    DEFAULT_OUTPUT_DIR,
    DEFAULT_SETTLE_FRAMES,
    DEFAULT_TIMEOUT_SECONDS,
    ROOT,
    parse_log,
    require_godot_exe,
    to_int,
    write_json,
)


BEAUTY_CAPTURE_MODE = "resolved_film"
CAMERA_POSE_KEY = "basic_visual_offaxis_observe_v0_pre"
EXPECTED_CASE_IDS = tuple(case_data["id"] for case_data in CASES)


def beauty_path(output_dir: Path, case_data: dict[str, Any]) -> Path:
    return output_dir / f"{case_data['id']}_beauty.png"


def beauty_metadata_path(output_dir: Path, case_data: dict[str, Any]) -> Path:
    return output_dir / f"{case_data['id']}_beauty_metadata.json"


def run_beauty_case(
    godot_exe: str,
    case_data: dict[str, Any],
    output_dir: Path,
    min_rows: int,
    settle_frames: int,
    timeout_seconds: int,
) -> dict[str, Any]:
    capture_path = beauty_path(output_dir, case_data)
    log_path = output_dir / "logs" / f"{case_data['id']}_beauty.log"
    log_path.parent.mkdir(parents=True, exist_ok=True)
    args = [
        f"--grin-basic-capture={capture_path.as_posix()}",
        f"--grin-basic-analysis-capture-mode={BEAUTY_CAPTURE_MODE}",
        f"--grin-basic-min-processed-rows={min_rows}",
        f"--grin-basic-settle-frames={settle_frames}",
        "--grin-basic-exit-after-capture=1",
    ]
    cmd = [godot_exe, "--path", ".", "--scene", case_data["scene"], "--", *args]
    env = os.environ.copy()
    env["XPRIMERAY_REQUESTED_LAUNCHER"] = case_data["launcher"]

    timed_out = False
    try:
        completed = subprocess.run(
            cmd,
            cwd=ROOT,
            env=env,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=timeout_seconds,
            check=False,
        )
        combined = completed.stdout + ("\n" + completed.stderr if completed.stderr else "")
        exit_code = completed.returncode
    except subprocess.TimeoutExpired as exc:
        timed_out = True

        def decode(value: Any) -> str:
            if value is None:
                return ""
            if isinstance(value, bytes):
                return value.decode("utf-8", errors="replace")
            return str(value)

        combined = decode(exc.stdout) + ("\n" + decode(exc.stderr) if exc.stderr else "")
        exit_code = -1

    log_path.write_text(combined, encoding="utf-8")
    parsed = parse_log(combined)
    art = parsed.get("captureArtifacts") or {}
    cfg = parsed.get("captureConfig") or {}
    audit = parsed.get("launchAudit") or {}
    tile = parsed.get("tileScheduler") or {}
    rows = parsed.get("rows") or {}

    failure = None
    if timed_out:
        failure = f"python_timeout_{timeout_seconds}s"
    elif exit_code != 0:
        failure = f"godot_exit_{exit_code}"
    elif parsed.get("captureFailure"):
        failure = parsed["captureFailure"]
    elif not capture_path.exists():
        failure = "missing_beauty_png"

    result: dict[str, Any] = {
        "schema": "xprimeray.offaxis_beauty_metadata.v1",
        "case_id": case_data["id"],
        "fixture": art.get("fixture") or cfg.get("fixture") or audit.get("actual_fixture"),
        "fixture_id": case_data["id"],
        "fixture_label": case_data["label"],
        "scene": case_data["scene"],
        "transport_assumption": case_data["transport_assumption"],
        "camera_pose_key": CAMERA_POSE_KEY,
        "semantic_scope": "presentation_only_observatory_context",
        "deterministic_capture_path": f"{case_data['id']}_beauty.png",
        "capture_path": str(capture_path),
        "log_path": str(log_path),
        "analysis_capture_mode": art.get("analysisCaptureMode"),
        "capture_config": {
            "min_rows": to_int(cfg.get("minRows")),
            "settle_frames": to_int(cfg.get("settleFrames")),
            "exit_after_capture": to_int(cfg.get("exitAfterCapture")),
            "overlay_enabled_for_analysis_capture": to_int(art.get("overlayEnabledForAnalysisCapture")),
        },
        "launch_audit": {
            "requested_launcher": audit.get("requested_launcher"),
            "actual_scene": audit.get("actual_scene"),
            "actual_fixture": audit.get("actual_fixture"),
            "scene_match": to_int(audit.get("scene_match")),
            "fixture_match": to_int(audit.get("fixture_match")),
            "status": audit.get("status"),
        },
        "scheduler_mode": tile.get("mode"),
        "traversal_mode": tile.get("mode"),
        "render_test_traversal_pass1_pass2": tile.get("mode"),
        "analysis_width": to_int(art.get("analysisWidth")),
        "analysis_height": to_int(art.get("analysisHeight")),
        "film_width": to_int(art.get("filmWidth")),
        "film_height": to_int(art.get("filmHeight")),
        "row_participation": {
            "total_rows_processed": to_int(rows.get("totalRowsProcessed")),
            "processed_row_start": to_int(rows.get("processedRowStart")),
            "processed_row_end": to_int(rows.get("processedRowEnd")),
            "processed_row_ranges": rows.get("processedRowRanges"),
        },
        "status": "PASS" if failure is None else "FAIL",
        "capture_failure": failure,
        "validation_failures": [],
        "guardrails": [
            "Beauty frame is observatory context only.",
            "No transport, scheduler, traversal, hit-selection, or oracle changes.",
            "No synthetic visual effects are added.",
        ],
    }
    validate_beauty_result(result)
    write_json(beauty_metadata_path(output_dir, case_data), result)
    return result


def validate_beauty_result(result: dict[str, Any]) -> None:
    failures: list[str] = []
    if result.get("analysis_capture_mode") != BEAUTY_CAPTURE_MODE:
        failures.append(f"analysis_capture_mode={result.get('analysis_capture_mode')}")
    if result.get("camera_pose_key") != CAMERA_POSE_KEY:
        failures.append(f"camera_pose_key={result.get('camera_pose_key')}")
    capture_config = result.get("capture_config") or {}
    if capture_config.get("overlay_enabled_for_analysis_capture") not in (0, None):
        failures.append(
            "overlay_enabled_for_analysis_capture="
            f"{capture_config.get('overlay_enabled_for_analysis_capture')}"
        )
    launch_audit = result.get("launch_audit") or {}
    if launch_audit.get("scene_match") == 0 or launch_audit.get("fixture_match") == 0:
        failures.append(f"launch_audit mismatch {launch_audit}")
    if launch_audit.get("status") not in ("ok", None):
        failures.append(f"launch_audit status={launch_audit.get('status')}")
    if not result.get("traversal_mode"):
        failures.append("missing traversal_mode")
    if result.get("analysis_width") != result.get("film_width") or result.get("analysis_height") != result.get("film_height"):
        failures.append(
            "dimension mismatch "
            f"analysis={result.get('analysis_width')}x{result.get('analysis_height')} "
            f"film={result.get('film_width')}x{result.get('film_height')}"
        )
    film_height = result.get("film_height")
    rows = result.get("row_participation") or {}
    if film_height is not None:
        if rows.get("total_rows_processed") is None or rows.get("total_rows_processed") < film_height:
            failures.append(
                f"incomplete row participation totalRowsProcessed={rows.get('total_rows_processed')} "
                f"filmHeight={film_height}"
            )
        if rows.get("processed_row_start") not in (0, None) or rows.get("processed_row_end") not in (film_height - 1, None):
            failures.append(
                f"incomplete row range processedRowStart={rows.get('processed_row_start')} "
                f"processedRowEnd={rows.get('processed_row_end')}"
            )
    if result.get("capture_failure"):
        failures.append(result["capture_failure"])
    if failures:
        result["status"] = "FAIL"
        result["validation_failures"] = failures
    else:
        result["status"] = "PASS"
        result["validation_failures"] = []


def validate_beauty_pair(results: list[dict[str, Any]]) -> list[str]:
    failures: list[str] = []
    case_ids = tuple(result.get("case_id") for result in results)
    if case_ids != EXPECTED_CASE_IDS:
        failures.append(f"unexpected beauty case pair={case_ids}")
        return failures

    first = results[0]
    second = results[1]
    for key in (
        "camera_pose_key",
        "analysis_width",
        "analysis_height",
        "film_width",
        "film_height",
        "scheduler_mode",
        "traversal_mode",
    ):
        if first.get(key) != second.get(key):
            failures.append(f"beauty pair mismatch {key}: {first.get(key)} != {second.get(key)}")

    first_config = first.get("capture_config") or {}
    second_config = second.get("capture_config") or {}
    for key in (
        "min_rows",
        "settle_frames",
        "exit_after_capture",
        "overlay_enabled_for_analysis_capture",
    ):
        if first_config.get(key) != second_config.get(key):
            failures.append(f"beauty capture_config mismatch {key}: {first_config.get(key)} != {second_config.get(key)}")

    for result in results:
        expected_path = f"{result['case_id']}_beauty.png"
        if Path(result["capture_path"]).name != expected_path:
            failures.append(f"non-deterministic beauty capture path for {result['case_id']}: {result['capture_path']}")

    return failures


def update_manifest(output_dir: Path, results: list[dict[str, Any]]) -> None:
    manifest_path = output_dir / "packet_manifest.json"
    if not manifest_path.exists():
        return

    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    case_by_id = {case_data["id"]: case_data for case_data in CASES}
    manifest["beauty_frames"] = [
        {
            "case_id": result["case_id"],
            "status": result["status"],
            "beauty_path": result["capture_path"],
            "metadata_path": str(beauty_metadata_path(output_dir, case_by_id[result["case_id"]])),
            "width": result.get("analysis_width"),
            "height": result.get("analysis_height"),
            "camera_pose_key": result.get("camera_pose_key"),
            "fixture": result.get("fixture"),
            "scheduler_mode": result.get("scheduler_mode"),
            "traversal_mode": result.get("traversal_mode"),
            "transport_assumption": result["transport_assumption"],
            "validation_failures": result["validation_failures"],
        }
        for result in results
    ]
    pair_failures = validate_beauty_pair(results)
    manifest["beauty_pair_contract"] = {
        "status": "PASS" if not pair_failures else "FAIL",
        "camera_pose_key": CAMERA_POSE_KEY,
        "capture_mode": BEAUTY_CAPTURE_MODE,
        "case_ids": list(EXPECTED_CASE_IDS),
        "deterministic_paths": [f"{case_id}_beauty.png" for case_id in EXPECTED_CASE_IDS],
        "validation_failures": pair_failures,
    }
    guardrails = manifest.setdefault("guardrails", [])
    if "Beauty frames are observatory context only." not in guardrails:
        guardrails.append("Beauty frames are observatory context only.")
    write_json(manifest_path, manifest)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--godot-exe", default=None, help="Path to Godot executable or wrapper.")
    parser.add_argument("--output-dir", type=Path, default=DEFAULT_OUTPUT_DIR)
    parser.add_argument("--min-rows", type=int, default=DEFAULT_MIN_ROWS)
    parser.add_argument("--settle-frames", type=int, default=DEFAULT_SETTLE_FRAMES)
    parser.add_argument("--timeout-seconds", type=int, default=DEFAULT_TIMEOUT_SECONDS)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    godot_exe = require_godot_exe(args.godot_exe)
    args.output_dir.mkdir(parents=True, exist_ok=True)

    print(f"[offaxis-beauty] output={args.output_dir}")
    results: list[dict[str, Any]] = []
    for case_data in CASES:
        print(f"[offaxis-beauty] exporting {case_data['label']} beauty ...")
        result = run_beauty_case(
            godot_exe,
            case_data,
            args.output_dir,
            args.min_rows,
            args.settle_frames,
            args.timeout_seconds,
        )
        suffix = f" failures={result['validation_failures']}" if result["validation_failures"] else ""
        print(f"[offaxis-beauty] {case_data['label']} beauty: {result['status']}{suffix}")
        results.append(result)

    pair_failures = validate_beauty_pair(results)
    if pair_failures:
        for result in results:
            result["status"] = "FAIL"
            result["validation_failures"].extend(pair_failures)
        print(f"[offaxis-beauty] beauty pair contract: FAIL failures={pair_failures}")
    else:
        print("[offaxis-beauty] beauty pair contract: PASS")

    update_manifest(args.output_dir, results)
    return 0 if all(result["status"] == "PASS" for result in results) else 1


if __name__ == "__main__":
    raise SystemExit(main())
