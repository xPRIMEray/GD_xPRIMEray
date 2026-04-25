#!/usr/bin/env python3
"""Incoherence-centered polar tiling diagnostics from existing artifacts."""

from __future__ import annotations

import argparse
import csv
import json
import math
from collections import Counter, deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


CHECKPOINTS = [
    ("mouth", "00_mouth"),
    ("post_throat_backstep_01", "01_post_throat_backstep_01"),
]

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
    a -= float(np.mean(a))
    b -= float(np.mean(b))
    den = math.sqrt(float(np.sum(a * a)) * float(np.sum(b * b)))
    return float(np.sum(a * b) / den) if den > 1e-12 else 0.0


def compare_edges(visible_gray: np.ndarray, target_gray: np.ndarray, max_distance: int = 6) -> dict:
    vmag, vgx, vgy = sobel(visible_gray)
    tmag, tgx, tgy = sobel(target_gray)
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
    direction_mask = np.logical_and.reduce((vedge, target_near, vmag > 1e-6, tmag > 1e-6))
    if np.any(direction_mask):
        v_len = np.sqrt((vgx * vgx) + (vgy * vgy)) + 1e-12
        t_len = np.sqrt((tgx * tgx) + (tgy * tgy)) + 1e-12
        dot = ((vgx * tgx) + (vgy * tgy)) / (v_len * t_len)
        direction_similarity = float(np.mean(np.abs(dot[direction_mask])))
        direction_samples = int(np.sum(direction_mask))
    else:
        direction_similarity = 0.0
        direction_samples = 0
    return {
        "visible_edge_pixels": int(np.sum(vedge)),
        "target_edge_pixels": int(np.sum(tedge)),
        "edge_overlap_pixels": int(np.sum(intersection)),
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
        "gradient_direction_samples": direction_samples,
        "gradient_magnitude_pearson": pearson(vmag, tmag),
    }


def normal_tuple(row: dict[str, str]) -> tuple[float, float, float]:
    nx = parse_float(row, "first_accepted_normal_x", parse_float(row, "normal_x"))
    ny = parse_float(row, "first_accepted_normal_y", parse_float(row, "normal_y"))
    nz = parse_float(row, "first_accepted_normal_z", parse_float(row, "normal_z"))
    length = math.sqrt((nx * nx) + (ny * ny) + (nz * nz))
    if length <= 1e-12:
        return (0.0, 0.0, 0.0)
    return (nx / length, ny / length, nz / length)


def variance(values: list[float]) -> float:
    if not values:
        return 0.0
    mean = sum(values) / len(values)
    return sum((v - mean) ** 2 for v in values) / len(values)


def percentile(values: list[float], pct: float) -> float:
    if not values:
        return 0.0
    ordered = sorted(values)
    index = min(len(ordered) - 1, max(0, int(round((pct / 100.0) * (len(ordered) - 1)))))
    return ordered[index]


def normalize01(arr: np.ndarray) -> np.ndarray:
    lo = float(np.min(arr))
    hi = float(np.max(arr))
    if hi <= lo:
        return np.zeros_like(arr, dtype=np.float32)
    return ((arr - lo) / (hi - lo)).astype(np.float32)


def fields_from_rows(width: int, height: int, rows: list[dict[str, str]]) -> tuple[np.ndarray, np.ndarray, np.ndarray, np.ndarray]:
    normals = np.zeros((height, width, 3), dtype=np.float32)
    segments = np.zeros((height, width), dtype=np.float32)
    distances = np.zeros((height, width), dtype=np.float32)
    colliders = np.zeros((height, width), dtype=np.int64)
    for row in rows:
        x = parse_int(row, "x")
        y = parse_int(row, "y")
        if 0 <= x < width and 0 <= y < height:
            normals[y, x] = normal_tuple(row)
            segments[y, x] = parse_float(row, "first_accepted_segment_index", parse_float(row, "segment_count"))
            distances[y, x] = parse_float(row, "first_accepted_hit_distance", parse_float(row, "hit_distance"))
            colliders[y, x] = parse_int(row, "first_accepted_collider_id", parse_int(row, "collider_id"))
    return normals, segments, distances, colliders


