#!/usr/bin/env python3
"""Extract xPRIMEray transport ownership topology graphs from diagnostics.

Transport Ownership Graph = nodes are connected components of equivalent
renderer transport signatures; edges are observed adjacency/seam relations
between those components.

This is passive validation tooling only. Outputs must not feed rendering,
scheduling, hit selection, shading, resolver decisions, traversal, or adaptive
precision.
"""

from __future__ import annotations

import argparse
import csv
import json
import math
from collections import Counter, defaultdict, deque
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import numpy as np
from PIL import Image, ImageDraw, ImageFont


GUARDRAIL = (
    "Transport ownership graphs are diagnostic-only renderer validation data. "
    "They must not feed rendering, scheduling, hit selection, shading, resolver "
    "decisions, traversal, or adaptive precision."
)
ORACLE_SAMPLE_WARNING = (
    "Oracle ladder-only data may annotate unstable sampled pixels, but must not "
    "be reported as full-frame graph persistence."
)
NODE_COLS = [
    "node_id",
    "transport_signature",
    "had_hit",
    "collider_id",
    "domain_id",
    "boundary_event_bucket",
    "area_px",
    "centroid_x",
    "centroid_y",
    "bbox_x0",
    "bbox_y0",
    "bbox_x1",
    "bbox_y1",
    "mean_hit_distance",
    "mean_path_length",
    "mean_step_count",
    "mean_boundary_event_count",
    "mean_normal_x",
    "mean_normal_y",
    "mean_normal_z",
    "is_unresolved_island_member",
    "precision_floor_label",
    "epsilon_stable_area_px",
    "source_step_length",
    "traversal",
    "stride",
]
EDGE_COLS = [
    "edge_id",
    "node_a",
    "node_b",
    "seam_length_px",
    "collider_flip_count",
    "domain_flip_count",
    "boundary_event_flip_count",
    "mean_continuity_discontinuity_score",
    "max_continuity_discontinuity_score",
    "mean_hit_distance_delta",
    "max_hit_distance_delta",
    "mean_path_length_delta",
    "max_path_length_delta",
    "mean_step_count_delta",
    "max_step_count_delta",
    "mean_normal_angle_delta",
    "max_normal_angle_delta",
    "unresolved_sample_count_on_seam",
    "ownership_transition_count",
    "edge_stability_class",
]
PERSISTENCE_COLS = [
    "group_key",
    "from_step",
    "to_step",
    "lineage_type",
    "from_node",
    "to_node",
    "overlap_px",
    "centroid_distance",
    "persistence_basis",
]


@dataclass
class CellGraph:
    cell: Path
    meta: dict[str, Any]
    nodes: list[dict[str, Any]]
    edges: list[dict[str, Any]]
    metrics: dict[str, Any]
    labels: np.ndarray | None
    offset_x: int
    offset_y: int
    node_pixels: dict[int, set[tuple[int, int]]]
    graph_basis: bool
    oracle_sample_only: bool
    persistence_basis: str


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


def finite_mean(values: list[float]) -> float:
    vals = [float(v) for v in values if math.isfinite(float(v))]
    return float(np.mean(vals)) if vals else math.nan


def fmt(value: float | int | str) -> str | float | int:
    if isinstance(value, float):
        if not math.isfinite(value):
            return ""
        return round(value, 6)
    return value


def parse_bbox(text: str | None) -> tuple[int, int, int, int] | None:
    if not text:
        return None
    parts = [int(round(float(p))) for p in text.replace(";", ",").split(",") if p.strip()]
    if len(parts) != 4:
        raise argparse.ArgumentTypeError("bbox must be x0,y0,x1,y1")
    x0, y0, x1, y1 = parts
    return min(x0, x1), min(y0, y1), max(x0, x1), max(y0, y1)


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


def write_csv(path: Path, rows: list[dict[str, Any]], cols: list[str] | None = None) -> None:
    cols = cols or (list(rows[0].keys()) if rows else [])
    with path.open("w", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=cols)
        writer.writeheader()
        for row in rows:
            writer.writerow({key: row.get(key, "") for key in cols})


def find_one(folder: Path, pattern: str) -> Path | None:
    matches = sorted(folder.glob(pattern))
    return matches[0] if matches else None


def discover_cells(root: Path) -> list[Path]:
    if any(root.glob("*.hit_diagnostics.csv")) or any(root.glob("*.reference_transport_oracle_comparisons.csv")):
        return [root]
    cells: list[Path] = []
    for candidate in sorted({p.parent for p in root.glob("**/metadata.json")} | {p.parent for p in root.glob("**/*.hit_diagnostics.csv")} | {p.parent for p in root.glob("**/*.reference_transport_oracle_comparisons.csv")}):
        if any(candidate.glob("*.hit_diagnostics.csv")) or any(candidate.glob("*.reference_transport_oracle_comparisons.csv")):
            cells.append(candidate)
    return sorted(cells)


def normal_angle_deg(a: np.ndarray, b: np.ndarray) -> float:
    la = float(np.linalg.norm(a))
    lb = float(np.linalg.norm(b))
    if la <= 1e-9 or lb <= 1e-9:
        return 0.0
    c = max(-1.0, min(1.0, float(np.dot(a, b) / (la * lb))))
    return math.degrees(math.acos(c))


