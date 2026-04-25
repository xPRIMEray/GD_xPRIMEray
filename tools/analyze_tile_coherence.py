#!/usr/bin/env python3
"""Build fixed/adaptive tile coherence diagnostics from cached per-pixel hit data."""

from __future__ import annotations

import argparse
import csv
import json
import math
from collections import Counter
from pathlib import Path

try:
    from PIL import Image, ImageDraw
except ImportError as exc:  # pragma: no cover - script dependency guard
    raise SystemExit("Pillow is required for tile coherence image outputs") from exc


def parse_float(row: dict[str, str], key: str, default: float = 0.0) -> float:
    try:
        return float(row.get(key, default))
    except (TypeError, ValueError):
        return default


def parse_int(row: dict[str, str], key: str, default: int = 0) -> int:
    try:
        return int(float(row.get(key, default)))
    except (TypeError, ValueError):
        return default


def percentile(values: list[float], pct: float) -> float:
    if not values:
        return 0.0
    ordered = sorted(values)
    index = min(len(ordered) - 1, max(0, int(round((pct / 100.0) * (len(ordered) - 1)))))
    return ordered[index]


def variance(values: list[float]) -> float:
    if not values:
        return 0.0
    mean = sum(values) / len(values)
    return sum((value - mean) ** 2 for value in values) / len(values)


def pearson(lhs: list[float], rhs: list[float]) -> float:
    if len(lhs) != len(rhs) or not lhs:
        return 0.0
    mean_l = sum(lhs) / len(lhs)
    mean_r = sum(rhs) / len(rhs)
    num = sum((l - mean_l) * (r - mean_r) for l, r in zip(lhs, rhs))
    den_l = math.sqrt(sum((l - mean_l) ** 2 for l in lhs))
    den_r = math.sqrt(sum((r - mean_r) ** 2 for r in rhs))
    den = den_l * den_r
    return num / den if den > 1e-12 else 0.0


def iou(lhs: list[bool], rhs: list[bool]) -> float:
    if len(lhs) != len(rhs) or not lhs:
        return 0.0
    intersection = sum(1 for l, r in zip(lhs, rhs) if l and r)
    union = sum(1 for l, r in zip(lhs, rhs) if l or r)
    return intersection / union if union else 0.0


def otsu_threshold(values: list[float]) -> float:
    if not values:
        return 0.0
    lo = min(values)
    hi = max(values)
    if hi <= lo:
        return hi

    bins = [0] * 256
    scale = 255.0 / (hi - lo)
    for value in values:
        bins[max(0, min(255, int((value - lo) * scale)))] += 1

    total = len(values)
    sum_total = sum(index * count for index, count in enumerate(bins))
    weight_bg = 0
    sum_bg = 0.0
    best_index = 0
    best_between = -1.0
    for index, count in enumerate(bins):
        weight_bg += count
        if weight_bg == 0:
            continue
        weight_fg = total - weight_bg
        if weight_fg == 0:
            break
        sum_bg += index * count
        mean_bg = sum_bg / weight_bg
        mean_fg = (sum_total - sum_bg) / weight_fg
        between = weight_bg * weight_fg * (mean_bg - mean_fg) ** 2
        if between > best_between:
            best_between = between
            best_index = index
    return lo + (best_index / 255.0) * (hi - lo)


def load_rows(csv_path: Path) -> tuple[int, int, list[dict[str, str]]]:
    rows: list[dict[str, str]] = []
    with csv_path.open(newline="", encoding="utf-8-sig") as handle:
        for row in csv.DictReader(handle):
            rows.append(row)
    width = max((parse_int(row, "x", -1) for row in rows), default=-1) + 1
    height = max((parse_int(row, "y", -1) for row in rows), default=-1) + 1
    return width, height, rows


def normal_tuple(row: dict[str, str]) -> tuple[float, float, float]:
    nx = parse_float(row, "normal_x")
    ny = parse_float(row, "normal_y")
    nz = parse_float(row, "normal_z")
    length = math.sqrt((nx * nx) + (ny * ny) + (nz * nz))
    if length <= 1e-12:
        return (0.0, 0.0, 0.0)
    return (nx / length, ny / length, nz / length)