def neighbor_normal_delta(normals: np.ndarray) -> np.ndarray:
    h, w, _ = normals.shape
    delta = np.zeros((h, w), dtype=np.float32)
    count = np.zeros((h, w), dtype=np.float32)
    right = 1.0 - np.sum(normals[:, :-1] * normals[:, 1:], axis=2)
    down = 1.0 - np.sum(normals[:-1, :] * normals[1:, :], axis=2)
    delta[:, :-1] += right
    delta[:, 1:] += right
    count[:, :-1] += 1
    count[:, 1:] += 1
    delta[:-1, :] += down
    delta[1:, :] += down
    count[:-1, :] += 1
    count[1:, :] += 1
    return delta / np.maximum(count, 1.0)


def compute_adaptive_incoherence(width: int, height: int, tile_size: int, rows: list[dict[str, str]], image_gray: np.ndarray) -> np.ndarray:
    normals, segments, distances, colliders = fields_from_rows(width, height, rows)
    mag, gx, gy = sobel(image_gray)
    edge = edge_mask(mag)
    edge_angle = np.arctan2(gy, gx) + (math.pi / 2.0)
    raw_tiles = []
    for y0 in range(0, height, tile_size):
        for x0 in range(0, width, tile_size):
            y1 = min(height, y0 + tile_size)
            x1 = min(width, x0 + tile_size)
            ns = normals[y0:y1, x0:x1].reshape(-1, 3)
            mean_norm = float(np.linalg.norm(np.mean(ns, axis=0)))
            normal_var = max(0.0, 1.0 - mean_norm)
            seg_var = float(np.var(segments[y0:y1, x0:x1]))
            local = edge[y0:y1, x0:x1]
            if np.any(local):
                angles = edge_angle[y0:y1, x0:x1][local]
                weights = mag[y0:y1, x0:x1][local]
                z = np.sum(weights * np.exp(2j * angles)) / max(float(np.sum(weights)), 1e-12)
                orient_var = float(1.0 - np.abs(z))
            else:
                orient_var = 0.0
            col = colliders[y0:y1, x0:x1]
            hswitch = col[:, :-1] != col[:, 1:] if col.shape[1] > 1 else np.zeros((col.shape[0], 0), dtype=bool)
            vswitch = col[:-1, :] != col[1:, :] if col.shape[0] > 1 else np.zeros((0, col.shape[1]), dtype=bool)
            collider_switch = float((np.count_nonzero(hswitch) + np.count_nonzero(vswitch)) / max(hswitch.size + vswitch.size, 1))
            raw_tiles.append((x0, y0, x1, y1, normal_var, seg_var, orient_var, collider_switch))
    components = [np.array([tile[i] for tile in raw_tiles], dtype=np.float32) for i in range(4, 8)]
    normed = []
    for comp in components:
        hi = float(np.percentile(comp, 95))
        normed.append(np.clip(comp / hi, 0, 1) if hi > 1e-9 else np.zeros_like(comp))
    incoherence = np.zeros((height, width), dtype=np.float32)
    for i, tile in enumerate(raw_tiles):
        score = float(np.mean([n[i] for n in normed]))
        x0, y0, x1, y1 = tile[:4]
        incoherence[y0:y1, x0:x1] = score
    return incoherence


def weighted_centroid(weights: np.ndarray) -> tuple[float, float]:
    yy, xx = np.indices(weights.shape)
    total = float(np.sum(weights))
    if total <= 1e-12:
        return ((weights.shape[1] - 1) * 0.5, (weights.shape[0] - 1) * 0.5)
    return (float(np.sum(xx * weights) / total), float(np.sum(yy * weights) / total))


