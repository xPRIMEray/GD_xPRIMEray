#!/usr/bin/env python3
"""Analyze hermetic hit-closure ladder diagnostics.

Hermetic closure validates transport completion within a known scene contract.
It does not establish physical correctness. This tool is post-process only and
must not feed rendering, scheduling, hit selection, shading, resolver scoring,
traversal, or adaptive precision.
"""

from __future__ import annotations

import argparse
import csv
import json
import math
from collections import Counter, deque
from pathlib import Path
from typing import Any

import numpy as np
from PIL import Image, ImageDraw, ImageFont


GUARDRAIL = (
    "Hermetic closure validates transport completion within a known scene "
    "contract. It does not establish physical correctness. Outputs are "
    "diagnostic-only and must not feed the renderer."
)

SUMMARY_COLS = [
    "cell",
    "step_length",
    "steps_per_ray",
    "curvature_strength",
    "traversal",
    "total_pixels",
    "hit_pixel_count",
    "hit_closure_percent",
    "no_hit_pixel_count",
    "budget_exhausted_without_hit_count",
    "max_steps_reached_count",
    "integration_escape_count",
    "unresolved_island_count",
    "closure_basin_count",
    "unstable_closure_basin_count",
    "ownership_graph_node_count",
    "seam_length_px_total",
    "closure_confidence_high",
    "closure_confidence_medium",
    "closure_confidence_low",
    "closure_confidence_unstable",
    "false_plateau_candidate",
    "transport_quality_phase",
    "recommended_next_action",
]


def parse_float(value: Any, default: float = math.nan) -> float:
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


def parse_bool(value: Any) -> bool:
    if isinstance(value, bool):
        return value
    token = str(value or "").strip().lower()
    return token in {"1", "true", "yes", "on"}


def load_json(path: Path | None) -> dict[str, Any]:
    if not path or not path.exists():
        return {}
    try:
        return json.loads(path.read_text())
    except Exception:
        return {}


def load_csv(path: Path | None) -> list[dict[str, str]]:
    if not path or not path.exists():
        return []
    with path.open(newline="", encoding="utf-8-sig") as handle:
        return list(csv.DictReader(handle))


def write_csv(path: Path, rows: list[dict[str, Any]], cols: list[str]) -> None:
    with path.open("w", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=cols)
        writer.writeheader()
        for row in rows:
            writer.writerow({col: row.get(col, "") for col in cols})


def discover_cells(root: Path) -> list[Path]:
    if any(root.glob("*.hit_diagnostics.csv")):
        return [root]
    cells = sorted({p.parent for p in root.glob("**/*.hit_diagnostics.csv")})
    return cells


def find_one(folder: Path, pattern: str) -> Path | None:
    matches = sorted(folder.glob(pattern))
    return matches[0] if matches else None


def infer_meta(folder: Path) -> dict[str, Any]:
    meta = load_json(folder / "metadata.json")
    if meta:
        return meta
    parts = folder.parts
    inferred: dict[str, Any] = {}
    for part in parts:
        if part.startswith("step_"):
            inferred["step_length"] = part.removeprefix("step_")
        elif part.startswith("budget_"):
            inferred["steps_per_ray"] = part.removeprefix("budget_")
        elif part.startswith("curvature_"):
            inferred["curvature_strength"] = part.removeprefix("curvature_").replace("p", ".")
        elif part in {"row", "reverse_row", "column", "tile", "checkerboard"}:
            inferred["traversal"] = part
    return inferred