def visible_band_signal(debug_image_path: Path, width: int, height: int) -> list[float]:
    with Image.open(debug_image_path) as image:
        rgb = image.convert("RGB")
        if rgb.size != (width, height):
            rgb = rgb.resize((width, height), Image.Resampling.BILINEAR)
        pixels = list(rgb.getdata())

    signal = [0.0] * (width * height)
    for y in range(height):
        for x in range(width):
            r, g, b = pixels[(y * width) + x]
            best = 0.0
            for dx, dy in ((1, 0), (0, 1)):
                nx = x + dx
                ny = y + dy
                if nx >= width or ny >= height:
                    continue
                nr, ng, nb = pixels[(ny * width) + nx]
                delta = math.sqrt((r - nr) ** 2 + (g - ng) ** 2 + (b - nb) ** 2) / (255.0 * math.sqrt(3.0))
                best = max(best, delta)
            signal[(y * width) + x] = best
    return signal


def color_ramp(value: float) -> tuple[int, int, int]:
    v = max(0.0, min(1.0, value))
    if v < 0.5:
        t = v / 0.5
        return (int(30 + 35 * t), int(68 + 142 * t), int(130 - 70 * t))
    t = (v - 0.5) / 0.5
    return (int(65 + 185 * t), int(210 - 90 * t), int(60 - 25 * t))


def normalize(values: list[float], value: float) -> float:
    hi = percentile(values, 95.0)
    if hi <= 1e-12:
        return 0.0
    return max(0.0, min(1.0, value / hi))


def compute_tiles(width: int, height: int, rows: list[dict[str, str]], tile_size: int) -> tuple[list[dict], list[float]]:
    by_pixel = build_pixel_index(rows)

    raw_tiles: list[dict] = []
    for y0 in range(0, height, tile_size):
        for x0 in range(0, width, tile_size):
            x1 = min(width, x0 + tile_size)
            y1 = min(height, y0 + tile_size)
            normals: list[tuple[float, float, float]] = []
            distances: list[float] = []
            segments: list[float] = []
            classes: Counter[str] = Counter()
            hit_count = 0
            collider_edges = 0
            comparable_edges = 0

            for y in range(y0, y1):
                for x in range(x0, x1):
                    row = by_pixel.get((x, y))
                    if not row:
                        continue
                    classes[row.get("hit_class", "unknown")] += 1
                    if parse_int(row, "had_hit", 0) == 0:
                        continue
                    hit_count += 1
                    normals.append(normal_tuple(row))
                    distances.append(parse_float(row, "hit_distance", 0.0))
                    segment = parse_int(row, "first_accepted_segment_index", parse_int(row, "segment_index", -1))
                    if segment < 0:
                        segment = parse_int(row, "segment_count", -1)
                    if segment >= 0:
                        segments.append(float(segment))

                    cid = parse_int(row, "collider_id", 0)
                    for dx, dy in ((1, 0), (0, 1)):
                        nx = x + dx
                        ny = y + dy
                        if nx >= x1 or ny >= y1:
                            continue
                        other = by_pixel.get((nx, ny))
                        if not other or parse_int(other, "had_hit", 0) == 0:
                            continue
                        comparable_edges += 1
                        if cid != parse_int(other, "collider_id", 0):
                            collider_edges += 1

            if normals:
                mx = sum(n[0] for n in normals) / len(normals)
                my = sum(n[1] for n in normals) / len(normals)
                mz = sum(n[2] for n in normals) / len(normals)
                normal_variance = 1.0 - max(0.0, min(1.0, math.sqrt((mx * mx) + (my * my) + (mz * mz))))
            else:
                normal_variance = 0.0

            tile_area = max(1, (x1 - x0) * (y1 - y0))
            raw_tiles.append(
                {
                    "x": x0,
                    "y": y0,
                    "w": x1 - x0,
                    "h": y1 - y0,
                    "hit_count": hit_count,
                    "hit_ratio": hit_count / tile_area,
                    "normal_variance": normal_variance,
                    "collider_switch_density": collider_edges / comparable_edges if comparable_edges else 0.0,
                    "hit_distance_variance": variance(distances),
                    "segment_index_variance": variance(segments),
                    "classification_diversity": len(classes) / max(1, sum(classes.values())),
                    "classification_count": len(classes),
                    "dominant_class": classes.most_common(1)[0][0] if classes else "none",
                }
            )

    normal_values = [tile["normal_variance"] for tile in raw_tiles]
    collider_values = [tile["collider_switch_density"] for tile in raw_tiles]
    distance_values = [tile["hit_distance_variance"] for tile in raw_tiles]
    segment_values = [tile["segment_index_variance"] for tile in raw_tiles]
    diversity_values = [tile["classification_diversity"] for tile in raw_tiles]

    pixel_scores = [0.0] * (width * height)
    for tile in raw_tiles:
        components = {
            "normal": normalize(normal_values, tile["normal_variance"]),
            "collider": normalize(collider_values, tile["collider_switch_density"]),
            "distance": normalize(distance_values, tile["hit_distance_variance"]),
            "segment": normalize(segment_values, tile["segment_index_variance"]),
            "classification": normalize(diversity_values, tile["classification_diversity"]),
        }
        score = (
            components["normal"] * 0.30
            + components["collider"] * 0.25
            + components["distance"] * 0.15
            + components["segment"] * 0.20
            + components["classification"] * 0.10
        )
        tile["component_scores"] = components
        tile["coherence_score"] = score
        for y in range(tile["y"], tile["y"] + tile["h"]):
            for x in range(tile["x"], tile["x"] + tile["w"]):
                pixel_scores[(y * width) + x] = score

    return raw_tiles, pixel_scores


