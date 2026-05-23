#!/usr/bin/env python3
"""Export measured off-axis observe transport classification disagreement packet.

This is presentation-only instrumentation. It re-runs the paired observe scenes
with the existing GrinBasicVisual transport-classification capture mode, writes
normalized classification PNGs and sidecars, then invokes
classification_delta_compare.py. It does not modify renderer transport,
scheduling, hit selection, traversal order, or oracle logic.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_GODOT_EXE = (
    r"C:\Users\wmbro\Downloads\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64"
    r"\Godot_v4.5.1-stable_mono_win64_console.exe"
)
DEFAULT_OUTPUT_DIR = ROOT / "output" / "observer_disagreement" / "offaxis_observe_delta"
DEFAULT_MIN_ROWS = 268
DEFAULT_SETTLE_FRAMES = 12
DEFAULT_TIMEOUT_SECONDS = 900
TRANSPORT_CLASSIFICATION_CAPTURE_MODE = "transport_classification"
FAIL_RE = re.compile(r"\[GrinBasicVisual\]\[Capture\]\[FAIL\].*")

CASES = [
    {
        "id": "straight_offaxis_observe",
        "label": "straight off-axis observe",
        "scene": "res://test-straight-basic-visual-offaxis-observe.tscn",
        "launcher": "run_grin_basic_visual_straight_offaxis_observe",
        "transport_assumption": "straight_reference",
    },
    {
        "id": "grin_offaxis_observe",
        "label": "curved GRIN off-axis observe",
        "scene": "res://test-grin-basic-visual-offaxis-observe.tscn",
        "launcher": "run_grin_basic_visual_offaxis_observe",
        "transport_assumption": "curved_grin",
    },
]


def require_godot_exe(override: str | None) -> str:
    candidate = override or os.environ.get("GODOT_EXE", DEFAULT_GODOT_EXE)
    if not Path(candidate).exists():
        raise FileNotFoundError(
            f"GODOT_EXE not found at '{candidate}'. Set GODOT_EXE or pass --godot-exe."
        )
    return candidate


def scalar(token: str | None) -> Any:
    if token is None:
        return None
    token = token.strip()
    if not token or token.lower() == "na":
        return None
    try:
        if any(ch in token for ch in ".eE"):
            return float(token)
        return int(token)
    except ValueError:
        return token


def to_int(value: Any) -> int | None:
    if value is None:
        return None
    if isinstance(value, int):
        return value
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def parse_kv(line: str, prefix: str) -> dict[str, Any] | None:
    idx = line.find(prefix)
    if idx < 0:
        return None
    result: dict[str, Any] = {}
    for token in line[idx + len(prefix):].strip().split():
        if "=" not in token:
            continue
        key, value = token.split("=", 1)
        result[key] = scalar(value)
    return result


def parse_log(text: str) -> dict[str, Any]:
    parsed: dict[str, Any] = {
        "capture": None,
        "captureArtifacts": None,
        "captureConfig": None,
        "coverage": None,
        "tileScheduler": None,
        "launchAudit": None,
        "rows": None,
        "captureFailure": None,
    }
    for line in text.splitlines():
        for key, prefix in {
            "capture": "[GrinBasicVisual][Capture]",
            "captureArtifacts": "[GrinBasicVisual][CaptureArtifacts]",
            "captureConfig": "[GrinBasicVisual][CaptureConfig]",
            "coverage": "[GrinBasicVisual][Coverage]",
            "tileScheduler": "[TileScheduler]",
            "launchAudit": "[LaunchAudit]",
            "rows": "[GrinBasicVisual][Rows]",
        }.items():
            data = parse_kv(line, prefix)
            if data:
                parsed[key] = data
        fail = FAIL_RE.search(line)
        if fail:
            parsed["captureFailure"] = fail.group(0).strip()
    return parsed


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def classification_path(output_dir: Path, case_data: dict[str, Any]) -> Path:
    return output_dir / f"{case_data['id']}_transport_classification.png"


def metadata_path(output_dir: Path, case_data: dict[str, Any]) -> Path:
    return output_dir / f"{case_data['id']}_transport_classification_metadata.json"


def coverage_path(output_dir: Path, case_data: dict[str, Any]) -> Path:
    return output_dir / f"{case_data['id']}_transport_classification_coverage.json"


def run_case(
    godot_exe: str,
    case_data: dict[str, Any],
    output_dir: Path,
    min_rows: int,
    settle_frames: int,
    timeout_seconds: int,
) -> dict[str, Any]:
    capture_path = classification_path(output_dir, case_data)
    log_path = output_dir / "logs" / f"{case_data['id']}_transport_classification.log"
    log_path.parent.mkdir(parents=True, exist_ok=True)
    args = [
        f"--grin-basic-capture={capture_path.as_posix()}",
        f"--grin-basic-analysis-capture-mode={TRANSPORT_CLASSIFICATION_CAPTURE_MODE}",
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
    cap = parsed.get("capture") or {}
    cov = parsed.get("coverage") or {}
    tile = parsed.get("tileScheduler") or {}

    failure = None
    if timed_out:
        failure = f"python_timeout_{timeout_seconds}s"
    elif exit_code != 0:
        failure = f"godot_exit_{exit_code}"
    elif parsed.get("captureFailure"):
        failure = parsed["captureFailure"]
    elif not capture_path.exists():
        failure = "missing_classification_png"

    result: dict[str, Any] = {
        "caseId": case_data["id"],
        "label": case_data["label"],
        "scene": case_data["scene"],
        "transportAssumption": case_data["transport_assumption"],
        "status": "PASS" if failure is None else "FAIL",
        "captureFailure": failure,
        "exitCode": exit_code,
        "timedOut": timed_out,
        "screenshotPath": str(capture_path),
        "logPath": str(log_path),
        "analysisCaptureMode": art.get("analysisCaptureMode"),
        "transportClassificationWritten": to_int(art.get("transportClassificationWritten")),
        "analysisWidth": to_int(art.get("analysisWidth")),
        "analysisHeight": to_int(art.get("analysisHeight")),
        "filmWidth": to_int(art.get("filmWidth")),
        "filmHeight": to_int(art.get("filmHeight")),
        "filmRowsRendered": to_int(art.get("filmRowsRendered")),
        "traversalRowsCompleted": to_int(art.get("traversalRowsCompleted")),
        "processedRows": to_int(cap.get("processedRows")),
        "rowTotalRowsProcessed": to_int((parsed.get("rows") or {}).get("totalRowsProcessed")),
        "rowProcessedRowStart": to_int((parsed.get("rows") or {}).get("processedRowStart")),
        "rowProcessedRowEnd": to_int((parsed.get("rows") or {}).get("processedRowEnd")),
        "rowProcessedRanges": (parsed.get("rows") or {}).get("processedRowRanges"),
        "tracedPixels": to_int(cap.get("tracedPixels")),
        "missHits": to_int(cap.get("missHits")),
        "sourceHits": to_int(cap.get("sourceHits")),
        "backgroundHits": to_int(cap.get("backgroundHits")),
        "totalPixels": to_int(cov.get("totalPixels")),
        "classifiedPixels": to_int(cov.get("classifiedPixels")),
        "escapedNoHitPixels": to_int(cov.get("escapedNoHitPixels")),
        "budgetExhaustedPixels": to_int(cov.get("budgetExhaustedPixels")),
        "hermeticRuleSatisfied": cov.get("hermeticRuleSatisfied"),
        "schedulerMode": tile.get("mode"),
        "_parsed": parsed,
    }
    validate_result(result)
    return result


def validate_result(result: dict[str, Any]) -> None:
    failures: list[str] = []
    if result.get("analysisCaptureMode") != TRANSPORT_CLASSIFICATION_CAPTURE_MODE:
        failures.append(f"analysisCaptureMode={result.get('analysisCaptureMode')}")
    if result.get("transportClassificationWritten") != 1:
        failures.append("transportClassificationWritten is not 1")
    width = result.get("analysisWidth")
    height = result.get("analysisHeight")
    film_width = result.get("filmWidth")
    film_height = result.get("filmHeight")
    if None in (width, height, film_width, film_height):
        failures.append(f"missing dimensions analysis={width}x{height} film={film_width}x{film_height}")
    elif width != film_width or height != film_height:
        failures.append(f"dimension mismatch analysis={width}x{height} film={film_width}x{film_height}")
    row_total = result.get("rowTotalRowsProcessed")
    row_start = result.get("rowProcessedRowStart")
    row_end = result.get("rowProcessedRowEnd")
    if film_height is not None:
        if row_total is None or row_total < film_height:
            failures.append(f"incomplete row participation totalRowsProcessed={row_total} filmHeight={film_height}")
        if row_start not in (0, None) or row_end not in (film_height - 1, None):
            failures.append(f"incomplete row range processedRowStart={row_start} processedRowEnd={row_end}")
    if result.get("captureFailure"):
        failures.append(result["captureFailure"])
    if failures:
        result["status"] = "FAIL"
        result["validationFailures"] = failures
    else:
        result["status"] = "PASS"
        result["validationFailures"] = []


def build_metadata(case_data: dict[str, Any], result: dict[str, Any]) -> dict[str, Any]:
    parsed = result.get("_parsed") or {}
    art = parsed.get("captureArtifacts") or {}
    cfg = parsed.get("captureConfig") or {}
    audit = parsed.get("launchAudit") or {}
    width = result.get("analysisWidth")
    height = result.get("analysisHeight")
    return {
        "schema": "xprimeray.classification_export_metadata.v1",
        "case_id": result["caseId"],
        "fixture": art.get("fixture") or cfg.get("fixture") or audit.get("actual_fixture"),
        "fixture_id": result["caseId"],
        "fixture_label": result["label"],
        "scene": result["scene"],
        "transport_assumption": result["transportAssumption"],
        "camera_pose_key": "basic_visual_offaxis_observe_v0_pre",
        "analysis_capture_mode": result.get("analysisCaptureMode"),
        "classification_path": result["screenshotPath"],
        "log_path": result["logPath"],
        "width": width,
        "height": height,
        "dimensions": {
            "analysis_width": width,
            "analysis_height": height,
            "film_width": result.get("filmWidth"),
            "film_height": result.get("filmHeight"),
            "matches_final_film": (
                width is not None
                and height is not None
                and width == result.get("filmWidth")
                and height == result.get("filmHeight")
            ),
        },
        "scheduler_mode": result.get("schedulerMode"),
        "traversal_mode": result.get("schedulerMode"),
        "render_test_traversal_pass1_pass2": result.get("schedulerMode"),
        "min_rows": result.get("minRows"),
        "traversal_rows_completed": result.get("traversalRowsCompleted"),
        "row_participation": {
            "total_rows_processed": result.get("rowTotalRowsProcessed"),
            "processed_row_start": result.get("rowProcessedRowStart"),
            "processed_row_end": result.get("rowProcessedRowEnd"),
            "processed_row_ranges": result.get("rowProcessedRanges"),
        },
        "transport_classification_written": result.get("transportClassificationWritten"),
        "status": result["status"],
        "capture_failure": result["captureFailure"],
        "validation_failures": result["validationFailures"],
        "semantic_scope": "presentation_only_export",
        "notes": [
            "Generated by re-running the paired off-axis observe scene with analysis_capture_mode=transport_classification.",
            "No transport semantics, scheduler order, hit selection, resolver decisions, traversal order, or oracle logic are modified by this export.",
        ],
    }


def build_coverage(case_data: dict[str, Any], result: dict[str, Any]) -> dict[str, Any]:
    parsed = result.get("_parsed") or {}
    return {
        "schema": "xprimeray.classification_export_coverage.v1",
        "case_id": result["caseId"],
        "fixture_label": result["label"],
        "transport_assumption": result["transportAssumption"],
        "classification_path": result["screenshotPath"],
        "analysis_capture_mode": result.get("analysisCaptureMode"),
        "coverage": parsed.get("coverage") or {},
        "summary_metrics": {
            "total_pixels": result.get("totalPixels"),
            "classified_pixels": result.get("classifiedPixels"),
            "escaped_no_hit_pixels": result.get("escapedNoHitPixels"),
            "budget_exhausted_pixels": result.get("budgetExhaustedPixels"),
            "hermetic_rule_satisfied": result.get("hermeticRuleSatisfied"),
            "miss_hits": result.get("missHits"),
            "source_hits": result.get("sourceHits"),
            "background_hits": result.get("backgroundHits"),
        },
    }


def run_delta(output_dir: Path, straight: dict[str, Any], curved: dict[str, Any]) -> dict[str, Any]:
    cmd = [
        sys.executable,
        str(ROOT / "tools" / "classification_delta_compare.py"),
        "--straight",
        straight["screenshotPath"],
        "--curved",
        curved["screenshotPath"],
        "--out-dir",
        str(output_dir),
        "--straight-metadata",
        str(metadata_path(output_dir, CASES[0])),
        "--curved-metadata",
        str(metadata_path(output_dir, CASES[1])),
        "--straight-coverage",
        str(coverage_path(output_dir, CASES[0])),
        "--curved-coverage",
        str(coverage_path(output_dir, CASES[1])),
        "--require-metadata",
        "--metadata-key",
        "width",
        "--metadata-key",
        "height",
        "--metadata-key",
        "camera_pose_key",
        "--metadata-key",
        "traversal_mode",
        "--metadata-key",
        "scheduler_mode",
        "--metadata-key",
        "render_test_traversal_pass1_pass2",
    ]
    completed = subprocess.run(
        cmd,
        cwd=ROOT,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=False,
    )
    log_path = output_dir / "logs" / "classification_delta_compare.log"
    log_path.parent.mkdir(parents=True, exist_ok=True)
    log_path.write_text(
        completed.stdout + ("\n" + completed.stderr if completed.stderr else ""),
        encoding="utf-8",
    )
    return {
        "status": "PASS" if completed.returncode == 0 else "FAIL",
        "exitCode": completed.returncode,
        "logPath": str(log_path),
    }


def write_packet_manifest(output_dir: Path, results: list[dict[str, Any]], delta: dict[str, Any]) -> None:
    manifest = {
        "schema": "xprimeray.observer_disagreement_offaxis_packet.v1",
        "semantic_scope": "presentation_only_export",
        "output_dir": str(output_dir),
        "cases": [
            {
                "case_id": result["caseId"],
                "status": result["status"],
                "classification_path": result["screenshotPath"],
                "metadata_path": str(metadata_path(output_dir, CASES[index])),
                "coverage_path": str(coverage_path(output_dir, CASES[index])),
                "width": result.get("analysisWidth"),
                "height": result.get("analysisHeight"),
                "transport_assumption": result["transportAssumption"],
                "validation_failures": result["validationFailures"],
            }
            for index, result in enumerate(results)
        ],
        "delta": {
            "status": delta["status"],
            "classification_delta": str(output_dir / "classification_delta.png"),
            "classification_delta_contours": str(output_dir / "classification_delta_contours.png"),
            "classification_delta_summary": str(output_dir / "classification_delta_summary.json"),
            "log_path": delta["logPath"],
        },
        "guardrails": [
            "Measured classification outputs only.",
            "No transport semantics, scheduler behavior, traversal order, hit selection, or oracle logic changes.",
            "Hermetic observatory remains the export sanity gate; this packet is the visual observer-disagreement artifact path.",
        ],
    }
    write_json(output_dir / "packet_manifest.json", manifest)


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
    output_dir = args.output_dir
    output_dir.mkdir(parents=True, exist_ok=True)

    print(f"[offaxis-classification] output={output_dir}")
    results: list[dict[str, Any]] = []
    for case_data in CASES:
        print(f"[offaxis-classification] exporting {case_data['label']} ...")
        result = run_case(
            godot_exe,
            case_data,
            output_dir,
            args.min_rows,
            args.settle_frames,
            args.timeout_seconds,
        )
        result["minRows"] = args.min_rows
        write_json(metadata_path(output_dir, case_data), build_metadata(case_data, result))
        write_json(coverage_path(output_dir, case_data), build_coverage(case_data, result))
        suffix = ""
        if result["validationFailures"]:
            suffix = f" failures={result['validationFailures']}"
        print(f"[offaxis-classification] {case_data['label']}: {result['status']}{suffix}")
        results.append(result)

    delta = {"status": "SKIP", "logPath": ""}
    if all(result["status"] == "PASS" for result in results):
        delta = run_delta(output_dir, results[0], results[1])
        print(f"[offaxis-classification] classification delta: {delta['status']}")
    else:
        print("[offaxis-classification] classification delta skipped; one or more exports failed")

    write_packet_manifest(output_dir, results, delta)
    return 0 if all(result["status"] == "PASS" for result in results) and delta["status"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
