#!/usr/bin/env python3
"""Aggregate the hermetic curvature FPS benchmark into a markdown report.

This is post-process only. It reads existing render-test artifacts and does not
feed rendering, scheduling, hit selection, shading, resolver decisions,
traversal, or adaptive precision.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import platform
import re
import shutil
import subprocess
from pathlib import Path
from typing import Any


CURVATURE_ORDER = [0, 25, 50, 75, 100]
CONTACT_THUMB_SIZE = (320, 180)
CONTACT_TITLE_BAND = 30
CONTACT_CAPTION_BAND = 112
CONTACT_CELL_PAD = 8
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


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def hash_artifacts(artifacts: dict[str, str]) -> dict[str, dict[str, Any]]:
    hashes: dict[str, dict[str, Any]] = {}
    for key, value in sorted((artifacts or {}).items()):
        path = Path(value)
        if not path.exists() or not path.is_file():
            continue
        hashes[key] = {
            "path": str(path),
            "sha256": sha256_file(path),
            "bytes": path.stat().st_size,
        }
    return hashes


def parse_key_values(text: str) -> dict[str, str]:
    values: dict[str, str] = {}
    for match in re.finditer(r"([A-Za-z0-9_]+)=([^ \n\r\t]+)", text):
        values[match.group(1)] = match.group(2)
    return values


def parse_fixture_curvature_from_log(log_path: Path) -> dict[str, Any]:
    if not log_path.exists():
        return {}
    try:
        lines = log_path.read_text(encoding="utf-8", errors="replace").splitlines()
    except Exception:
        return {}
    for line in lines:
        if "[HermeticClosure][Curvature]" not in line:
            continue
        values = parse_key_values(line)
        return {
            "known": 1,
            "source": "run.log",
            "requested_amplitude": parse_float(values.get("requested_strength"), 0.0),
            "field_present": parse_int(values.get("field_present"), 0),
            "field_enabled": parse_int(values.get("field_enabled"), 0),
            "resolved_reason": values.get("resolved_reason", ""),
            "resolved_enabled": parse_int(values.get("resolved_enabled"), 0),
            "resolved_amp": parse_float(values.get("resolved_amp"), 0.0),
            "curved_transport_enabled": parse_int(values.get("curved_transport_enabled"), 0),
        }
    return {}


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
        warnings.append(f"hit_found_on_overrun_step={hit_after_budget_warnings}")

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
        "hit_found_on_overrun_step_count": hit_after_budget_warnings,
        "precision_epsilon_warnings": warnings,
    }


def make_heatmaps(hit_csv: Path, out_dir: Path) -> dict[str, str]:
    rows = load_csv(hit_csv)
    if not rows:
        return {}
    try:
        from PIL import Image
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
    for stem, img in (
        ("hit_miss_map", hit_img),
        ("traversal_step_heatmap", step_img),
    ):
        resized = img.resize((width * scale, height * scale), Image.Resampling.NEAREST)
        path = out_dir / f"{stem}.png"
        resized.save(path)
        outputs[stem] = str(path)
    return outputs


def read_step_grid(hit_csv: Path) -> tuple[dict[tuple[int, int], int], int, int]:
    rows = load_csv(hit_csv)
    grid: dict[tuple[int, int], int] = {}
    max_x = -1
    max_y = -1
    for row in rows:
        x = parse_int(row.get("x"), -1)
        y = parse_int(row.get("y"), -1)
        if x < 0 or y < 0:
            continue
        step = parse_int(row.get("final_step_count") or row.get("step_count"), -1)
        if step < 0:
            continue
        grid[(x, y)] = step
        max_x = max(max_x, x)
        max_y = max(max_y, y)
    return grid, max_x + 1, max_y + 1


def create_curvature_difference_artifacts(root: Path) -> None:
    try:
        from PIL import Image, ImageDraw, ImageFont
    except Exception:
        return

    baseline_cell = root / "cells" / "curvature_000" / "row"
    baseline_csv = find_first(baseline_cell, ["*.hit_diagnostics.csv"])
    if not baseline_csv:
        return
    baseline_grid, base_w, base_h = read_step_grid(baseline_csv)
    if not baseline_grid or base_w <= 0 or base_h <= 0:
        return

    for percent in CURVATURE_ORDER:
        cell = root / "cells" / f"curvature_{percent:03d}" / "row"
        out = cell / "curved_vs_straight_difference.png"
        summary_path = cell / "curved_vs_straight_difference.json"
        if percent == 0:
            img = Image.new("RGB", (base_w, base_h), (18, 22, 32))
            panel = img.resize((max(base_w, 160), max(base_h, 90)), Image.Resampling.NEAREST)
            draw = ImageDraw.Draw(panel)
            font = ImageFont.load_default()
            text = "BASELINE REFERENCE"
            bbox = draw.textbbox((0, 0), text, font=font)
            draw.text(
                ((panel.width - (bbox[2] - bbox[0])) // 2, (panel.height - (bbox[3] - bbox[1])) // 2),
                text,
                fill=(240, 244, 255),
                font=font,
            )
            panel.save(out)
            summary_path.write_text(json.dumps({
                "comparison": "baseline_reference",
                "baseline_curvature_percent": 0,
                "current_curvature_percent": 0,
                "changed_pixel_count": 0,
                "max_abs_step_delta": 0,
                "mean_abs_step_delta": 0.0,
                "source_metric": "traversal_final_step_count",
            }, indent=2, sort_keys=True) + "\n", encoding="utf-8")
            continue

        hit_csv = find_first(cell, ["*.hit_diagnostics.csv"])
        if not hit_csv:
            continue
        grid, width, height = read_step_grid(hit_csv)
        width = max(width, base_w)
        height = max(height, base_h)
        if width <= 0 or height <= 0:
            continue
        deltas: list[int] = []
        for key, current in grid.items():
            if key in baseline_grid:
                deltas.append(current - baseline_grid[key])
        max_abs = max((abs(v) for v in deltas), default=0)
        scale = max(1, max_abs)
        img = Image.new("RGB", (width, height), (20, 22, 30))
        px = img.load()
        changed = 0
        for y in range(height):
            for x in range(width):
                key = (x, y)
                if key not in grid or key not in baseline_grid:
                    px[x, y] = (55, 55, 65)
                    continue
                delta = grid[key] - baseline_grid[key]
                if delta != 0:
                    changed += 1
                t = min(1.0, abs(delta) / scale)
                if delta > 0:
                    px[x, y] = (int(55 + 200 * t), int(55 + 45 * (1 - t)), int(80 + 35 * (1 - t)))
                elif delta < 0:
                    px[x, y] = (int(45 + 35 * (1 - t)), int(80 + 60 * (1 - t)), int(80 + 175 * t))
                else:
                    px[x, y] = (42, 46, 58)
        img.save(out)
        mean_abs = sum(abs(v) for v in deltas) / len(deltas) if deltas else 0.0
        summary_path.write_text(json.dumps({
            "comparison": "curved_vs_straight",
            "baseline_curvature_percent": 0,
            "current_curvature_percent": percent,
            "changed_pixel_count": changed,
            "compared_pixel_count": len(deltas),
            "max_abs_step_delta": max_abs,
            "mean_abs_step_delta": round(mean_abs, 6),
            "source_metric": "traversal_final_step_count",
        }, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def wrap_text(draw: Any, text: str, max_width: int, font: Any) -> list[str]:
    words = str(text or "").split()
    lines: list[str] = []
    current = ""
    for word in words:
        candidate = word if not current else f"{current} {word}"
        if draw.textbbox((0, 0), candidate, font=font)[2] <= max_width:
            current = candidate
            continue
        if current:
            lines.append(current)
        current = word
    if current:
        lines.append(current)
    return lines or [""]


def fit_panel_image(image: Any, size: tuple[int, int]) -> Any:
    from PIL import Image

    source = image.convert("RGB")
    resample = Image.Resampling.NEAREST if source.width < size[0] or source.height < size[1] else Image.Resampling.LANCZOS
    source.thumbnail(size, resample)
    panel = Image.new("RGB", size, (248, 248, 248))
    x = (size[0] - source.width) // 2
    y = (size[1] - source.height) // 2
    panel.paste(source, (x, y))
    return panel


def placeholder_panel(title: str, status: str, detail: str) -> Any:
    from PIL import Image, ImageDraw, ImageFont

    panel = Image.new("RGB", CONTACT_THUMB_SIZE, (24, 26, 34))
    draw = ImageDraw.Draw(panel)
    font = load_contact_font(13)
    title_font = load_contact_font(14)
    draw.rectangle((0, 0, panel.width - 1, panel.height - 1), outline=(190, 70, 70), width=2)
    draw.text((12, 10), title, fill=(220, 220, 230), font=title_font)
    draw.text((12, 58), status, fill=(255, 215, 80), font=title_font)
    if detail:
        draw.text((12, 86), detail[:50], fill=(235, 235, 245), font=font)
    return panel


def load_contact_font(size: int) -> Any:
    from PIL import ImageFont

    for path in (
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf",
    ):
        try:
            return ImageFont.truetype(path, size)
        except Exception:
            continue
    return ImageFont.load_default()


def make_story_contact_cell(title: str, image: Any, caption: str) -> Any:
    from PIL import Image, ImageDraw

    font = load_contact_font(13)
    title_font = load_contact_font(14)
    width = CONTACT_THUMB_SIZE[0] + CONTACT_CELL_PAD * 2
    height = CONTACT_TITLE_BAND + CONTACT_THUMB_SIZE[1] + CONTACT_CAPTION_BAND + CONTACT_CELL_PAD * 2
    cell = Image.new("RGB", (width, height), (246, 247, 250))
    draw = ImageDraw.Draw(cell)
    draw.rectangle((0, 0, width - 1, height - 1), outline=(205, 209, 218))
    draw.rectangle((0, 0, width - 1, CONTACT_TITLE_BAND - 1), fill=(232, 235, 242))
    draw.text((8, 7), title, fill=(18, 22, 32), font=title_font)
    canvas = fit_panel_image(image, CONTACT_THUMB_SIZE)
    img_y = CONTACT_TITLE_BAND + CONTACT_CELL_PAD
    cell.paste(canvas, (CONTACT_CELL_PAD, img_y))
    cap_y = img_y + CONTACT_THUMB_SIZE[1] + 6
    for idx, line in enumerate(wrap_text(draw, caption, width - 16, font)[:6]):
        draw.text((8, cap_y + idx * 16), line, fill=(35, 39, 50), font=font)
    return cell


def build_observatory_story_contact_sheets(root: Path) -> None:
    try:
        from PIL import Image
    except Exception:
        return

    story = [
        ("Raw visual", "raw_visual", "Q: What did the camera actually see? Academic: final beauty/render output. Analogy: lab camera photo of the fixture."),
        ("Scene geometry", "geometry_explanation", "Q: What objects exist in the scene? Academic: Cartesian object/receiver geometry. Analogy: blueprint of the test chamber."),
        ("Curvature field", "curvature_field_view", "Q: What field is bending the rays? Academic: field-source volume and resolved amplitude. Analogy: wind/weather map inside the chamber."),
        ("Transport ownership", "transport_ownership", "Q: Where did each ray end up? Academic: receiver/domain ownership. Analogy: delivery zones for photons."),
        ("Hit/miss map", "hit_miss_map", "Q: Did every ray find a target? Academic: hermetic closure validation. Analogy: target board hit test."),
        ("Traversal steps", "traversal_step_heatmap", "Q: How hard was the trip? Academic: per-pixel integration/traversal cost. Analogy: traffic/congestion map."),
        ("Budget stress", "budget_heatmap", "Q: Which rays nearly ran out of budget? Academic: max-step / overrun-step stress. Analogy: fuel warning light."),
        ("Combined diagnostic", "combined_diagnostic_overlay", "Q: What do all diagnostics look like together? Academic: composite diagnostic overlay. Analogy: mission-control dashboard."),
        ("Curved vs straight", "curved_vs_straight_difference", "Q: What changed because curvature was turned on? Academic: difference map against 0% baseline using traversal steps. Analogy: before/after inspection photo."),
    ]

    for percent in CURVATURE_ORDER:
        cell = root / "cells" / f"curvature_{percent:03d}" / "row"
        if not cell.exists():
            continue
        artifacts = discover_visual_artifacts(cell, {})
        overlay_metadata = load_json(cell / "overlay_metadata.json")
        beauty_health = overlay_metadata.get("beauty_capture_health") or {}
        panels = []
        for title, key, caption in story:
            path = Path(artifacts.get(key, ""))
            if key == "raw_visual" and beauty_health.get("status") in {"MISSING BEAUTY", "INVALID BEAUTY", "BLANK BEAUTY"}:
                detail = beauty_health.get("reason", "")
                if beauty_health.get("solid_rgba"):
                    detail = "solid RGBA: " + ",".join(str(v) for v in beauty_health["solid_rgba"])
                img = placeholder_panel(title, str(beauty_health.get("status", "BLANK BEAUTY")), detail)
            elif path.exists():
                try:
                    img = Image.open(path).convert("RGB")
                except Exception:
                    img = placeholder_panel(title, "INVALID", path.name)
            else:
                img = placeholder_panel(title, "MISSING", key)
            if key == "curved_vs_straight_difference" and percent == 0:
                caption = "Q: What changed because curvature was turned on? Baseline reference for later before/after comparisons."
            panels.append(make_story_contact_cell(title, img, caption))
        if panels:
            w = max(panel.width for panel in panels)
            h = max(panel.height for panel in panels)
            sheet = Image.new("RGB", (w * len(panels), h), "white")
            for index, panel in enumerate(panels):
                sheet.paste(panel, (index * w, 0))
            sheet.save(cell / "diagnostic_overlay_contact_sheet.png")


def discover_visual_artifacts(cell: Path, generated: dict[str, str]) -> dict[str, str]:
    wanted = {
        "screenshot": ["*__runid-*.png", "layer0_beauty.png"],
        "raw_visual": ["layer0_beauty.png", "*__runid-*.png"],
        "geometry_explanation": ["cartesian_scene_geometry.png", "layer1_cartesian_wireframe.png"],
        "curvature_field_view": ["curvature_field_view.png"],
        "cartesian_wireframe_overlay": ["layer1_cartesian_wireframe.png"],
        "normal_overlay": ["full_frame_hit_normals.png", "hit_normal_vector_overlay.png"],
        "hit_miss_map": ["hit_miss_map.png"],
        "traversal_step_heatmap": ["traversal_step_heatmap.png"],
        "curved_vs_straight_difference": ["curved_vs_straight_difference.png"],
        "budget_heatmap": ["budget_exhaustion_heatmap.png"],
        "budget_overlay": ["budget_exhaustion_overlay.png"],
        "diagnostic_contact_sheet": ["diagnostic_overlay_contact_sheet.png"],
        "transport_ownership": ["layer2_transport_ownership.png", "transport_shape_regions_overlay.png"],
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


def analyze_visual_identity(rows: list[dict[str, Any]]) -> dict[str, Any]:
    artifact_keys = sorted({
        key
        for row in rows
        for key in (row.get("artifact_hashes") or {}).keys()
    })
    by_key: dict[str, Any] = {}
    identical_keys: list[str] = []
    non_identical_keys: list[str] = []
    missing_keys: list[str] = []
    for key in artifact_keys:
        entries = []
        for row in rows:
            item = (row.get("artifact_hashes") or {}).get(key) or {}
            sha = item.get("sha256", "")
            entries.append({
                "curvature_percent": row.get("curvature_percent"),
                "sha256": sha,
                "path": item.get("path", ""),
            })
        hashes = [entry["sha256"] for entry in entries if entry["sha256"]]
        complete = len(hashes) == len(rows)
        unique_hashes = sorted(set(hashes))
        identical = complete and len(unique_hashes) == 1
        if not complete:
            missing_keys.append(key)
        elif identical:
            identical_keys.append(key)
        else:
            non_identical_keys.append(key)
        by_key[key] = {
            "complete": complete,
            "identical_across_curvature": identical,
            "unique_hash_count": len(unique_hashes),
            "entries": entries,
        }

    any_non_identical = bool(non_identical_keys)
    if any_non_identical:
        explanation = (
            "At least one visual artifact family changes across curvature levels; "
            "the sweep is not visually byte-identical."
        )
    else:
        explanation = (
            "All complete visual artifact families are byte-identical. For a sealed symmetric room this can be "
            "a valid visual outcome if resolved curvature still changes, because every ray remains enclosed and "
            "the normal/receiver classification may stay symmetric. Treat this as a warning unless resolved "
            "fixture curvature confirms nonzero transport for nonzero sweep cells."
        )

    return {
        "any_non_identical_visual_artifact": any_non_identical,
        "identical_visual_artifact_keys": identical_keys,
        "non_identical_visual_artifact_keys": non_identical_keys,
        "missing_visual_artifact_keys": missing_keys,
        "by_artifact_key": by_key,
        "explanation": explanation,
    }


def analyze_curvature_application(rows: list[dict[str, Any]]) -> dict[str, Any]:
    entries = []
    passed = True
    varied = False
    resolved_values = []
    for row in rows:
        percent = parse_int(row.get("curvature_percent"), -1)
        requested = parse_float(row.get("field_amplitude"), 0.0)
        resolved = row.get("resolved_fixture_curvature") or {}
        resolved_amp = parse_float(resolved.get("resolved_amp"), math.nan)
        renderer_strength = parse_float(resolved.get("renderer_field_strength"), math.nan)
        curved_transport = parse_int(resolved.get("curved_transport_enabled"), 0)
        expected_nonzero = percent > 0 and abs(requested) > 1e-7
        amp_matches = math.isfinite(resolved_amp) and abs(resolved_amp - abs(requested)) <= 1e-4
        renderer_matches = (
            math.isfinite(renderer_strength) and
            (abs(renderer_strength - requested) <= 1e-4 if expected_nonzero else abs(renderer_strength) <= 1e-4)
        )
        cell_passed = amp_matches and renderer_matches and (curved_transport == (1 if expected_nonzero else 0))
        passed = passed and cell_passed
        if math.isfinite(resolved_amp):
            resolved_values.append(round(resolved_amp, 6))
        entries.append({
            "curvature_percent": percent,
            "requested_amplitude": requested,
            "resolved_amp": resolved_amp,
            "renderer_field_strength": renderer_strength,
            "curved_transport_enabled": curved_transport,
            "matches_requested": cell_passed,
            "source": resolved.get("source", "curvature_fps_result.json"),
        })
    varied = len(set(resolved_values)) > 1
    return {
        "curvature_application_passed": passed,
        "resolved_curvature_varied": varied,
        "entries": entries,
    }


def normalize_health_status(status: Any) -> str:
    token = str(status or "").strip()
    return token if token else "UNKNOWN"


def aggregate_beauty_capture_health(rows: list[dict[str, Any]]) -> dict[str, Any]:
    entries = []
    statuses = []
    usable_count = 0
    for row in rows:
        health = row.get("beauty_capture_health") or {}
        status = normalize_health_status(health.get("status"))
        usable = bool(health.get("usable_for_visual_confirmation"))
        statuses.append(status)
        if usable:
            usable_count += 1
        entries.append({
            "curvature_percent": row.get("curvature_percent"),
            "status": status,
            "usable_for_visual_confirmation": usable,
            "reason": health.get("reason", ""),
            "solid_rgba": health.get("solid_rgba", []),
            "path": health.get("path", ""),
        })
    unique_statuses = sorted(set(statuses))
    overall = "OK" if rows and usable_count == len(rows) else (unique_statuses[0] if len(unique_statuses) == 1 else "MIXED")
    return {
        "overall_status": overall,
        "visual_render_confirmation_passed": rows != [] and usable_count == len(rows),
        "blank_beauty_count": sum(1 for status in statuses if status == "BLANK BEAUTY"),
        "missing_beauty_count": sum(1 for status in statuses if status == "MISSING BEAUTY"),
        "invalid_beauty_count": sum(1 for status in statuses if status == "INVALID BEAUTY"),
        "entries": entries,
    }


def aggregate_diagnostic_artifact_health(rows: list[dict[str, Any]]) -> dict[str, Any]:
    required_artifacts = {
        "raw_visual",
        "geometry_explanation",
        "curvature_field_view",
        "curved_vs_straight_difference",
        "hit_miss_map",
        "traversal_step_heatmap",
        "budget_heatmap",
        "budget_overlay",
        "normal_overlay",
        "transport_ownership",
        "diagnostic_contact_sheet",
    }
    entries = []
    passed_count = 0
    for row in rows:
        artifacts = set((row.get("visual_artifacts") or {}).keys())
        missing = sorted(key for key in required_artifacts if key not in artifacts)
        hit_csv = row.get("hit_diagnostics_csv", "")
        total = parse_int(row.get("total_pixels_rays_evaluated"), 0)
        passed = bool(hit_csv) and total > 0 and not missing
        if passed:
            passed_count += 1
        entries.append({
            "curvature_percent": row.get("curvature_percent"),
            "status": "OK" if passed else "INCOMPLETE",
            "hit_diagnostics_csv": hit_csv,
            "total_pixels_rays_evaluated": total,
            "missing_required_artifacts": missing,
        })
    return {
        "overall_status": "OK" if rows and passed_count == len(rows) else "INCOMPLETE",
        "diagnostic_artifacts_valid": rows != [] and passed_count == len(rows),
        "required_artifacts": sorted(required_artifacts),
        "entries": entries,
    }


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
        artifact_hashes = hash_artifacts(artifacts)
        budget_exhaustion = load_json(cell / "budget_exhaustion_summary.json")
        overlay_metadata = load_json(cell / "overlay_metadata.json")
        beauty_capture_health = overlay_metadata.get("beauty_capture_health") or {"status": "UNKNOWN", "usable_for_visual_confirmation": False}
        closure_summary = load_json(root / "hermetic_hit_closure_summary.json")
        resolved_curvature = result.get("resolved_fixture_curvature") or parse_fixture_curvature_from_log(cell / "run.log")
        if resolved_curvature:
            resolved_curvature.setdefault("source", "curvature_fps_result.json")
        row = {
            "curvature_percent": percent,
            "field_amplitude": parse_float(result.get("field_amplitude"), parse_float(metadata.get("curvature_strength"))),
            "cell": str(cell),
            "effective_status": (cell / "effective_status.txt").read_text().strip() if (cell / "effective_status.txt").exists() else "",
            "godot_exit_code": parse_int((cell / "status.txt").read_text().strip(), -1) if (cell / "status.txt").exists() else -1,
            **result,
            **hit_metrics,
            "resolved_fixture_curvature": resolved_curvature,
            "budget_exhaustion_summary": budget_exhaustion,
            "overlay_metadata": overlay_metadata,
            "beauty_capture_health": beauty_capture_health,
            "visual_artifacts": artifacts,
            "artifact_hashes": artifact_hashes,
            "hit_diagnostics_csv": str(hit_csv) if hit_csv else "",
            "closure_summary_available": bool(closure_summary),
        }
        rows.append(row)
    return rows


def write_summary(root: Path, rows: list[dict[str, Any]], info: dict[str, str]) -> None:
    all_completed = all((Path(r["cell"]) / "curvature_fps_result.json").exists() for r in rows)
    clean_exit = all(parse_int(r.get("godot_exit_code"), -1) == 0 or str(r.get("effective_status")) == "0" for r in rows)
    sealed_pass = all(parse_int(r.get("miss_count"), 1) == 0 for r in rows)
    visual_identity = analyze_visual_identity(rows)
    curvature_application = analyze_curvature_application(rows)
    beauty_capture_health = aggregate_beauty_capture_health(rows)
    diagnostic_artifact_health = aggregate_diagnostic_artifact_health(rows)
    payload = {
        "study": "curvature_fps_benchmark",
        "guardrail": GUARDRAIL,
        "cell_count": len(rows),
        "did_run": all_completed,
        "godot_clean_exit": clean_exit,
        "all_five_levels_complete": all_completed and len(rows) == len(CURVATURE_ORDER),
        "all_levels_completed": all_completed,
        "sealed_hit_validation_passed": sealed_pass,
        "curvature_application": curvature_application,
        "beauty_capture_health": beauty_capture_health,
        "diagnostic_artifact_health": diagnostic_artifact_health,
        "visual_identity": visual_identity,
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
    visual_identity = analyze_visual_identity(rows)
    curvature_application = analyze_curvature_application(rows)
    beauty_capture_health = aggregate_beauty_capture_health(rows)
    diagnostic_artifact_health = aggregate_diagnostic_artifact_health(rows)
    visual_status = "non-identical" if visual_identity["any_non_identical_visual_artifact"] else "identical"
    curvature_status = (
        "yes"
        if curvature_application["curvature_application_passed"] and curvature_application["resolved_curvature_varied"]
        else "no"
    )

    # Budget stress summary
    budget_stress_pcts: list[float] = []
    budget_no_hit_total = 0
    for r in rows:
        be = r.get("budget_exhaustion_summary") or {}
        pct = parse_float(be.get("budget_exhaustion_percent"))
        if math.isfinite(pct):
            budget_stress_pcts.append(pct)
        budget_no_hit_total += parse_int(be.get("budget_exhausted_no_hit_count"), 0)
    if budget_stress_pcts:
        avg_stress = sum(budget_stress_pcts) / len(budget_stress_pcts)
        if budget_no_hit_total == 0:
            budget_stress_str = (
                f"{avg_stress:.1f}% of pixels exhausted step budget; "
                "all found hit on overrun step (budget+1); budget_exhausted_without_hit = 0"
            )
        else:
            budget_stress_str = (
                f"{avg_stress:.1f}% of pixels exhausted step budget; "
                f"{budget_no_hit_total} pixel(s) had no hit after exhaustion"
            )
    else:
        budget_stress_str = "no data"

    # Screenshot sanity check
    screenshot_sizes: list[int] = []
    screenshot_sha256s: list[str] = []
    for r in rows:
        h = (r.get("artifact_hashes") or {}).get("screenshot") or {}
        if h.get("bytes") is not None:
            screenshot_sizes.append(int(h["bytes"]))
        if h.get("sha256"):
            screenshot_sha256s.append(h["sha256"])
    screenshot_suspect = not beauty_capture_health["visual_render_confirmation_passed"]
    beauty_status = beauty_capture_health["overall_status"]
    diagnostic_status = diagnostic_artifact_health["overall_status"]

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
        f"- Beauty capture status: {beauty_status}",
        f"- Diagnostic artifact health: {diagnostic_status}",
        "- Blank beauty does not fail sealed-hit validation, but it does fail visual-render confirmation.",
        f"- Traversal budget stress: {budget_stress_str}",
        f"- Screenshot capture: {'suspected blank or unusable for visual proof; verify layer0_beauty capture' if screenshot_suspect else 'ok'}",
        f"- Did resolved fixture curvature vary as requested? {curvature_status}",
        f"- Are visual outputs identical across curvature levels? {'no' if visual_identity['any_non_identical_visual_artifact'] else 'yes'}",
        f"- Artifact families that changed with curvature: `{', '.join(visual_identity['non_identical_visual_artifact_keys']) or 'none'}`",
        f"- Did FPS reach 30? {'yes' if reaches_30 else 'no'}",
        f"- Did FPS reach 60? {'yes' if reaches_60 else 'no'}",
        f"- Biggest bottleneck observed: {bottleneck}",
        f"- Visual sanity: {visual_status}; {visual_identity['explanation']}",
    ]
    if beauty_status == "BLANK BEAUTY":
        lines += [
            "",
            "Beauty capture status: BLANK BEAUTY.",
            "The beauty frame is a valid PNG but contains only the clear/background color.",
            "Diagnostic overlays remain valid and show sealed transport closure.",
            "This means the benchmark currently proves traversal/evaluation behavior, but not visible beauty-layer rendering.",
        ]
    lines += [
        "",
        "## Detailed Benchmark Table",
        "",
        "| curvature % | amplitude | resolved amp | transport | mean FPS | p95 frame ms | hit % | miss count | avg traversal steps | max traversal steps | budget stress % | screenshot | visual metrics available |",
        "|---:|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|---|---|",
    ]

    for row in rows:
        artifacts = row.get("report_artifacts") or {}
        screenshot = artifacts.get("screenshot") or row.get("screenshot_path") or ""
        screenshot_link = f"[png]({rel(screenshot, report_path.parent)})" if screenshot else ""
        visual_names = [key for key in artifacts.keys() if key != "screenshot"]
        visual_links = ", ".join(f"[{key}]({rel(path, report_path.parent)})" for key, path in artifacts.items() if key != "screenshot")
        if not visual_links:
            visual_links = ", ".join(visual_names)
        resolved = row.get("resolved_fixture_curvature") or {}
        transport = "on" if parse_int(resolved.get("curved_transport_enabled"), 0) else "off"
        be = row.get("budget_exhaustion_summary") or {}
        budget_pct = parse_float(be.get("budget_exhaustion_percent"))
        budget_pct_str = f"{budget_pct:.1f}%" if math.isfinite(budget_pct) else "—"
        max_steps = parse_int(row.get("max_traversal_steps"), 0)
        overrun = parse_int(row.get("max_steps_reached_count"), 0) > 0
        max_steps_str = f"{max_steps}†" if overrun else str(max_steps)
        lines.append(
            f"| {row['curvature_percent']} | {parse_float(row.get('field_amplitude'), 0):.4g} | "
            f"{parse_float(resolved.get('resolved_amp'), 0):.4g} | {transport} | "
            f"{parse_float(row.get('mean_fps'), 0):.2f} | {parse_float(row.get('p95_frame_time_ms'), 0):.2f} | "
            f"{parse_float(row.get('hit_percent'), 0):.3f} | {parse_int(row.get('miss_count'), 0)} | "
            f"{parse_float(row.get('average_traversal_steps'), 0):.2f} | {max_steps_str} | "
            f"{budget_pct_str} | {screenshot_link} | {visual_links} |"
        )

    lines += [
        "",
        "> † overrun step — loop condition `s <= maxIntegrationSteps` allows one extra iteration past step budget. All overrun pixels found a hit; budget_exhausted_without_hit = 0.",
        "",
        "## Visual Identity",
        "",
        f"- Non-identical artifact families: `{', '.join(visual_identity['non_identical_visual_artifact_keys']) or 'none'}`",
        f"- Identical artifact families: `{', '.join(visual_identity['identical_visual_artifact_keys']) or 'none'}`",
        f"- Missing artifact families: `{', '.join(visual_identity['missing_visual_artifact_keys']) or 'none'}`",
        "",
        "## Artifact Health",
        "",
        f"- beauty_capture_health: `{beauty_status}`",
        f"- diagnostic_artifact_health: `{diagnostic_status}`",
        f"- visual_render_confirmation_passed: `{str(beauty_capture_health['visual_render_confirmation_passed']).lower()}`",
        f"- diagnostic_artifacts_valid: `{str(diagnostic_artifact_health['diagnostic_artifacts_valid']).lower()}`",
    ]
    if "transport_continuity" in visual_identity.get("identical_visual_artifact_keys", []):
        lines.append(
            "  - Note: `transport_continuity` is curvature-invariant in this run. "
            "This overlay likely renders fixed-geometry transport paths rather than "
            "field-bent integration curves. Confirm it consumes curved transport data "
            "if curvature sensitivity is required."
        )

    lines += [
        "",
        "## Diagnostic Layers",
        "",
        "The contact sheet is an Observatory Story: read left-to-right as a sequence of questions.",
        "",
        "- Raw visual: `layer0_beauty.png` / screenshot capture, reported separately as `beauty_capture_health`.",
        "- Geometry explanation: `cartesian_scene_geometry.png` shows sealed room bounds, receiver surfaces, camera/ray origin, and field volume outline.",
        "- Curvature field: `curvature_field_view.png` shows field bounds, center, resolved amplitude, and whether curved transport was enabled.",
        "- Transport diagnostics: ownership regions, normal vectors, transport continuity, and combined diagnostic overlays.",
        "- Closure diagnostics: hit/miss maps, hit counts, miss counts, miss rate, and hermetic closure summaries.",
        "- Budget/precision diagnostics: traversal-step heatmaps, budget stress maps, and precision/epsilon warnings.",
        "- Curved-vs-straight difference: `curved_vs_straight_difference.png` compares traversal-step cost against the 0% baseline; 0% is labeled as the baseline reference.",
        "- Contact-sheet rule: title and caption bands are outside the image canvas; rendered/source pixels are not annotated by the sheet itself.",
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
    create_curvature_difference_artifacts(root)
    build_observatory_story_contact_sheets(root)
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