def load_hit_fields(path: Path, roi_bbox: tuple[int, int, int, int] | None) -> dict[str, Any]:
    rows = load_csv(path)
    if not rows:
        raise ValueError(f"No rows in hit diagnostics: {path}")
    max_x = max(parse_int(row.get("x"), -1) for row in rows)
    max_y = max(parse_int(row.get("y"), -1) for row in rows)
    if roi_bbox:
        x0, y0, x1, y1 = roi_bbox
    else:
        x0, y0, x1, y1 = 0, 0, max_x, max_y
    width, height = max(0, x1 - x0 + 1), max(0, y1 - y0 + 1)
    present = np.zeros((height, width), dtype=bool)
    had = np.zeros((height, width), dtype=bool)
    collider = np.zeros((height, width), dtype=np.uint64)
    domain = np.zeros((height, width), dtype=np.int64)
    hit_distance = np.full((height, width), np.nan, dtype=np.float64)
    path_length = np.full((height, width), np.nan, dtype=np.float64)
    boundary = np.zeros((height, width), dtype=np.int64)
    step_count = np.full((height, width), -1, dtype=np.int64)
    normal = np.zeros((height, width, 3), dtype=np.float64)
    valued: set[str] = set()
    for row in rows:
        gx = parse_int(row.get("x"), -1)
        gy = parse_int(row.get("y"), -1)
        if not (x0 <= gx <= x1 and y0 <= gy <= y1):
            continue
        x, y = gx - x0, gy - y0
        present[y, x] = True
        had[y, x] = str(row.get("had_hit", "0")).lower() in {"1", "true"}
        collider[y, x] = parse_int(row.get("collider_id", row.get("current_collider_id", "0")), 0)
        domain_value = row.get("domain_id", row.get("curvature_domain_id", ""))
        if domain_value != "":
            domain[y, x] = parse_int(domain_value, 0)
            valued.add("domain_id")
        hd = row.get("hit_distance", "")
        if hd not in {"", "nan", "NaN"}:
            hit_distance[y, x] = parse_float(hd)
            valued.add("hit_distance")
        path_value = row.get("path_length", row.get("accumulated_transport_length", ""))
        if path_value not in {"", "nan", "NaN"}:
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
        for i, name in enumerate(("normal_x", "normal_y", "normal_z")):
            if row.get(name, "") != "":
                normal[y, x, i] = parse_float(row.get(name), 0.0)
                valued.add(name)
        if row.get("portal_event_count", "") != "":
            valued.add("portal_event_count")
        if row.get("throat_event_count", "") != "":
            valued.add("throat_event_count")
    optional = ["hit_distance", "domain_id", "path_length", "boundary_event_count", "portal_event_count", "throat_event_count", "step_count", "normal_x", "normal_y", "normal_z"]
    return {
        "offset_x": x0,
        "offset_y": y0,
        "width": width,
        "height": height,
        "present": present,
        "had": had,
        "collider": collider,
        "domain": domain,
        "hit_distance": hit_distance,
        "path_length": path_length,
        "boundary": boundary,
        "step_count": step_count,
        "normal": normal,
        "missing_optional_fields": [name for name in optional if name not in valued],
    }


def derive_oracle_annotations(cell: Path) -> dict[tuple[int, int], dict[str, Any]]:
    annotations: dict[tuple[int, int], dict[str, Any]] = {}
    island_rows = load_csv(cell / "unresolved_island_summary.csv")
    if not island_rows:
        island_rows = load_csv(cell.parent / "unresolved_island_summary.csv")
    for row in island_rows:
        x, y = parse_int(row.get("x"), -1), parse_int(row.get("y"), -1)
        if x < 0 or y < 0:
            continue
        annotations[(x, y)] = {
            "classification": row.get("classification", ""),
            "first_stable_step": row.get("first_stable_step", ""),
            "ownership_transition": parse_int(row.get("ownership_transition"), 0),
            "stable_at_0.00625": parse_int(row.get("stable_at_0.00625"), 0),
            "stable_at_0.003125": parse_int(row.get("stable_at_0.003125"), 0),
            "decision_risk": parse_float(row.get("max_decision_risk"), 0.0),
        }
    if annotations:
        return annotations

    comparisons = load_csv(find_one(cell, "*.reference_transport_oracle_comparisons.csv"))
    by_pixel: dict[tuple[int, int], list[dict[str, str]]] = defaultdict(list)
    for row in comparisons:
        x, y = parse_int(row.get("pixel_x"), -1), parse_int(row.get("pixel_y"), -1)
        if x >= 0 and y >= 0:
            by_pixel[(x, y)].append(row)
    for pixel, rows in by_pixel.items():
        stable_steps = [parse_float(r.get("production_step_length")) for r in rows if r.get("epsilon_stability_class") == "stable"]
        classes = Counter(r.get("epsilon_stability_class", "unknown") for r in rows)
        finest = sorted(rows, key=lambda r: parse_float(r.get("production_step_length"), math.inf))[0]
        cls = finest.get("epsilon_stability_class", "unknown")
        if classes.get("multi_solution", 0) and cls != "stable":
            cls = "multi_solution"
        annotations[pixel] = {
            "classification": cls,
            "first_stable_step": f"{max(stable_steps):g}" if stable_steps else "",
            "ownership_transition": int(any(r.get("collider_match") not in {"1", "true", "True"} or r.get("domain_match") not in {"1", "true", "True"} for r in rows)),
            "stable_at_0.00625": int(any(abs(parse_float(r.get("production_step_length")) - 0.00625) < 1e-9 and r.get("epsilon_stability_class") == "stable" for r in rows)),
            "stable_at_0.003125": int(any(abs(parse_float(r.get("production_step_length")) - 0.003125) < 1e-9 and r.get("epsilon_stability_class") == "stable" for r in rows)),
            "decision_risk": max([parse_float(r.get("decision_risk"), 0.0) for r in rows] or [0.0]),
        }
    return annotations


def sampled_sealed_step(cell: Path, annotations: dict[tuple[int, int], dict[str, Any]]) -> str:
    summary = load_json(cell / "unresolved_island_summary.json") or load_json(cell.parent / "unresolved_island_summary.json")
    if summary.get("sealed_at_0.00625") is True:
        return str(summary.get("sealed_step", 0.00625))
    if annotations and all(a.get("stable_at_0.00625") == 1 for a in annotations.values()):
        return "0.00625"
    return ""


def graph_group(meta: dict[str, Any]) -> str:
    return "|".join([
        str(meta.get("fixture", "")),
        str(meta.get("traversal", "")),
        str(meta.get("stride", "")),
        str(meta.get("roi_label", meta.get("center", ""))),
    ])


