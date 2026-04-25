#!/usr/bin/env python3
"""Polar coherence diagnostics from cached per-pixel scout data."""

from __future__ import annotations

import argparse
import csv
import json
import math
from collections import Counter, defaultdict, deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


SOBEL_X = np.array(((-1, 0, 1), (-2, 0, 2), (-1, 0, 1)), dtype=np.float32)
SOBEL_Y = np.array(((-1, -2, -1), (0, 0, 0), (1, 2, 1)), dtype=np.float32)


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


def load_rows(csv_path: Path) -> tuple[int, int, list[dict[str, str]]]:
    rows: list[dict[str, str]] = []
    with csv_path.open(newline="", encoding="utf-8-sig") as handle:
        rows.extend(csv.DictReader(handle))
    width = max((parse_int(row, "x", -1) for row in rows), default=-1) + 1
    height = max((parse_int(row, "y", -1) for row in rows), default=-1) + 1
    return width, height, rows


def load_gray(path: Path, size: tuple[int, int] | None = None) -> np.ndarray:
    with Image.open(path) as image:
        gray = image.convert("L")
        if size is not None and gray.size != size:
            gray = gray.resize(size, Image.Resampling.BILINEAR)
        return np.asarray(gray, dtype=np.float32) / 255.0


def load_rgb(path: Path, size: tuple[int, int] | None = None) -> Image.Image:
    with Image.open(path) as image:
        rgb = image.convert("RGB")
        if size is not None and rgb.size != size:
            rgb = rgb.resize(size, Image.Resampling.BILINEAR)
        return rgb.copy()


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


def normal_tuple(row: dict[str, str]) -> tuple[float, float, float]:
    nx = parse_float(row, "normal_x")
    ny = parse_float(row, "normal_y")
    nz = parse_float(row, "normal_z")
    length = math.sqrt((nx * nx) + (ny * ny) + (nz * nz))
    if length <= 1e-12:
        return (0.0, 0.0, 0.0)
    return (nx / length, ny / length, nz / length)


def estimate_center(mask: np.ndarray) -> tuple[float, float, str]:
    ys, xs = np.nonzero(mask > 0.5)
    if xs.size == 0:
        return ((mask.shape[1] - 1) * 0.5, (mask.shape[0] - 1) * 0.5, "image_center_fallback")
    return (float(np.mean(xs)), float(np.mean(ys)), "visible_band_mask_centroid")


def polar_bin_for_pixel(
    x: int,
    y: int,
    cx: float,
    cy: float,
    max_radius: float,
    radial_bands: int,
    angular_sectors: int,
) -> tuple[int, int]:
    dx = (x + 0.5) - cx
    dy = (y + 0.5) - cy
    r = math.sqrt((dx * dx) + (dy * dy))
    theta = math.atan2(dy, dx)
    if theta < 0:
        theta += math.tau
    radial = min(radial_bands - 1, max(0, int((r / max_radius) * radial_bands)))
    angular = min(angular_sectors - 1, max(0, int((theta / math.tau) * angular_sectors)))
    return radial, angular


def color_ramp(value: float) -> tuple[int, int, int]:
    v = max(0.0, min(1.0, value))
    if v < 0.5:
        t = v / 0.5
        return (int(22 + 58 * t), int(60 + 160 * t), int(150 - 70 * t))
    t = (v - 0.5) / 0.5
    return (int(80 + 175 * t), int(220 - 110 * t), int(80 - 45 * t))


def compute_bin_metrics(pixels: list[dict[str, str]], edge_pairs: list[tuple[int, int]]) -> dict:
    normals: list[tuple[float, float, float]] = []
    distances: list[float] = []
    segments: list[float] = []
    classes: Counter[str] = Counter()
    hit_count = 0
    for row in pixels:
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

    if normals:
        mx = sum(n[0] for n in normals) / len(normals)
        my = sum(n[1] for n in normals) / len(normals)
        mz = sum(n[2] for n in normals) / len(normals)
        normal_variance = 1.0 - max(0.0, min(1.0, math.sqrt((mx * mx) + (my * my) + (mz * mz))))
    else:
        normal_variance = 0.0

    collider_switches = sum(1 for lhs, rhs in edge_pairs if lhs != rhs)
    return {
        "pixel_count": len(pixels),
        "hit_count": hit_count,
        "normal_variance": normal_variance,
        "collider_switch_density": collider_switches / len(edge_pairs) if edge_pairs else 0.0,
        "hit_distance_variance": variance(distances),
        "segment_index_variance": variance(segments),
        "classification_diversity": len(classes) / max(1, sum(classes.values())),
        "classification_count": len(classes),
        "dominant_class": classes.most_common(1)[0][0] if classes else "none",
    }


