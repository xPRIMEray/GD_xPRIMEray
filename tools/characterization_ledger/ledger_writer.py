#!/usr/bin/env python3
import argparse
import csv
import json
import os
import struct
import subprocess
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
TOOLS_ROOT = ROOT / "tools"
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))


LEDGER_PATH = ROOT / "output" / "characterization_ledger" / "fixture_runs.csv"
DEFAULT_BASELINES = {
    "fixture_001": ROOT / "output" / "fixture_runs" / "fixture_001" / "2026-03-19T22-57-53" / "capture.png",
}
FIELDNAMES = [
    "timestamp",
    "fixture_id",
    "commit_hash",
    "transport_model",
    "requested_stepLength",
    "requested_min_stepLength",
    "steps_per_ray",
    "effective_stepLength",
    "effective_min_stepLength",
    "status",
    "capture_succeeded",
    "launch_audit_status",
    "guard_progress",
    "forcedAdvance",
    "processed_rows",
    "traced_pixels",
    "runtime",
    "source_hits",
    "miss_hits",
    "backgroundHits",
    "useful_hit_ratio",
    "ssim_vs_baseline",
    "mad_vs_baseline",
    "image_width",
    "image_height",
    "visual_tag",
    "decision_tag",
]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Append a fixture run to the characterization ledger.")
    parser.add_argument("--summary-json", type=Path, default=None)
    parser.add_argument("--metrics-json", type=Path, default=None)
    parser.add_argument("--params-json", type=Path, default=None)
    parser.add_argument("--capture-path", type=Path, default=None)
    parser.add_argument("--fixture-id", default=None)
    parser.add_argument("--timestamp", default=None)
    parser.add_argument("--baseline-path", type=Path, default=None)
    parser.add_argument("--ledger-path", type=Path, default=LEDGER_PATH)
    return parser.parse_args()


def load_json(path: Path | None) -> dict:
    if path is None or not path.exists():
        return {}
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return {}


def first_non_empty(*values):
    for value in values:
        if value is None:
            continue
        if isinstance(value, str) and value == "":
            continue
        if isinstance(value, (list, tuple, dict, set)) and len(value) == 0:
            continue
        return value
    return None


def get_commit_hash() -> str:
    try:
        completed = subprocess.run(
            ["git", "rev-parse", "HEAD"],
            cwd=ROOT,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            check=True,
        )
        return completed.stdout.strip()
    except (OSError, subprocess.SubprocessError):
        return ""


def safe_divide(numerator, denominator):
    if not isinstance(numerator, (int, float)):
        return None
    if not isinstance(denominator, (int, float)) or denominator == 0:
        return None
    return numerator / denominator


def detect_image_size(capture_path: Path | None) -> tuple[int | None, int | None]:
    if capture_path is None or not capture_path.exists():
        return None, None
    try:
        with capture_path.open("rb") as handle:
            header = handle.read(24)
        if len(header) >= 24 and header[:8] == b"\x89PNG\r\n\x1a\n" and header[12:16] == b"IHDR":
            return struct.unpack(">II", header[16:24])
    except OSError:
        return None, None
    return None, None


def resolve_baseline_path(args: argparse.Namespace, fixture_id: str | None) -> Path | None:
    explicit = args.baseline_path
    if explicit and explicit.exists():
        return explicit

    for env_name in ("FIXTURE_001_BASELINE_CAPTURE", "CHARACTERIZATION_LEDGER_BASELINE_CAPTURE"):
        env_value = os.environ.get(env_name)
        if env_value:
            candidate = Path(env_value).expanduser()
            if candidate.exists():
                return candidate

    candidate = DEFAULT_BASELINES.get(fixture_id or "")
    if candidate and candidate.exists():
        return candidate
    return None


def compute_image_metrics(capture_path: Path | None, baseline_path: Path | None) -> tuple[float | None, float | None]:
    if capture_path is None or baseline_path is None:
        return None, None
    if not capture_path.exists() or not baseline_path.exists():
        return None, None
    try:
        from image_compare import compare_metrics

        return compare_metrics(str(capture_path), str(baseline_path))
    except Exception:
        return None, None


