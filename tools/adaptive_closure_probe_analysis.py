#!/usr/bin/env python3
"""Prototype adaptive closure recovery analysis for hermetic fixtures.

This is instrumentation only. It compares a normal/baseline hermetic capture
against a higher-budget retry capture and scores recovery only inside baseline
closure-failure regions. It does not modify beauty rendering, scheduling, hit
selection, shading, resolver behavior, traversal, or adaptive precision.
"""

from __future__ import annotations

import argparse
import csv
import json
import math
from collections import deque
from pathlib import Path
from typing import Any

import numpy as np
from PIL import Image, ImageDraw, ImageFont


GUARDRAIL = (
    "adaptive_closure_probe is prototype instrumentation only. It preserves "
    "baseline outputs and must not feed production scheduling, hit selection, "
    "shading, resolver decisions, traversal, or adaptive precision."
)


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
    return sorted({p.parent for p in root.glob("**/*.hit_diagnostics.csv")})


def find_one(folder: Path, pattern: str) -> Path | None:
    matches = sorted(folder.glob(pattern))
    return matches[0] if matches else None


def sampled(row: dict[str, str]) -> bool:
    hit_class = str(row.get("hit_class", "")).strip().lower()
    return (
        parse_int(row.get("segment_count"), 0) > 0
        or parse_int(row.get("step_count"), 0) > 0
        or hit_class not in {"", "unknown"}
        or parse_bool(row.get("had_hit"))
        or parse_bool(row.get("budget_exhausted_without_hit"))
        or parse_bool(row.get("max_steps_reached"))
    )


def load_hit_map(cell: Path) -> dict[str, Any]:
    hit_csv = find_one(cell, "*.hit_diagnostics.csv")
    rows = load_csv(hit_csv)
    if not rows:
        raise FileNotFoundError(f"missing hit diagnostics in {cell}")
    coords: dict[tuple[int, int], dict[str, Any]] = {}
    max_x = max(parse_int(r.get("x"), -1) for r in rows)
    max_y = max(parse_int(r.get("y"), -1) for r in rows)
    width, height = max_x + 1, max_y + 1
    had = np.zeros((height, width), dtype=bool)
    present = np.zeros((height, width), dtype=bool)
    budget = np.zeros((height, width), dtype=bool)
    max_steps = np.zeros((height, width), dtype=bool)
    final_steps = np.zeros((height, width), dtype=np.int32)
    for row in rows:
        x = parse_int(row.get("x"), -1)
        y = parse_int(row.get("y"), -1)
        if x < 0 or y < 0 or x >= width or y >= height or not sampled(row):
            continue
        present[y, x] = True
        had[y, x] = parse_bool(row.get("had_hit"))
        budget[y, x] = parse_bool(row.get("budget_exhausted_without_hit"))
        max_steps[y, x] = parse_bool(row.get("max_steps_reached"))
        final_steps[y, x] = parse_int(row.get("final_step_count") or row.get("step_count"), 0)
        coords[(x, y)] = {
            "had_hit": had[y, x],
            "budget": budget[y, x],
            "max_steps": max_steps[y, x],
            "final_steps": int(final_steps[y, x]),
            "collider_id": row.get("collider_id") or row.get("current_collider_id") or "",
            "domain_id": row.get("domain_id") or row.get("curvature_domain_id") or "",
        }
    return {
        "cell": cell,
        "meta": load_json(cell / "metadata.json"),
        "coords": coords,
        "width": width,
        "height": height,
        "present": present,
        "had": had,
        "budget": budget,
        "max_steps": max_steps,
        "final_steps": final_steps,
        "graph": load_json(cell / "transport_ownership_graph_metrics.json"),
    }


