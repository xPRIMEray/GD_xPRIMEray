#!/usr/bin/env python3
"""Aggregate the hermetic curvature FPS benchmark into a markdown report.

This is post-process only. It reads existing render-test artifacts and does not
feed rendering, scheduling, hit selection, shading, resolver decisions,
traversal, or adaptive precision.
"""

from __future__ import annotations

import argparse
import csv
import json
import math
import platform
import shutil
import subprocess
from pathlib import Path
from typing import Any


CURVATURE_ORDER = [0, 25, 50, 75, 100]
GUARDRAIL = (
    "Hermetic closure validates transport completion within a known scene "
    "contract. It does not establish physical correctness."
)


def load_json(path: Path) -> dict[str, Any]:
    if not path.exists():
        return {}
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return {}


def parse_int(value: Any, default: int = 0) -> int:
    try:
        if value in ("", None):
            return default
        return int(round(float(value)))
    except Exception:
        return default


def parse_float(value: Any, default: float = math.nan) -> float:
    try:
        if value in ("", None, "nan", "NaN"):
            return default
        return float(value)
    except Exception:
        return default


def parse_bool(value: Any) -> bool:
    return str(value or "").strip().lower() in {"1", "true", "yes", "on"}


def load_csv(path: Path) -> list[dict[str, str]]:
    if not path.exists():
        return []
    with path.open(newline="", encoding="utf-8-sig") as handle:
        return list(csv.DictReader(handle))


def find_first(folder: Path, patterns: list[str]) -> Path | None:
    for pattern in patterns:
        matches = sorted(folder.glob(pattern))
        if matches:
            return matches[0]
    return None


def safe_name(value: str) -> str:
    keep = []
    for ch in value:
        if ch.isalnum() or ch in {"-", "_", "."}:
            keep.append(ch)
        else:
            keep.append("_")
    return "".join(keep).strip("_") or "artifact"


def compute_hit_metrics(hit_csv: Path) -> dict[str, Any]:
    rows = load_csv(hit_csv)
    sampled = []
    for row in rows:
        segment_count = parse_int(row.get("segment_count"), 0)
        step_count = parse_int(row.get("step_count"), 0)
        hit_class = str(row.get("hit_class", "")).strip().lower()
        if (
            segment_count > 0
            or step_count > 0
            or hit_class not in {"", "unknown"}
            or parse_bool(row.get("had_hit"))
            or parse_bool(row.get("budget_exhausted_without_hit"))
            or parse_bool(row.get("max_steps_reached"))
        ):
            sampled.append(row)

    total = len(sampled)
    hits = sum(1 for row in sampled if parse_bool(row.get("had_hit")))
    misses = total - hits
    miss_rate = misses / total if total else math.nan
    hit_percent = 100.0 * hits / total if total else math.nan
    step_values = []
    max_step_warnings = 0
    budget_warnings = 0
    hit_after_budget_warnings = 0
    for row in sampled:
        step = parse_int(row.get("final_step_count") or row.get("step_count"), 0)
        if step > 0:
            step_values.append(step)
        if parse_bool(row.get("max_steps_reached")):
            max_step_warnings += 1
        if parse_bool(row.get("budget_exhausted_without_hit")):
            budget_warnings += 1
        if parse_bool(row.get("hit_found_after_budget_warning")):
            hit_after_budget_warnings += 1

    warnings = []
    if max_step_warnings:
        warnings.append(f"max_steps_reached={max_step_warnings}")
    if budget_warnings:
        warnings.append(f"budget_exhausted_without_hit={budget_warnings}")
    if hit_after_budget_warnings:
        warnings.append(f"hit_found_after_budget_warning={hit_after_budget_warnings}")

    return {
        "total_pixels_rays_evaluated": total,
        "hit_count": hits,
        "miss_count": misses,
        "miss_rate": miss_rate,
        "hit_percent": hit_percent,
        "average_traversal_steps": sum(step_values) / len(step_values) if step_values else math.nan,
        "max_traversal_steps": max(step_values) if step_values else 0,
        "max_steps_reached_count": max_step_warnings,
        "budget_exhausted_without_hit_count": budget_warnings,
        "hit_found_after_budget_warning_count": hit_after_budget_warnings,
        "precision_epsilon_warnings": warnings,
    }