def build_pixel_index(rows: list[dict[str, str]]) -> dict[tuple[int, int], dict[str, str]]:
    by_pixel: dict[tuple[int, int], dict[str, str]] = {}
    for row in rows:
        by_pixel[(parse_int(row, "x", -1), parse_int(row, "y", -1))] = row
    return by_pixel


def compute_region_metrics(
    by_pixel: dict[tuple[int, int], dict[str, str]],
    x0: int,
    y0: int,
    w: int,
    h: int,
) -> dict:
    x1 = x0 + w
    y1 = y0 + h
    normals: list[tuple[float, float, float]] = []
    distances: list[float] = []
    segments: list[float] = []
    classes: Counter[str] = Counter()
    hit_count = 0
    collider_edges = 0
    comparable_edges = 0

    for y in range(y0, y1):
        for x in range(x0, x1):
            row = by_pixel.get((x, y))
            if not row:
                continue
            classes[row.get("hit_class", "unknown")] += 1
            if parse_int(row, "had_hit", 0) == 0:
                continue
            hit_count += 1
            normals.append(normal_tuple(row))
            distances.append(parse_float(row, "hit_distance", 0.0))
            segment = parse_int(row, "first_accepted_segment_index", parse_int(row, "segment_index", -1))
            if segment < 0:
                segment = parse_int(row, "segment_count", -1)
            if segment >= 0:
                segments.append(float(segment))

            cid = parse_int(row, "collider_id", 0)
            for dx, dy in ((1, 0), (0, 1)):
                nx = x + dx
                ny = y + dy
                if nx >= x1 or ny >= y1:
                    continue
                other = by_pixel.get((nx, ny))
                if not other or parse_int(other, "had_hit", 0) == 0:
                    continue
                comparable_edges += 1
                if cid != parse_int(other, "collider_id", 0):
                    collider_edges += 1

    if normals:
        mx = sum(n[0] for n in normals) / len(normals)
        my = sum(n[1] for n in normals) / len(normals)
        mz = sum(n[2] for n in normals) / len(normals)
        normal_variance = 1.0 - max(0.0, min(1.0, math.sqrt((mx * mx) + (my * my) + (mz * mz))))
    else:
        normal_variance = 0.0

    tile_area = max(1, w * h)
    return {
        "x": x0,
        "y": y0,
        "w": w,
        "h": h,
        "hit_count": hit_count,
        "hit_ratio": hit_count / tile_area,
        "normal_variance": normal_variance,
        "collider_switch_density": collider_edges / comparable_edges if comparable_edges else 0.0,
        "hit_distance_variance": variance(distances),
        "segment_index_variance": variance(segments),
        "classification_diversity": len(classes) / max(1, sum(classes.values())),
        "classification_count": len(classes),
        "dominant_class": classes.most_common(1)[0][0] if classes else "none",
    }