def step_value(meta: dict[str, Any], default: str = "") -> str:
    return str(meta.get("step_length", meta.get("production_step", default)) or default)


def build_graph(cell: Path, fields: dict[str, Any], annotations: dict[tuple[int, int], dict[str, Any]], epsilon: float, meta: dict[str, Any]) -> tuple[list[dict[str, Any]], list[dict[str, Any]], dict[str, Any], np.ndarray, dict[int, set[tuple[int, int]]]]:
    present = fields["present"]
    had = fields["had"]
    collider = fields["collider"]
    domain = fields["domain"]
    hit_distance = fields["hit_distance"]
    path_length = fields["path_length"]
    boundary = fields["boundary"]
    step_count = fields["step_count"]
    normal = fields["normal"]
    h, w = present.shape
    ox, oy = fields["offset_x"], fields["offset_y"]
    labels = np.full((h, w), -1, dtype=np.int32)
    node_pixels: dict[int, set[tuple[int, int]]] = {}
    nodes: list[dict[str, Any]] = []
    node_id = 0

    def sig_at(x: int, y: int) -> tuple[int, int, int, int]:
        bucket = int(boundary[y, x]) if "boundary_event_count" not in fields["missing_optional_fields"] else 0
        return int(bool(had[y, x])), int(collider[y, x]), int(domain[y, x]), bucket

    for y0 in range(h):
        for x0 in range(w):
            if not present[y0, x0] or labels[y0, x0] >= 0:
                continue
            sig = sig_at(x0, y0)
            q: deque[tuple[int, int]] = deque([(x0, y0)])
            labels[y0, x0] = node_id
            pts: list[tuple[int, int]] = []
            while q:
                x, y = q.popleft()
                pts.append((x, y))
                for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                    if not (0 <= nx < w and 0 <= ny < h) or not present[ny, nx] or labels[ny, nx] >= 0:
                        continue
                    if sig_at(nx, ny) == sig:
                        labels[ny, nx] = node_id
                        q.append((nx, ny))
            globals_ = {(x + ox, y + oy) for x, y in pts}
            node_pixels[node_id] = globals_
            xs = [p[0] + ox for p in pts]
            ys = [p[1] + oy for p in pts]
            hds = [float(hit_distance[y, x]) for x, y in pts]
            pls = [float(path_length[y, x]) for x, y in pts]
            scs = [float(step_count[y, x]) for x, y in pts if int(step_count[y, x]) >= 0]
            bes = [float(boundary[y, x]) for x, y in pts]
            normals = [normal[y, x] for x, y in pts if float(np.linalg.norm(normal[y, x])) > 1e-9]
            nmean = np.mean(normals, axis=0) if normals else np.array([math.nan, math.nan, math.nan])
            ann = [annotations[p] for p in globals_ if p in annotations]
            unresolved = [a for a in ann if a.get("classification") not in {"", "stable", "plateaued"}]
            stable_count = sum(1 for a in ann if a.get("classification") in {"stable", "plateaued"})
            floors = Counter(str(a.get("first_stable_step", "")) for a in ann if a.get("first_stable_step", "") != "")
            precision_floor = floors.most_common(1)[0][0] if floors else ""
            nodes.append({
                "node_id": node_id,
                "transport_signature": f"{sig[0]}|{sig[1]}|{sig[2]}|{sig[3]}",
                "had_hit": sig[0],
                "collider_id": sig[1],
                "domain_id": sig[2],
                "boundary_event_bucket": sig[3],
                "area_px": len(pts),
                "centroid_x": fmt(float(np.mean(xs))),
                "centroid_y": fmt(float(np.mean(ys))),
                "bbox_x0": min(xs),
                "bbox_y0": min(ys),
                "bbox_x1": max(xs),
                "bbox_y1": max(ys),
                "mean_hit_distance": fmt(finite_mean(hds)),
                "mean_path_length": fmt(finite_mean(pls)),
                "mean_step_count": fmt(finite_mean(scs)),
                "mean_boundary_event_count": fmt(finite_mean(bes)),
                "mean_normal_x": fmt(float(nmean[0])),
                "mean_normal_y": fmt(float(nmean[1])),
                "mean_normal_z": fmt(float(nmean[2])),
                "is_unresolved_island_member": int(bool(unresolved)),
                "precision_floor_label": precision_floor,
                "epsilon_stable_area_px": stable_count if ann else "",
                "source_step_length": step_value(meta),
                "traversal": str(meta.get("traversal", "")),
                "stride": str(meta.get("stride", "")),
            })
            node_id += 1

    edge_acc: dict[tuple[int, int], dict[str, Any]] = {}

    def get_edge(a: int, b: int) -> dict[str, Any]:
        key = (a, b) if a < b else (b, a)
        return edge_acc.setdefault(key, {
            "node_a": key[0],
            "node_b": key[1],
            "seam_length_px": 0,
            "collider_flip_count": 0,
            "domain_flip_count": 0,
            "boundary_event_flip_count": 0,
            "continuity_scores": [],
            "hit_distance_deltas": [],
            "path_length_deltas": [],
            "step_count_deltas": [],
            "normal_angle_deltas": [],
            "unresolved_sample_count_on_seam": 0,
            "ownership_transition_count": 0,
        })

    for y in range(h):
        for x in range(w):
            if labels[y, x] < 0:
                continue
            a = int(labels[y, x])
            for nx, ny in ((x + 1, y), (x, y + 1)):
                if nx >= w or ny >= h or labels[ny, nx] < 0:
                    continue
                b = int(labels[ny, nx])
                if a == b:
                    continue
                e = get_edge(a, b)
                e["seam_length_px"] += 1
                e["collider_flip_count"] += int(int(collider[y, x]) != int(collider[ny, nx]) or bool(had[y, x]) != bool(had[ny, nx]))
                e["domain_flip_count"] += int(int(domain[y, x]) != int(domain[ny, nx]))
                e["boundary_event_flip_count"] += int(int(boundary[y, x]) != int(boundary[ny, nx]))
                if math.isfinite(float(hit_distance[y, x])) and math.isfinite(float(hit_distance[ny, nx])):
                    e["hit_distance_deltas"].append(abs(float(hit_distance[y, x]) - float(hit_distance[ny, nx])))
                if math.isfinite(float(path_length[y, x])) and math.isfinite(float(path_length[ny, nx])):
                    e["path_length_deltas"].append(abs(float(path_length[y, x]) - float(path_length[ny, nx])))
                if int(step_count[y, x]) >= 0 and int(step_count[ny, nx]) >= 0:
                    e["step_count_deltas"].append(abs(float(step_count[y, x]) - float(step_count[ny, nx])))
                e["normal_angle_deltas"].append(normal_angle_deg(normal[y, x], normal[ny, nx]))
                for gp in ((x + ox, y + oy), (nx + ox, ny + oy)):
                    ann = annotations.get(gp)
                    if ann and ann.get("classification") not in {"", "stable", "plateaued"}:
                        e["unresolved_sample_count_on_seam"] += 1
                    if ann and int(ann.get("ownership_transition", 0) or 0):
                        e["ownership_transition_count"] += 1

    continuity_path = cell / "transport_continuity_vectors.csv"
    if continuity_path.exists():
        for row in load_csv(continuity_path):
            gx, gy = parse_int(row.get("x"), -1), parse_int(row.get("y"), -1)
            ngx, ngy = parse_int(row.get("neighbor_x"), -1), parse_int(row.get("neighbor_y"), -1)
            x, y = gx - ox, gy - oy
            nx, ny = ngx - ox, ngy - oy
            if not (0 <= x < w and 0 <= y < h and 0 <= nx < w and 0 <= ny < h):
                continue
            if labels[y, x] < 0 or labels[ny, nx] < 0:
                continue
            a, b = int(labels[y, x]), int(labels[ny, nx])
            if a != b:
                get_edge(a, b)["continuity_scores"].append(parse_float(row.get("total_transport_discontinuity_score"), 0.0))

    edges: list[dict[str, Any]] = []
    for edge_id, (_, e) in enumerate(sorted(edge_acc.items()), start=1):
        scores = e["continuity_scores"]
        high = bool(scores and max(scores) > epsilon)
        if e["unresolved_sample_count_on_seam"] > 0:
            cls = "unresolved"
        elif high:
            cls = "precision_sensitive"
        elif scores:
            cls = "stable"
        else:
            cls = "unknown"
        edges.append({
            "edge_id": edge_id,
            "node_a": e["node_a"],
            "node_b": e["node_b"],
            "seam_length_px": e["seam_length_px"],
            "collider_flip_count": e["collider_flip_count"],
            "domain_flip_count": e["domain_flip_count"],
            "boundary_event_flip_count": e["boundary_event_flip_count"],
            "mean_continuity_discontinuity_score": fmt(finite_mean(scores)) if scores else "",
            "max_continuity_discontinuity_score": fmt(max(scores)) if scores else "",
            "mean_hit_distance_delta": fmt(finite_mean(e["hit_distance_deltas"])),
            "max_hit_distance_delta": fmt(max(e["hit_distance_deltas"])) if e["hit_distance_deltas"] else "",
            "mean_path_length_delta": fmt(finite_mean(e["path_length_deltas"])),
            "max_path_length_delta": fmt(max(e["path_length_deltas"])) if e["path_length_deltas"] else "",
            "mean_step_count_delta": fmt(finite_mean(e["step_count_deltas"])),
            "max_step_count_delta": fmt(max(e["step_count_deltas"])) if e["step_count_deltas"] else "",
            "mean_normal_angle_delta": fmt(finite_mean(e["normal_angle_deltas"])),
            "max_normal_angle_delta": fmt(max(e["normal_angle_deltas"])) if e["normal_angle_deltas"] else "",
            "unresolved_sample_count_on_seam": e["unresolved_sample_count_on_seam"],
            "ownership_transition_count": e["ownership_transition_count"],
            "edge_stability_class": cls,
        })

    ann_count = len(annotations)
    stable_ann = sum(1 for a in annotations.values() if a.get("classification") in {"stable", "plateaued"})
    unresolved_count = sum(1 for a in annotations.values() if a.get("classification") not in {"", "stable", "plateaued"})
    precision_hist = Counter(str(a.get("first_stable_step", "")) for a in annotations.values() if a.get("first_stable_step", "") != "")
    hit_area = sum(int(n["area_px"]) for n in nodes if int(n["had_hit"]) == 1)
    high_nodes = {int(e["node_a"]) for e in edges if e["edge_stability_class"] == "precision_sensitive"} | {int(e["node_b"]) for e in edges if e["edge_stability_class"] == "precision_sensitive"}
    stable_area = sum(int(n["area_px"]) for n in nodes if int(n["had_hit"]) == 1 and int(n["node_id"]) not in high_nodes)
    metrics = {
        "graph_node_count": len(nodes),
        "graph_edge_count": len(edges),
        "seam_length_px_total": sum(int(e["seam_length_px"]) for e in edges),
        "high_discontinuity_edge_count": sum(1 for e in edges if e["edge_stability_class"] == "precision_sensitive"),
        "unstable_subgraph_count": sum(1 for n in nodes if int(n["is_unresolved_island_member"]) == 1),
        "unresolved_pixel_count": unresolved_count,
        "epsilon_stable_area_percent": round(100.0 * stable_ann / ann_count, 6) if ann_count else round(100.0 * stable_area / max(1, hit_area), 6),
        "precision_floor_histogram": dict(sorted(precision_hist.items())),
        "sampled_topology_sealed_step": sampled_sealed_step(cell, annotations),
        "threshold_snap_count": sum(1 for a in annotations.values() if a.get("classification") == "threshold_snap"),
        "missing_optional_fields": fields["missing_optional_fields"],
    }
    return nodes, edges, metrics, labels, node_pixels