def score_bins(metrics: list[dict]) -> None:
    normalizer_keys = (
        "normal_variance",
        "collider_switch_density",
        "hit_distance_variance",
        "segment_index_variance",
        "classification_diversity",
    )
    normalizers = {key: percentile([float(m[key]) for m in metrics], 95.0) for key in normalizer_keys}
    normalizers = {key: value if value > 1e-12 else 1.0 for key, value in normalizers.items()}
    for metric in metrics:
        components = {
            "normal": min(1.0, float(metric["normal_variance"]) / normalizers["normal_variance"]),
            "collider": min(1.0, float(metric["collider_switch_density"]) / normalizers["collider_switch_density"]),
            "distance": min(1.0, float(metric["hit_distance_variance"]) / normalizers["hit_distance_variance"]),
            "segment": min(1.0, float(metric["segment_index_variance"]) / normalizers["segment_index_variance"]),
            "classification": min(1.0, float(metric["classification_diversity"]) / normalizers["classification_diversity"]),
        }
        score = (
            components["normal"] * 0.30
            + components["collider"] * 0.25
            + components["distance"] * 0.15
            + components["segment"] * 0.20
            + components["classification"] * 0.10
        )
        metric["component_scores"] = components
        metric["incoherence_score"] = score
        metric["coherence_quality"] = 1.0 - score


def build_polar_bins(
    width: int,
    height: int,
    rows: list[dict[str, str]],
    center: tuple[float, float],
    radial_bands: int,
    angular_sectors: int,
) -> tuple[list[dict], np.ndarray, np.ndarray]:
    cx, cy = center
    max_radius = max(
        math.hypot(0 - cx, 0 - cy),
        math.hypot(width - cx, 0 - cy),
        math.hypot(0 - cx, height - cy),
        math.hypot(width - cx, height - cy),
    )
    by_coord = {(parse_int(row, "x", -1), parse_int(row, "y", -1)): row for row in rows}
    bin_id = np.full((height, width), -1, dtype=np.int32)
    pixels_by_bin: dict[int, list[dict[str, str]]] = defaultdict(list)
    for row in rows:
        x = parse_int(row, "x", -1)
        y = parse_int(row, "y", -1)
        if x < 0 or y < 0 or x >= width or y >= height:
            continue
        radial, angular = polar_bin_for_pixel(x, y, cx, cy, max_radius, radial_bands, angular_sectors)
        bid = radial * angular_sectors + angular
        bin_id[y, x] = bid
        pixels_by_bin[bid].append(row)

    edge_pairs_by_bin: dict[int, list[tuple[int, int]]] = defaultdict(list)
    for y in range(height):
        for x in range(width):
            row = by_coord.get((x, y))
            if not row:
                continue
            bid = int(bin_id[y, x])
            if bid < 0:
                continue
            cid = parse_int(row, "collider_id", 0)
            for dx, dy in ((1, 0), (0, 1)):
                nx = x + dx
                ny = y + dy
                if nx >= width or ny >= height or int(bin_id[ny, nx]) != bid:
                    continue
                other = by_coord.get((nx, ny))
                if not other or parse_int(other, "had_hit", 0) == 0 or parse_int(row, "had_hit", 0) == 0:
                    continue
                edge_pairs_by_bin[bid].append((cid, parse_int(other, "collider_id", 0)))

    metrics: list[dict] = []
    for radial in range(radial_bands):
        for angular in range(angular_sectors):
            bid = radial * angular_sectors + angular
            metric = compute_bin_metrics(pixels_by_bin.get(bid, []), edge_pairs_by_bin.get(bid, []))
            metric.update({"bin_id": bid, "radial_band": radial, "angular_sector": angular})
            metrics.append(metric)
    score_bins(metrics)
    score_by_bin = {int(metric["bin_id"]): float(metric["incoherence_score"]) for metric in metrics}
    score_map = np.zeros((height, width), dtype=np.float32)
    for y in range(height):
        for x in range(width):
            score_map[y, x] = score_by_bin.get(int(bin_id[y, x]), 0.0)
    return metrics, bin_id, score_map