def score_tile(metrics: dict, normalizers: dict[str, float]) -> tuple[dict[str, float], float]:
    components = {
        "normal": max(0.0, min(1.0, metrics["normal_variance"] / normalizers["normal_variance"])),
        "collider": max(0.0, min(1.0, metrics["collider_switch_density"] / normalizers["collider_switch_density"])),
        "distance": max(0.0, min(1.0, metrics["hit_distance_variance"] / normalizers["hit_distance_variance"])),
        "segment": max(0.0, min(1.0, metrics["segment_index_variance"] / normalizers["segment_index_variance"])),
        "classification": max(0.0, min(1.0, metrics["classification_diversity"] / normalizers["classification_diversity"])),
    }
    incoherence_score = (
        components["normal"] * 0.30
        + components["collider"] * 0.25
        + components["distance"] * 0.15
        + components["segment"] * 0.20
        + components["classification"] * 0.10
    )
    return components, incoherence_score


def build_base_normalizers(
    width: int,
    height: int,
    by_pixel: dict[tuple[int, int], dict[str, str]],
    base_tile_size: int,
) -> dict[str, float]:
    base_metrics: list[dict] = []
    for y0 in range(0, height, base_tile_size):
        for x0 in range(0, width, base_tile_size):
            w = min(base_tile_size, width - x0)
            h = min(base_tile_size, height - y0)
            base_metrics.append(compute_region_metrics(by_pixel, x0, y0, w, h))

    normalizers = {
        "normal_variance": percentile([m["normal_variance"] for m in base_metrics], 95.0),
        "collider_switch_density": percentile([m["collider_switch_density"] for m in base_metrics], 95.0),
        "hit_distance_variance": percentile([m["hit_distance_variance"] for m in base_metrics], 95.0),
        "segment_index_variance": percentile([m["segment_index_variance"] for m in base_metrics], 95.0),
        "classification_diversity": percentile([m["classification_diversity"] for m in base_metrics], 95.0),
    }
    return {key: value if value > 1e-12 else 1.0 for key, value in normalizers.items()}


