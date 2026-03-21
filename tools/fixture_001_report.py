#!/usr/bin/env python3
import argparse
import json
import re
import struct
from pathlib import Path


FAIL_RE = re.compile(r"\[GrinBasicVisual\]\[Capture\]\[FAIL\].*")
GUARD_PROGRESS_RE = re.compile(r"reason=guard_progress\b")
FORCED_ADVANCE_RE = re.compile(r"forcedAdvance=1\b")


def scalar(token: str):
    token = token.strip()
    if not token or token.lower() == "na":
        return None
    try:
        if any(ch in token for ch in ".eE"):
            return float(token)
        return int(token)
    except ValueError:
        return token


def parse_kv(line: str, prefix: str):
    idx = line.find(prefix)
    if idx < 0:
        return None

    result = {}
    for token in line[idx + len(prefix):].strip().split():
        if "=" not in token:
            continue
        key, value = token.split("=", 1)
        result[key] = scalar(value)
    return result


def parse_log(text: str) -> dict:
    parsed = {
        "capture": None,
        "launchAudit": None,
        "renderer": None,
        "captureFailure": None,
        "guardProgress": len(GUARD_PROGRESS_RE.findall(text)),
        "forcedAdvance": len(FORCED_ADVANCE_RE.findall(text)),
    }
    for line in text.splitlines():
        capture = parse_kv(line, "[GrinBasicVisual][Capture]")
        if capture:
            parsed["capture"] = capture

        launch = parse_kv(line, "[LaunchAudit]")
        if launch:
            parsed["launchAudit"] = launch

        renderer = parse_kv(line, "[GrinBasicVisual][Renderer]")
        if renderer:
            parsed["renderer"] = renderer

        fail = FAIL_RE.search(line)
        if fail:
            parsed["captureFailure"] = fail.group(0).strip()

    return parsed


def detect_image_size(path: Path) -> dict:
    if not path.exists() or path.stat().st_size <= 0:
        return {"width": None, "height": None}
    try:
        with path.open("rb") as handle:
            header = handle.read(24)
        if len(header) >= 24 and header[:8] == b"\x89PNG\r\n\x1a\n" and header[12:16] == b"IHDR":
            width, height = struct.unpack(">II", header[16:24])
            return {"width": width, "height": height}
    except OSError:
        return {"width": None, "height": None}
    return {"width": None, "height": None}


def build_params(args: argparse.Namespace, parsed: dict) -> dict:
    launch = parsed.get("launchAudit") or {}
    return {
        "timestamp": args.timestamp,
        "fixture_id": args.fixture_id,
        "scene": args.scene,
        "launcher": args.launcher,
        "resolved_fixture_identity": launch.get("actual_fixture"),
        "run_dir": str(args.run_dir),
        "capture_path": str(args.capture_path),
        "log_path": str(args.log_path),
        "requested_transport_model": args.requested_transport_model,
        "requested_step_length": args.requested_step_length,
        "requested_min_step_length": args.requested_min_step_length,
        "requested_steps_per_ray": args.requested_steps_per_ray,
        "thresholds": {
            "settle_frames": args.settle_frames,
            "min_render_health_step": args.min_rh_step,
            "min_processed_rows": args.min_processed_rows,
            "capture_film_opacity": args.capture_film_opacity,
            "compare_grid": args.compare_grid,
            "compare_crosshair": args.compare_crosshair,
        },
    }


def build_metrics(args: argparse.Namespace, parsed: dict) -> dict:
    capture = parsed.get("capture") or {}
    launch = parsed.get("launchAudit") or {}
    renderer = parsed.get("renderer") or {}
    image = detect_image_size(args.capture_path)
    traced_pixels = capture.get("tracedPixels")
    source_hits = capture.get("sourceHits")
    miss_hits = capture.get("missHits")
    ready_frames = capture.get("readyFrames")
    render_health_step = capture.get("rhStep")
    processed_rows = capture.get("processedRows")
    capture_exists = args.capture_path.exists() and args.capture_path.stat().st_size > 0

    hit_success_rate = None
    miss_rate = None
    if isinstance(traced_pixels, (int, float)) and traced_pixels > 0:
        if isinstance(source_hits, (int, float)):
            hit_success_rate = source_hits / traced_pixels
        if isinstance(miss_hits, (int, float)):
            miss_rate = miss_hits / traced_pixels

    capture_failure = parsed.get("captureFailure")
    status = "ok"
    failure_reason = None

    if args.godot_exit_code != 0:
        status = "failed"
        failure_reason = f"godot_exit_{args.godot_exit_code}"
    elif capture_failure:
        status = "failed"
        failure_reason = capture_failure
    elif not capture_exists:
        status = "failed"
        failure_reason = "missing_capture_png"
    elif not capture:
        status = "failed"
        failure_reason = "missing_capture_log"
    elif launch.get("status") != "ok":
        status = "failed"
        failure_reason = "launch_audit_not_ok"

    return {
        "fixture_id": args.fixture_id,
        "scene": args.scene,
        "launcher": args.launcher,
        "status": status,
        "failure_reason": failure_reason,
        "transport_model_used": renderer.get("transport"),
        "launch_audit_status": launch.get("status"),
        "capture_succeeded": status == "ok",
        "runtime_seconds": args.runtime_seconds,
        "traced_pixels": traced_pixels,
        "source_hits": source_hits,
        "background_hits": capture.get("backgroundHits"),
        "absorbed_hits": capture.get("absorbedHits"),
        "miss_hits": miss_hits,
        "ready_frames": ready_frames,
        "render_health_step": render_health_step,
        "processed_rows": processed_rows,
        "guard_progress": parsed.get("guardProgress"),
        "forced_advance": parsed.get("forcedAdvance"),
        "effective_steps_per_ray": renderer.get("stepsPerRay"),
        "effective_step_length": renderer.get("stepLength"),
        "effective_min_step_length": renderer.get("minStepLength"),
        "image_width": image.get("width"),
        "image_height": image.get("height"),
        "hit_success_rate": hit_success_rate,
        "miss_rate": miss_rate,
        "launch_audit": {
            "requested_launcher": launch.get("requested_launcher"),
            "actual_scene": launch.get("actual_scene"),
            "actual_fixture": launch.get("actual_fixture"),
            "expected_scene": launch.get("expected_scene"),
            "expected_fixture": launch.get("expected_fixture"),
            "scene_match": launch.get("scene_match"),
            "fixture_match": launch.get("fixture_match"),
            "status": launch.get("status"),
        },
    }