def polar_bin_metrics(width: int, height: int, rows: list[dict[str, str]], center: tuple[float, float], radial_bins: int, angular_sectors: int, log_radius: bool) -> tuple[np.ndarray, np.ndarray, list[dict]]:
    cx, cy = center
    max_radius = math.sqrt(max(cx, width - cx) ** 2 + max(cy, height - cy) ** 2)
    bins: dict[tuple[int, int], list[dict[str, str]]] = {}
    for row in rows:
        x = parse_int(row, "x")
        y = parse_int(row, "y")
        dx = x - cx
        dy = y - cy
        r = math.sqrt(dx * dx + dy * dy)
        theta = (math.atan2(dy, dx) + 2 * math.pi) % (2 * math.pi)
        if log_radius:
            rb = int(min(radial_bins - 1, max(0, math.floor(math.log1p(r) / math.log1p(max_radius) * radial_bins))))
        else:
            rb = int(min(radial_bins - 1, max(0, math.floor(r / max_radius * radial_bins))))
        ab = int(min(angular_sectors - 1, math.floor(theta / (2 * math.pi) * angular_sectors)))
        bins.setdefault((rb, ab), []).append(row)
    scores = np.zeros((radial_bins, angular_sectors), dtype=np.float32)
    details: list[dict] = []
    raw = []
    for rb in range(radial_bins):
        for ab in range(angular_sectors):
            group = bins.get((rb, ab), [])
            normals = [normal_tuple(row) for row in group]
            if normals:
                mean_norm = math.sqrt(sum((sum(n[i] for n in normals) / len(normals)) ** 2 for i in range(3)))
                normal_var = max(0.0, 1.0 - mean_norm)
            else:
                normal_var = 0.0
            colliders = [parse_int(row, "first_accepted_collider_id", parse_int(row, "collider_id")) for row in group]
            collider_switch = 1.0 - (Counter(colliders).most_common(1)[0][1] / len(colliders)) if colliders else 0.0
            distances = [parse_float(row, "first_accepted_hit_distance", parse_float(row, "hit_distance")) for row in group]
            segments = [parse_float(row, "first_accepted_segment_index", parse_float(row, "segment_count")) for row in group]
            hit_dist_var = variance(distances)
            seg_var = variance(segments)
            raw.append((normal_var, collider_switch, hit_dist_var, seg_var))
            details.append({"radial_bin": rb, "angular_sector": ab, "pixel_count": len(group), "normal_variance": normal_var, "collider_switch_density": collider_switch, "hit_distance_variance": hit_dist_var, "segment_index_variance": seg_var})
    comps = [np.array([r[i] for r in raw], dtype=np.float32) for i in range(4)]
    normed = []
    for comp in comps:
        hi = float(np.percentile(comp, 95))
        normed.append(np.clip(comp / hi, 0, 1) if hi > 1e-9 else np.zeros_like(comp))
    for i, detail in enumerate(details):
        score = float(np.mean([n[i] for n in normed]))
        detail["phase_incoherence_score"] = score
        scores[detail["radial_bin"], detail["angular_sector"]] = score
    return scores, np.kron(scores, np.ones((10, 10), dtype=np.float32)), details


def polar_boundary_image(width: int, height: int, center: tuple[float, float], radial_bins: int, angular_sectors: int, log_radius: bool) -> np.ndarray:
    cx, cy = center
    yy, xx = np.indices((height, width))
    r = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    theta = (np.arctan2(yy - cy, xx - cx) + 2 * np.pi) % (2 * np.pi)
    max_radius = math.sqrt(max(cx, width - cx) ** 2 + max(cy, height - cy) ** 2)
    if log_radius:
        rb = np.floor(np.log1p(r) / math.log1p(max_radius) * radial_bins)
    else:
        rb = np.floor(r / max_radius * radial_bins)
    ab = np.floor(theta / (2 * np.pi) * angular_sectors)
    boundary = np.zeros((height, width), dtype=bool)
    boundary[:, 1:] |= rb[:, 1:] != rb[:, :-1]
    boundary[1:, :] |= rb[1:, :] != rb[:-1, :]
    boundary[:, 1:] |= ab[:, 1:] != ab[:, :-1]
    boundary[1:, :] |= ab[1:, :] != ab[:-1, :]
    return boundary.astype(np.float32)


def save_heatmap(scores: np.ndarray, path: Path, label: str) -> None:
    arr = normalize01(scores)
    image = Image.fromarray(np.uint8(plt_colormap(arr) * 255), "RGB").resize((480, 270), Image.Resampling.NEAREST)
    draw = ImageDraw.Draw(image)
    draw.rectangle((0, 0, 480, 20), fill=(0, 0, 0))
    draw.text((6, 4), label, fill=(255, 255, 255))
    image.save(path)


def plt_colormap(arr: np.ndarray) -> np.ndarray:
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    return plt.get_cmap("magma")(arr)[..., :3]