def read_hit_grid(hit_csv: Path) -> dict[str, Any]:
    rows = load_csv(hit_csv)
    if not rows:
        return {"rows": [], "width": 0, "height": 0}
    xs = [parse_int(r.get("x"), -1) for r in rows]
    ys = [parse_int(r.get("y"), -1) for r in rows]
    max_x = max(xs)
    max_y = max(ys)
    width = max_x + 1
    height = max_y + 1
    had = np.zeros((height, width), dtype=bool)
    budget = np.zeros((height, width), dtype=bool)
    max_steps = np.zeros((height, width), dtype=bool)
    warning = np.zeros((height, width), dtype=bool)
    present = np.zeros((height, width), dtype=bool)
    final_path = np.full((height, width), np.nan, dtype=np.float32)
    domain = np.full((height, width), 0, dtype=np.int64)
    for row in rows:
        x = parse_int(row.get("x"), -1)
        y = parse_int(row.get("y"), -1)
        if x < 0 or y < 0 or y >= height or x >= width:
            continue
        segment_count = parse_int(row.get("segment_count"), 0)
        step_count = parse_int(row.get("step_count"), 0)
        hit_class = str(row.get("hit_class", "")).strip().lower()
        row_was_sampled = (
            segment_count > 0
            or step_count > 0
            or hit_class not in {"", "unknown"}
            or parse_bool(row.get("had_hit"))
            or parse_bool(row.get("budget_exhausted_without_hit"))
            or parse_bool(row.get("max_steps_reached"))
        )
        if not row_was_sampled:
            continue
        present[y, x] = True
        had[y, x] = parse_bool(row.get("had_hit"))
        budget[y, x] = parse_bool(row.get("budget_exhausted_without_hit"))
        max_steps[y, x] = parse_bool(row.get("max_steps_reached"))
        warning[y, x] = parse_bool(row.get("hit_found_after_budget_warning"))
        final_path[y, x] = parse_float(row.get("final_path_length") or row.get("path_length"))
        domain[y, x] = parse_int(row.get("domain_id") or row.get("curvature_domain_id"), 0)
    return {
        "rows": rows,
        "width": width,
        "height": height,
        "present": present,
        "had": had,
        "budget": budget,
        "max_steps": max_steps,
        "warning": warning,
        "final_path": final_path,
        "domain": domain,
    }


def connected_components(mask: np.ndarray) -> list[dict[str, Any]]:
    height, width = mask.shape if mask.size else (0, 0)
    seen = np.zeros_like(mask, dtype=bool)
    comps: list[dict[str, Any]] = []
    for y in range(height):
        for x in range(width):
            if seen[y, x] or not mask[y, x]:
                continue
            q: deque[tuple[int, int]] = deque([(x, y)])
            seen[y, x] = True
            pts: list[tuple[int, int]] = []
            while q:
                px, py = q.popleft()
                pts.append((px, py))
                for nx, ny in ((px + 1, py), (px - 1, py), (px, py + 1), (px, py - 1)):
                    if 0 <= nx < width and 0 <= ny < height and not seen[ny, nx] and mask[ny, nx]:
                        seen[ny, nx] = True
                        q.append((nx, ny))
            xs = [p[0] for p in pts]
            ys = [p[1] for p in pts]
            comps.append({
                "points": pts,
                "area_px": len(pts),
                "bbox_x0": min(xs),
                "bbox_y0": min(ys),
                "bbox_x1": max(xs),
                "bbox_y1": max(ys),
                "centroid_x": sum(xs) / len(xs),
                "centroid_y": sum(ys) / len(ys),
            })
    return comps


def classify_phase(summary: dict[str, Any], prior: dict[str, Any] | None) -> str:
    if int(summary["budget_exhausted_without_hit_count"]) > 0:
        return "budget_saturated"
    if int(summary["no_hit_pixel_count"]) > 0 or int(summary["integration_escape_count"]) > 0 or int(summary["unresolved_island_count"]) > 0:
        return "underresolved"
    if prior:
        prev_closure = float(prior.get("hit_closure_percent", 0))
        if float(summary["hit_closure_percent"]) > prev_closure + 0.01:
            return "converging"
    return "plateau"


def recommended_action(phase: str) -> str:
    return {
        "budget_saturated": "increase max traversal/step budget or use adaptive budget scaling",
        "underresolved": "reduce step size, increase local fixture focus, or inspect closure basins",
        "converging": "continue ladder around neighboring steps and budgets",
        "plateau": "candidate operating window only if closure is complete and budget pressure is flat",
    }.get(phase, "inspect diagnostics")