def analyze_oracle_only(cell: Path, out: Path, epsilon: float) -> CellGraph:
    annotations = derive_oracle_annotations(cell)
    unresolved_count = sum(1 for a in annotations.values() if a.get("classification") not in {"", "stable", "plateaued"})
    precision_hist = Counter(str(a.get("first_stable_step", "")) for a in annotations.values() if a.get("first_stable_step", "") != "")
    metrics = {
        "graph_node_count": 0,
        "graph_edge_count": 0,
        "seam_length_px_total": 0,
        "high_discontinuity_edge_count": 0,
        "unstable_subgraph_count": unresolved_count,
        "unresolved_pixel_count": unresolved_count,
        "epsilon_stable_area_percent": round(100.0 * (len(annotations) - unresolved_count) / max(1, len(annotations)), 6),
        "precision_floor_histogram": dict(sorted(precision_hist.items())),
        "sampled_topology_sealed_step": sampled_sealed_step(cell, annotations),
        "threshold_snap_count": sum(1 for a in annotations.values() if a.get("classification") == "threshold_snap"),
        "full_frame_graph_basis": False,
        "oracle_sample_only": True,
        "persistence_basis": "oracle_sample_only",
        "warnings": [ORACLE_SAMPLE_WARNING],
        "epsilon": epsilon,
    }
    rows = [
        {
            "x": x,
            "y": y,
            "classification": ann.get("classification", ""),
            "first_stable_step": ann.get("first_stable_step", ""),
            "decision_risk": ann.get("decision_risk", ""),
            "ownership_transition": ann.get("ownership_transition", ""),
        }
        for (x, y), ann in sorted(annotations.items())
    ]
    write_csv(out / "unstable_subgraphs.csv", rows, ["x", "y", "classification", "first_stable_step", "decision_risk", "ownership_transition"])
    return CellGraph(cell, load_json(cell / "metadata.json"), [], [], metrics, None, 0, 0, {}, False, True, "oracle_sample_only")