def overlay_boundaries(base: Image.Image, boundary: np.ndarray, mask: np.ndarray, center: tuple[float, float], path: Path, label: str) -> None:
    rgb = np.asarray(base.convert("RGB")).copy()
    rgb[mask > 0.5] = (0.65 * rgb[mask > 0.5] + np.array([0, 255, 255]) * 0.35).astype(np.uint8)
    rgb[boundary > 0.5] = (255, 50, 20)
    image = Image.fromarray(rgb, "RGB")
    draw = ImageDraw.Draw(image)
    cx, cy = center
    draw.ellipse((cx - 5, cy - 5, cx + 5, cy + 5), outline=(255, 255, 255), width=2)
    draw.rectangle((0, 0, image.width, 20), fill=(0, 0, 0))
    draw.text((6, 4), label, fill=(255, 255, 255))
    image.save(path)


def analyze_checkpoint(name: str, prefix: str, diagnostics_dir: Path, visible_mask_dir: Path, output_dir: Path, radial_bins: int, angular_sectors: int, log_radius: bool, prior: dict) -> dict:
    tile_summary_path = diagnostics_dir / f"{prefix}_tile_summary.json"
    tile_summary = json.loads(tile_summary_path.read_text(encoding="utf-8"))
    csv_path = Path(tile_summary["csv_path"])
    base_path = Path(tile_summary["debug_image_path"])
    width, height, rows = load_rows(csv_path)
    size = (width, height)
    base = load_rgb(base_path, size=size)
    image_gray = load_gray(base_path, size=size)
    visible_mask_path = visible_mask_dir / f"{name}_visible_band_mask.png"
    visible = load_gray(visible_mask_path, size=size)

    normals, _, _, _ = fields_from_rows(width, height, rows)
    n_delta = normalize01(neighbor_normal_delta(normals))
    phase_incoherence = compute_adaptive_incoherence(width, height, int(tile_summary["tile_size"]), rows, image_gray)
    visible_weight = normalize01(visible)
    combined = normalize01(0.40 * visible_weight + 0.30 * n_delta + 0.30 * phase_incoherence)
    cx, cy = weighted_centroid(combined)

    scores, heatmap_grid, details = polar_bin_metrics(width, height, rows, (cx, cy), radial_bins, angular_sectors, log_radius)
    boundary = polar_boundary_image(width, height, (cx, cy), radial_bins, angular_sectors, log_radius)

    heatmap_path = output_dir / f"{prefix}_incoherence_centered_polar_heatmap.png"
    boundary_path = output_dir / f"{prefix}_incoherence_centered_polar_boundary_overlay.png"
    centroid_path = output_dir / f"{prefix}_centroid_overlay.png"
    save_heatmap(scores, heatmap_path, f"{name}: incoherence-centered polar bins")
    overlay_boundaries(base, boundary, visible, (cx, cy), boundary_path, f"{name}: polar boundary from incoherence centroid")
    overlay_boundaries(base, np.zeros_like(boundary), combined, (cx, cy), centroid_path, f"{name}: centroid over combined incoherence")

    new_metrics = compare_edges(visible, boundary)
    aperture = prior["polar"][name]["edge_alignment"]["polar_boundary_overlay"]
    adaptive = prior["adaptive"][name]
    return {
        "checkpoint": name,
        "centroid": {"x": cx, "y": cy, "source": "weighted visible_mask(0.40)+neighbor_normal_delta(0.30)+phase_incoherence(0.30)"},
        "inputs": {"tile_summary": str(tile_summary_path), "csv": str(csv_path), "visible_band_mask": str(visible_mask_path), "debug_normal_rgb": str(base_path)},
        "outputs": {"incoherence_centered_polar_heatmap": str(heatmap_path), "incoherence_centered_polar_boundary_overlay": str(boundary_path), "centroid_overlay": str(centroid_path)},
        "polar_config": {"radial_bins": radial_bins, "angular_sectors": angular_sectors, "log_radius": log_radius},
        "metrics": {"new_incoherence_centered_polar": new_metrics, "prior_aperture_centered_polar": aperture, "prior_adaptive_square": adaptive},
        "bin_summary": {"mean_incoherence": float(np.mean(scores)), "p95_incoherence": float(np.percentile(scores, 95)), "top_bins": sorted(details, key=lambda row: row["phase_incoherence_score"], reverse=True)[:12]},
    }


