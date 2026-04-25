#!/usr/bin/env python3
"""Compare adaptive tile artifact edges against visible band mask edges."""

from __future__ import annotations

import argparse
import json
import math
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


SOBEL_X = np.array(((-1, 0, 1), (-2, 0, 2), (-1, 0, 1)), dtype=np.float32)
SOBEL_Y = np.array(((-1, -2, -1), (0, 0, 0), (1, 2, 1)), dtype=np.float32)


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
    threshold = max(otsu(mag), 0.08)
    return mag > threshold


def dilate(mask: np.ndarray, radius: int) -> np.ndarray:
    if radius <= 0:
        return mask.copy()
    result = np.zeros_like(mask, dtype=bool)
    ys, xs = np.nonzero(mask)
    height, width = mask.shape
    for y, x in zip(ys, xs):
        y0 = max(0, y - radius)
        y1 = min(height, y + radius + 1)
        x0 = max(0, x - radius)
        x1 = min(width, x + radius + 1)
        result[y0:y1, x0:x1] = True
    return result


def distance_to_edges(edge: np.ndarray, max_radius: int) -> np.ndarray:
    height, width = edge.shape
    max_distance = max_radius + 1
    dist = np.full((height, width), max_distance, dtype=np.int16)
    q: deque[tuple[int, int]] = deque()
    ys, xs = np.nonzero(edge)
    for y, x in zip(ys, xs):
        dist[y, x] = 0
        q.append((int(y), int(x)))
    while q:
        y, x = q.popleft()
        next_dist = int(dist[y, x]) + 1
        if next_dist > max_radius:
            continue
        for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
            if ny < 0 or ny >= height or nx < 0 or nx >= width:
                continue
            if next_dist < dist[ny, nx]:
                dist[ny, nx] = next_dist
                q.append((ny, nx))
    return dist.astype(np.float32)


def pearson(lhs: np.ndarray, rhs: np.ndarray) -> float:
    a = lhs.reshape(-1).astype(np.float64)
    b = rhs.reshape(-1).astype(np.float64)
    if a.size == 0 or a.size != b.size:
        return 0.0
    a = a - float(np.mean(a))
    b = b - float(np.mean(b))
    den = math.sqrt(float(np.sum(a * a)) * float(np.sum(b * b)))
    return float(np.sum(a * b) / den) if den > 1e-12 else 0.0


def compare_edges(
    visible_edge: np.ndarray,
    visible_mag: np.ndarray,
    visible_gx: np.ndarray,
    visible_gy: np.ndarray,
    target_edge: np.ndarray,
    target_mag: np.ndarray,
    target_gx: np.ndarray,
    target_gy: np.ndarray,
    max_distance: int,
) -> dict:
    intersection = np.logical_and(visible_edge, target_edge)
    union = np.logical_or(visible_edge, target_edge)
    target_near = dilate(target_edge, max_distance)
    visible_near = dilate(visible_edge, max_distance)
    visible_count = int(np.sum(visible_edge))
    target_count = int(np.sum(target_edge))

    target_dist = distance_to_edges(target_edge, max_distance)
    visible_dist = distance_to_edges(visible_edge, max_distance)
    visible_samples = target_dist[visible_edge]
    target_samples = visible_dist[target_edge]
    visible_score = float(np.mean(np.maximum(0.0, 1.0 - (visible_samples / (max_distance + 1.0))))) if visible_samples.size else 0.0
    target_score = float(np.mean(np.maximum(0.0, 1.0 - (target_samples / (max_distance + 1.0))))) if target_samples.size else 0.0

    direction_mask = np.logical_and(visible_edge, target_near)
    direction_mask = np.logical_and(direction_mask, visible_mag > 1e-6)
    direction_mask = np.logical_and(direction_mask, target_mag > 1e-6)
    if np.any(direction_mask):
        v_len = np.sqrt((visible_gx * visible_gx) + (visible_gy * visible_gy)) + 1e-12
        t_len = np.sqrt((target_gx * target_gx) + (target_gy * target_gy)) + 1e-12
        dot = ((visible_gx * target_gx) + (visible_gy * target_gy)) / (v_len * t_len)
        direction_similarity = float(np.mean(np.abs(dot[direction_mask])))
        direction_samples = int(np.sum(direction_mask))
    else:
        direction_similarity = 0.0
        direction_samples = 0

    return {
        "visible_edge_pixels": visible_count,
        "target_edge_pixels": target_count,
        "edge_overlap_pixels": int(np.sum(intersection)),
        "edge_iou": float(np.sum(intersection) / max(1, np.sum(union))),
        "visible_edge_recall_within_radius": float(np.sum(np.logical_and(visible_edge, target_near)) / max(1, visible_count)),
        "target_edge_precision_within_radius": float(np.sum(np.logical_and(target_edge, visible_near)) / max(1, target_count)),
        "symmetric_near_overlap": 0.5
        * (
            float(np.sum(np.logical_and(visible_edge, target_near)) / max(1, visible_count))
            + float(np.sum(np.logical_and(target_edge, visible_near)) / max(1, target_count))
        ),
        "edge_distance_score_visible_to_target": visible_score,
        "edge_distance_score_target_to_visible": target_score,
        "symmetric_edge_distance_score": 0.5 * (visible_score + target_score),
        "gradient_direction_similarity": direction_similarity,
        "gradient_direction_samples": direction_samples,
        "gradient_magnitude_pearson": pearson(visible_mag, target_mag),
    }


def mask_image(edge: np.ndarray) -> Image.Image:
    return Image.fromarray((edge.astype(np.uint8) * 255), mode="L").convert("RGB")