def analyze_cell(cell: Path, out: Path, roi_bbox: tuple[int, int, int, int] | None, epsilon: float, visualize: bool) -> CellGraph:
    meta = load_json(cell / "metadata.json")
    hit_path = find_one(cell, "*.hit_diagnostics.csv")
    if not hit_path:
        if find_one(cell, "*.reference_transport_oracle_comparisons.csv") or (cell / "unresolved_island_summary.csv").exists():
            return analyze_oracle_only(cell, out, epsilon)
        raise SystemExit(f"No graph basis found in {cell}: missing *.hit_diagnostics.csv and oracle comparison CSV.")
    fields = load_hit_fields(hit_path, roi_bbox)
    annotations = derive_oracle_annotations(cell)
    if roi_bbox:
        x0, y0, x1, y1 = roi_bbox
        annotations = {p: ann for p, ann in annotations.items() if x0 <= p[0] <= x1 and y0 <= p[1] <= y1}
    nodes, edges, metrics, labels, node_pixels = build_graph(cell, fields, annotations, epsilon, meta)
    metrics.update({
        "full_frame_graph_basis": True,
        "oracle_sample_only": False,
        "persistence_basis": "unavailable",
        "epsilon": epsilon,
        "roi_bbox": roi_bbox or "",
        "warnings": [],
    })
    graph = CellGraph(cell, meta, nodes, edges, metrics, labels, fields["offset_x"], fields["offset_y"], node_pixels, True, False, "unavailable")
    if visualize:
        draw_node_map(out / "ownership_graph_node_map.png", labels, fields["offset_x"], fields["offset_y"], nodes)
        draw_seam_map(out / "ownership_graph_seam_map.png", labels)
        draw_unstable_overlay(out / "unstable_subgraph_overlay.png", labels, fields["offset_x"], fields["offset_y"], annotations)
    return graph


def node_color(node_id: int) -> tuple[int, int, int, int]:
    return (
        60 + (node_id * 53) % 180,
        60 + (node_id * 97) % 180,
        60 + (node_id * 193) % 180,
        255,
    )