def failure_components(mask: np.ndarray) -> list[dict[str, Any]]:
    height, width = mask.shape
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
                    if 0 <= nx < width and 0 <= ny < height and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        q.append((nx, ny))
            xs = [p[0] for p in pts]
            ys = [p[1] for p in pts]
            comps.append({
                "points": pts,
                "area_px": len(pts),
                "bbox": (min(xs), min(ys), max(xs), max(ys)),
                "centroid": (sum(xs) / len(xs), sum(ys) / len(ys)),
            })
    return comps


def group_key(cell: dict[str, Any]) -> tuple[str, str, str]:
    meta = cell["meta"]
    return (
        str(meta.get("step_length", "")),
        str(meta.get("curvature_strength", "")),
        str(meta.get("traversal", "")),
    )


def budget_value(cell: dict[str, Any]) -> int:
    return parse_int(cell["meta"].get("steps_per_ray") or cell["meta"].get("traversal_budget"), 0)


def draw_recovery_heatmap(path: Path, baseline: dict[str, Any], retry: dict[str, Any], recovered: set[tuple[int, int]], still_failed: set[tuple[int, int]]) -> None:
    width, height = baseline["width"], baseline["height"]
    scale = max(1, min(8, 720 // max(width, 1)))
    img = Image.new("RGBA", (width, height), (8, 9, 16, 255))
    pix = img.load()
    failed = set(recovered) | set(still_failed)
    for y in range(height):
        for x in range(width):
            if (x, y) in recovered:
                pix[x, y] = (30, 230, 110, 255)
            elif (x, y) in still_failed:
                pix[x, y] = (255, 55, 105, 255)
            elif baseline["had"][y, x]:
                pix[x, y] = (30, 80, 90, 255)
            elif baseline["budget"][y, x]:
                pix[x, y] = (255, 155, 40, 255)
            elif (x, y) in failed:
                pix[x, y] = (255, 80, 140, 255)
    img = img.resize((width * scale, height * scale), Image.Resampling.NEAREST)
    out = Image.new("RGBA", (img.width, img.height + 34), (5, 6, 16, 255))
    out.alpha_composite(img, (0, 34))
    draw = ImageDraw.Draw(out)
    draw.text((8, 8), "Adaptive closure recovery heatmap: green=recovered, magenta=still failed", fill=(235, 240, 255, 255), font=ImageFont.load_default())
    out.save(path)


def make_storyboard(path: Path, root: Path, heatmap: Path, efficiency: dict[str, Any]) -> None:
    panels = [
        ("baseline failure regions", root / "hermetic_hit_closure_heatmap.png"),
        ("adaptive recovery delta", heatmap),
        ("closure ladder", root / "hermetic_closure_ladder.png"),
        ("failure storyboard", root / "hermetic_failure_storyboard.png"),
    ]
    w, h = 360, 210
    out = Image.new("RGBA", (w * 2, h * 2 + 48), (5, 6, 16, 255))
    draw = ImageDraw.Draw(out)
    draw.text((18, 12), "Closure Recovery Storyboard", fill=(245, 245, 255, 255), font=ImageFont.load_default())
    draw.text((18, 28), f"recovered={efficiency.get('recovered_pixel_count', 0)} ratio={efficiency.get('recovery_efficiency_ratio', 0)}", fill=(150, 220, 210, 255), font=ImageFont.load_default())
    for idx, (label, src) in enumerate(panels):
        x = (idx % 2) * w
        y = 48 + (idx // 2) * h
        panel = Image.new("RGBA", (w, h), (10, 12, 20, 255))
        if src.exists():
            img = Image.open(src).convert("RGBA")
            img.thumbnail((w, h), Image.Resampling.LANCZOS)
            panel.alpha_composite(img, ((w - img.width) // 2, (h - img.height) // 2))
        else:
            ImageDraw.Draw(panel).text((18, 92), "unavailable", fill=(160, 160, 180, 255), font=ImageFont.load_default())
        out.alpha_composite(panel, (x, y))
        draw.rectangle((x, y, x + w - 1, y + h - 1), outline=(120, 70, 180, 255), width=1)
        draw.text((x + 8, y + 8), label, fill=(245, 245, 255, 255), font=ImageFont.load_default())
    out.save(path)


def analyze_pair(baseline: dict[str, Any], retry: dict[str, Any], root: Path) -> tuple[list[dict[str, Any]], dict[str, Any], set[tuple[int, int]], set[tuple[int, int]]]:
    base_failure = baseline["present"] & ~baseline["had"]
    comps = failure_components(base_failure)
    component_by_pixel: dict[tuple[int, int], int] = {}
    for idx, comp in enumerate(comps, start=1):
        for pt in comp["points"]:
            component_by_pixel[pt] = idx
    recovered: set[tuple[int, int]] = set()
    still_failed: set[tuple[int, int]] = set()
    rows: list[dict[str, Any]] = []
    baseline_cost = 0
    retry_cost = 0
    for coord, base_row in sorted(baseline["coords"].items(), key=lambda item: (item[0][1], item[0][0])):
        x, y = coord
        if not base_failure[y, x]:
            continue
        retry_row = retry["coords"].get(coord)
        retry_hit = bool(retry_row and retry_row.get("had_hit"))
        if retry_hit:
            recovered.add(coord)
        else:
            still_failed.add(coord)
        base_steps = int(base_row.get("final_steps", 0) or 0)
        retry_steps = int(retry_row.get("final_steps", 0) or 0) if retry_row else 0
        baseline_cost += base_steps
        retry_cost += retry_steps
        rows.append({
            "x": x,
            "y": y,
            "retry_region_id": component_by_pixel.get(coord, ""),
            "baseline_had_hit": int(bool(base_row.get("had_hit"))),
            "retry_had_hit": int(retry_hit),
            "recovered": int(retry_hit),
            "baseline_budget_exhausted_without_hit": int(bool(base_row.get("budget"))),
            "retry_budget_exhausted_without_hit": int(bool(retry_row and retry_row.get("budget"))),
            "baseline_final_step_count": base_steps,
            "retry_final_step_count": retry_steps,
            "additional_step_cost": max(0, retry_steps - base_steps),
            "baseline_collider_id": base_row.get("collider_id", ""),
            "retry_collider_id": retry_row.get("collider_id", "") if retry_row else "",
            "baseline_domain_id": base_row.get("domain_id", ""),
            "retry_domain_id": retry_row.get("domain_id", "") if retry_row else "",
        })

    base_graph = baseline.get("graph") or {}
    retry_graph = retry.get("graph") or {}
    graph_delta = {
        "node_count_delta": parse_int(retry_graph.get("graph_node_count"), 0) - parse_int(base_graph.get("graph_node_count"), 0),
        "edge_count_delta": parse_int(retry_graph.get("graph_edge_count"), 0) - parse_int(base_graph.get("graph_edge_count"), 0),
        "seam_length_delta": parse_float(retry_graph.get("seam_length_px_total"), 0) - parse_float(base_graph.get("seam_length_px_total"), 0),
        "high_discontinuity_edge_delta": parse_int(retry_graph.get("high_discontinuity_edge_count"), 0) - parse_int(base_graph.get("high_discontinuity_edge_count"), 0),
    }
    additional_cost = max(0, retry_cost - baseline_cost)
    efficiency = {
        "guardrail": GUARDRAIL,
        "baseline_cell": str(baseline["cell"].relative_to(root)),
        "retry_cell": str(retry["cell"].relative_to(root)),
        "step_length": baseline["meta"].get("step_length", ""),
        "curvature_strength": baseline["meta"].get("curvature_strength", ""),
        "traversal": baseline["meta"].get("traversal", ""),
        "baseline_steps_per_ray": budget_value(baseline),
        "retry_steps_per_ray": budget_value(retry),
        "retry_region_count": len(comps),
        "failed_pixel_count_before": int(np.count_nonzero(base_failure)),
        "recovered_pixel_count": len(recovered),
        "still_unresolved_pixel_count_after": len(still_failed),
        "unresolved_before_after": [int(np.count_nonzero(base_failure)), len(still_failed)],
        "recovery_budget_cost": additional_cost,
        "recovery_efficiency_ratio": round(len(recovered) / additional_cost, 9) if additional_cost > 0 else 0,
        "seam_delta_after_recovery": graph_delta["seam_length_delta"],
        "graph_delta_after_recovery": graph_delta,
    }
    return rows, efficiency, recovered, still_failed


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("root", type=Path)
    parser.add_argument("--out", type=Path, default=None)
    args = parser.parse_args()
    root = args.root.resolve()
    out = (args.out.resolve() if args.out else root)
    out.mkdir(parents=True, exist_ok=True)

    cells = [load_hit_map(cell) for cell in discover_cells(root)]
    groups: dict[tuple[str, str, str], list[dict[str, Any]]] = {}
    for cell in cells:
        groups.setdefault(group_key(cell), []).append(cell)

    all_delta_rows: list[dict[str, Any]] = []
    efficiencies: list[dict[str, Any]] = []
    first_heatmap: Path | None = None
    for key, items in groups.items():
        items = sorted(items, key=budget_value)
        if len(items) < 2:
            continue
        baseline = items[0]
        retry = items[-1]
        rows, efficiency, recovered, still_failed = analyze_pair(baseline, retry, root)
        all_delta_rows.extend(rows)
        efficiencies.append(efficiency)
        suffix = f"step_{key[0]}_curv_{key[1]}_{key[2]}".replace("/", "_").replace(".", "p")
        heatmap = out / f"adaptive_closure_recovery_heatmap_{suffix}.png"
        draw_recovery_heatmap(heatmap, baseline, retry, recovered, still_failed)
        if first_heatmap is None:
            first_heatmap = heatmap

    write_csv(out / "closure_recovery_delta.csv", all_delta_rows, [
        "x", "y", "retry_region_id", "baseline_had_hit", "retry_had_hit", "recovered",
        "baseline_budget_exhausted_without_hit", "retry_budget_exhausted_without_hit",
        "baseline_final_step_count", "retry_final_step_count", "additional_step_cost",
        "baseline_collider_id", "retry_collider_id", "baseline_domain_id", "retry_domain_id",
    ])
    summary = {
        "guardrail": GUARDRAIL,
        "pair_count": len(efficiencies),
        "recovered_pixel_count": sum(int(e.get("recovered_pixel_count", 0)) for e in efficiencies),
        "recovery_budget_cost": sum(int(e.get("recovery_budget_cost", 0)) for e in efficiencies),
        "recovery_efficiency_ratio": 0,
        "pairs": efficiencies,
    }
    if summary["recovery_budget_cost"]:
        summary["recovery_efficiency_ratio"] = round(summary["recovered_pixel_count"] / summary["recovery_budget_cost"], 9)
    (out / "closure_recovery_efficiency.json").write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n")

    if first_heatmap:
        target = out / "adaptive_closure_recovery_heatmap.png"
        if first_heatmap != target:
            target.write_bytes(first_heatmap.read_bytes())
        make_storyboard(out / "closure_recovery_storyboard.png", out, target, summary)
    else:
        Image.new("RGBA", (320, 180), (5, 6, 16, 255)).save(out / "adaptive_closure_recovery_heatmap.png")
        make_storyboard(out / "closure_recovery_storyboard.png", out, out / "adaptive_closure_recovery_heatmap.png", summary)

    print(f"[adaptive-closure-probe] output={out}")
    print(f"[adaptive-closure-probe] pairs={len(efficiencies)} recovered={summary['recovered_pixel_count']} cost={summary['recovery_budget_cost']}")
    print(f"[adaptive-closure-probe] heatmap={out / 'adaptive_closure_recovery_heatmap.png'}")
    return 0 if efficiencies else 1


if __name__ == "__main__":
    raise SystemExit(main())