def build_sheet(results: list[dict], key: str, path: Path) -> None:
    cells = []
    for result in results:
        with Image.open(result["outputs"][key]) as image:
            cells.append(image.convert("RGB").resize((480, 270), Image.Resampling.BILINEAR))
    sheet = Image.new("RGB", (480, 270 * len(cells)), (18, 18, 18))
    for i, cell in enumerate(cells):
        sheet.paste(cell, (0, i * 270))
    sheet.save(path)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--diagnostics-dir", type=Path, required=True)
    parser.add_argument("--visible-mask-dir", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path)
    parser.add_argument("--radial-bins", type=int, default=24)
    parser.add_argument("--angular-sectors", type=int, default=48)
    parser.add_argument("--log-radius", action="store_true")
    args = parser.parse_args()
    output_dir = args.output_dir or (args.diagnostics_dir / "incoherence_centered_polar")
    output_dir.mkdir(parents=True, exist_ok=True)

    polar_summary = json.loads((args.diagnostics_dir / "polar_edge_comparison_summary.json").read_text(encoding="utf-8"))
    prior = {
        "polar": {row["checkpoint"]: row for row in polar_summary["checkpoints"]},
        "adaptive": polar_summary["adaptive_square_edge_baseline"],
    }
    results = [
        analyze_checkpoint(name, prefix, args.diagnostics_dir, args.visible_mask_dir, output_dir, args.radial_bins, args.angular_sectors, args.log_radius, prior)
        for name, prefix in CHECKPOINTS
    ]

    sheet_paths = {
        "incoherence_centered_polar_heatmap": output_dir / "incoherence_centered_polar_heatmap.png",
        "incoherence_centered_polar_boundary_overlay": output_dir / "incoherence_centered_polar_boundary_overlay.png",
        "centroid_overlay": output_dir / "centroid_overlay.png",
    }
    for key, path in sheet_paths.items():
        build_sheet(results, key, path)

    def avg_metric(which: str, key: str) -> float:
        return float(np.mean([row["metrics"][which][key] for row in results]))

    aggregate = {
        "new_mean_recall": avg_metric("new_incoherence_centered_polar", "visible_edge_recall_within_radius"),
        "aperture_polar_mean_recall": avg_metric("prior_aperture_centered_polar", "visible_edge_recall_within_radius"),
        "adaptive_square_mean_recall": avg_metric("prior_adaptive_square", "visible_edge_recall_within_radius"),
        "new_mean_direction_similarity": avg_metric("new_incoherence_centered_polar", "gradient_direction_similarity"),
        "aperture_polar_mean_direction_similarity": avg_metric("prior_aperture_centered_polar", "gradient_direction_similarity"),
        "adaptive_square_mean_direction_similarity": avg_metric("prior_adaptive_square", "gradient_direction_similarity"),
        "new_mean_symmetric_distance": avg_metric("new_incoherence_centered_polar", "symmetric_edge_distance_score"),
        "aperture_polar_mean_symmetric_distance": avg_metric("prior_aperture_centered_polar", "symmetric_edge_distance_score"),
        "adaptive_square_mean_symmetric_distance": avg_metric("prior_adaptive_square", "symmetric_edge_distance_score"),
    }
    better_than_aperture = aggregate["new_mean_direction_similarity"] > aggregate["aperture_polar_mean_direction_similarity"] and aggregate["new_mean_symmetric_distance"] >= aggregate["aperture_polar_mean_symmetric_distance"] * 0.95
    verdict = (
        "Incoherence-centered polar tiling improves direction fidelity over aperture-centered polar while retaining comparable boundary distance alignment."
        if better_than_aperture
        else "Incoherence-centered polar tiling does not clearly beat the existing aperture-centered polar/adaptive-square diagnostics."
    )
    summary = {
        "analysis_label": "exploratory_sampling_domain_analysis_incoherence_centered_polar",
        "diagnostics_dir": str(args.diagnostics_dir),
        "visible_mask_dir": str(args.visible_mask_dir),
        "outputs": {k: str(v) for k, v in sheet_paths.items()} | {"summary_json": str(output_dir / "incoherence_polar_summary.json")},
        "checkpoints": results,
        "aggregate": aggregate,
        "verdict": verdict,
        "next_recommended_sampling_texture": "If this does not beat adaptive square direction fidelity, try annular sectors with adaptive angular subdivision or diagonal/triangular texture probes, keeping raw validation separate from coherence overlays.",
    }
    (output_dir / "incoherence_polar_summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"outputs": summary["outputs"], "aggregate": aggregate, "verdict": verdict}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