def draw_node_map(path: Path, labels: np.ndarray | None, ox: int, oy: int, nodes: list[dict[str, Any]]) -> None:
    if labels is None:
        return
    h, w = labels.shape
    scale = max(1, min(8, 640 // max(1, w)))
    img = Image.new("RGBA", (w * scale, h * scale), (18, 20, 24, 255))
    draw = ImageDraw.Draw(img)
    for y in range(h):
        for x in range(w):
            label = int(labels[y, x])
            color = (35, 35, 40, 255) if label < 0 else node_color(label)
            draw.rectangle((x * scale, y * scale, (x + 1) * scale - 1, (y + 1) * scale - 1), fill=color)
    draw.text((4, 4), f"ownership graph nodes offset=({ox},{oy})", fill=(255, 255, 255, 255), font=ImageFont.load_default())
    img.save(path)


def draw_seam_map(path: Path, labels: np.ndarray | None) -> None:
    if labels is None:
        return
    h, w = labels.shape
    scale = max(1, min(8, 640 // max(1, w)))
    img = Image.new("RGBA", (w * scale, h * scale), (18, 20, 24, 255))
    draw = ImageDraw.Draw(img)
    for y in range(h):
        for x in range(w):
            if labels[y, x] < 0:
                continue
            seam = (x + 1 < w and labels[y, x + 1] >= 0 and labels[y, x + 1] != labels[y, x]) or (y + 1 < h and labels[y + 1, x] >= 0 and labels[y + 1, x] != labels[y, x])
            color = (255, 230, 60, 255) if seam else (40, 55, 70, 255)
            draw.rectangle((x * scale, y * scale, (x + 1) * scale - 1, (y + 1) * scale - 1), fill=color)
    draw.text((4, 4), "ownership graph seams", fill=(255, 255, 255, 255), font=ImageFont.load_default())
    img.save(path)


def draw_unstable_overlay(path: Path, labels: np.ndarray | None, ox: int, oy: int, annotations: dict[tuple[int, int], dict[str, Any]]) -> None:
    if labels is None:
        return
    h, w = labels.shape
    scale = max(1, min(8, 640 // max(1, w)))
    img = Image.new("RGBA", (w * scale, h * scale), (18, 20, 24, 255))
    draw = ImageDraw.Draw(img)
    for y in range(h):
        for x in range(w):
            gp = (x + ox, y + oy)
            ann = annotations.get(gp)
            if not ann:
                color = (35, 35, 40, 255)
            elif ann.get("classification") in {"stable", "plateaued"}:
                color = (45, 220, 105, 255)
            else:
                color = (255, 55, 85, 255)
            draw.rectangle((x * scale, y * scale, (x + 1) * scale - 1, (y + 1) * scale - 1), fill=color)
    draw.text((4, 4), "unstable sampled subgraphs", fill=(255, 255, 255, 255), font=ImageFont.load_default())
    img.save(path)


def bbox_overlap(a: dict[str, Any], b: dict[str, Any]) -> bool:
    return not (
        int(a["bbox_x1"]) < int(b["bbox_x0"]) or int(a["bbox_x0"]) > int(b["bbox_x1"]) or
        int(a["bbox_y1"]) < int(b["bbox_y0"]) or int(a["bbox_y0"]) > int(b["bbox_y1"])
    )


def centroid_distance(a: dict[str, Any], b: dict[str, Any]) -> float:
    return math.hypot(parse_float(a.get("centroid_x"), 0) - parse_float(b.get("centroid_x"), 0), parse_float(a.get("centroid_y"), 0) - parse_float(b.get("centroid_y"), 0))


def match_graphs(old: CellGraph, new: CellGraph) -> tuple[list[dict[str, Any]], str]:
    if not old.graph_basis or not new.graph_basis:
        return [], "oracle_sample_only" if (old.oracle_sample_only or new.oracle_sample_only) else "unavailable"
    rows: list[dict[str, Any]] = []
    old_to_new: dict[int, list[tuple[int, int, float, str]]] = defaultdict(list)
    new_to_old: dict[int, list[int]] = defaultdict(list)
    basis = "unavailable"
    for oid, opix in old.node_pixels.items():
        overlaps: list[tuple[int, int]] = []
        for nid, npix in new.node_pixels.items():
            ov = len(opix & npix)
            if ov > 0:
                overlaps.append((nid, ov))
        if overlaps:
            basis = "overlap"
            for nid, ov in sorted(overlaps, key=lambda item: item[1], reverse=True):
                old_to_new[oid].append((nid, ov, 0.0, "overlap"))
                new_to_old[nid].append(oid)
        elif basis != "overlap":
            onode = old.nodes[oid]
            candidates = []
            for nnode in new.nodes:
                if onode.get("transport_signature") == nnode.get("transport_signature") and bbox_overlap(onode, nnode):
                    dist = centroid_distance(onode, nnode)
                    if dist <= 3.0:
                        candidates.append((int(nnode["node_id"]), 0, dist, "centroid_fallback"))
            if candidates:
                basis = "centroid_fallback"
                best = sorted(candidates, key=lambda item: item[2])[0]
                old_to_new[oid].append(best)
                new_to_old[best[0]].append(oid)

    old_ids = {int(n["node_id"]) for n in old.nodes}
    new_ids = {int(n["node_id"]) for n in new.nodes}
    for oid in sorted(old_ids):
        matches = old_to_new.get(oid, [])
        if not matches:
            rows.append(lineage_row(old, new, "disappear", oid, "", 0, "", basis))
        elif len(matches) > 1:
            for nid, ov, dist, mbasis in matches:
                rows.append(lineage_row(old, new, "split", oid, nid, ov, dist, mbasis))
        else:
            nid, ov, dist, mbasis = matches[0]
            lineage = "merge" if len(new_to_old.get(nid, [])) > 1 else "persist"
            rows.append(lineage_row(old, new, lineage, oid, nid, ov, dist, mbasis))
    matched_new = {nid for matches in old_to_new.values() for nid, _, _, _ in matches}
    for nid in sorted(new_ids - matched_new):
        rows.append(lineage_row(old, new, "appear", "", nid, 0, "", basis))
    return rows, basis


def lineage_row(old: CellGraph, new: CellGraph, lineage_type: str, old_id: int | str, new_id: int | str, overlap: int, dist: float | str, basis: str) -> dict[str, Any]:
    return {
        "group_key": graph_group(old.meta),
        "from_step": step_value(old.meta),
        "to_step": step_value(new.meta),
        "lineage_type": lineage_type,
        "from_node": old_id,
        "to_node": new_id,
        "overlap_px": overlap,
        "centroid_distance": fmt(dist) if isinstance(dist, float) else dist,
        "persistence_basis": basis,
    }


def compute_persistence(graphs: list[CellGraph]) -> tuple[list[dict[str, Any]], list[dict[str, Any]], dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    basis_values: list[str] = []
    by_group: dict[str, list[CellGraph]] = defaultdict(list)
    for graph in graphs:
        by_group[graph_group(graph.meta)].append(graph)
    for group_graphs in by_group.values():
        group_graphs.sort(key=lambda g: parse_float(step_value(g.meta), math.inf), reverse=True)
        for old, new in zip(group_graphs, group_graphs[1:]):
            matched, basis = match_graphs(old, new)
            rows.extend(matched)
            if basis != "unavailable":
                basis_values.append(basis)
    merge_split = [row for row in rows if row["lineage_type"] in {"merge", "split"}]
    persist_count = sum(1 for row in rows if row["lineage_type"] == "persist")
    old_count = sum(1 for row in rows if row["from_node"] != "")
    basis = "overlap" if "overlap" in basis_values else ("centroid_fallback" if "centroid_fallback" in basis_values else ("oracle_sample_only" if "oracle_sample_only" in basis_values else "unavailable"))
    metrics = {
        "node_persistence_rate": round(persist_count / max(1, old_count), 6) if rows else "",
        "edge_persistence_rate": "",
        "merge_count": sum(1 for row in rows if row["lineage_type"] == "merge"),
        "split_count": sum(1 for row in rows if row["lineage_type"] == "split"),
        "persistence_basis": basis,
    }
    return rows, merge_split, metrics


def graph_edit_distance(graph: CellGraph, ref: CellGraph | None) -> float | str:
    if ref is None:
        return ""
    return round(
        abs(int(graph.metrics["graph_node_count"]) - int(ref.metrics["graph_node_count"]))
        + abs(int(graph.metrics["graph_edge_count"]) - int(ref.metrics["graph_edge_count"]))
        + abs(int(graph.metrics["seam_length_px_total"]) - int(ref.metrics["seam_length_px_total"])) / 100.0
        + abs(int(graph.metrics["high_discontinuity_edge_count"]) - int(ref.metrics["high_discontinuity_edge_count"])),
        6,
    )


def write_visual_lineage(path: Path, rows: list[dict[str, Any]]) -> None:
    img = Image.new("RGBA", (980, 420), (18, 20, 24, 255))
    draw = ImageDraw.Draw(img)
    font = ImageFont.load_default()
    draw.text((12, 10), "graph persistence lineage: persist/merge/split/appear/disappear", fill=(255, 255, 255, 255), font=font)
    if not rows:
        draw.text((24, 190), "No multi-level graph lineage available.", fill=(220, 220, 220, 255), font=font)
        img.save(path)
        return
    steps = []
    for row in rows:
        for key in ("from_step", "to_step"):
            if row.get(key) not in steps and row.get(key) != "":
                steps.append(row.get(key))
    steps.sort(key=lambda s: parse_float(s, 0), reverse=True)
    x_for = {step: 90 + i * max(80, min(160, 800 // max(1, len(steps) - 1))) for i, step in enumerate(steps)}
    colors = {
        "persist": (70, 220, 110, 255),
        "merge": (255, 180, 40, 255),
        "split": (255, 90, 200, 255),
        "appear": (90, 180, 255, 255),
        "disappear": (255, 70, 80, 255),
    }
    for step, x in x_for.items():
        draw.line((x, 50, x, 390), fill=(80, 85, 95, 255))
        draw.text((x - 16, 32), str(step), fill=(235, 235, 235, 255), font=font)
    for i, row in enumerate(rows[:220]):
        y = 70 + (i * 19) % 300
        x0 = x_for.get(row.get("from_step"), 70)
        x1 = x_for.get(row.get("to_step"), x0 + 80)
        color = colors.get(row.get("lineage_type"), (180, 180, 180, 255))
        draw.line((x0, y, x1, y), fill=color, width=2)
        if row.get("from_node") != "":
            draw.text((x0 - 8, y - 6), str(row.get("from_node")), fill=color, font=font)
        if row.get("to_node") != "":
            draw.text((x1 + 3, y - 6), str(row.get("to_node")), fill=color, font=font)
        if i < 16:
            draw.text((720, 58 + i * 18), f"{row['lineage_type']} {row['from_node']} -> {row['to_node']}", fill=color, font=font)
    img.save(path)


def write_visual_ladder(path: Path, graphs: list[CellGraph]) -> None:
    panels = []
    for graph in graphs:
        if graph.labels is None:
            continue
        tmp = Image.new("RGBA", (160, 120), (18, 20, 24, 255))
        h, w = graph.labels.shape
        scale = max(1, min(8, 140 // max(1, w), 90 // max(1, h)))
        draw = ImageDraw.Draw(tmp)
        for y in range(h):
            for x in range(w):
                label = int(graph.labels[y, x])
                color = (40, 42, 48, 255) if label < 0 else node_color(label)
                draw.rectangle((8 + x * scale, 28 + y * scale, 8 + (x + 1) * scale - 1, 28 + (y + 1) * scale - 1), fill=color)
        draw.text((6, 6), step_value(graph.meta) or graph.cell.name, fill=(255, 255, 255, 255), font=ImageFont.load_default())
        panels.append(tmp)
    if not panels:
        write_visual_lineage(path, [])
        return
    img = Image.new("RGBA", (160 * len(panels), 120), (18, 20, 24, 255))
    for i, panel in enumerate(panels):
        img.alpha_composite(panel, (i * 160, 0))
    img.save(path)


def write_merge_split_overlay(path: Path, rows: list[dict[str, Any]]) -> None:
    img = Image.new("RGBA", (760, 320), (18, 20, 24, 255))
    draw = ImageDraw.Draw(img)
    font = ImageFont.load_default()
    draw.text((12, 12), "merge/split events", fill=(255, 255, 255, 255), font=font)
    if not rows:
        draw.text((24, 150), "No merge or split events detected.", fill=(220, 220, 220, 255), font=font)
    for i, row in enumerate(rows[:14]):
        color = (255, 180, 40, 255) if row["lineage_type"] == "merge" else (255, 90, 200, 255)
        draw.text((24, 48 + i * 18), f"{row['lineage_type']}: {row['from_step']} node {row['from_node']} -> {row['to_step']} node {row['to_node']}", fill=color, font=font)
    img.save(path)


def write_outputs(out: Path, graphs: list[CellGraph], persistence: list[dict[str, Any]], merge_split: list[dict[str, Any]], persistence_metrics: dict[str, Any], reference_step: str, visualize: bool) -> None:
    out.mkdir(parents=True, exist_ok=True)
    primary = graphs[0] if graphs else None
    nodes = primary.nodes if primary else []
    edges = primary.edges if primary else []
    metrics = dict(primary.metrics if primary else {})
    refs = {graph_group(g.meta): g for g in graphs if step_value(g.meta) == reference_step}
    if primary:
        metrics["graph_edit_distance_vs_reference"] = graph_edit_distance(primary, refs.get(graph_group(primary.meta)))
    metrics.update(persistence_metrics)
    if primary and primary.oracle_sample_only:
        metrics["warnings"] = [ORACLE_SAMPLE_WARNING]
        metrics["persistence_basis"] = "oracle_sample_only"
    metrics.setdefault("full_frame_graph_basis", bool(primary and primary.graph_basis))
    metrics.setdefault("oracle_sample_only", bool(primary and primary.oracle_sample_only))
    metrics.setdefault("persistence_basis", persistence_metrics.get("persistence_basis", "unavailable"))
    write_csv(out / "transport_ownership_graph_nodes.csv", nodes, NODE_COLS)
    write_csv(out / "transport_ownership_graph_edges.csv", edges, EDGE_COLS)
    write_csv(out / "transport_ownership_graph_persistence.csv", persistence, PERSISTENCE_COLS)
    write_csv(out / "transport_ownership_graph_merges_splits.csv", merge_split, PERSISTENCE_COLS)
    unstable_rows = []
    if primary:
        for node in primary.nodes:
            if int(node.get("is_unresolved_island_member", 0)):
                unstable_rows.append(node)
    if not unstable_rows and primary and primary.oracle_sample_only:
        unstable_rows = load_csv(out / "unstable_subgraphs.csv")
    else:
        write_csv(out / "unstable_subgraphs.csv", unstable_rows, NODE_COLS if unstable_rows and "node_id" in unstable_rows[0] else None)
    graph_packet = {
        "definition": "Transport Ownership Graph = nodes are connected components of equivalent renderer transport signatures; edges are observed adjacency/seam relations between components.",
        "guardrail": GUARDRAIL,
        "oracle_sample_warning": ORACLE_SAMPLE_WARNING,
        "metrics": metrics,
        "nodes": nodes,
        "edges": edges,
        "persistence": persistence,
        "merge_split": merge_split,
    }
    (out / "transport_ownership_graph.json").write_text(json.dumps(graph_packet, indent=2, sort_keys=True) + "\n")
    (out / "transport_ownership_graph_metrics.json").write_text(json.dumps(metrics, indent=2, sort_keys=True) + "\n")
    write_summary(out / "transport_ownership_graph_summary.md", metrics, nodes, edges, persistence, merge_split)
    if visualize:
        write_visual_lineage(out / "graph_persistence_lineage.png", persistence)
        write_visual_ladder(out / "graph_persistence_ladder.png", graphs)
        write_merge_split_overlay(out / "merge_split_overlay.png", merge_split)


def write_summary(path: Path, metrics: dict[str, Any], nodes: list[dict[str, Any]], edges: list[dict[str, Any]], persistence: list[dict[str, Any]], merge_split: list[dict[str, Any]]) -> None:
    lines = [
        "# Transport Ownership Graph Extraction Summary",
        "",
        "> **Transport Ownership Graph** = nodes are connected components of equivalent renderer transport signatures; edges are observed adjacency/seam relations between those components.",
        "",
        GUARDRAIL,
        "",
        "## Concepts",
        "",
        "- Topology stability: graph nodes/edges remain structurally consistent across refinement levels.",
        "- Scalar precision stability: path length, hit distance, normal angle, and decision-risk deltas remain within epsilon.",
        "- Seam persistence: a boundary/adjacency relation remains present across step ladder levels.",
        "- Unresolved island closure: sampled unstable pixels become stable by a tested production step.",
        "",
        "## Metrics",
        "",
    ]
    for key in [
        "graph_node_count",
        "graph_edge_count",
        "seam_length_px_total",
        "high_discontinuity_edge_count",
        "unstable_subgraph_count",
        "unresolved_pixel_count",
        "epsilon_stable_area_percent",
        "precision_floor_histogram",
        "node_persistence_rate",
        "edge_persistence_rate",
        "merge_count",
        "split_count",
        "graph_edit_distance_vs_reference",
        "sampled_topology_sealed_step",
        "full_frame_graph_basis",
        "oracle_sample_only",
        "persistence_basis",
    ]:
        lines.append(f"- {key}: {metrics.get(key, '')}")
    lines.extend(["", "## Confidence And Warnings", ""])
    if metrics.get("oracle_sample_only"):
        lines.append(f"- WARNING: {ORACLE_SAMPLE_WARNING}")
    if metrics.get("missing_optional_fields"):
        lines.append(f"- Missing optional fields: {', '.join(metrics.get('missing_optional_fields', []))}")
    if not metrics.get("full_frame_graph_basis"):
        lines.append("- No dense hit diagnostics were available; full-frame graph claims are unavailable.")
    lines.extend([
        "",
        "## Outputs",
        "",
        "- `transport_ownership_graph_nodes.csv`",
        "- `transport_ownership_graph_edges.csv`",
        "- `transport_ownership_graph.json`",
        "- `transport_ownership_graph_metrics.json`",
        "- `transport_ownership_graph_persistence.csv`",
        "- `transport_ownership_graph_merges_splits.csv`",
        "- `unstable_subgraphs.csv`",
    ])
    path.write_text("\n".join(lines) + "\n")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input_root_or_cell", type=Path)
    parser.add_argument("--epsilon", type=float, default=0.05)
    parser.add_argument("--roi-bbox", type=parse_bbox, default=None)
    parser.add_argument("--reference-step", default="0.003125")
    parser.add_argument("--out", type=Path, default=None)
    parser.add_argument("--visualize", type=int, default=1)
    args = parser.parse_args()

    source = args.input_root_or_cell
    out = args.out or source
    out.mkdir(parents=True, exist_ok=True)
    cells = discover_cells(source)
    if not cells:
        raise SystemExit(f"No graph basis found in {source}: expected hit diagnostics or oracle comparison CSVs.")

    graphs: list[CellGraph] = []
    for cell in cells:
        cell_out = out if len(cells) == 1 else cell
        graphs.append(analyze_cell(cell, cell_out, args.roi_bbox, args.epsilon, bool(args.visualize)))
    persistence, merge_split, persistence_metrics = compute_persistence(graphs)
    write_outputs(out, graphs, persistence, merge_split, persistence_metrics, str(args.reference_step), bool(args.visualize))
    print(f"[transport-ownership-graph-extractor] cells={len(cells)} out={out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