def overlap_image(visible_edge: np.ndarray, target_edge: np.ndarray) -> Image.Image:
    arr = np.zeros((visible_edge.shape[0], visible_edge.shape[1], 3), dtype=np.uint8)
    arr[visible_edge] = (60, 170, 255)
    arr[target_edge] = (255, 70, 150)
    arr[np.logical_and(visible_edge, target_edge)] = (255, 240, 80)
    return Image.fromarray(arr, mode="RGB")


def label_cell(image: Image.Image, label: str) -> Image.Image:
    out = image.copy()
    draw = ImageDraw.Draw(out)
    draw.rectangle((0, 0, out.width, 15), fill=(0, 0, 0))
    draw.text((4, 2), label, fill=(255, 255, 255))
    return out


def build_contact_sheet(rows: list[tuple[str, Image.Image, Image.Image, Image.Image, Image.Image]]) -> Image.Image:
    if not rows:
        return Image.new("RGB", (1, 1), (0, 0, 0))
    cell_w = rows[0][1].width
    cell_h = rows[0][1].height
    sheet = Image.new("RGB", (cell_w * 4, cell_h * len(rows)), (0, 0, 0))
    for row_index, (label, visible, target, overlap, source) in enumerate(rows):
        y = row_index * cell_h
        sheet.paste(label_cell(visible, f"{label} visible edges"), (0, y))
        sheet.paste(label_cell(target, "target edges"), (cell_w, y))
        sheet.paste(label_cell(overlap, "overlap: blue/pink/yellow"), (cell_w * 2, y))
        sheet.paste(label_cell(source, "source artifact"), (cell_w * 3, y))
    return sheet


def analyze_pair(
    checkpoint: str,
    visible_mask_path: Path,
    heatmap_path: Path,
    overlay_path: Path,
    max_distance: int,
) -> tuple[dict, list[tuple[str, Image.Image, Image.Image, Image.Image, Image.Image]]]:
    visible = load_gray(visible_mask_path)
    size = (visible.shape[1], visible.shape[0])
    heatmap = load_gray(heatmap_path, size)
    overlay = load_gray(overlay_path, size)

    visible_mag, visible_gx, visible_gy = sobel(visible)
    visible_edge = edge_mask(visible_mag)
    contact_rows: list[tuple[str, Image.Image, Image.Image, Image.Image, Image.Image]] = []
    result = {
        "checkpoint": checkpoint,
        "visible_mask_path": str(visible_mask_path),
        "adaptive_tile_heatmap_path": str(heatmap_path),
        "adaptive_tile_overlay_path": str(overlay_path),
        "max_distance_px": max_distance,
        "targets": {},
    }

    for target_name, target, target_path in (
        ("adaptive_tile_heatmap", heatmap, heatmap_path),
        ("adaptive_tile_boundary_overlay", overlay, overlay_path),
    ):
        target_mag, target_gx, target_gy = sobel(target)
        target_edge = edge_mask(target_mag)
        result["targets"][target_name] = compare_edges(
            visible_edge,
            visible_mag,
            visible_gx,
            visible_gy,
            target_edge,
            target_mag,
            target_gx,
            target_gy,
            max_distance,
        )
        contact_rows.append(
            (
                f"{checkpoint} {target_name}",
                mask_image(visible_edge),
                mask_image(target_edge),
                overlap_image(visible_edge, target_edge),
                load_rgb(target_path, size),
            )
        )
    return result, contact_rows


def default_checkpoint_inputs(adaptive_dir: Path, visible_mask_dir: Path) -> list[tuple[str, Path, Path, Path]]:
    specs = []
    for checkpoint, prefix in (
        ("mouth", "00_mouth"),
        ("post_throat_backstep_01", "01_post_throat_backstep_01"),
    ):
        visible_name = f"{checkpoint}_visible_band_mask.png"
        specs.append(
            (
                checkpoint,
                visible_mask_dir / visible_name,
                adaptive_dir / f"{prefix}_adaptive_tile_heatmap.png",
                adaptive_dir / f"{prefix}_adaptive_tile_overlay.png",
            )
        )
    return specs


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--adaptive-dir", type=Path, required=True)
    parser.add_argument("--visible-mask-dir", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path)
    parser.add_argument("--max-distance", type=int, default=6)
    args = parser.parse_args()

    output_dir = args.output_dir or args.adaptive_dir
    output_dir.mkdir(parents=True, exist_ok=True)
    checkpoint_results = []
    contact_rows: list[tuple[str, Image.Image, Image.Image, Image.Image, Image.Image]] = []
    missing: list[str] = []
    for checkpoint, visible_mask, heatmap, overlay in default_checkpoint_inputs(args.adaptive_dir, args.visible_mask_dir):
        needed = (visible_mask, heatmap, overlay)
        if not all(path.exists() for path in needed):
            missing.extend(str(path) for path in needed if not path.exists())
            continue
        result, rows = analyze_pair(checkpoint, visible_mask, heatmap, overlay, max(1, args.max_distance))
        checkpoint_results.append(result)
        contact_rows.extend(rows)

    summary = {
        "adaptive_dir": str(args.adaptive_dir),
        "visible_mask_dir": str(args.visible_mask_dir),
        "max_distance_px": max(1, args.max_distance),
        "missing_inputs": missing,
        "checkpoints": checkpoint_results,
    }
    summary_path = output_dir / "edge_alignment_summary.json"
    contact_path = output_dir / "edge_alignment_contact_sheet.png"
    summary_path.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    build_contact_sheet(contact_rows).save(contact_path)
    print(json.dumps({**summary, "outputs": {"summary": str(summary_path), "contact_sheet": str(contact_path)}}, indent=2))
    return 1 if missing else 0


if __name__ == "__main__":
    raise SystemExit(main())