def write_heatmap(score_map: np.ndarray, output_path: Path) -> None:
    height, width = score_map.shape
    image = Image.new("RGB", (width, height), (0, 0, 0))
    pixels = image.load()
    for y in range(height):
        for x in range(width):
            pixels[x, y] = color_ramp(float(score_map[y, x]))
    image.save(output_path)


def write_boundary_overlay(bin_id: np.ndarray, base_image_path: Path, output_path: Path) -> np.ndarray:
    height, width = bin_id.shape
    with Image.open(base_image_path) as image:
        base = image.convert("RGBA")
        if base.size != (width, height):
            base = base.resize((width, height), Image.Resampling.BILINEAR)
    overlay = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    boundary = np.zeros((height, width), dtype=np.float32)
    for y in range(height):
        for x in range(width):
            bid = int(bin_id[y, x])
            if (x + 1 < width and int(bin_id[y, x + 1]) != bid) or (y + 1 < height and int(bin_id[y + 1, x]) != bid):
                boundary[y, x] = 1.0
                draw.point((x, y), fill=(255, 40, 190, 230))
    Image.alpha_composite(base, overlay).save(output_path)
    return boundary


def convolve3(image: np.ndarray, kernel: np.ndarray) -> np.ndarray:
    padded = np.pad(image, ((1, 1), (1, 1)), mode="edge")
    out = np.zeros_like(image, dtype=np.float32)
    for ky in range(3):
        for kx in range(3):
            out += padded[ky : ky + image.shape[0], kx : kx + image.shape[1]] * kernel[ky, kx]
    return out


def sobel(image: np.ndarray) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    gx = convolve3(image, SOBEL_X)
    gy = convolve3(image, SOBEL_Y)
    mag = np.sqrt((gx * gx) + (gy * gy))
    max_mag = float(np.max(mag))
    if max_mag > 1e-12:
        mag = mag / max_mag
    return mag, gx, gy


def otsu(values: np.ndarray) -> float:
    flat = values.reshape(-1)
    if flat.size == 0:
        return 0.0
    lo = float(np.min(flat))
    hi = float(np.max(flat))
    if hi <= lo:
        return hi
    hist, _ = np.histogram(flat, bins=256, range=(lo, hi))
    total = int(flat.size)
    sum_total = float(sum(i * int(c) for i, c in enumerate(hist)))
    weight_bg = 0
    sum_bg = 0.0
    best_index = 0
    best_between = -1.0
    for index, count in enumerate(hist):
        weight_bg += int(count)
        if weight_bg == 0:
            continue
        weight_fg = total - weight_bg
        if weight_fg == 0:
            break
        sum_bg += index * int(count)
        mean_bg = sum_bg / weight_bg
        mean_fg = (sum_total - sum_bg) / weight_fg
        between = weight_bg * weight_fg * (mean_bg - mean_fg) ** 2
        if between > best_between:
            best_between = between
            best_index = index
    return lo + (best_index / 255.0) * (hi - lo)


def edge_mask(mag: np.ndarray) -> np.ndarray:
    return mag > max(otsu(mag), 0.08)


def dilate(mask: np.ndarray, radius: int) -> np.ndarray:
    if radius <= 0:
        return mask.copy()
    result = np.zeros_like(mask, dtype=bool)
    ys, xs = np.nonzero(mask)
    height, width = mask.shape
    for y, x in zip(ys, xs):
        result[max(0, y - radius) : min(height, y + radius + 1), max(0, x - radius) : min(width, x + radius + 1)] = True
    return result


def distance_to_edges(edge: np.ndarray, max_radius: int) -> np.ndarray:
    height, width = edge.shape
    dist = np.full((height, width), max_radius + 1, dtype=np.int16)
    q: deque[tuple[int, int]] = deque()
    ys, xs = np.nonzero(edge)
    for y, x in zip(ys, xs):
        dist[y, x] = 0
        q.append((int(y), int(x)))
    while q:
        y, x = q.popleft()
        nd = int(dist[y, x]) + 1
        if nd > max_radius:
            continue
        for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
            if 0 <= ny < height and 0 <= nx < width and nd < dist[ny, nx]:
                dist[ny, nx] = nd
                q.append((ny, nx))
    return dist.astype(np.float32)