def make_heatmaps(hit_csv: Path, out_dir: Path) -> dict[str, str]:
    rows = load_csv(hit_csv)
    if not rows:
        return {}
    try:
        from PIL import Image, ImageDraw
    except Exception:
        return {}

    xs = [parse_int(r.get("x"), -1) for r in rows]
    ys = [parse_int(r.get("y"), -1) for r in rows]
    width = max(xs) + 1 if xs else 0
    height = max(ys) + 1 if ys else 0
    if width <= 0 or height <= 0:
        return {}

    hit_img = Image.new("RGBA", (width, height), (28, 30, 40, 255))
    step_img = Image.new("RGBA", (width, height), (6, 8, 18, 255))
    hit_pix = hit_img.load()
    steps: list[tuple[int, int, int]] = []
    max_step = 1
    for row in rows:
        x = parse_int(row.get("x"), -1)
        y = parse_int(row.get("y"), -1)
        if x < 0 or y < 0 or x >= width or y >= height:
            continue
        sampled = parse_int(row.get("segment_count"), 0) > 0 or parse_int(row.get("step_count"), 0) > 0 or parse_bool(row.get("had_hit"))
        if not sampled:
            continue
        if parse_bool(row.get("had_hit")):
            hit_pix[x, y] = (35, 190, 120, 255)
        elif parse_bool(row.get("budget_exhausted_without_hit")):
            hit_pix[x, y] = (255, 145, 35, 255)
        else:
            hit_pix[x, y] = (240, 50, 95, 255)
        step = parse_int(row.get("final_step_count") or row.get("step_count"), 0)
        if step > 0:
            steps.append((x, y, step))
            max_step = max(max_step, step)

    step_pix = step_img.load()
    for x, y, step in steps:
        t = min(1.0, step / max_step)
        step_pix[x, y] = (int(30 + 225 * t), int(70 + 120 * (1.0 - t)), int(210 * (1.0 - t)), 255)

    scale = max(1, min(4, 640 // max(width, 1)))
    outputs = {}
    for stem, img, title in (
        ("hit_miss_map", hit_img, "hit/miss map"),
        ("traversal_step_heatmap", step_img, "traversal step heatmap"),
    ):
        resized = img.resize((width * scale, height * scale), Image.Resampling.NEAREST)
        panel = Image.new("RGBA", (resized.width, resized.height + 24), (4, 5, 12, 255))
        panel.alpha_composite(resized, (0, 24))
        draw = ImageDraw.Draw(panel)
        draw.text((8, 6), title, fill=(238, 238, 248, 255))
        path = out_dir / f"{stem}.png"
        panel.save(path)
        outputs[stem] = str(path)
    return outputs


def discover_visual_artifacts(cell: Path, generated: dict[str, str]) -> dict[str, str]:
    wanted = {
        "screenshot": ["*__runid-*.png", "layer0_beauty.png"],
        "normal_overlay": ["full_frame_hit_normals.png", "hit_normal_vector_overlay.png"],
        "budget_heatmap": ["budget_exhaustion_heatmap.png"],
        "budget_overlay": ["budget_exhaustion_overlay.png"],
        "diagnostic_contact_sheet": ["diagnostic_overlay_contact_sheet.png"],
        "combined_diagnostic_overlay": ["combined_diagnostic_overlay.png"],
        "transport_continuity": ["layer5_transport_continuity_vectors.png"],
        "ownership_seams": ["ownership_graph_seam_map.png"],
    }
    artifacts: dict[str, str] = {}
    for key, patterns in wanted.items():
        found = find_first(cell, patterns)
        if found:
            artifacts[key] = str(found)
    artifacts.update(generated)
    return artifacts


def copy_report_assets(rows: list[dict[str, Any]], assets_dir: Path) -> None:
    if assets_dir.exists():
        shutil.rmtree(assets_dir)
    assets_dir.mkdir(parents=True, exist_ok=True)
    for row in rows:
        prefix = f"curvature_{row['curvature_percent']:03d}"
        copied: dict[str, str] = {}
        for key, value in (row.get("visual_artifacts") or {}).items():
            path = Path(value)
            if not path.exists() or not path.is_file():
                continue
            dest = assets_dir / f"{prefix}_{safe_name(key)}{path.suffix.lower()}"
            shutil.copy2(path, dest)
            copied[key] = str(dest)
        row["report_artifacts"] = copied


def command_text(args: list[str]) -> str:
    try:
        result = subprocess.run(args, check=False, text=True, capture_output=True, timeout=5)
    except Exception:
        return ""
    return (result.stdout or result.stderr or "").strip()


def hardware_info() -> dict[str, str]:
    info = {
        "platform": platform.platform(),
        "processor": platform.processor(),
        "python": platform.python_version(),
    }
    lscpu = command_text(["lscpu"])
    if lscpu:
        for line in lscpu.splitlines():
            if line.startswith(("Model name:", "CPU(s):", "Thread(s) per core:", "Core(s) per socket:")):
                key, _, value = line.partition(":")
                info[key.strip().lower().replace(" ", "_")] = value.strip()
    gpu = command_text(["nvidia-smi", "--query-gpu=name,driver_version", "--format=csv,noheader"])
    if not gpu:
        gpu = command_text(["lspci"])
        gpu = "\n".join(line for line in gpu.splitlines() if "VGA" in line or "3D controller" in line)[:500]
    if gpu:
        info["gpu"] = gpu
    return info


def dominant_bottleneck(rows: list[dict[str, Any]]) -> str:
    stage_keys = [
        "pass1_ms",
        "pass2_phys_ms",
        "pass2_query_ms",
        "pass2_hit_resolve_ms",
        "pass2_shade_ms",
        "pass2_commit_ms",
        "scheduler_ms",
        "film_update_ms",
        "overlay_build_ms",
    ]
    totals = {key: 0.0 for key in stage_keys}
    counts = {key: 0 for key in stage_keys}
    for row in rows:
        perf = row.get("latest_perf_frame_report") or {}
        for key in stage_keys:
            value = parse_float(perf.get(key))
            if math.isfinite(value):
                totals[key] += value
                counts[key] += 1
    if not any(counts.values()):
        return "No perf-stage timings were available."
    best = max(stage_keys, key=lambda key: totals[key] / counts[key] if counts[key] else -1.0)
    return f"{best} averaged {totals[best] / max(1, counts[best]):.3f} ms across available cells."


def collect_rows(root: Path) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for percent in CURVATURE_ORDER:
        cell = root / "cells" / f"curvature_{percent:03d}" / "row"
        result = load_json(cell / "curvature_fps_result.json")
        metadata = load_json(cell / "metadata.json")
        hit_csv = find_first(cell, ["*.hit_diagnostics.csv"])
        hit_metrics = compute_hit_metrics(hit_csv) if hit_csv else {}
        heatmaps = make_heatmaps(hit_csv, cell) if hit_csv else {}
        artifacts = discover_visual_artifacts(cell, heatmaps)
        closure_summary = load_json(root / "hermetic_hit_closure_summary.json")
        row = {
            "curvature_percent": percent,
            "field_amplitude": parse_float(result.get("field_amplitude"), parse_float(metadata.get("curvature_strength"))),
            "cell": str(cell),
            "effective_status": (cell / "effective_status.txt").read_text().strip() if (cell / "effective_status.txt").exists() else "",
            "godot_exit_code": parse_int((cell / "status.txt").read_text().strip(), -1) if (cell / "status.txt").exists() else -1,
            **result,
            **hit_metrics,
            "visual_artifacts": artifacts,
            "hit_diagnostics_csv": str(hit_csv) if hit_csv else "",
            "closure_summary_available": bool(closure_summary),
        }
        rows.append(row)
    return rows


def write_summary(root: Path, rows: list[dict[str, Any]], info: dict[str, str]) -> None:
    all_completed = all((Path(r["cell"]) / "curvature_fps_result.json").exists() for r in rows)
    clean_exit = all(parse_int(r.get("godot_exit_code"), -1) == 0 or str(r.get("effective_status")) == "0" for r in rows)
    sealed_pass = all(parse_int(r.get("miss_count"), 1) == 0 for r in rows)
    payload = {
        "study": "curvature_fps_benchmark",
        "guardrail": GUARDRAIL,
        "cell_count": len(rows),
        "did_run": all_completed,
        "godot_clean_exit": clean_exit,
        "all_five_levels_complete": all_completed and len(rows) == len(CURVATURE_ORDER),
        "all_levels_completed": all_completed,
        "sealed_hit_validation_passed": sealed_pass,
        "hardware": info,
        "results": rows,
    }
    (root / "summary.json").write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def rel(path: str | Path, base: Path) -> str:
    try:
        return Path(path).resolve().relative_to(base.resolve()).as_posix()
    except Exception:
        return Path(path).as_posix()


def write_report(root: Path, rows: list[dict[str, Any]], report_path: Path, assets_dir: Path, info: dict[str, str]) -> None:
    all_completed = all((Path(r["cell"]) / "curvature_fps_result.json").exists() for r in rows)
    clean_exit = all(parse_int(r.get("godot_exit_code"), -1) == 0 or str(r.get("effective_status")) == "0" for r in rows)
    sealed_pass = all(parse_int(r.get("miss_count"), 1) == 0 for r in rows)
    fps_values = [parse_float(r.get("mean_fps")) for r in rows if math.isfinite(parse_float(r.get("mean_fps")))]
    reaches_30 = bool(fps_values) and min(fps_values) >= 30.0
    reaches_60 = bool(fps_values) and min(fps_values) >= 60.0
    bottleneck = dominant_bottleneck(rows)

    lines = [
        "# Weekend FPS Curvature Sweep",
        "",
        GUARDRAIL,
        "",
        "## Executive Summary",
        "",
        f"- Did it run? {'yes' if all_completed else 'no'}",
        f"- Did Godot exit cleanly? {'yes' if clean_exit else 'no'}",
        f"- Did all five curvature levels complete? {'yes' if all_completed else 'no'}",
        f"- Did sealed-scene hit validation pass? {'yes' if sealed_pass else 'no'}",
        f"- Did FPS reach 30? {'yes' if reaches_30 else 'no'}",
        f"- Did FPS reach 60? {'yes' if reaches_60 else 'no'}",
        f"- Biggest bottleneck observed: {bottleneck}",
        "",
        "## Detailed Benchmark Table",
        "",
        "| curvature % | amplitude | mean FPS | p95 frame ms | hit % | miss count | avg traversal steps | max traversal steps | screenshot | visual metrics available |",
        "|---:|---:|---:|---:|---:|---:|---:|---:|---|---|",
    ]
    for row in rows:
        artifacts = row.get("report_artifacts") or {}
        screenshot = artifacts.get("screenshot") or row.get("screenshot_path") or ""
        screenshot_link = f"[png]({rel(screenshot, report_path.parent)})" if screenshot else ""
        visual_names = [key for key in artifacts.keys() if key != "screenshot"]
        visual_links = ", ".join(f"[{key}]({rel(path, report_path.parent)})" for key, path in artifacts.items() if key != "screenshot")
        if not visual_links:
            visual_links = ", ".join(visual_names)
        lines.append(
            f"| {row['curvature_percent']} | {parse_float(row.get('field_amplitude'), 0):.4g} | "
            f"{parse_float(row.get('mean_fps'), 0):.2f} | {parse_float(row.get('p95_frame_time_ms'), 0):.2f} | "
            f"{parse_float(row.get('hit_percent'), 0):.3f} | {parse_int(row.get('miss_count'), 0)} | "
            f"{parse_float(row.get('average_traversal_steps'), 0):.2f} | {parse_int(row.get('max_traversal_steps'), 0)} | "
            f"{screenshot_link} | {visual_links} |"
        )

    lines += [
        "",
        "## Hardware",
        "",
    ]
    for key, value in info.items():
        lines.append(f"- {key}: `{str(value).replace('`', '')}`")

    lines += [
        "",
        "## Notes",
        "",
        "- Primary gate: hermetic sealed-room hit closure.",
        "- Optional ownership, oracle, island, and cathedral-style diagnostics are report attachments only when existing tools produce them.",
        f"- Raw output root: `{root}`",
    ]
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", type=Path)
    parser.add_argument("--repo-root", type=Path, default=Path.cwd())
    parser.add_argument("--report-path", type=Path, default=Path("reports/weekend_fps_curvature_sweep.md"))
    parser.add_argument("--assets-dir", type=Path, default=Path("reports/weekend_fps_curvature_sweep_assets"))
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    root = args.root.resolve()
    repo = args.repo_root.resolve()
    report_path = (repo / args.report_path).resolve() if not args.report_path.is_absolute() else args.report_path
    assets_dir = (repo / args.assets_dir).resolve() if not args.assets_dir.is_absolute() else args.assets_dir
    rows = collect_rows(root)
    copy_report_assets(rows, assets_dir)
    info = hardware_info()
    write_summary(root, rows, info)
    write_report(root, rows, report_path, assets_dir, info)
    print(f"[curvature-fps-report] summary={root / 'summary.json'}")
    print(f"[curvature-fps-report] report={report_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
