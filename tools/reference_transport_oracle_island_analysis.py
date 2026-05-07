#!/usr/bin/env python3
"""Focused unresolved-island analysis for ReferenceTransportOracle packets."""

from __future__ import annotations

import argparse
import csv
import json
import math
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont


def load_csv(path: Path | None) -> list[dict[str, str]]:
    if path is None or not path.exists():
        return []
    with path.open(newline="", encoding="utf-8-sig") as handle:
        return list(csv.DictReader(handle))


def load_json(path: Path | None) -> dict[str, Any]:
    if path is None or not path.exists():
        return {}
    try:
        return json.loads(path.read_text())
    except Exception:
        return {}


def write_csv(path: Path, rows: list[dict[str, Any]], fieldnames: list[str]) -> None:
    with path.open("w", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        for row in rows:
            writer.writerow({key: row.get(key, "") for key in fieldnames})


def parse_float(value: Any, default: float = 0.0) -> float:
    try:
        if value in ("", None, "nan", "NaN"):
            return default
        return float(value)
    except Exception:
        return default


def parse_int(value: Any, default: int = 0) -> int:
    try:
        if value in ("", None):
            return default
        return int(round(float(value)))
    except Exception:
        return default


def parse_bbox(text: str) -> tuple[int, int, int, int]:
    parts = [int(round(float(p))) for p in text.replace(";", ",").split(",") if p.strip()]
    if len(parts) != 4:
        raise argparse.ArgumentTypeError("bbox must be xmin,ymin,xmax,ymax")
    x0, y0, x1, y1 = parts
    return min(x0, x1), min(y0, y1), max(x0, x1), max(y0, y1)


def find_one(folder: Path, pattern: str) -> Path | None:
    matches = sorted(folder.glob(pattern))
    return matches[0] if matches else None


def stable_bool(row: dict[str, str]) -> bool:
    return row.get("epsilon_stability_class") == "stable"


def classify_pixel(rows: list[dict[str, str]], sealed_step: float) -> str:
    by_step = {parse_float(row.get("production_step_length")): row for row in rows}
    if any(row.get("epsilon_stability_class") == "multi_solution" for row in rows):
        return "multi_solution"
    if any(row.get("oracle_repeat_match") not in {"1", "true", "True"} for row in rows):
        return "multi_solution"
    finest = by_step.get(0.003125) or by_step.get(min(by_step.keys(), default=math.inf))
    sealed = by_step.get(sealed_step)
    if finest and not stable_bool(finest):
        return "extra_fine_required" if rows else "unresolved"
    if any(row.get("epsilon_stability_class") == "threshold_snap" for row in rows):
        return "threshold_snap"
    if sealed and stable_bool(sealed):
        coarser_stable = any(stable_bool(row) and parse_float(row.get("production_step_length")) > sealed_step for row in rows)
        return "plateaued" if finest and stable_bool(finest) and not coarser_stable else "stable"
    if finest and stable_bool(finest):
        return "extra_fine_required"
    return "unresolved"


def first_stable_step(rows: list[dict[str, str]]) -> float:
    stable_steps = [parse_float(row.get("production_step_length"), math.inf) for row in rows if stable_bool(row)]
    if not stable_steps:
        return math.inf
    # Coarsest accepted step: larger step length is cheaper and earlier in the convergence ladder.
    return max(stable_steps)


def row_for_step(rows: list[dict[str, str]], step: float) -> dict[str, str] | None:
    for row in rows:
        if abs(parse_float(row.get("production_step_length")) - step) <= 1e-9:
            return row
    return None


def color_ramp(value: float, max_value: float, mode: str = "risk") -> tuple[int, int, int, int]:
    t = 0.0 if max_value <= 0 else max(0.0, min(1.0, value / max_value))
    if mode == "blue":
        return (20, int(80 + 120 * (1 - t)), int(160 + 95 * t), 245)
    return (int(40 + 215 * t), int(230 * (1.0 - t)), int(255 * (1.0 - 0.6 * t)), 245)


def class_color(label: str) -> tuple[int, int, int, int]:
    return {
        "stable": (45, 220, 105, 245),
        "plateaued": (20, 190, 210, 245),
        "threshold_snap": (255, 210, 40, 245),
        "extra_fine_required": (255, 120, 35, 245),
        "multi_solution": (175, 80, 255, 245),
        "unresolved": (255, 55, 85, 245),
    }.get(label, (180, 180, 180, 245))


def step_color(step: float) -> tuple[int, int, int, int]:
    if not math.isfinite(step):
        return (255, 55, 85, 245)
    ordered = [0.02, 0.018, 0.016, 0.015, 0.014, 0.013, 0.0125, 0.011, 0.010, 0.0075, 0.00625, 0.003125]
    if step not in ordered:
        ordered.append(step)
        ordered.sort(reverse=True)
    i = ordered.index(step)
    t = i / max(1, len(ordered) - 1)
    return (int(50 + 205 * t), int(230 - 150 * t), int(90 + 120 * (1 - t)), 245)


def draw_label(draw: ImageDraw.ImageDraw, xy: tuple[int, int], text: str) -> None:
    font = ImageFont.load_default()
    x, y = xy
    box = draw.textbbox((x, y), text, font=font)
    draw.rectangle((box[0] - 2, box[1] - 2, box[2] + 2, box[3] + 2), fill=(0, 0, 0, 190))
    draw.text((x, y), text, fill=(255, 255, 255, 255), font=font)


def draw_patch_map(
    folder: Path,
    name: str,
    title: str,
    bbox: tuple[int, int, int, int],
    pixels: dict[tuple[int, int], dict[str, Any]],
    value_key: str,
    color_mode: str = "risk",
) -> None:
    x0, y0, x1, y1 = bbox
    scale = 18
    pad = 42
    width = (x1 - x0 + 1) * scale + pad * 2
    height = (y1 - y0 + 1) * scale + pad * 2 + 20
    img = Image.new("RGBA", (width, height), (18, 20, 24, 255))
    draw = ImageDraw.Draw(img)
    draw_label(draw, (8, 8), title)
    vals = [parse_float(p.get(value_key)) for p in pixels.values()]
    max_value = max(vals) if vals else 0.0
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            px = pad + (x - x0) * scale
            py = pad + (y - y0) * scale + 20
            rec = pixels.get((x, y), {})
            if value_key == "classification":
                color = class_color(str(rec.get(value_key, "missing")))
            elif value_key == "first_stable_step":
                color = step_color(parse_float(rec.get(value_key), math.inf))
            elif value_key == "ownership_transition":
                color = (255, 60, 210, 245) if parse_float(rec.get(value_key), 0) > 0 else (45, 220, 105, 245)
            else:
                color = color_ramp(parse_float(rec.get(value_key)), max_value, color_mode)
            if not rec:
                color = (55, 58, 66, 245)
            draw.rectangle((px, py, px + scale - 2, py + scale - 2), fill=color)
    for x in range(x0, x1 + 1):
        px = pad + (x - x0) * scale
        draw.text((px, pad - 12), str(x), fill=(190, 190, 190, 255), font=ImageFont.load_default())
    for y in range(y0, y1 + 1):
        py = pad + (y - y0) * scale + 20
        draw.text((8, py), str(y), fill=(190, 190, 190, 255), font=ImageFont.load_default())
    img.save(folder / name)


def draw_ladder(folder: Path, bbox: tuple[int, int, int, int], by_pixel: dict[tuple[int, int], list[dict[str, str]]]) -> None:
    steps = sorted({parse_float(row.get("production_step_length")) for rows in by_pixel.values() for row in rows}, reverse=True)
    if not steps:
        return
    x0, y0, x1, y1 = bbox
    scale = 10
    pad = 28
    panel_w = (x1 - x0 + 1) * scale + pad
    panel_h = (y1 - y0 + 1) * scale + pad + 18
    sheet = Image.new("RGBA", (panel_w * len(steps), panel_h), (18, 20, 24, 255))
    for i, step in enumerate(steps):
        panel = Image.new("RGBA", (panel_w, panel_h), (18, 20, 24, 255))
        draw = ImageDraw.Draw(panel)
        draw_label(draw, (4, 4), f"step {step:g}")
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                row = row_for_step(by_pixel.get((x, y), []), step)
                label = row.get("epsilon_stability_class", "missing") if row else "missing"
                color = class_color(label)
                if not row:
                    color = (55, 58, 66, 245)
                px = pad + (x - x0) * scale
                py = pad + (y - y0) * scale + 18
                draw.rectangle((px, py, px + scale - 2, py + scale - 2), fill=color)
        sheet.alpha_composite(panel, (i * panel_w, 0))
    sheet.save(folder / "island_convergence_ladder.png")


def bbox_overlap(row: dict[str, str], bbox: tuple[int, int, int, int]) -> bool:
    x0, y0, x1, y1 = bbox
    rx0 = parse_int(row.get("bbox_x0", row.get("min_x", row.get("x", 0))))
    ry0 = parse_int(row.get("bbox_y0", row.get("min_y", row.get("y", 0))))
    rx1 = parse_int(row.get("bbox_x1", row.get("max_x", row.get("x", 0))))
    ry1 = parse_int(row.get("bbox_y1", row.get("max_y", row.get("y", 0))))
    return not (rx1 < x0 or rx0 > x1 or ry1 < y0 or ry0 > y1)


def filter_local_diagnostics(folder: Path, patch_bbox: tuple[int, int, int, int]) -> tuple[int, int]:
    x0, y0, x1, y1 = patch_bbox
    continuity = load_csv(find_one(folder, "transport_continuity_vectors.csv"))
    local_vectors = []
    for row in continuity:
        x = parse_int(row.get("x"), -999)
        y = parse_int(row.get("y"), -999)
        nx = parse_int(row.get("neighbor_x"), x)
        ny = parse_int(row.get("neighbor_y"), y)
        if x0 <= x <= x1 and y0 <= y <= y1 and x0 <= nx <= x1 and y0 <= ny <= y1:
            local_vectors.append(row)
    if continuity:
        write_csv(folder / "local_continuity_vectors.csv", local_vectors, list(continuity[0].keys()))
    else:
        write_csv(folder / "local_continuity_vectors.csv", [], ["x", "y", "neighbor_x", "neighbor_y", "total_transport_discontinuity_score"])

    regions = load_csv(find_one(folder, "transport_shape_regions.csv"))
    local_regions = [row for row in regions if bbox_overlap(row, patch_bbox)]
    if regions:
        write_csv(folder / "local_transport_shape_regions.csv", local_regions, list(regions[0].keys()))
    else:
        write_csv(folder / "local_transport_shape_regions.csv", [], ["region_id", "collider_id", "area", "centroid_x", "centroid_y", "bbox_x0", "bbox_y0", "bbox_x1", "bbox_y1"])
    return len(local_vectors), len(local_regions)


def analyze(folder: Path, patch_bbox: tuple[int, int, int, int], target_bbox: tuple[int, int, int, int], sealed_step: float) -> int:
    metadata = load_json(find_one(folder, "*.reference_transport_oracle.json"))
    comparisons = load_csv(find_one(folder, "*.reference_transport_oracle_comparisons.csv"))
    cost_rows = load_csv(find_one(folder, "*.reference_transport_oracle_cost_curves.csv"))
    if not comparisons:
        raise SystemExit(f"No ReferenceTransportOracle comparisons found in {folder}")

    by_pixel: dict[tuple[int, int], list[dict[str, str]]] = defaultdict(list)
    for row in comparisons:
        by_pixel[(parse_int(row.get("pixel_x")), parse_int(row.get("pixel_y")))].append(row)

    pixel_records: dict[tuple[int, int], dict[str, Any]] = {}
    step_counts: Counter[str] = Counter()
    class_counts: Counter[str] = Counter()
    unresolved_at_finest: list[tuple[int, int]] = []
    sealed_count = 0
    for pixel, rows in sorted(by_pixel.items()):
        fs = first_stable_step(rows)
        cls = classify_pixel(rows, sealed_step)
        class_counts[cls] += 1
        step_counts["never" if not math.isfinite(fs) else f"{fs:g}"] += 1
        sealed_row = row_for_step(rows, sealed_step)
        fine_row = row_for_step(rows, 0.003125)
        if sealed_row and stable_bool(sealed_row):
            sealed_count += 1
        if fine_row and not stable_bool(fine_row):
            unresolved_at_finest.append(pixel)
        max_risk = max(parse_float(r.get("decision_risk")) for r in rows)
        max_path = max(abs(parse_float(r.get("path_length_delta"))) for r in rows)
        max_normal = max(abs(parse_float(r.get("normal_angle_delta"))) for r in rows)
        ownership_transition = int(any(r.get("collider_match") not in {"1", "true", "True"} or r.get("domain_match") not in {"1", "true", "True"} for r in rows))
        row_00625 = row_for_step(rows, 0.00625)
        row_003125 = row_for_step(rows, 0.003125)
        pixel_records[pixel] = {
            "x": pixel[0],
            "y": pixel[1],
            "classification": cls,
            "first_stable_step": fs if math.isfinite(fs) else "",
            "max_decision_risk": max_risk,
            "max_path_length_delta": max_path,
            "max_normal_angle_delta": max_normal,
            "ownership_transition": ownership_transition,
            "stable_at_0.00625": int(bool(row_00625 and stable_bool(row_00625))),
            "stable_at_0.003125": int(bool(row_003125 and stable_bool(row_003125))),
            "risk_0.00625": parse_float(row_00625.get("decision_risk")) if row_00625 else "",
            "risk_0.003125": parse_float(row_003125.get("decision_risk")) if row_003125 else "",
            "path_delta_0.00625": parse_float(row_00625.get("path_length_delta")) if row_00625 else "",
            "path_delta_0.003125": parse_float(row_003125.get("path_length_delta")) if row_003125 else "",
            "normal_delta_0.00625": parse_float(row_00625.get("normal_angle_delta")) if row_00625 else "",
            "normal_delta_0.003125": parse_float(row_003125.get("normal_angle_delta")) if row_003125 else "",
        }

    local_vector_count, local_region_count = filter_local_diagnostics(folder, patch_bbox)

    rows_out = [pixel_records[p] for p in sorted(pixel_records)]
    columns = [
        "x", "y", "classification", "first_stable_step", "max_decision_risk",
        "max_path_length_delta", "max_normal_angle_delta", "ownership_transition",
        "stable_at_0.00625", "stable_at_0.003125", "risk_0.00625", "risk_0.003125",
        "path_delta_0.00625", "path_delta_0.003125", "normal_delta_0.00625", "normal_delta_0.003125",
    ]
    write_csv(folder / "unresolved_island_summary.csv", rows_out, columns)

    all_sealed = sealed_count == len(by_pixel) and bool(by_pixel)
    compare_pairs = []
    for rec in pixel_records.values():
        if rec["risk_0.00625"] != "" and rec["risk_0.003125"] != "":
            compare_pairs.append(abs(float(rec["risk_0.00625"]) - float(rec["risk_0.003125"])))
    repeat_failures = sum(1 for row in comparisons if row.get("oracle_repeat_match") not in {"1", "true", "True"})
    summary = {
        "study": "reference_transport_oracle_unresolved_island",
        "diagnostic_only": True,
        "guardrail": "Outputs must not feed rendering, scheduling, hit selection, shading, resolver decisions, traversal, or adaptive precision.",
        "sample_count": len(by_pixel),
        "comparison_count": len(comparisons),
        "cost_curve_rows": len(cost_rows),
        "oracle_step_length": metadata.get("oracle_step_length", ""),
        "target_bbox": {"x0": target_bbox[0], "y0": target_bbox[1], "x1": target_bbox[2], "y1": target_bbox[3]},
        "local_patch_bbox": {"x0": patch_bbox[0], "y0": patch_bbox[1], "x1": patch_bbox[2], "y1": patch_bbox[3]},
        "classification_counts": dict(sorted(class_counts.items())),
        "first_stable_step_counts": dict(sorted(step_counts.items())),
        "sealed_at_0.00625": all_sealed,
        "sealed_step": sealed_step,
        "stable_at_0.00625_count": sealed_count,
        "unresolved_at_0.003125_count": len(unresolved_at_finest),
        "oracle_replay_failure_count": repeat_failures,
        "risk_delta_0.00625_vs_0.003125_mean": sum(compare_pairs) / max(1, len(compare_pairs)),
        "risk_delta_0.00625_vs_0.003125_max": max(compare_pairs) if compare_pairs else 0.0,
        "local_continuity_vector_count": local_vector_count,
        "local_transport_shape_region_count": local_region_count,
    }
    (folder / "unresolved_island_summary.json").write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n")
    with (folder / "extra_fine_required_pixels.csv").open("w", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(["x", "y"])
        for x, y in unresolved_at_finest:
            writer.writerow([x, y])

    draw_patch_map(folder, "first_stable_step_map.png", "First stable production step", target_bbox, pixel_records, "first_stable_step")
    draw_patch_map(folder, "decision_risk_gradient.png", "Max decision-risk gradient", target_bbox, pixel_records, "max_decision_risk")
    draw_patch_map(folder, "path_length_delta_map.png", "Max path-length delta", target_bbox, pixel_records, "max_path_length_delta", "blue")
    draw_patch_map(folder, "normal_angle_delta_map.png", "Max normal-angle delta", target_bbox, pixel_records, "max_normal_angle_delta")
    draw_patch_map(folder, "ownership_transition_map.png", "Ownership transition map", target_bbox, pixel_records, "ownership_transition")
    draw_ladder(folder, target_bbox, by_pixel)

    lines = [
        "# ReferenceTransportOracle Unresolved-Island Refinement",
        "",
        "Diagnostic-only renderer validation. This is a best-known renderer-reference transport comparison, not physical ground truth.",
        "",
        f"- Samples: {len(by_pixel)}",
        f"- Comparisons: {len(comparisons)}",
        f"- Oracle replay failures: {repeat_failures}",
        f"- Local continuity vectors in x={patch_bbox[0]}..{patch_bbox[2]}, y={patch_bbox[1]}..{patch_bbox[3]}: {local_vector_count}",
        f"- Local transport shape regions touching patch: {local_region_count}",
        f"- Sealed at 0.00625: {str(all_sealed).lower()}",
        f"- Unresolved at 0.003125: {len(unresolved_at_finest)}",
        "",
        "## Pixel Classes",
        "",
    ]
    for key, value in sorted(class_counts.items()):
        lines.append(f"- {key}: {value}")
    lines.extend(["", "## First Stable Step", ""])
    for key, value in sorted(step_counts.items()):
        lines.append(f"- {key}: {value}")
    lines.extend([
        "",
        "## Step Comparisons",
        "",
        f"- Mean absolute decision-risk delta, 0.00625 vs 0.003125: {summary['risk_delta_0.00625_vs_0.003125_mean']:.6f}",
        f"- Max absolute decision-risk delta, 0.00625 vs 0.003125: {summary['risk_delta_0.00625_vs_0.003125_max']:.6f}",
        "",
        "## Guardrail",
        "",
        "- This analyzer writes diagnostics only. It does not smooth, alter beauty output, or feed any render decision.",
    ])
    if all_sealed:
        lines.extend(["", "## Stopping Rule", "", "- The island is sealed at 0.00625 for the sampled patch. Extra-fine rerun is not required by this packet."])
    elif unresolved_at_finest:
        lines.extend(["", "## Stopping Rule", "", "- Some pixels remain unresolved at 0.003125. Use ORACLE_ISLAND_EXTRA_FINE=1 to rerun only those pixel centers with the extra-fine oracle step."])
    (folder / "unresolved_island_summary.md").write_text("\n".join(lines) + "\n")

    print(f"[reference-transport-oracle-island] folder={folder} samples={len(by_pixel)} sealed_at_0.00625={all_sealed} unresolved_finest={len(unresolved_at_finest)}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("folder", type=Path)
    parser.add_argument("--patch-bbox", type=parse_bbox, default=parse_bbox("32,27,48,43"))
    parser.add_argument("--target-bbox", type=parse_bbox, default=parse_bbox("36,31,44,37"))
    parser.add_argument("--sealed-step", type=float, default=0.00625)
    args = parser.parse_args()
    return analyze(args.folder, args.patch_bbox, args.target_bbox, args.sealed_step)


if __name__ == "__main__":
    raise SystemExit(main())