def analyze_cell(cell: Path, root: Path) -> tuple[dict[str, Any], list[dict[str, Any]], list[dict[str, Any]], dict[str, Any]]:
    hit_csv = find_one(cell, "*.hit_diagnostics.csv")
    if not hit_csv:
        raise FileNotFoundError(f"missing hit diagnostics in {cell}")
    meta = infer_meta(cell)
    grid = read_hit_grid(hit_csv)
    had = grid["had"]
    present = grid["present"]
    budget = grid["budget"]
    max_steps = grid["max_steps"]
    warning = grid["warning"]
    total = int(np.count_nonzero(present))
    hit_count = int(np.count_nonzero(had & present))
    no_hit = present & ~had
    budget_no_hit = no_hit & budget
    integration_escape = no_hit & ~budget

    graph_metrics = load_json(cell / "transport_ownership_graph_metrics.json")
    ownership_graph_node_count = int(graph_metrics.get("graph_node_count", 0) or 0)
    seam_length_px_total = float(graph_metrics.get("seam_length_px_total", 0) or 0)
    unresolved_from_graph = int(graph_metrics.get("unresolved_pixel_count", 0) or 0)

    closed = present & had & ~budget
    high = closed & ~max_steps & ~warning
    medium = closed & (max_steps | warning)
    low = present & had & budget
    unstable = no_hit | budget_no_hit
    failure_mask = unstable | integration_escape
    closure_basins = connected_components(closed)
    failure_islands = connected_components(failure_mask)
    unstable_closure_basins = len(failure_islands)

    islands: list[dict[str, Any]] = []
    for idx, comp in enumerate(failure_islands, start=1):
        pts = comp.pop("points")
        dominant_counter: Counter[str] = Counter()
        for x, y in pts:
            if budget_no_hit[y, x]:
                dominant_counter["budget_exhausted_no_hit"] += 1
            elif integration_escape[y, x]:
                dominant_counter["integration_escape"] += 1
            else:
                dominant_counter["topology_ambiguous"] += 1
        dominant = dominant_counter.most_common(1)[0][0] if dominant_counter else "unknown"
        islands.append({
            "cell": str(cell.relative_to(root)),
            "island_id": idx,
            "closure_oracle_class": dominant,
            "area_px": comp["area_px"],
            "bbox_x0": comp["bbox_x0"],
            "bbox_y0": comp["bbox_y0"],
            "bbox_x1": comp["bbox_x1"],
            "bbox_y1": comp["bbox_y1"],
            "centroid_x": round(comp["centroid_x"], 3),
            "centroid_y": round(comp["centroid_y"], 3),
        })

    escapes: list[dict[str, Any]] = []
    for row in grid["rows"]:
        x = parse_int(row.get("x"), -1)
        y = parse_int(row.get("y"), -1)
        if x < 0 or y < 0 or y >= grid["height"] or x >= grid["width"] or not integration_escape[y, x]:
            continue
        escapes.append({
            "cell": str(cell.relative_to(root)),
            "x": x,
            "y": y,
            "final_direction_x": "",
            "final_direction_y": "",
            "final_direction_z": "",
            "last_valid_position_x": "",
            "last_valid_position_y": "",
            "last_valid_position_z": "",
            "final_domain": row.get("domain_id") or row.get("curvature_domain_id") or "",
            "last_seam_crossed": "",
            "final_path_length": row.get("final_path_length") or row.get("path_length") or "",
            "missing_fields": "final_direction,last_valid_position,last_seam_crossed",
        })

    summary: dict[str, Any] = {
        "cell": str(cell.relative_to(root)),
        "step_length": meta.get("step_length", ""),
        "steps_per_ray": meta.get("steps_per_ray", meta.get("traversal_budget", "")),
        "curvature_strength": meta.get("curvature_strength", ""),
        "traversal": meta.get("traversal", "row"),
        "total_pixels": total,
        "hit_pixel_count": hit_count,
        "hit_closure_percent": round(100.0 * hit_count / total, 6) if total else 0,
        "no_hit_pixel_count": int(np.count_nonzero(no_hit)),
        "budget_exhausted_without_hit_count": int(np.count_nonzero(budget_no_hit)),
        "max_steps_reached_count": int(np.count_nonzero(max_steps & present)),
        "integration_escape_count": int(np.count_nonzero(integration_escape)),
        "unresolved_island_count": max(unresolved_from_graph, len(failure_islands)),
        "closure_basin_count": len(closure_basins),
        "unstable_closure_basin_count": unstable_closure_basins,
        "ownership_graph_node_count": ownership_graph_node_count,
        "seam_length_px_total": round(seam_length_px_total, 3),
        "closure_confidence_high": int(np.count_nonzero(high)),
        "closure_confidence_medium": int(np.count_nonzero(medium)),
        "closure_confidence_low": int(np.count_nonzero(low)),
        "closure_confidence_unstable": int(np.count_nonzero(unstable)),
        "false_plateau_candidate": False,
        "transport_quality_phase": "unknown",
        "recommended_next_action": "",
    }

    return summary, islands, escapes, grid