def subdivide_rect(x: int, y: int, w: int, h: int) -> list[tuple[int, int, int, int]]:
    if w <= 1 and h <= 1:
        return []
    w0 = max(1, w // 2)
    h0 = max(1, h // 2)
    w1 = w - w0
    h1 = h - h0
    children = [(x, y, w0, h0)]
    if w1 > 0:
        children.append((x + w0, y, w1, h0))
    if h1 > 0:
        children.append((x, y + h0, w0, h1))
    if w1 > 0 and h1 > 0:
        children.append((x + w0, y + h0, w1, h1))
    return children


def build_adaptive_tile_tree(
    by_pixel: dict[tuple[int, int], dict[str, str]],
    x: int,
    y: int,
    w: int,
    h: int,
    depth: int,
    min_tile_size: int,
    coherence_threshold: float,
    normalizers: dict[str, float],
) -> dict:
    metrics = compute_region_metrics(by_pixel, x, y, w, h)
    components, incoherence_score = score_tile(metrics, normalizers)
    coherence_quality = 1.0 - incoherence_score
    can_subdivide = w > min_tile_size or h > min_tile_size
    should_subdivide = coherence_quality < coherence_threshold and can_subdivide
    node = {
        **metrics,
        "depth": depth,
        "component_scores": components,
        "incoherence_score": incoherence_score,
        "coherence_quality": coherence_quality,
        "subdivided": should_subdivide,
        "children": [],
    }
    if should_subdivide:
        for child in subdivide_rect(x, y, w, h):
            cx, cy, cw, ch = child
            if cw <= 0 or ch <= 0:
                continue
            node["children"].append(
                build_adaptive_tile_tree(
                    by_pixel,
                    cx,
                    cy,
                    cw,
                    ch,
                    depth + 1,
                    min_tile_size,
                    coherence_threshold,
                    normalizers,
                )
            )
    return node


def flatten_leaf_tiles(node: dict) -> list[dict]:
    children = node.get("children") or []
    if not children:
        return [node]
    leaves: list[dict] = []
    for child in children:
        leaves.extend(flatten_leaf_tiles(child))
    return leaves


def iter_nodes(node: dict) -> list[dict]:
    nodes = [node]
    for child in node.get("children") or []:
        nodes.extend(iter_nodes(child))
    return nodes


def write_adaptive_heatmap(width: int, height: int, leaves: list[dict], output_path: Path) -> list[float]:
    image = Image.new("RGB", (width, height), (0, 0, 0))
    draw = ImageDraw.Draw(image)
    pixel_scores = [0.0] * (width * height)
    for tile in leaves:
        score = float(tile["incoherence_score"])
        color = color_ramp(score)
        box = (tile["x"], tile["y"], tile["x"] + tile["w"] - 1, tile["y"] + tile["h"] - 1)
        draw.rectangle(box, fill=color)
        for yy in range(tile["y"], tile["y"] + tile["h"]):
            for xx in range(tile["x"], tile["x"] + tile["w"]):
                pixel_scores[(yy * width) + xx] = score
    image.save(output_path)
    return pixel_scores


def write_adaptive_overlay(width: int, height: int, leaves: list[dict], base_image_path: Path, output_path: Path) -> list[float]:
    with Image.open(base_image_path) as image:
        base = image.convert("RGBA")
        if base.size != (width, height):
            base = base.resize((width, height), Image.Resampling.BILINEAR)

    overlay = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    boundary_signal = [0.0] * (width * height)
    max_depth = max((int(tile["depth"]) for tile in leaves), default=0)
    for tile in leaves:
        depth = int(tile["depth"])
        alpha = 90 + int(120 * (depth / max(1, max_depth)))
        color = (255, 42, 180, alpha)
        box = (tile["x"], tile["y"], tile["x"] + tile["w"] - 1, tile["y"] + tile["h"] - 1)
        draw.rectangle(box, outline=color, width=1)
        x0 = tile["x"]
        y0 = tile["y"]
        x1 = min(width - 1, tile["x"] + tile["w"] - 1)
        y1 = min(height - 1, tile["y"] + tile["h"] - 1)
        for xx in range(x0, x1 + 1):
            boundary_signal[(y0 * width) + xx] = 1.0
            boundary_signal[(y1 * width) + xx] = 1.0
        for yy in range(y0, y1 + 1):
            boundary_signal[(yy * width) + x0] = 1.0
            boundary_signal[(yy * width) + x1] = 1.0

    Image.alpha_composite(base, overlay).save(output_path)
    return boundary_signal


def write_depth_map(width: int, height: int, leaves: list[dict], output_path: Path) -> list[float]:
    max_depth = max((int(tile["depth"]) for tile in leaves), default=0)
    image = Image.new("RGB", (width, height), (0, 0, 0))
    draw = ImageDraw.Draw(image)
    pixel_depths = [0.0] * (width * height)
    for tile in leaves:
        depth = int(tile["depth"])
        normalized = depth / max(1, max_depth)
        color = color_ramp(normalized)
        box = (tile["x"], tile["y"], tile["x"] + tile["w"] - 1, tile["y"] + tile["h"] - 1)
        draw.rectangle(box, fill=color)
        for yy in range(tile["y"], tile["y"] + tile["h"]):
            for xx in range(tile["x"], tile["x"] + tile["w"]):
                pixel_depths[(yy * width) + xx] = float(depth)
    image.save(output_path)
    return pixel_depths


def dilate_binary_signal(signal: list[float], width: int, height: int, radius: int) -> list[bool]:
    if radius <= 0:
        return [value > 0.0 for value in signal]
    result = [False] * len(signal)
    active = [idx for idx, value in enumerate(signal) if value > 0.0]
    for idx in active:
        x = idx % width
        y = idx // width
        for yy in range(max(0, y - radius), min(height, y + radius + 1)):
            for xx in range(max(0, x - radius), min(width, x + radius + 1)):
                result[(yy * width) + xx] = True
    return result


def summarize_adaptive_tiles(
    roots: list[dict],
    leaves: list[dict],
    pixel_scores: list[float],
    pixel_depths: list[float],
    boundary_signal: list[float],
    visible_signal: list[float],
    width: int,
    height: int,
) -> dict:
    all_nodes: list[dict] = []
    for root in roots:
        all_nodes.extend(iter_nodes(root))
    visible_threshold = otsu_threshold(visible_signal) if visible_signal else 0.0
    visible_mask = [signal > visible_threshold for signal in visible_signal] if visible_signal else []
    boundary_mask = dilate_binary_signal(boundary_signal, width, height, 1)
    pixel_score_threshold = otsu_threshold(pixel_scores)
    score_mask = [score > pixel_score_threshold for score in pixel_scores]
    depth_values = [int(tile["depth"]) for tile in leaves]
    metric_names = [
        "normal_variance",
        "collider_switch_density",
        "segment_index_variance",
        "classification_diversity",
        "incoherence_score",
        "coherence_quality",
    ]
    metrics = {
        name: {
            "mean": sum(float(tile[name]) for tile in leaves) / max(1, len(leaves)),
            "p95": percentile([float(tile[name]) for tile in leaves], 95.0),
            "max": max((float(tile[name]) for tile in leaves), default=0.0),
        }
        for name in metric_names
    }
    return {
        "root_tile_count": len(roots),
        "node_count": len(all_nodes),
        "leaf_tile_count": len(leaves),
        "max_depth": max(depth_values, default=0),
        "mean_depth": sum(depth_values) / max(1, len(depth_values)),
        "leaf_depth_histogram": {
            str(depth): sum(1 for value in depth_values if value == depth)
            for depth in sorted(set(depth_values))
        },
        "metrics": metrics,
        "visible_band_alignment": {
            "visible_band_pixels": sum(1 for value in visible_mask if value) if visible_mask else 0,
            "boundary_pearson_to_visible_band_signal": pearson(boundary_signal, visible_signal) if visible_signal else 0.0,
            "boundary_iou_to_visible_band_mask": iou(boundary_mask, visible_mask) if visible_mask else 0.0,
            "heatmap_pearson_to_visible_band_signal": pearson(pixel_scores, visible_signal) if visible_signal else 0.0,
            "heatmap_iou_to_visible_band_mask": iou(score_mask, visible_mask) if visible_mask else 0.0,
            "depth_pearson_to_visible_band_signal": pearson(pixel_depths, visible_signal) if visible_signal else 0.0,
        },
        "top_leaf_tiles": [
            {
                key: tile[key]
                for key in (
                    "x",
                    "y",
                    "w",
                    "h",
                    "depth",
                    "incoherence_score",
                    "coherence_quality",
                    "normal_variance",
                    "collider_switch_density",
                    "segment_index_variance",
                    "classification_diversity",
                    "dominant_class",
                )
            }
            for tile in sorted(leaves, key=lambda tile: float(tile["incoherence_score"]), reverse=True)[:12]
        ],
    }


def analyze_adaptive(
    csv_path: Path,
    base_image_path: Path,
    debug_image_path: Path | None,
    base_tile_size: int,
    min_tile_size: int,
    coherence_threshold: float,
    output_prefix: Path,
) -> dict:
    width, height, rows = load_rows(csv_path)
    by_pixel = build_pixel_index(rows)
    normalizers = build_base_normalizers(width, height, by_pixel, base_tile_size)
    roots: list[dict] = []
    for y0 in range(0, height, base_tile_size):
        for x0 in range(0, width, base_tile_size):
            w = min(base_tile_size, width - x0)
            h = min(base_tile_size, height - y0)
            roots.append(
                build_adaptive_tile_tree(
                    by_pixel,
                    x0,
                    y0,
                    w,
                    h,
                    0,
                    min_tile_size,
                    coherence_threshold,
                    normalizers,
                )
            )
    leaves: list[dict] = []
    for root in roots:
        leaves.extend(flatten_leaf_tiles(root))

    adaptive_heatmap_path = output_prefix.with_name(output_prefix.name + "_adaptive_tile_heatmap.png")
    adaptive_overlay_path = output_prefix.with_name(output_prefix.name + "_adaptive_tile_overlay.png")
    adaptive_tree_path = output_prefix.with_name(output_prefix.name + "_adaptive_tile_tree.json")
    depth_map_path = output_prefix.with_name(output_prefix.name + "_tile_depth_map.png")

    pixel_scores = write_adaptive_heatmap(width, height, leaves, adaptive_heatmap_path)
    boundary_signal = write_adaptive_overlay(width, height, leaves, base_image_path, adaptive_overlay_path)
    pixel_depths = write_depth_map(width, height, leaves, depth_map_path)
    visible_signal = visible_band_signal(debug_image_path, width, height) if debug_image_path else []

    summary = {
        "csv_path": str(csv_path),
        "base_image_path": str(base_image_path),
        "debug_image_path": str(debug_image_path) if debug_image_path else "",
        "base_tile_size": base_tile_size,
        "min_tile_size": min_tile_size,
        "coherence_threshold": coherence_threshold,
        "normalizers": normalizers,
        "width": width,
        "height": height,
        "outputs": {
            "adaptive_tile_overlay": str(adaptive_overlay_path),
            "adaptive_tile_heatmap": str(adaptive_heatmap_path),
            "adaptive_tile_tree": str(adaptive_tree_path),
            "tile_depth_map": str(depth_map_path),
        },
        **summarize_adaptive_tiles(roots, leaves, pixel_scores, pixel_depths, boundary_signal, visible_signal, width, height),
        "tree": roots,
    }
    adaptive_tree_path.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    return summary


def write_heatmap(width: int, height: int, tiles: list[dict], output_path: Path) -> None:
    image = Image.new("RGB", (width, height), (0, 0, 0))
    draw = ImageDraw.Draw(image)
    for tile in tiles:
        color = color_ramp(float(tile["coherence_score"]))
        box = (tile["x"], tile["y"], tile["x"] + tile["w"] - 1, tile["y"] + tile["h"] - 1)
        draw.rectangle(box, fill=color)
    image.save(output_path)


def write_overlay(width: int, height: int, tiles: list[dict], base_image_path: Path, output_path: Path) -> None:
    with Image.open(base_image_path) as image:
        base = image.convert("RGBA")
        if base.size != (width, height):
            base = base.resize((width, height), Image.Resampling.BILINEAR)

    overlay = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    threshold = percentile([float(tile["coherence_score"]) for tile in tiles], 85.0)
    for tile in tiles:
        score = float(tile["coherence_score"])
        if score < threshold:
            continue
        alpha = int(80 + 130 * max(0.0, min(1.0, score)))
        box = (tile["x"], tile["y"], tile["x"] + tile["w"] - 1, tile["y"] + tile["h"] - 1)
        draw.rectangle(box, outline=(255, 40, 190, alpha), width=2)
        draw.rectangle(box, fill=(255, 40, 190, 24))

    Image.alpha_composite(base, overlay).save(output_path)


def summarize_tiles(tiles: list[dict], pixel_scores: list[float], visible_signal: list[float]) -> dict:
    tile_scores = [float(tile["coherence_score"]) for tile in tiles]
    threshold = otsu_threshold(tile_scores)
    visible_threshold = otsu_threshold(visible_signal) if visible_signal else 0.0
    pixel_score_threshold = otsu_threshold(pixel_scores)
    tile_mask = [score > pixel_score_threshold for score in pixel_scores]
    visible_mask = [signal > visible_threshold for signal in visible_signal] if visible_signal else []

    metric_names = [
        "normal_variance",
        "collider_switch_density",
        "hit_distance_variance",
        "segment_index_variance",
        "classification_diversity",
        "coherence_score",
    ]
    metric_summary = {
        name: {
            "mean": sum(float(tile[name]) for tile in tiles) / max(1, len(tiles)),
            "p95": percentile([float(tile[name]) for tile in tiles], 95.0),
            "max": max((float(tile[name]) for tile in tiles), default=0.0),
        }
        for name in metric_names
    }

    top_tiles = sorted(tiles, key=lambda tile: float(tile["coherence_score"]), reverse=True)[:12]
    return {
        "tile_count": len(tiles),
        "coherence_threshold": threshold,
        "hot_tile_count": sum(1 for score in tile_scores if score > threshold),
        "metrics": metric_summary,
        "visible_band_alignment": {
            "visible_band_pixels": sum(1 for value in visible_mask if value) if visible_mask else 0,
            "pearson_to_visible_band_signal": pearson(pixel_scores, visible_signal) if visible_signal else 0.0,
            "iou_to_visible_band_mask": iou(tile_mask, visible_mask) if visible_mask else 0.0,
        },
        "top_tiles": [
            {
                key: tile[key]
                for key in (
                    "x",
                    "y",
                    "w",
                    "h",
                    "coherence_score",
                    "normal_variance",
                    "collider_switch_density",
                    "hit_distance_variance",
                    "segment_index_variance",
                    "classification_diversity",
                    "dominant_class",
                )
            }
            for tile in top_tiles
        ],
    }


def analyze(csv_path: Path, base_image_path: Path, debug_image_path: Path | None, tile_size: int, output_prefix: Path) -> dict:
    width, height, rows = load_rows(csv_path)
    tiles, pixel_scores = compute_tiles(width, height, rows, tile_size)
    visible_signal = visible_band_signal(debug_image_path, width, height) if debug_image_path else []

    heatmap_path = output_prefix.with_name(output_prefix.name + "_tile_coherence_heatmap.png")
    overlay_path = output_prefix.with_name(output_prefix.name + "_tile_boundary_overlay.png")
    summary_path = output_prefix.with_name(output_prefix.name + "_tile_summary.json")

    write_heatmap(width, height, tiles, heatmap_path)
    write_overlay(width, height, tiles, base_image_path, overlay_path)

    summary = {
        "csv_path": str(csv_path),
        "base_image_path": str(base_image_path),
        "debug_image_path": str(debug_image_path) if debug_image_path else "",
        "tile_size": tile_size,
        "width": width,
        "height": height,
        "outputs": {
            "tile_coherence_heatmap": str(heatmap_path),
            "tile_boundary_overlay": str(overlay_path),
            "tile_summary": str(summary_path),
        },
        **summarize_tiles(tiles, pixel_scores, visible_signal),
    }
    summary_path.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    return summary


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("csv", type=Path)
    parser.add_argument("--base-image", type=Path, required=True)
    parser.add_argument("--debug-image", type=Path)
    parser.add_argument("--tile-size", type=int, default=16)
    parser.add_argument("--adaptive", action="store_true")
    parser.add_argument("--base-tile-size", type=int, default=32)
    parser.add_argument("--min-tile-size", type=int, default=4)
    parser.add_argument("--coherence-threshold", type=float, default=0.55)
    parser.add_argument("--output-prefix", type=Path, required=True)
    args = parser.parse_args()

    if args.adaptive:
        base_tile_size = max(2, args.base_tile_size)
        min_tile_size = max(1, min(args.min_tile_size, base_tile_size))
        coherence_threshold = max(0.0, min(1.0, args.coherence_threshold))
        summary = analyze_adaptive(
            args.csv,
            args.base_image,
            args.debug_image,
            base_tile_size,
            min_tile_size,
            coherence_threshold,
            args.output_prefix,
        )
        printed = {key: value for key, value in summary.items() if key != "tree"}
        print(json.dumps(printed, indent=2))
    else:
        tile_size = max(2, args.tile_size)
        summary = analyze(args.csv, args.base_image, args.debug_image, tile_size, args.output_prefix)
        print(json.dumps(summary, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