def pearson(lhs: np.ndarray, rhs: np.ndarray) -> float:
    a = lhs.reshape(-1).astype(np.float64)
    b = rhs.reshape(-1).astype(np.float64)
    if a.size == 0 or a.size != b.size:
        return 0.0
    a -= float(np.mean(a))
    b -= float(np.mean(b))
    den = math.sqrt(float(np.sum(a * a)) * float(np.sum(b * b)))
    return float(np.sum(a * b) / den) if den > 1e-12 else 0.0


def compare_edges(visible: np.ndarray, target: np.ndarray, max_distance: int) -> dict:
    vmag, vgx, vgy = sobel(visible)
    tmag, tgx, tgy = sobel(target)
    vedge = edge_mask(vmag)
    tedge = edge_mask(tmag)
    intersection = np.logical_and(vedge, tedge)
    union = np.logical_or(vedge, tedge)
    target_near = dilate(tedge, max_distance)
    visible_near = dilate(vedge, max_distance)
    target_dist = distance_to_edges(tedge, max_distance)
    visible_dist = distance_to_edges(vedge, max_distance)
    visible_samples = target_dist[vedge]
    target_samples = visible_dist[tedge]
    visible_score = float(np.mean(np.maximum(0.0, 1.0 - (visible_samples / (max_distance + 1.0))))) if visible_samples.size else 0.0
    target_score = float(np.mean(np.maximum(0.0, 1.0 - (target_samples / (max_distance + 1.0))))) if target_samples.size else 0.0
    direction_mask = np.logical_and(vedge, target_near)
    direction_mask = np.logical_and(direction_mask, vmag > 1e-6)
    direction_mask = np.logical_and(direction_mask, tmag > 1e-6)
    if np.any(direction_mask):
        v_len = np.sqrt((vgx * vgx) + (vgy * vgy)) + 1e-12
        t_len = np.sqrt((tgx * tgx) + (tgy * tgy)) + 1e-12
        dot = ((vgx * tgx) + (vgy * tgy)) / (v_len * t_len)
        direction_similarity = float(np.mean(np.abs(dot[direction_mask])))
    else:
        direction_similarity = 0.0
    return {
        "visible_edge_pixels": int(np.sum(vedge)),
        "target_edge_pixels": int(np.sum(tedge)),
        "edge_iou": float(np.sum(intersection) / max(1, np.sum(union))),
        "visible_edge_recall_within_radius": float(np.sum(np.logical_and(vedge, target_near)) / max(1, np.sum(vedge))),
        "target_edge_precision_within_radius": float(np.sum(np.logical_and(tedge, visible_near)) / max(1, np.sum(tedge))),
        "symmetric_near_overlap": 0.5
        * (
            float(np.sum(np.logical_and(vedge, target_near)) / max(1, np.sum(vedge)))
            + float(np.sum(np.logical_and(tedge, visible_near)) / max(1, np.sum(tedge)))
        ),
        "edge_distance_score_visible_to_target": visible_score,
        "edge_distance_score_target_to_visible": target_score,
        "symmetric_edge_distance_score": 0.5 * (visible_score + target_score),
        "gradient_direction_similarity": direction_similarity,
        "gradient_magnitude_pearson": pearson(vmag, tmag),
    }


def summarize_metrics(metrics: list[dict]) -> dict:
    keys = ("normal_variance", "collider_switch_density", "hit_distance_variance", "segment_index_variance", "incoherence_score")
    return {
        key: {
            "mean": sum(float(metric[key]) for metric in metrics) / max(1, len(metrics)),
            "p95": percentile([float(metric[key]) for metric in metrics], 95.0),
            "max": max((float(metric[key]) for metric in metrics), default=0.0),
        }
        for key in keys
    }