def update_phase_rows(rows: list[dict[str, Any]]) -> None:
    groups: dict[tuple[str, str, str], list[dict[str, Any]]] = {}
    for row in rows:
        key = (str(row.get("traversal", "")), str(row.get("curvature_strength", "")), str(row.get("steps_per_ray", "")))
        groups.setdefault(key, []).append(row)
    for items in groups.values():
        items.sort(key=lambda r: parse_float(r.get("step_length"), math.inf), reverse=True)
        prior: dict[str, Any] | None = None
        for row in items:
            phase = classify_phase(row, prior)
            row["transport_quality_phase"] = phase
            row["false_plateau_candidate"] = (
                phase == "plateau"
                and (int(row["no_hit_pixel_count"]) > 0 or int(row["budget_exhausted_without_hit_count"]) > 0)
            )
            row["recommended_next_action"] = recommended_action(phase)
            prior = row


def draw_heatmap(path: Path, grid: dict[str, Any], title: str) -> None:
    width, height = grid["width"], grid["height"]
    if width <= 0 or height <= 0:
        return
    scale = max(1, min(4, 640 // max(width, 1)))
    img = Image.new("RGBA", (width, height), (0, 0, 0, 255))
    pix = img.load()
    had = grid["had"]
    present = grid["present"]
    budget = grid["budget"]
    for y in range(height):
        for x in range(width):
            if not present[y, x]:
                pix[x, y] = (32, 32, 40, 255)
            elif had[y, x]:
                pix[x, y] = (20, 90, 70, 255)
            elif budget[y, x]:
                pix[x, y] = (255, 128, 0, 255)
            else:
                pix[x, y] = (255, 40, 90, 255)
    img = img.resize((width * scale, height * scale), Image.Resampling.NEAREST)
    panel_h = img.height + 28
    out = Image.new("RGBA", (img.width, panel_h), (4, 5, 12, 255))
    out.alpha_composite(img, (0, 28))
    draw = ImageDraw.Draw(out)
    draw.text((8, 8), title, fill=(235, 235, 255, 255), font=ImageFont.load_default())
    out.save(path)


def draw_ladder(path: Path, rows: list[dict[str, Any]]) -> None:
    if not rows:
        return
    rows = sorted(rows, key=lambda r: (str(r.get("curvature_strength", "")), str(r.get("steps_per_ray", "")), -parse_float(r.get("step_length"), 0)))
    w = 1120
    row_h = 30
    h = 90 + row_h * len(rows)
    img = Image.new("RGBA", (w, h), (5, 6, 16, 255))
    draw = ImageDraw.Draw(img)
    font = ImageFont.load_default()
    draw.text((24, 18), "Hermetic Closure Ladder", fill=(245, 245, 255, 255), font=font)
    draw.text((24, 38), "scene-contract closure, not physical truth", fill=(150, 160, 180, 255), font=font)
    headers = ["step", "budget", "curv", "trav", "closure %", "unresolved %", "budget %", "phase", "next action"]
    xs = [24, 92, 170, 238, 310, 500, 690, 810, 930]
    for x, header in zip(xs, headers):
        draw.text((x, 68), header, fill=(170, 210, 240, 255), font=font)
    phase_color = {
        "underresolved": (230, 70, 80, 255),
        "converging": (245, 190, 55, 255),
        "plateau": (80, 210, 100, 255),
        "budget_saturated": (160, 80, 230, 255),
    }
    for idx, row in enumerate(rows):
        y = 92 + idx * row_h
        closure = float(row.get("hit_closure_percent", 0))
        total = max(1, int(row.get("total_pixels", 0) or 0))
        unresolved = 100.0 * int(row.get("unresolved_island_count", 0) or 0) / total
        budget = 100.0 * int(row.get("budget_exhausted_without_hit_count", 0) or 0) / total
        phase = str(row.get("transport_quality_phase", "unknown"))
        color = phase_color.get(phase, (180, 180, 190, 255))
        draw.text((xs[0], y), str(row.get("step_length", "")), fill=(230, 230, 240, 255), font=font)
        draw.text((xs[1], y), str(row.get("steps_per_ray", "")), fill=(230, 230, 240, 255), font=font)
        draw.text((xs[2], y), str(row.get("curvature_strength", "")), fill=(230, 230, 240, 255), font=font)
        draw.text((xs[3], y), str(row.get("traversal", "")), fill=(230, 230, 240, 255), font=font)
        draw.rectangle((xs[4], y + 4, xs[4] + int(closure * 1.5), y + 15), fill=(50, 190, 120, 255))
        draw.text((xs[4] + 160, y), f"{closure:0.2f}", fill=(230, 230, 240, 255), font=font)
        draw.rectangle((xs[5], y + 4, xs[5] + int(unresolved * 1.5), y + 15), fill=(230, 70, 120, 255))
        draw.text((xs[5] + 160, y), f"{unresolved:0.2f}", fill=(230, 230, 240, 255), font=font)
        draw.rectangle((xs[6], y + 4, xs[6] + int(budget * 1.5), y + 15), fill=(245, 150, 40, 255))
        draw.text((xs[6] + 160, y), f"{budget:0.2f}", fill=(230, 230, 240, 255), font=font)
        draw.ellipse((xs[7], y + 3, xs[7] + 12, y + 15), fill=color)
        draw.text((xs[7] + 18, y), phase, fill=color, font=font)
        action = str(row.get("recommended_next_action", ""))
        draw.text((xs[8], y), action[:42], fill=(210, 210, 225, 255), font=font)
        if str(row.get("false_plateau_candidate", "")).lower() == "true":
            draw.text((xs[7] + 120, y), "false plateau?", fill=(255, 190, 80, 255), font=font)
    img.save(path)


def find_image(cell: Path, names: list[str]) -> Path | None:
    for name in names:
        p = cell / name
        if p.exists():
            return p
    pngs = [p for p in cell.glob("*.png") if not p.name.startswith(("layer", "combined", "diagnostic", "transport_", "budget_", "ownership_"))]
    return sorted(pngs, key=lambda p: p.stat().st_size, reverse=True)[0] if pngs else None


def make_storyboard(path: Path, root: Path, worst_cell: Path | None, heatmap: Path, ladder: Path) -> None:
    w, h = 320, 180
    labels = [
        "1 rendered reference",
        "2 Cartesian sealed-room projection",
        "3 hit normals / transport vectors",
        "4 ownership seams",
        "5 hit-closure failure heatmap",
        "6 phase / lineage / next action",
    ]
    sources: list[Path | None] = []
    if worst_cell:
        sources = [
            find_image(worst_cell, ["layer0_beauty.png"]),
            worst_cell / "layer1_cartesian_wireframe.png",
            worst_cell / "full_frame_hit_normals.png",
            worst_cell / "ownership_graph_seam_map.png",
            heatmap,
            ladder,
        ]
    else:
        sources = [None, None, None, None, heatmap, ladder]
    out = Image.new("RGBA", (w * 3, h * 2 + 56), (4, 5, 12, 255))
    draw = ImageDraw.Draw(out)
    draw.text((18, 12), "Hermetic Failure Storyboard", fill=(245, 245, 255, 255), font=ImageFont.load_default())
    draw.text((18, 28), "geometry -> transport -> topology -> quality constraints -> evolution", fill=(140, 210, 230, 255), font=ImageFont.load_default())
    for i, src in enumerate(sources):
        x = (i % 3) * w
        y = 56 + (i // 3) * h
        if src and src.exists():
            img = Image.open(src).convert("RGBA")
            img.thumbnail((w, h), Image.Resampling.LANCZOS)
            panel = Image.new("RGBA", (w, h), (10, 12, 20, 255))
            panel.alpha_composite(img, ((w - img.width) // 2, (h - img.height) // 2))
        else:
            panel = Image.new("RGBA", (w, h), (12, 14, 22, 255))
            pd = ImageDraw.Draw(panel)
            pd.text((16, 76), "unavailable", fill=(160, 160, 180, 255), font=ImageFont.load_default())
        out.alpha_composite(panel, (x, y))
        draw.rectangle((x, y, x + w - 1, y + h - 1), outline=(120, 70, 180, 255), width=1)
        draw.text((x + 8, y + 8), labels[i], fill=(245, 245, 255, 255), font=ImageFont.load_default())
    out.save(path)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("root")
    parser.add_argument("--out", default=None)
    args = parser.parse_args()
    root = Path(args.root).resolve()
    out = Path(args.out).resolve() if args.out else root
    out.mkdir(parents=True, exist_ok=True)

    summaries: list[dict[str, Any]] = []
    all_islands: list[dict[str, Any]] = []
    all_escapes: list[dict[str, Any]] = []
    grids: dict[Path, dict[str, Any]] = {}
    errors: list[str] = []
    for cell in discover_cells(root):
        try:
            summary, islands, escapes, grid = analyze_cell(cell, root)
            summaries.append(summary)
            all_islands.extend(islands)
            all_escapes.extend(escapes)
            grids[cell] = grid
        except Exception as exc:
            errors.append(f"{cell}: {exc}")

    update_phase_rows(summaries)
    write_csv(out / "hermetic_hit_closure_summary.csv", summaries, SUMMARY_COLS)
    write_csv(out / "hermetic_failure_islands.csv", all_islands, [
        "cell", "island_id", "closure_oracle_class", "area_px",
        "bbox_x0", "bbox_y0", "bbox_x1", "bbox_y1", "centroid_x", "centroid_y",
    ])
    write_csv(out / "integration_escape_vectors.csv", all_escapes, [
        "cell", "x", "y", "final_direction_x", "final_direction_y", "final_direction_z",
        "last_valid_position_x", "last_valid_position_y", "last_valid_position_z",
        "final_domain", "last_seam_crossed", "final_path_length", "missing_fields",
    ])

    aggregate = {
        "guardrail": GUARDRAIL,
        "cell_count": len(summaries),
        "errors": errors,
        "summary_rows": summaries,
        "failure_island_count": len(all_islands),
        "integration_escape_vector_count": len(all_escapes),
        "phase_counts": dict(Counter(str(r.get("transport_quality_phase", "unknown")) for r in summaries)),
    }
    (out / "hermetic_hit_closure_summary.json").write_text(json.dumps(aggregate, indent=2))

    worst: dict[str, Any] | None = None
    if summaries:
        worst = max(summaries, key=lambda r: int(r.get("no_hit_pixel_count", 0)) + int(r.get("budget_exhausted_without_hit_count", 0)))
        worst_cell = root / str(worst["cell"])
    else:
        worst_cell = None
    if worst_cell and worst_cell in grids:
        draw_heatmap(out / "hermetic_hit_closure_heatmap.png", grids[worst_cell], f"Hit-closure failure heatmap: {worst['cell']}")
    else:
        Image.new("RGBA", (320, 180), (5, 6, 16, 255)).save(out / "hermetic_hit_closure_heatmap.png")
    draw_ladder(out / "hermetic_closure_ladder.png", summaries)
    make_storyboard(out / "hermetic_failure_storyboard.png", root, worst_cell, out / "hermetic_hit_closure_heatmap.png", out / "hermetic_closure_ladder.png")

    md = [
        "# Hermetic Hit Closure Summary",
        "",
        GUARDRAIL,
        "",
        "## Results",
        "",
        f"- Cells analyzed: {len(summaries)}",
        f"- Failure islands: {len(all_islands)}",
        f"- Integration escape vectors: {len(all_escapes)}",
        f"- Phase counts: {dict(Counter(str(r.get('transport_quality_phase', 'unknown')) for r in summaries))}",
        "",
        "## Cell Table",
        "",
        "| cell | step | budget | curvature | traversal | closure % | no-hit | budget no-hit | phase | next action |",
        "|---|---:|---:|---:|---|---:|---:|---:|---|---|",
    ]
    for row in summaries:
        md.append(
            f"| {row['cell']} | {row['step_length']} | {row['steps_per_ray']} | {row['curvature_strength']} | "
            f"{row['traversal']} | {row['hit_closure_percent']} | {row['no_hit_pixel_count']} | "
            f"{row['budget_exhausted_without_hit_count']} | {row['transport_quality_phase']} | "
            f"{row['recommended_next_action']} |"
        )
    if errors:
        md += ["", "## Errors", ""]
        md += [f"- {err}" for err in errors]
    (out / "hermetic_hit_closure_summary.md").write_text("\n".join(md) + "\n")

    rec = [
        "# Hermetic Oracle Recommendations",
        "",
        "This report treats closure as a scene-contract validation signal, not physical truth.",
        "",
    ]
    if summaries:
        for phase, count in Counter(str(r.get("transport_quality_phase", "unknown")) for r in summaries).most_common():
            rec.append(f"- {phase}: {count} cells. {recommended_action(phase)}.")
    else:
        rec.append("- No hit diagnostics were available; rerun with render-test capture diagnostics enabled.")
    (out / "hermetic_oracle_recommendations.md").write_text("\n".join(rec) + "\n")

    print(f"[hermetic-analysis] output={out}")
    print(f"[hermetic-analysis] cells={len(summaries)} failures={len(all_islands)} escapes={len(all_escapes)}")
    print(f"[hermetic-analysis] plot={out / 'hermetic_closure_ladder.png'}")
    return 0 if summaries else 1


if __name__ == "__main__":
    raise SystemExit(main())
