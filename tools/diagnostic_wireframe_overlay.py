#!/usr/bin/env python3
"""Build passive diagnostic overlay packets for xPRIMEray render-test captures."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import os
from collections import deque
from pathlib import Path
from typing import Any

import numpy as np
from PIL import Image, ImageDraw, ImageFont


SCHEMA_VERSION = 1
DEFAULT_MANUAL_ROIS = "40,35;280,35;40,145;280,145"
GENERATED_NAMES = {
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
DIAGNOSTIC_SUFFIXES = (
    ".boundary_confidence.png",
    ".domain_confidence.png",
    ".domain_id.png",
    ".normal_discontinuity.png",
    ".selection_flip.png",
    ".step_convergence_confidence.png",
    ".step_sensitivity.png",
    ".precision_required.png",
    ".probe_hit_distance_delta.png",
    ".probe_normal_delta.png",
    ".probe_collider_mismatch.png",
    ".corner_required_precision_map.png",
    ".corner_hit_distance_delta.png",
    ".corner_normal_delta.png",
    ".corner_collider_flip_map.png",
    ".corner_convergence_profile.png",
)


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def load_json(path: Path | None) -> dict[str, Any]:
    if not path or not path.exists():
        return {}
    try:
        return json.loads(path.read_text())
    except Exception:
        return {}


def find_beauty_png(folder: Path) -> Path | None:
    candidates: list[Path] = []
    for path in sorted(folder.glob("*.png")):
        if path.name in GENERATED_NAMES:
            continue
        if path.name.startswith("corner_"):
            continue
        if path.name.endswith(DIAGNOSTIC_SUFFIXES):
            continue
        candidates.append(path)
    if not candidates:
        return None
    candidates.sort(key=lambda p: p.stat().st_size if p.exists() else 0, reverse=True)
    return candidates[0]


def parse_rois(raw: str | None) -> list[tuple[int, int]]:
    rois: list[tuple[int, int]] = []
    for item in (raw or "").split(";"):
        parts = [p.strip() for p in item.split(",")]
        if len(parts) != 2:
            continue
        try:
            rois.append((int(parts[0]), int(parts[1])))
        except ValueError:
            continue
    return rois


def primitive_rois(primitives: dict[str, Any], fallback: str) -> list[tuple[int, int]]:
    items = primitives.get("manual_rois") or []
    rois: list[tuple[int, int]] = []
    for item in items:
        try:
            rois.append((int(item["x"]), int(item["y"])))
        except Exception:
            pass
    return rois or parse_rois(fallback)


def draw_label(draw: ImageDraw.ImageDraw, xy: tuple[int, int], text: str, fill: tuple[int, int, int, int]) -> None:
    if not text:
        return
    x, y = xy
    font = ImageFont.load_default()
    bbox = draw.textbbox((x, y), text, font=font)
    pad = 2
    draw.rectangle((bbox[0] - pad, bbox[1] - pad, bbox[2] + pad, bbox[3] + pad), fill=(0, 0, 0, 180))
    draw.text((x, y), text, fill=fill, font=font)


def circle(draw: ImageDraw.ImageDraw, x: int, y: int, r: int, outline: tuple[int, int, int, int], fill: tuple[int, int, int, int] | None = None, width: int = 1) -> None:
    draw.ellipse((x - r, y - r, x + r, y + r), outline=outline, fill=fill, width=width)


def line_layer(base: Image.Image) -> Image.Image:
    return base.convert("RGBA")


def load_hit_fields(path: Path | None, width: int, height: int) -> dict[str, Any]:
    had = np.zeros((height, width), dtype=bool)
    collider = np.zeros((height, width), dtype=np.uint64)
    domain = np.zeros((height, width), dtype=np.int64)
    hit_distance = np.full((height, width), np.nan, dtype=np.float32)
    path_length = np.full((height, width), np.nan, dtype=np.float32)
    boundary_events = np.zeros((height, width), dtype=np.int64)
    step_count = np.full((height, width), -1, dtype=np.int64)
    normal = np.zeros((height, width, 3), dtype=np.float32)
    present_fields: set[str] = set()
    valued_fields: set[str] = set()
    if not path or not path.exists():
        return {
            "had": had,
            "collider": collider,
            "domain": domain,
            "hit_distance": hit_distance,
            "path_length": path_length,
            "boundary_events": boundary_events,
            "step_count": step_count,
            "normal": normal,
            "present_fields": present_fields,
            "missing_fields": ["hit_diagnostics_csv"],
        }
    with path.open(newline="", encoding="utf-8-sig") as handle:
        reader = csv.DictReader(handle)
        present_fields = set(reader.fieldnames or [])
        for row in reader:
            try:
                x = int(row.get("x", -1))
                y = int(row.get("y", -1))
                if not (0 <= x < width and 0 <= y < height):
                    continue
                h = row.get("had_hit", "0") in {"1", "true", "True"}
                had[y, x] = h
                collider[y, x] = int(row.get("collider_id", "0") or 0)
                domain_value = row.get("domain_id", row.get("curvature_domain_id", ""))
                if domain_value != "":
                    domain[y, x] = int(domain_value or 0)
                    valued_fields.add("domain_id")
                path_value = row.get("path_length", row.get("accumulated_transport_length", ""))
                if path_value not in {"", "nan", "NaN"}:
                    path_length[y, x] = float(path_value)
                    valued_fields.add("path_length")
                boundary_value = row.get("boundary_event_count", row.get("boundary_events", ""))
                if boundary_value != "":
                    boundary_events[y, x] = int(boundary_value or 0)
                    valued_fields.add("boundary_event_count")
                if row.get("portal_event_count", "") != "":
                    valued_fields.add("portal_event_count")
                if row.get("throat_event_count", "") != "":
                    valued_fields.add("throat_event_count")
                step_value = row.get("step_count", row.get("segment_count", ""))
                if step_value != "":
                    step_count[y, x] = int(step_value or 0)
                    valued_fields.add("step_count")
                if "hit_distance" in present_fields:
                    value = row.get("hit_distance", "")
                    if value not in {"", "nan", "NaN"}:
                        hit_distance[y, x] = float(value)
                for idx, name in enumerate(("normal_x", "normal_y", "normal_z")):
                    if name in present_fields:
                        normal[y, x, idx] = float(row.get(name, "0") or 0)
            except Exception:
                continue
    optional = ["domain_id", "path_length", "boundary_event_count", "portal_event_count", "throat_event_count", "step_count"]
    missing_fields = [name for name in optional if name not in valued_fields]
    return {
        "had": had,
        "collider": collider,
        "domain": domain,
        "hit_distance": hit_distance,
        "path_length": path_length,
        "boundary_events": boundary_events,
        "step_count": step_count,
        "normal": normal,
        "present_fields": present_fields,
        "valued_fields": valued_fields,
        "missing_fields": missing_fields,
    }


def extract_regions(had: np.ndarray, collider: np.ndarray) -> list[dict[str, Any]]:
    height, width = had.shape
    seen = np.zeros_like(had, dtype=bool)
    regions: list[dict[str, Any]] = []
    region_id = 0
    for y0 in range(height):
        for x0 in range(width):
            cid = int(collider[y0, x0])
            if seen[y0, x0] or not had[y0, x0] or cid == 0:
                continue
            q: deque[tuple[int, int]] = deque([(x0, y0)])
            seen[y0, x0] = True
            pts: list[tuple[int, int]] = []
            contour = 0
            while q:
                x, y = q.popleft()
                pts.append((x, y))
                is_boundary = False
                for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                    if not (0 <= nx < width and 0 <= ny < height):
                        is_boundary = True
                        continue
                    if not had[ny, nx] or int(collider[ny, nx]) != cid:
                        is_boundary = True
                        continue
                    if not seen[ny, nx]:
                        seen[ny, nx] = True
                        q.append((nx, ny))
                if is_boundary:
                    contour += 1
            xs = [p[0] for p in pts]
            ys = [p[1] for p in pts]
            area = len(pts)
            regions.append({
                "region_id": region_id,
                "collider_id": cid,
                "area": area,
                "centroid_x": round(float(sum(xs)) / max(1, area), 3),
                "centroid_y": round(float(sum(ys)) / max(1, area), 3),
                "bbox_x0": min(xs),
                "bbox_y0": min(ys),
                "bbox_x1": max(xs),
                "bbox_y1": max(ys),
                "contour_pixel_count": contour,
            })
            region_id += 1
    return regions


def draw_regions(base: Image.Image, had: np.ndarray, collider: np.ndarray, regions: list[dict[str, Any]]) -> Image.Image:
    img = base.convert("RGBA")
    overlay = Image.new("RGBA", img.size, (0, 0, 0, 0))
    px = overlay.load()
    height, width = had.shape
    for y in range(height):
        for x in range(width):
            cid = int(collider[y, x])
            if not had[y, x] or cid == 0:
                continue
            color_seed = cid % 255
            px[x, y] = (20, 120 + color_seed // 4, 220, 45)
    img = Image.alpha_composite(img, overlay)
    draw = ImageDraw.Draw(img)
    for r in regions:
        x0, y0, x1, y1 = int(r["bbox_x0"]), int(r["bbox_y0"]), int(r["bbox_x1"]), int(r["bbox_y1"])
        draw.rectangle((x0, y0, x1, y1), outline=(0, 220, 255, 170), width=1)
        circle(draw, int(float(r["centroid_x"])), int(float(r["centroid_y"])), 2, (0, 255, 255, 220), fill=(0, 255, 255, 160))
    return img


def draw_cartesian(base: Image.Image, primitives: dict[str, Any], labels: bool) -> Image.Image:
    img = line_layer(base)
    draw = ImageDraw.Draw(img)
    cart = (primitives.get("primitives") or {}).get("cartesian") or {}
    for line in cart.get("lines") or []:
        draw.line((line["x0"], line["y0"], line["x1"], line["y1"]), fill=(255, 40, 30, 235), width=2)
    for pt in cart.get("points") or []:
        x, y = int(pt.get("x", -1)), int(pt.get("y", -1))
        kind = str(pt.get("kind", "anchor"))
        r = 4 if kind == "corner" else 3
        fill = (255, 40, 30, 210) if kind in {"corner", "centroid"} else (255, 120, 80, 210)
        circle(draw, x, y, r, (255, 30, 20, 255), fill=fill, width=1)
        if labels and pt.get("label"):
            draw_label(draw, (x + 5, y + 4), str(pt.get("label")), (255, 255, 255, 255))
    return img


def find_optional_csv(folder: Path, suffix: str) -> Path | None:
    matches = sorted(folder.glob(f"*{suffix}"))
    if matches:
        return matches[0]
    return None


def draw_risk(base: Image.Image, folder: Path, rois: list[tuple[int, int]]) -> Image.Image:
    img = line_layer(base)
    draw = ImageDraw.Draw(img)
    for idx, (x, y) in enumerate(rois):
        circle(draw, x, y, 7, (255, 230, 20, 255), width=2)
        draw.line((x - 9, y, x + 9, y), fill=(255, 230, 20, 230), width=1)
        draw.line((x, y - 9, x, y + 9), fill=(255, 230, 20, 230), width=1)
        draw_label(draw, (x + 8, y - 8), f"ROI{idx}", (255, 255, 255, 255))
    for path in [find_optional_csv(folder, ".corner_transport_probe.csv"), folder / "corner_transport_probe.csv"]:
        if not path or not path.exists():
            continue
        try:
            with path.open(newline="") as handle:
                for row in csv.DictReader(handle):
                    x, y = int(row.get("x", -1)), int(row.get("y", -1))
                    if x < 0 or y < 0:
                        continue
                    risk = float(row.get("max_decision_risk", row.get("decision_risk", 0)) or 0)
                    flip = str(row.get("collider_flip_any", "False")).lower() == "true" or str(row.get("hit_ownership_change_any", "False")).lower() == "true"
                    color = (255, 0, 220, 230) if flip else (255, 180, 0, 190)
                    circle(draw, x, y, 2 if risk < 1 else 3, color, fill=color)
        except Exception:
            pass
        break
    return img


def draw_spacetime(base: Image.Image, primitives: dict[str, Any]) -> Image.Image | None:
    space = (primitives.get("primitives") or {}).get("spacetime") or {}
    if not space.get("polylines") and not space.get("events"):
        return None
    img = line_layer(base)
    draw = ImageDraw.Draw(img)
    for poly in space.get("polylines") or []:
        pts = [(int(p["x"]), int(p["y"])) for p in poly.get("points", []) if "x" in p and "y" in p]
        if len(pts) >= 2:
            order = int(poly.get("order_index", 0))
            color = (40, min(255, 120 + order * 7), 80, 220)
            draw.line(pts, fill=color, width=1)
            mid = pts[len(pts) // 2]
            draw_label(draw, (mid[0] + 2, mid[1] + 2), str(order), (255, 255, 255, 230))
    for ev in space.get("events") or []:
        x, y = int(ev.get("x", -1)), int(ev.get("y", -1))
        circle(draw, x, y, 4, (255, 145, 20, 255), fill=(255, 145, 20, 160), width=1)
    return img


def normal_angle_degrees(a: np.ndarray, b: np.ndarray) -> float:
    an = float(np.linalg.norm(a))
    bn = float(np.linalg.norm(b))
    if an <= 1e-8 or bn <= 1e-8:
        return 0.0
    dot = float(np.clip(np.dot(a, b) / (an * bn), -1.0, 1.0))
    return float(np.degrees(np.arccos(dot)))


def finite_delta(a: float, b: float) -> float:
    if not np.isfinite(a) or not np.isfinite(b):
        return 0.0
    return float(abs(a - b))


def classify_vector_color(row: dict[str, Any]) -> tuple[int, int, int, int]:
    if int(row["boundary_event_delta"]) != 0:
        return (255, 140, 0, 230)
    if int(row["collider_change"]) or int(row["domain_change"]):
        return (255, 0, 220, 235)
    normal_score = min(1.0, float(row["hit_normal_angle_delta"]) / 45.0)
    distance_score = min(1.0, float(row["hit_distance_delta"]) / 5.0) + min(1.0, float(row["path_length_delta"]) / 5.0)
    if normal_score >= distance_score and normal_score > 0:
        return (255, 230, 20, 220)
    return (0, 220, 255, 220)


def compute_continuity_vectors(fields: dict[str, Any], stride: int, threshold: float, include_diagonal: bool) -> list[dict[str, Any]]:
    had = fields["had"]
    collider = fields["collider"]
    domain = fields["domain"]
    hit_distance = fields["hit_distance"]
    path_length = fields["path_length"]
    boundary_events = fields["boundary_events"]
    step_count = fields["step_count"]
    normal = fields["normal"]
    height, width = had.shape
    directions = [(1, 0), (0, 1)]
    if include_diagonal:
        directions.extend([(1, 1), (-1, 1)])
    step = max(1, int(stride))
    rows: list[dict[str, Any]] = []
    for y in range(height):
        for x in range(width):
            for dx, dy in directions:
                nx, ny = x + dx, y + dy
                if not (0 <= nx < width and 0 <= ny < height):
                    continue
                if not had[ny, nx] and int(collider[ny, nx]) == 0:
                    continue
                collider_change = 1 if bool(had[y, x]) != bool(had[ny, nx]) or int(collider[y, x]) != int(collider[ny, nx]) else 0
                domain_change = 1 if int(domain[y, x]) != int(domain[ny, nx]) else 0
                hit_distance_delta = finite_delta(float(hit_distance[y, x]), float(hit_distance[ny, nx]))
                path_length_delta = finite_delta(float(path_length[y, x]), float(path_length[ny, nx]))
                step_count_delta = 0.0
                if int(step_count[y, x]) >= 0 and int(step_count[ny, nx]) >= 0:
                    step_count_delta = float(abs(int(step_count[y, x]) - int(step_count[ny, nx])))
                angle_delta = normal_angle_degrees(normal[y, x], normal[ny, nx]) if had[y, x] and had[ny, nx] else 0.0
                boundary_delta = abs(int(boundary_events[y, x]) - int(boundary_events[ny, nx]))
                ownership_flip_score = 1.0 if collider_change or domain_change else 0.0
                total = (
                    ownership_flip_score
                    + min(1.0, hit_distance_delta / 5.0)
                    + min(1.0, angle_delta / 45.0)
                    + min(1.0, path_length_delta / 5.0)
                    + min(1.0, step_count_delta / 64.0)
                    + min(1.0, float(boundary_delta))
                )
                sampled = (x % step == 0 and y % step == 0)
                if not sampled and ownership_flip_score <= 0:
                    continue
                if total < threshold and ownership_flip_score <= 0:
                    continue
                rows.append({
                    "x": x,
                    "y": y,
                    "neighbor_x": nx,
                    "neighbor_y": ny,
                    "collider_change": collider_change,
                    "domain_change": domain_change,
                    "hit_distance_delta": round(hit_distance_delta, 6),
                    "hit_normal_angle_delta": round(angle_delta, 6),
                    "path_length_delta": round(path_length_delta, 6),
                    "boundary_event_delta": boundary_delta,
                    "step_count_delta": round(step_count_delta, 6),
                    "ownership_flip_score": round(ownership_flip_score, 6),
                    "total_transport_discontinuity_score": round(total, 6),
                })
    rows.sort(key=lambda r: float(r["total_transport_discontinuity_score"]), reverse=True)
    return rows


def draw_arrow(draw: ImageDraw.ImageDraw, x0: int, y0: int, x1: int, y1: int, color: tuple[int, int, int, int], width: int) -> None:
    draw.line((x0, y0, x1, y1), fill=color, width=width)
    vx, vy = x1 - x0, y1 - y0
    length = max(1.0, (vx * vx + vy * vy) ** 0.5)
    ux, uy = vx / length, vy / length
    px, py = -uy, ux
    size = 4 + width
    p1 = (int(x1 - ux * size + px * size * 0.55), int(y1 - uy * size + py * size * 0.55))
    p2 = (int(x1 - ux * size - px * size * 0.55), int(y1 - uy * size - py * size * 0.55))
    draw.polygon([(x1, y1), p1, p2], fill=color)


def draw_continuity_vectors(base: Image.Image, vectors: list[dict[str, Any]], max_vectors: int) -> Image.Image:
    img = line_layer(base)
    draw = ImageDraw.Draw(img)
    for row in vectors[: max(0, int(max_vectors))]:
        score = float(row["total_transport_discontinuity_score"])
        color = classify_vector_color(row)
        alpha = int(min(245, max(90, 90 + score * 55)))
        color = (color[0], color[1], color[2], alpha)
        width = 1 + int(score >= 1.0) + int(score >= 2.0)
        draw_arrow(draw, int(row["x"]), int(row["y"]), int(row["neighbor_x"]), int(row["neighbor_y"]), color, width)
    return img


def write_continuity_csv(path: Path, vectors: list[dict[str, Any]]) -> None:
    cols = [
        "x",
        "y",
        "neighbor_x",
        "neighbor_y",
        "collider_change",
        "domain_change",
        "hit_distance_delta",
        "hit_normal_angle_delta",
        "path_length_delta",
        "boundary_event_delta",
        "step_count_delta",
        "ownership_flip_score",
        "total_transport_discontinuity_score",
    ]
    with path.open("w", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=cols)
        writer.writeheader()
        for row in vectors:
            writer.writerow({c: row.get(c, "") for c in cols})


def point_near_roi(x: int, y: int, rois: list[tuple[int, int]], radius: int = 12) -> bool:
    rr = radius * radius
    return any((x - rx) * (x - rx) + (y - ry) * (y - ry) <= rr for rx, ry in rois)


def correlate_vectors_with_regions(regions: list[dict[str, Any]], vectors: list[dict[str, Any]], high_threshold: float) -> list[dict[str, Any]]:
    for region in regions:
        x0, y0 = int(region["bbox_x0"]), int(region["bbox_y0"])
        x1, y1 = int(region["bbox_x1"]), int(region["bbox_y1"])
        region_vectors = [
            row for row in vectors
            if (
                x0 <= int(row["x"]) <= x1 and y0 <= int(row["y"]) <= y1
            ) or (
                x0 <= int(row["neighbor_x"]) <= x1 and y0 <= int(row["neighbor_y"]) <= y1
            )
        ]
        scores = [float(row["total_transport_discontinuity_score"]) for row in region_vectors]
        flips = sum(1 for row in region_vectors if int(row["collider_change"]) or int(row["domain_change"]))
        high = sum(1 for score in scores if score >= high_threshold)
        contour = max(1, int(region.get("contour_pixel_count", 1) or 1))
        region["mean_discontinuity_score"] = round(float(np.mean(scores)) if scores else 0.0, 6)
        region["max_discontinuity_score"] = round(max(scores) if scores else 0.0, 6)
        region["boundary_flip_count"] = flips
        region["high_vector_count"] = high
        region["boundary_high_vector_density"] = round(high / contour, 6)
        region["boundary_aligns_with_high_vector_density"] = bool(high / contour >= 0.05)
    return regions


def build_continuity_summary(
    vectors: list[dict[str, Any]],
    rois: list[tuple[int, int]],
    regions: list[dict[str, Any]],
    threshold: float,
    high_threshold: float,
    missing_fields: list[str],
) -> dict[str, Any]:
    scores = [float(row["total_transport_discontinuity_score"]) for row in vectors]
    roi_overlap = sum(1 for row in vectors if point_near_roi(int(row["x"]), int(row["y"]), rois) or point_near_roi(int(row["neighbor_x"]), int(row["neighbor_y"]), rois))
    shape_overlap = sum(
        1 for row in vectors
        if any(
            (
                int(r["bbox_x0"]) <= int(row["x"]) <= int(r["bbox_x1"])
                and int(r["bbox_y0"]) <= int(row["y"]) <= int(r["bbox_y1"])
            ) or (
                int(r["bbox_x0"]) <= int(row["neighbor_x"]) <= int(r["bbox_x1"])
                and int(r["bbox_y0"]) <= int(row["neighbor_y"]) <= int(r["bbox_y1"])
            )
            for r in regions
        )
    )
    return {
        "total_vectors": len(vectors),
        "threshold": threshold,
        "high_threshold": high_threshold,
        "high_discontinuity_vectors": sum(1 for score in scores if score >= high_threshold),
        "mean_discontinuity_score": round(float(np.mean(scores)) if scores else 0.0, 6),
        "max_discontinuity_score": round(max(scores) if scores else 0.0, 6),
        "top_10_discontinuity_locations": vectors[:10],
        "manual_roi_overlap_count": roi_overlap,
        "transport_shape_region_overlap_count": shape_overlap,
        "missing_optional_fields": missing_fields,
        "shape_region_correlations": [
            {
                "region_id": r.get("region_id"),
                "collider_id": r.get("collider_id"),
                "mean_discontinuity_score": r.get("mean_discontinuity_score", 0),
                "max_discontinuity_score": r.get("max_discontinuity_score", 0),
                "boundary_flip_count": r.get("boundary_flip_count", 0),
                "boundary_aligns_with_high_vector_density": r.get("boundary_aligns_with_high_vector_density", False),
            }
            for r in regions
        ],
    }


def write_continuity_summary_md(path: Path, summary: dict[str, Any]) -> None:
    lines = [
        "# Transport Continuity Summary",
        "",
        "Passive post-capture diagnostic only. These vectors are not consumed by rendering, scheduling, hit selection, shading, resolver scoring, or adaptive precision.",
        "",
        f"- total_vectors: {summary['total_vectors']}",
        f"- high_discontinuity_vectors: {summary['high_discontinuity_vectors']}",
        f"- mean_discontinuity_score: {summary['mean_discontinuity_score']}",
        f"- max_discontinuity_score: {summary['max_discontinuity_score']}",
        f"- manual_roi_overlap_count: {summary['manual_roi_overlap_count']}",
        f"- transport_shape_region_overlap_count: {summary['transport_shape_region_overlap_count']}",
    ]
    if summary.get("missing_optional_fields"):
        lines.append(f"- missing_optional_fields: {', '.join(summary['missing_optional_fields'])}")
    lines.extend(["", "## Top Discontinuities", ""])
    for row in summary.get("top_10_discontinuity_locations", []):
        lines.append(
            f"- ({row['x']},{row['y']}) -> ({row['neighbor_x']},{row['neighbor_y']}): "
            f"score={row['total_transport_discontinuity_score']} collider_change={row['collider_change']} "
            f"normal_angle={row['hit_normal_angle_delta']}"
        )
    path.write_text("\n".join(lines) + "\n")


def add_legend(img: Image.Image, labels: list[tuple[str, tuple[int, int, int, int]]]) -> Image.Image:
    out = img.convert("RGBA")
    draw = ImageDraw.Draw(out)
    font = ImageFont.load_default()
    widths = [draw.textbbox((0, 0), text, font=font)[2] for text, _ in labels]
    w = max(widths or [0]) + 32
    h = 16 * len(labels) + 8
    x = max(6, (out.width - w) // 2)
    y = 6
    draw.rectangle((x, y, x + w, y + h), fill=(0, 0, 0, 150))
    yy = y + 6
    for text, color in labels:
        draw.rectangle((x + 6, yy + 3, x + 16, yy + 10), fill=color)
        draw.text((x + 22, yy), text, fill=(255, 255, 255, 240), font=font)
        yy += 16
    return out


def save_contact_sheet(folder: Path, images: list[tuple[str, Path]]) -> None:
    thumbs: list[tuple[str, Image.Image]] = []
    for title, path in images:
        if not path.exists():
            continue
        img = Image.open(path).convert("RGB")
        img.thumbnail((240, 135), Image.Resampling.LANCZOS)
        thumbs.append((title, img))
    if not thumbs:
        return
    w, h, label_h = 240, 135, 24
    sheet = Image.new("RGB", (w * len(thumbs), h + label_h), "white")
    draw = ImageDraw.Draw(sheet)
    for i, (title, img) in enumerate(thumbs):
        x = i * w
        draw.text((x + 6, 6), title, fill=(0, 0, 0))
        sheet.paste(img, (x, label_h))
    sheet.save(folder / "diagnostic_overlay_contact_sheet.png")


def write_regions_csv(path: Path, regions: list[dict[str, Any]]) -> None:
    cols = [
        "region_id",
        "collider_id",
        "area",
        "centroid_x",
        "centroid_y",
        "bbox_x0",
        "bbox_y0",
        "bbox_x1",
        "bbox_y1",
        "contour_pixel_count",
        "mean_discontinuity_score",
        "max_discontinuity_score",
        "boundary_flip_count",
        "high_vector_count",
        "boundary_high_vector_density",
        "boundary_aligns_with_high_vector_density",
    ]
    with path.open("w", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=cols)
        writer.writeheader()
        for row in regions:
            writer.writerow({c: row.get(c, "") for c in cols})


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("capture_folder", type=Path)
    parser.add_argument("--use-opencv-contours", type=int, default=0)
    parser.add_argument("--manual-rois", default=DEFAULT_MANUAL_ROIS)
    parser.add_argument("--continuity", "--diagnostic-wireframe-continuity", dest="continuity", type=int, default=int(os.environ.get("DIAGNOSTIC_WIREFRAME_CONTINUITY", "0") or 0))
    parser.add_argument("--continuity-stride", type=int, default=4)
    parser.add_argument("--continuity-threshold", type=float, default=0.2)
    parser.add_argument("--continuity-high-threshold", type=float, default=1.0)
    parser.add_argument("--continuity-diagonal", type=int, default=0)
    parser.add_argument("--continuity-max-vectors", type=int, default=2500)
    args = parser.parse_args()

    folder = args.capture_folder
    beauty_path = find_beauty_png(folder)
    if beauty_path is None:
        raise SystemExit(f"no beauty png found in {folder}")
    beauty = Image.open(beauty_path).convert("RGBA")
    width, height = beauty.size
    stem = beauty_path.stem
    primitives_path = folder / f"{stem}.diagnostic_wireframe_primitives.json"
    if not primitives_path.exists():
        found = sorted(folder.glob("*.diagnostic_wireframe_primitives.json"))
        primitives_path = found[0] if found else primitives_path
    primitives = load_json(primitives_path if primitives_path.exists() else None)
    hit_csv = folder / f"{stem}.hit_diagnostics.csv"
    if not hit_csv.exists():
        found = sorted(folder.glob("*.hit_diagnostics.csv"))
        hit_csv = found[0] if found else hit_csv
    metadata_path = folder / "metadata.json"
    metadata = load_json(metadata_path if metadata_path.exists() else None)

    enabled = primitives.get("enabled_layers") or {}
    labels_enabled = bool(enabled.get("labels", True))
    rois = primitive_rois(primitives, args.manual_rois or metadata.get("diagnostic_wireframe_manual_rois") or DEFAULT_MANUAL_ROIS)
    missing: list[str] = []
    if not primitives_path.exists():
        missing.append("diagnostic_wireframe_primitives_json")
    if not hit_csv.exists():
        missing.append("hit_diagnostics_csv")

    beauty.save(folder / "layer0_beauty.png")

    layer1 = draw_cartesian(beauty, primitives, labels_enabled)
    layer1 = add_legend(layer1, [("Cartesian object projection", (255, 40, 30, 230))])
    layer1.save(folder / "layer1_cartesian_wireframe.png")

    hit_fields = load_hit_fields(hit_csv if hit_csv.exists() else None, width, height)
    if bool(args.continuity or enabled.get("continuity", False)):
        for field in hit_fields.get("missing_fields", []):
            token = f"continuity_optional_field_{field}"
            if token not in missing:
                missing.append(token)
    had = hit_fields["had"]
    collider = hit_fields["collider"]
    regions = extract_regions(had, collider) if hit_csv.exists() else []
    layer2 = draw_regions(beauty, had, collider, regions)
    layer2 = add_legend(layer2, [("Transport ownership", (0, 220, 255, 200))])
    layer2.save(folder / "layer2_transport_ownership.png")
    layer2.save(folder / "transport_shape_regions_overlay.png")

    layer3 = draw_risk(beauty, folder, rois)
    layer3 = add_legend(layer3, [("Manual ROI", (255, 230, 20, 230)), ("Risk/probe sample", (255, 0, 220, 230))])
    layer3.save(folder / "layer3_risk_probe_markers.png")

    layer4 = draw_spacetime(beauty, primitives)
    if layer4 is not None:
        layer4 = add_legend(layer4, [("Symbolic path", (40, 220, 80, 220)), ("First-hit event", (255, 145, 20, 220))])
        layer4.save(folder / "layer4_spacetime_transport_diagram.png")

    continuity_enabled = bool(args.continuity or enabled.get("continuity", False))
    continuity_vectors: list[dict[str, Any]] = []
    continuity_summary: dict[str, Any] = {}
    if continuity_enabled:
        continuity_vectors = compute_continuity_vectors(
            hit_fields,
            stride=max(1, args.continuity_stride),
            threshold=max(0.0, args.continuity_threshold),
            include_diagonal=bool(args.continuity_diagonal),
        ) if hit_csv.exists() else []
        regions = correlate_vectors_with_regions(regions, continuity_vectors, max(args.continuity_high_threshold, args.continuity_threshold))
        layer5 = draw_continuity_vectors(beauty, continuity_vectors, args.continuity_max_vectors)
        layer5 = add_legend(layer5, [
            ("Collider/domain flip", (255, 0, 220, 235)),
            ("Normal delta", (255, 230, 20, 220)),
            ("Distance/path delta", (0, 220, 255, 220)),
            ("Boundary event delta", (255, 140, 0, 230)),
        ])
        if not hit_csv.exists():
            draw_label(ImageDraw.Draw(layer5), (8, height - 18), "continuity unavailable: missing hit diagnostics", (255, 255, 255, 240))
        layer5.save(folder / "layer5_transport_continuity_vectors.png")
        write_continuity_csv(folder / "transport_continuity_vectors.csv", continuity_vectors)
        continuity_summary = build_continuity_summary(
            continuity_vectors,
            rois,
            regions,
            threshold=max(0.0, args.continuity_threshold),
            high_threshold=max(args.continuity_high_threshold, args.continuity_threshold),
            missing_fields=list(hit_fields.get("missing_fields", [])),
        )
        (folder / "transport_continuity_summary.json").write_text(json.dumps(continuity_summary, indent=2, sort_keys=True) + "\n")
        write_continuity_summary_md(folder / "transport_continuity_summary.md", continuity_summary)
    write_regions_csv(folder / "transport_shape_regions.csv", regions)

    combined = beauty.convert("RGBA")
    for layer_path in (
        "layer1_cartesian_wireframe.png",
        "layer2_transport_ownership.png",
        "layer3_risk_probe_markers.png",
        "layer4_spacetime_transport_diagram.png",
        "layer5_transport_continuity_vectors.png",
    ):
        p = folder / layer_path
        if p.exists():
            layer = Image.open(p).convert("RGBA")
            # Blend layer overlays softly with the original beauty to preserve context.
            blend = 0.50 if "transport_ownership" in layer_path else 0.72
            if "continuity" in layer_path:
                blend = 0.78
            combined = Image.blend(combined, layer, blend)
    combined = add_legend(combined, [
        ("Cartesian", (255, 40, 30, 230)),
        ("Transport", (0, 220, 255, 200)),
        ("Risk/probe", (255, 230, 20, 230)),
        ("Continuity", (255, 0, 220, 235)),
    ])
    combined.save(folder / "combined_diagnostic_overlay.png")

    contact_items = [
        ("beauty", folder / "layer0_beauty.png"),
        ("Cartesian", folder / "layer1_cartesian_wireframe.png"),
        ("transport", folder / "layer2_transport_ownership.png"),
        ("risk/probe", folder / "layer3_risk_probe_markers.png"),
        ("continuity", folder / "layer5_transport_continuity_vectors.png"),
        ("combined", folder / "combined_diagnostic_overlay.png"),
    ]
    save_contact_sheet(folder, contact_items)

    counts = primitives.get("primitive_count_by_layer") or {}
    object_count = int(primitives.get("object_count", 0) or 0)
    metadata_out = {
        "schema_version": SCHEMA_VERSION,
        "capture_stem": stem,
        "enabled_layers": {
            "cartesian": True,
            "transport": True,
            "risk": True,
            "spacetime": (folder / "layer4_spacetime_transport_diagram.png").exists(),
            "continuity": continuity_enabled,
            "labels": labels_enabled,
        },
        "missing_inputs": missing,
        "object_count": object_count,
        "primitive_count_by_layer": counts,
        "hit_region_count": len(regions),
        "manual_rois": [{"x": x, "y": y} for x, y in rois],
        "beauty_hash": sha256_file(beauty_path),
        "beauty_path": str(beauty_path),
        "primitive_path": str(primitives_path) if primitives_path.exists() else "",
        "hit_diagnostics_csv": str(hit_csv) if hit_csv.exists() else "",
        "post_process_only": True,
        "transport_continuity_vectors_csv": str(folder / "transport_continuity_vectors.csv") if (folder / "transport_continuity_vectors.csv").exists() else "",
        "transport_continuity_summary": continuity_summary,
        "opencv_requested": bool(args.use_opencv_contours),
        "opencv_used": False,
    }
    (folder / "overlay_metadata.json").write_text(json.dumps(metadata_out, indent=2, sort_keys=True) + "\n")
    print(f"[diagnostic-wireframe-overlay] folder={folder} stem={stem} objects={object_count} regions={len(regions)} missing={','.join(missing) or 'none'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