def build_row(args: argparse.Namespace) -> dict:
    summary = load_json(args.summary_json)
    metrics = load_json(args.metrics_json)
    params = load_json(args.params_json)

    summary_params = summary.get("params") or {}
    summary_metrics = summary.get("metrics") or {}
    summary_capture = summary.get("capture") or {}
    summary_renderer = summary.get("renderer") or {}
    summary_scheduler = summary.get("scheduler") or {}
    summary_image = summary.get("image") or {}
    summary_launch = summary.get("launchAudit") or {}

    fixture_id = first_non_empty(
        args.fixture_id,
        summary.get("fixture_id"),
        summary_params.get("fixture_id"),
        metrics.get("fixture_id"),
        params.get("fixture_id"),
    )
    capture_path = first_non_empty(
        args.capture_path,
        Path(summary.get("capture_path")) if summary.get("capture_path") else None,
        Path(summary_params.get("capture_path")) if summary_params.get("capture_path") else None,
    )
    baseline_path = resolve_baseline_path(args, fixture_id)
    image_width, image_height = detect_image_size(capture_path)
    ssim_score, mad_score = compute_image_metrics(capture_path, baseline_path)

    source_hits = first_non_empty(summary_metrics.get("source_hits"), metrics.get("source_hits"), summary_capture.get("sourceHits"))
    traced_pixels = first_non_empty(summary_metrics.get("traced_pixels"), metrics.get("traced_pixels"), summary_capture.get("tracedPixels"))

    return {
        "timestamp": first_non_empty(args.timestamp, summary.get("timestamp")),
        "fixture_id": fixture_id,
        "commit_hash": get_commit_hash(),
        "transport_model": first_non_empty(
            summary_metrics.get("transport_model_used"),
            metrics.get("transport_model_used"),
            summary_renderer.get("transport"),
            summary_params.get("requested_transport_model"),
        ),
        "requested_stepLength": first_non_empty(summary_params.get("requested_step_length"), params.get("requested_step_length")),
        "requested_min_stepLength": first_non_empty(summary_params.get("requested_min_step_length"), params.get("requested_min_step_length")),
        "steps_per_ray": first_non_empty(
            summary_params.get("requested_steps_per_ray"),
            summary_metrics.get("effective_steps_per_ray"),
            summary_renderer.get("stepsPerRay"),
        ),
        "effective_stepLength": first_non_empty(summary_metrics.get("effective_step_length"), summary_renderer.get("stepLength")),
        "effective_min_stepLength": first_non_empty(summary_metrics.get("effective_min_step_length"), summary_renderer.get("minStepLength")),
        "status": first_non_empty(summary_metrics.get("status"), metrics.get("status")),
        "capture_succeeded": first_non_empty(summary_metrics.get("capture_succeeded"), metrics.get("capture_succeeded")),
        "launch_audit_status": first_non_empty(
            summary_metrics.get("launch_audit_status"),
            (metrics.get("launch_audit") or {}).get("status"),
            summary_launch.get("status"),
        ),
        "guard_progress": first_non_empty(summary_scheduler.get("guard_progress"), summary_metrics.get("guard_progress")),
        "forcedAdvance": first_non_empty(summary_scheduler.get("forcedAdvance"), summary_metrics.get("forced_advance")),
        "processed_rows": first_non_empty(summary_metrics.get("processed_rows"), metrics.get("processed_rows"), summary_capture.get("processedRows")),
        "traced_pixels": traced_pixels,
        "runtime": first_non_empty(summary_metrics.get("runtime_seconds"), metrics.get("runtime_seconds"), summary.get("runtime_seconds")),
        "source_hits": source_hits,
        "miss_hits": first_non_empty(summary_metrics.get("miss_hits"), metrics.get("miss_hits"), summary_capture.get("missHits")),
        "backgroundHits": first_non_empty(summary_metrics.get("background_hits"), metrics.get("background_hits"), summary_capture.get("backgroundHits")),
        "useful_hit_ratio": safe_divide(source_hits, traced_pixels),
        "ssim_vs_baseline": first_non_empty(summary_image.get("ssim_vs_baseline"), ssim_score),
        "mad_vs_baseline": first_non_empty(summary_image.get("mad_vs_baseline"), mad_score),
        "image_width": first_non_empty(summary_image.get("width"), image_width),
        "image_height": first_non_empty(summary_image.get("height"), image_height),
        "visual_tag": first_non_empty(summary.get("visual_tag"), ""),
        "decision_tag": first_non_empty(summary.get("decision_tag"), ""),
    }


def ensure_header(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists() or path.stat().st_size == 0:
        with path.open("w", newline="", encoding="utf-8") as handle:
            writer = csv.DictWriter(handle, fieldnames=FIELDNAMES)
            writer.writeheader()


def append_row(path: Path, row: dict) -> int:
    ensure_header(path)
    with path.open("r", newline="", encoding="utf-8") as handle:
        existing_rows = sum(1 for _ in handle) - 1
    sanitized_row = {field: normalize_value(row.get(field)) for field in FIELDNAMES}
    with path.open("a", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=FIELDNAMES)
        writer.writerow(sanitized_row)
    return existing_rows + 1


def normalize_value(value):
    if value is None:
        return ""
    if isinstance(value, bool):
        return "true" if value else "false"
    return value


def main() -> int:
    args = parse_args()
    row = build_row(args)
    row_index = append_row(args.ledger_path, row)
    print(f"ledger_write_ok row={row_index} timestamp={row.get('timestamp', '')}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