def build_summary(metrics: dict, args: argparse.Namespace) -> str:
    lines = [
        f"Fixture: {metrics['fixture_id']}",
        f"Timestamp: {args.timestamp}",
        f"Scene: {metrics['scene']}",
        f"Status: {metrics['status']}",
        f"Runtime Seconds: {metrics['runtime_seconds']}",
        f"Traced Pixels: {metrics['traced_pixels']}",
        f"Source Hits: {metrics['source_hits']}",
        f"Miss Hits: {metrics['miss_hits']}",
        f"Capture Succeeded: {str(metrics['capture_succeeded']).lower()}",
        f"Launch Audit Status: {metrics['launch_audit_status']}",
        f"Guard Progress Exits: {metrics['guard_progress']}",
        f"Forced Advance Events: {metrics['forced_advance']}",
        f"Render Health Step: {metrics['render_health_step']}",
        f"Processed Rows: {metrics['processed_rows']}",
        f"Output Path: {args.run_dir}",
    ]
    if metrics.get("failure_reason"):
        lines.append(f"Failure Reason: {metrics['failure_reason']}")
    return "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description="Build Fixture 001 run artifacts from a raw Godot log.")
    parser.add_argument("--fixture-id", required=True)
    parser.add_argument("--timestamp", required=True)
    parser.add_argument("--scene", required=True)
    parser.add_argument("--launcher", required=True)
    parser.add_argument("--run-dir", type=Path, required=True)
    parser.add_argument("--log-path", type=Path, required=True)
    parser.add_argument("--capture-path", type=Path, required=True)
    parser.add_argument("--runtime-seconds", type=float, required=True)
    parser.add_argument("--godot-exit-code", type=int, required=True)
    parser.add_argument("--settle-frames", type=int, required=True)
    parser.add_argument("--min-rh-step", type=int, required=True)
    parser.add_argument("--min-processed-rows", type=int, required=True)
    parser.add_argument("--capture-film-opacity", required=True)
    parser.add_argument("--compare-grid", required=True)
    parser.add_argument("--compare-crosshair", required=True)
    parser.add_argument("--requested-transport-model", default=None)
    parser.add_argument("--requested-step-length", type=float, default=None)
    parser.add_argument("--requested-min-step-length", type=float, default=None)
    parser.add_argument("--requested-steps-per-ray", type=int, default=None)
    args = parser.parse_args()

    args.run_dir.mkdir(parents=True, exist_ok=True)
    log_text = args.log_path.read_text(encoding="utf-8", errors="replace") if args.log_path.exists() else ""
    parsed = parse_log(log_text)

    params = build_params(args, parsed)
    metrics = build_metrics(args, parsed)
    summary = build_summary(metrics, args)
    summary_json = {
        "timestamp": args.timestamp,
        "fixture_id": args.fixture_id,
        "scene": args.scene,
        "launcher": args.launcher,
        "run_dir": str(args.run_dir),
        "capture_path": str(args.capture_path),
        "log_path": str(args.log_path),
        "runtime_seconds": args.runtime_seconds,
        "params": params,
        "metrics": metrics,
        "capture": parsed.get("capture") or {},
        "launchAudit": parsed.get("launchAudit") or {},
        "renderer": parsed.get("renderer") or {},
        "scheduler": {
            "guard_progress": parsed.get("guardProgress"),
            "forcedAdvance": parsed.get("forcedAdvance"),
        },
        "image": {
            "width": metrics.get("image_width"),
            "height": metrics.get("image_height"),
        },
        "visual_tag": "",
        "decision_tag": "",
    }

    (args.run_dir / "params.json").write_text(json.dumps(params, indent=2) + "\n", encoding="utf-8")
    (args.run_dir / "metrics.json").write_text(json.dumps(metrics, indent=2) + "\n", encoding="utf-8")
    (args.run_dir / "summary.json").write_text(json.dumps(summary_json, indent=2) + "\n", encoding="utf-8")
    (args.run_dir / "summary.txt").write_text(summary, encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