def analyze_checkpoint(
    checkpoint: str,
    csv_path: Path,
    base_image_path: Path,
    visible_mask_path: Path,
    radial_bands: int,
    angular_sectors: int,
    max_distance: int,
    output_prefix: Path,
) -> dict:
    width, height, rows = load_rows(csv_path)
    visible = load_gray(visible_mask_path, (width, height))
    cx, cy, center_source = estimate_center(visible)
    metrics, bin_id, score_map = build_polar_bins(width, height, rows, (cx, cy), radial_bands, angular_sectors)
    heatmap_path = output_prefix.with_name(output_prefix.name + "_polar_coherence_heatmap.png")
    overlay_path = output_prefix.with_name(output_prefix.name + "_polar_boundary_overlay.png")
    summary_path = output_prefix.with_name(output_prefix.name + "_polar_tile_summary.json")
    write_heatmap(score_map, heatmap_path)
    boundary = write_boundary_overlay(bin_id, base_image_path, overlay_path)
    heatmap_alignment = compare_edges(visible, score_map, max_distance)
    boundary_alignment = compare_edges(visible, boundary, max_distance)
    summary = {
        "checkpoint": checkpoint,
        "csv_path": str(csv_path),
        "base_image_path": str(base_image_path),
        "visible_mask_path": str(visible_mask_path),
        "width": width,
        "height": height,
        "center": {"x": cx, "y": cy, "source": center_source},
        "radial_bands": radial_bands,
        "angular_sectors": angular_sectors,
        "polar_bin_count": len(metrics),
        "outputs": {
            "polar_coherence_heatmap": str(heatmap_path),
            "polar_boundary_overlay": str(overlay_path),
            "polar_tile_summary": str(summary_path),
        },
        "metrics": summarize_metrics(metrics),
        "edge_alignment": {
            "polar_coherence_heatmap": heatmap_alignment,
            "polar_boundary_overlay": boundary_alignment,
        },
        "top_bins": sorted(metrics, key=lambda metric: float(metric["incoherence_score"]), reverse=True)[:12],
    }
    summary_path.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    return summary


def default_inputs(diagnostics_dir: Path, visible_dir: Path) -> list[tuple[str, str, Path, Path, Path]]:
    return [
        (
            "mouth",
            "00_mouth",
            diagnostics_dir / "00_mouth_hit_diagnostics.csv",
            diagnostics_dir / "00_mouth_debug_normal_rgb.png",
            visible_dir / "mouth_visible_band_mask.png",
        ),
        (
            "post_throat_backstep_01",
            "01_post_throat_backstep_01",
            diagnostics_dir / "01_post_throat_backstep_01_hit_diagnostics.csv",
            diagnostics_dir / "01_post_throat_backstep_01_debug_normal_rgb.png",
            visible_dir / "post_throat_backstep_01_visible_band_mask.png",
        ),
    ]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--diagnostics-dir", type=Path, required=True)
    parser.add_argument("--visible-mask-dir", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path)
    parser.add_argument("--radial-bands", type=int, default=24)
    parser.add_argument("--angular-sectors", type=int, default=48)
    parser.add_argument("--max-distance", type=int, default=6)
    parser.add_argument("--adaptive-edge-summary", type=Path)
    args = parser.parse_args()

    output_dir = args.output_dir or args.diagnostics_dir
    output_dir.mkdir(parents=True, exist_ok=True)
    results = []
    missing: list[str] = []
    for checkpoint, prefix, csv_path, base_image, visible_mask in default_inputs(args.diagnostics_dir, args.visible_mask_dir):
        needed = (csv_path, base_image, visible_mask)
        if not all(path.exists() for path in needed):
            missing.extend(str(path) for path in needed if not path.exists())
            continue
        results.append(
            analyze_checkpoint(
                checkpoint,
                csv_path,
                base_image,
                visible_mask,
                max(1, args.radial_bands),
                max(1, args.angular_sectors),
                max(1, args.max_distance),
                output_dir / prefix,
            )
        )

    adaptive_comparison = {}
    if args.adaptive_edge_summary and args.adaptive_edge_summary.exists():
        adaptive = json.loads(args.adaptive_edge_summary.read_text())
        for checkpoint_result in adaptive.get("checkpoints", []):
            checkpoint = checkpoint_result.get("checkpoint", "")
            target = checkpoint_result.get("targets", {}).get("adaptive_tile_boundary_overlay", {})
            adaptive_comparison[checkpoint] = target

    aggregate = {
        "diagnostics_dir": str(args.diagnostics_dir),
        "visible_mask_dir": str(args.visible_mask_dir),
        "radial_bands": max(1, args.radial_bands),
        "angular_sectors": max(1, args.angular_sectors),
        "max_distance_px": max(1, args.max_distance),
        "missing_inputs": missing,
        "adaptive_square_edge_baseline": adaptive_comparison,
        "checkpoints": results,
    }
    aggregate_path = output_dir / "polar_edge_comparison_summary.json"
    aggregate_path.write_text(json.dumps(aggregate, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({**aggregate, "outputs": {"aggregate_summary": str(aggregate_path)}}, indent=2))
    return 1 if missing else 0


if __name__ == "__main__":
    raise SystemExit(main())
