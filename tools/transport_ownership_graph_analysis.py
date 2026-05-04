#!/usr/bin/env python3
"""Analyze xPRIMEray transport ownership graph precision sweep cells."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
from collections import defaultdict, deque
from pathlib import Path
from typing import Any

import numpy as np


SUMMARY_COLS = [
    "timestamp",
    "phase",
    "step_length",
    "reference_step_length",
    "traversal",
    "stride",
    "roi_label",
    "cell_dir",
    "effective_status",
    "beauty_hash",
    "graph_node_count",
    "graph_edge_count",
    "seam_length_px_total",
    "high_discontinuity_edge_count",
    "stable_basin_count",
    "unsealed_region_count",
    "epsilon_stable_area_percent",
    "precision_floor_histogram",
    "threshold_snap_count",
    "graph_edit_distance_vs_reference",
    "traversal_resonance_delta",
    "missing_optional_fields",
    "plateaued",
    "notes",
]

DIAGNOSTIC_NAMES = {
    "layer0_beauty.png",
    "layer1_cartesian_wireframe.png",
    "layer2_transport_ownership.png",
    "layer3_risk_probe_markers.png",
    "layer4_spacetime_transport_diagram.png",
    "layer5_transport_continuity_vectors.png",
    "combined_diagnostic_overlay.png",
    "diagnostic_overlay_contact_sheet.png",
    "transport_shape_regions_overlay.png",
}


def load_json(path: Path) -> dict[str, Any]:
    try:
        return json.loads(path.read_text())
    except Exception:
        return {}


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def find_beauty(cell: Path) -> Path | None:
    candidates = []
    for path in cell.glob("*.png"):
        if path.name in DIAGNOSTIC_NAMES or path.name.startswith("corner_"):
            continue
        candidates.append(path)
    if not candidates:
        return None
    candidates.sort(key=lambda p: p.stat().st_size, reverse=True)
    return candidates[0]


def find_hit_csv(cell: Path) -> Path | None:
    matches = sorted(cell.glob("*.hit_diagnostics.csv"))
    return matches[0] if matches else None


def parse_float(value: str | None, default: float = math.nan) -> float:
    if value is None or value == "" or value.lower() == "nan":
        return default
    try:
        return float(value)
    except ValueError:
        return default


def parse_int(value: str | None, default: int = 0) -> int:
    if value is None or value == "":
        return default
    try:
        return int(float(value))
    except ValueError:
        return default


def load_hit_fields(path: Path) -> dict[str, Any]:
    rows: list[dict[str, str]] = []
    with path.open(newline="", encoding="utf-8-sig") as handle:
        reader = csv.DictReader(handle)
        fieldnames = set(reader.fieldnames or [])
        max_x = max_y = -1
        for row in reader:
            rows.append(row)
            max_x = max(max_x, parse_int(row.get("x"), -1))
            max_y = max(max_y, parse_int(row.get("y"), -1))
    width, height = max_x + 1, max_y + 1
    had = np.zeros((height, width), dtype=bool)
    collider = np.zeros((height, width), dtype=np.uint64)
    domain = np.zeros((height, width), dtype=np.int32)
    path_length = np.full((height, width), np.nan, dtype=np.float32)
    step_count = np.full((height, width), -1, dtype=np.int32)
    boundary = np.zeros((height, width), dtype=np.int32)
    valued: set[str] = set()
    for row in rows:
        x = parse_int(row.get("x"), -1)
        y = parse_int(row.get("y"), -1)
        if not (0 <= x < width and 0 <= y < height):
            continue
        had[y, x] = str(row.get("had_hit", "0")).lower() in {"1", "true"}
        collider[y, x] = parse_int(row.get("collider_id"), 0)
        domain_value = row.get("domain_id", row.get("curvature_domain_id", ""))
        if domain_value != "":
            domain[y, x] = parse_int(domain_value, 0)
            valued.add("domain_id")
        path_value = row.get("path_length", row.get("accumulated_transport_length", ""))
        if path_value != "":
            path_length[y, x] = parse_float(path_value)
            valued.add("path_length")
        boundary_value = row.get("boundary_event_count", row.get("boundary_events", ""))
        if boundary_value != "":
            boundary[y, x] = parse_int(boundary_value, 0)
            valued.add("boundary_event_count")
        step_value = row.get("step_count", row.get("segment_count", ""))
        if step_value != "":
            step_count[y, x] = parse_int(step_value, -1)
            valued.add("step_count")
        if row.get("portal_event_count", "") != "":
            valued.add("portal_event_count")
        if row.get("throat_event_count", "") != "":
            valued.add("throat_event_count")
    optional = ["domain_id", "path_length", "boundary_event_count", "portal_event_count", "throat_event_count", "step_count"]
    return {
        "width": width,
        "height": height,
        "had": had,
        "collider": collider,
        "domain": domain,
        "path_length": path_length,
        "step_count": step_count,
        "boundary": boundary,
        "fieldnames": fieldnames,
        "missing_optional_fields": [f for f in optional if f not in valued],
    }


def finite_mean(values: list[float]) -> float:
    vals = [v for v in values if math.isfinite(v)]
    return float(np.mean(vals)) if vals else math.nan


def build_graph(fields: dict[str, Any], continuity_csv: Path | None, epsilon: float) -> tuple[list[dict[str, Any]], list[dict[str, Any]], dict[str, Any]]:
    had = fields["had"]
    collider = fields["collider"]
    domain = fields["domain"]
    path_length = fields["path_length"]
    step_count = fields["step_count"]
    boundary = fields["boundary"]
    height, width = had.shape
    labels = np.full((height, width), -1, dtype=np.int32)
    nodes: list[dict[str, Any]] = []
    node_id = 0

    for y0 in range(height):
        for x0 in range(width):
            if labels[y0, x0] >= 0:
                continue
            sig = (bool(had[y0, x0]), int(collider[y0, x0]), int(domain[y0, x0]))
            q: deque[tuple[int, int]] = deque([(x0, y0)])
            labels[y0, x0] = node_id
            pts: list[tuple[int, int]] = []
            while q:
                x, y = q.popleft()
                pts.append((x, y))
                for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                    if not (0 <= nx < width and 0 <= ny < height) or labels[ny, nx] >= 0:
                        continue
                    nsig = (bool(had[ny, nx]), int(collider[ny, nx]), int(domain[ny, nx]))
                    if nsig == sig:
                        labels[ny, nx] = node_id
                        q.append((nx, ny))
            xs = [p[0] for p in pts]
            ys = [p[1] for p in pts]
            pvals = [float(path_length[y, x]) for x, y in pts]
            svals = [float(step_count[y, x]) for x, y in pts if int(step_count[y, x]) >= 0]
            bvals = [float(boundary[y, x]) for x, y in pts]
            nodes.append({
                "node_id": node_id,
                "hit": int(sig[0]),
                "collider_id": sig[1],
                "domain_id": sig[2],
                "area_px": len(pts),
                "centroid_x": round(float(np.mean(xs)), 3),
                "centroid_y": round(float(np.mean(ys)), 3),
                "bbox_x0": min(xs),
                "bbox_y0": min(ys),
                "bbox_x1": max(xs),
                "bbox_y1": max(ys),
                "mean_path_length": round(finite_mean(pvals), 6) if math.isfinite(finite_mean(pvals)) else "",
                "mean_step_count": round(finite_mean(svals), 6) if math.isfinite(finite_mean(svals)) else "",
                "mean_boundary_event_count": round(finite_mean(bvals), 6) if math.isfinite(finite_mean(bvals)) else "",
            })
            node_id += 1

    edge_acc: dict[tuple[int, int], dict[str, Any]] = {}
    for y in range(height):
        for x in range(width):
            a = int(labels[y, x])
            for nx, ny in ((x + 1, y), (x, y + 1)):
                if nx >= width or ny >= height:
                    continue
                b = int(labels[ny, nx])
                if a == b:
                    continue
                key = (a, b) if a < b else (b, a)
                e = edge_acc.setdefault(key, {
                    "node_a": key[0],
                    "node_b": key[1],
                    "seam_length_px": 0,
                    "collider_flip_count": 0,
                    "domain_flip_count": 0,
                    "path_deltas": [],
                    "step_deltas": [],
                    "boundary_deltas": [],
                    "continuity_scores": [],
                })
                e["seam_length_px"] += 1
                e["collider_flip_count"] += int(int(collider[y, x]) != int(collider[ny, nx]) or bool(had[y, x]) != bool(had[ny, nx]))
                e["domain_flip_count"] += int(int(domain[y, x]) != int(domain[ny, nx]))
                if math.isfinite(float(path_length[y, x])) and math.isfinite(float(path_length[ny, nx])):
                    e["path_deltas"].append(abs(float(path_length[y, x]) - float(path_length[ny, nx])))
                if int(step_count[y, x]) >= 0 and int(step_count[ny, nx]) >= 0:
                    e["step_deltas"].append(abs(float(step_count[y, x]) - float(step_count[ny, nx])))
                e["boundary_deltas"].append(abs(float(boundary[y, x]) - float(boundary[ny, nx])))

    if continuity_csv and continuity_csv.exists():
        with continuity_csv.open(newline="") as handle:
            for row in csv.DictReader(handle):
                x = parse_int(row.get("x"), -1)
                y = parse_int(row.get("y"), -1)
                nx = parse_int(row.get("neighbor_x"), -1)
                ny = parse_int(row.get("neighbor_y"), -1)
                if not (0 <= x < width and 0 <= y < height and 0 <= nx < width and 0 <= ny < height):
                    continue
                a, b = int(labels[y, x]), int(labels[ny, nx])
                if a == b:
                    continue
                key = (a, b) if a < b else (b, a)
                if key in edge_acc:
                    edge_acc[key]["continuity_scores"].append(parse_float(row.get("total_transport_discontinuity_score"), 0.0))

    edges: list[dict[str, Any]] = []
    for edge_id, (key, e) in enumerate(sorted(edge_acc.items()), start=1):
        scores = e["continuity_scores"] or [0.0]
        path_d = e["path_deltas"]
        step_d = e["step_deltas"]
        boundary_d = e["boundary_deltas"]
        edges.append({
            "edge_id": edge_id,
            "node_a": e["node_a"],
            "node_b": e["node_b"],
            "seam_length_px": e["seam_length_px"],
            "mean_continuity_discontinuity_score": round(float(np.mean(scores)), 6),
            "max_continuity_discontinuity_score": round(float(np.max(scores)), 6),
            "collider_flip_count": e["collider_flip_count"],
            "domain_flip_count": e["domain_flip_count"],
            "path_length_delta_mean": round(float(np.mean(path_d)), 6) if path_d else "",
            "path_length_delta_max": round(float(np.max(path_d)), 6) if path_d else "",
            "step_count_delta_mean": round(float(np.mean(step_d)), 6) if step_d else "",
            "step_count_delta_max": round(float(np.max(step_d)), 6) if step_d else "",
            "boundary_event_delta_mean": round(float(np.mean(boundary_d)), 6) if boundary_d else "",
            "boundary_event_delta_max": round(float(np.max(boundary_d)), 6) if boundary_d else "",
        })

    hit_area = sum(int(n["area_px"]) for n in nodes if int(n["hit"]) == 1)
    stable_area = sum(
        int(n["area_px"]) for n in nodes
        if int(n["hit"]) == 1 and not any(e for e in edges if (e["node_a"] == n["node_id"] or e["node_b"] == n["node_id"]) and float(e["max_continuity_discontinuity_score"]) > epsilon)
    )
    metrics = {
        "graph_node_count": len(nodes),
        "graph_edge_count": len(edges),
        "seam_length_px_total": int(sum(int(e["seam_length_px"]) for e in edges)),
        "high_discontinuity_edge_count": int(sum(1 for e in edges if float(e["max_continuity_discontinuity_score"]) > epsilon)),
        "stable_basin_count": int(sum(1 for n in nodes if int(n["hit"]) == 1 and int(n["area_px"]) >= 8)),
        "unsealed_region_count": int(sum(1 for e in edges if float(e["max_continuity_discontinuity_score"]) > epsilon)),
        "epsilon_stable_area_percent": round(100.0 * stable_area / max(1, hit_area), 6),
        "precision_floor_histogram": {},
        "threshold_snap_count": 0,
    }
    return nodes, edges, metrics


def write_csv(path: Path, rows: list[dict[str, Any]]) -> None:
    if not rows:
        path.write_text("")
        return
    cols = list(rows[0].keys())
    with path.open("w", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=cols)
        writer.writeheader()
        for row in rows:
            writer.writerow(row)


def graph_distance(metrics: dict[str, Any], ref: dict[str, Any] | None) -> float | str:
    if not ref:
        return ""
    return round(
        abs(metrics["graph_node_count"] - ref["graph_node_count"])
        + abs(metrics["graph_edge_count"] - ref["graph_edge_count"])
        + abs(metrics["seam_length_px_total"] - ref["seam_length_px_total"]) / 100.0
        + abs(metrics["high_discontinuity_edge_count"] - ref["high_discontinuity_edge_count"]),
        6,
    )


def analyze_cell(cell: Path, epsilon: float) -> dict[str, Any] | None:
    meta = load_json(cell / "metadata.json")
    hit_csv = find_hit_csv(cell)
    if not hit_csv:
        return None
    fields = load_hit_fields(hit_csv)
    continuity_csv = cell / "transport_continuity_vectors.csv"
    nodes, edges, metrics = build_graph(fields, continuity_csv if continuity_csv.exists() else None, epsilon)
    write_csv(cell / "ownership_graph_nodes.csv", nodes)
    write_csv(cell / "ownership_graph_edges.csv", edges)

    beauty = find_beauty(cell)
    summary = {
        "diagnostic_only_guardrail": "Transport ownership graphs are analysis only and must not feed render scheduling, hit selection, shading, resolver decisions, or adaptive precision.",
        "cell_dir": str(cell),
        "step_length": str(meta.get("step_length", "")),
        "reference_step_length": str(meta.get("reference_step_length", "0.003125")),
        "traversal": str(meta.get("traversal", "")),
        "stride": int(meta.get("stride", 0) or 0),
        "roi_label": str(meta.get("roi_label", "full_frame_control")),
        "beauty_hash": sha256_file(beauty) if beauty else "",
        "missing_optional_fields": fields["missing_optional_fields"],
        **metrics,
    }
    (cell / "ownership_graph_summary.json").write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n")
    md = [
        "# Transport Ownership Graph Summary",
        "",
        summary["diagnostic_only_guardrail"],
        "",
        f"- nodes: {summary['graph_node_count']}",
        f"- edges: {summary['graph_edge_count']}",
        f"- seam_length_px_total: {summary['seam_length_px_total']}",
        f"- high_discontinuity_edge_count: {summary['high_discontinuity_edge_count']}",
        f"- epsilon_stable_area_percent: {summary['epsilon_stable_area_percent']}",
        f"- missing_optional_fields: {', '.join(summary['missing_optional_fields']) or 'none'}",
    ]
    (cell / "ownership_graph_summary.md").write_text("\n".join(md) + "\n")
    return summary


def discover_cells(root: Path) -> list[Path]:
    cells = []
    for meta in root.glob("**/metadata.json"):
        cell = meta.parent
        if find_hit_csv(cell):
            cells.append(cell)
    return sorted(cells)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("root", type=Path)
    parser.add_argument("--epsilon", type=float, default=0.05)
    args = parser.parse_args()

    root = args.root
    cells = discover_cells(root)
    summaries: list[dict[str, Any]] = []
    for cell in cells:
        result = analyze_cell(cell, args.epsilon)
        if result:
            summaries.append(result)

    refs: dict[tuple[str, int, str], dict[str, Any]] = {}
    for s in summaries:
        if str(s.get("step_length")) == str(s.get("reference_step_length")):
            refs[(str(s.get("traversal")), int(s.get("stride", 0) or 0), str(s.get("roi_label", "")))] = s
    row_stride1: dict[str, dict[str, Any]] = {}
    cb_stride4: dict[str, dict[str, Any]] = {}
    for s in summaries:
        key = str(s.get("step_length"))
        if s.get("traversal") == "row" and int(s.get("stride", 0) or 0) == 1:
            row_stride1[key] = s
        if s.get("traversal") == "checkerboard" and int(s.get("stride", 0) or 0) == 4:
            cb_stride4[key] = s

    rows: list[dict[str, Any]] = []
    for s in summaries:
        ref = refs.get((str(s.get("traversal")), int(s.get("stride", 0) or 0), str(s.get("roi_label", ""))))
        resonance = ""
        step = str(s.get("step_length"))
        if step in row_stride1 and step in cb_stride4:
            resonance = round(float(row_stride1[step]["high_discontinuity_edge_count"]) - float(cb_stride4[step]["high_discontinuity_edge_count"]), 6)
        row = {
            "timestamp": load_json(Path(s["cell_dir"]) / "metadata.json").get("timestamp", ""),
            "phase": load_json(Path(s["cell_dir"]) / "metadata.json").get("phase", "graph_precision_sweep"),
            "step_length": s.get("step_length", ""),
            "reference_step_length": s.get("reference_step_length", ""),
            "traversal": s.get("traversal", ""),
            "stride": s.get("stride", ""),
            "roi_label": s.get("roi_label", ""),
            "cell_dir": s.get("cell_dir", ""),
            "effective_status": (Path(s["cell_dir"]) / "effective_status.txt").read_text().strip() if (Path(s["cell_dir"]) / "effective_status.txt").exists() else "",
            "beauty_hash": s.get("beauty_hash", ""),
            "graph_node_count": s.get("graph_node_count", ""),
            "graph_edge_count": s.get("graph_edge_count", ""),
            "seam_length_px_total": s.get("seam_length_px_total", ""),
            "high_discontinuity_edge_count": s.get("high_discontinuity_edge_count", ""),
            "stable_basin_count": s.get("stable_basin_count", ""),
            "unsealed_region_count": s.get("unsealed_region_count", ""),
            "epsilon_stable_area_percent": s.get("epsilon_stable_area_percent", ""),
            "precision_floor_histogram": json.dumps(s.get("precision_floor_histogram", {}), sort_keys=True),
            "threshold_snap_count": s.get("threshold_snap_count", ""),
            "graph_edit_distance_vs_reference": graph_distance(s, ref),
            "traversal_resonance_delta": resonance,
            "missing_optional_fields": ";".join(s.get("missing_optional_fields", [])),
            "plateaued": load_json(Path(s["cell_dir"]) / "metadata.json").get("plateaued", 0),
            "notes": load_json(Path(s["cell_dir"]) / "metadata.json").get("notes", ""),
        }
        rows.append(row)

    with (root / "graph_sweep_summary.csv").open("w", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=SUMMARY_COLS)
        writer.writeheader()
        for row in rows:
            writer.writerow(row)
    (root / "graph_sweep_summary.json").write_text(json.dumps(rows, indent=2, sort_keys=True) + "\n")
    lines = [
        "# Transport Ownership Boundary Graph Precision Sweep Summary",
        "",
        "Diagnostic-only graph extraction. The graph is not consumed by rendering, scheduling, hit selection, shading, resolver decisions, or adaptive precision.",
        "",
        f"- analyzed_cells: {len(rows)}",
        "",
        "| step | traversal | stride | nodes | edges | seam_px | high_edges | edit_vs_ref | resonance_delta | missing |",
        "|---:|---|---:|---:|---:|---:|---:|---:|---:|---|",
    ]
    for row in rows:
        lines.append(
            f"| {row['step_length']} | `{row['traversal']}` | {row['stride']} | {row['graph_node_count']} | "
            f"{row['graph_edge_count']} | {row['seam_length_px_total']} | {row['high_discontinuity_edge_count']} | "
            f"{row['graph_edit_distance_vs_reference']} | {row['traversal_resonance_delta']} | {row['missing_optional_fields'] or 'none'} |"
        )
    (root / "graph_sweep_summary.md").write_text("\n".join(lines) + "\n")
    print(f"[transport-ownership-graph-analysis] cells={len(rows)} out={root}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
