#!/usr/bin/env python3
"""Segment curvature-center ownership domains from existing diagnostics."""

from __future__ import annotations

import argparse
import csv
import json
import math
from pathlib import Path

import cv2
import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
from PIL import Image, ImageDraw


CHECKPOINTS = [
    ("mouth", "00_mouth"),
    ("post_throat_backstep_01", "01_post_throat_backstep_01"),
]

PALETTE = np.array(
    [
        [239, 83, 80],
        [66, 165, 245],
        [102, 187, 106],
        [255, 202, 40],
    ],
    dtype=np.uint8,
)


def safe_float(value: str | None, fallback: float = 0.0) -> float:
    try:
        return float(value) if value is not None else fallback
    except ValueError:
        return fallback


def normalize01(values: np.ndarray, pct: float = 95.0) -> np.ndarray:
    hi = float(np.percentile(values, pct)) if values.size else 1.0
    if hi <= 1e-9:
        return np.zeros_like(values, dtype=np.float32)
    return np.clip(values / hi, 0.0, 1.0).astype(np.float32)


def load_gray(path: Path, size: tuple[int, int]) -> np.ndarray:
    with Image.open(path) as image:
        gray = image.convert("L")
        if gray.size != size:
            gray = gray.resize(size, Image.Resampling.BILINEAR)
        return np.asarray(gray, dtype=np.float32) / 255.0


def load_normals(csv_path: Path, width: int, height: int) -> np.ndarray:
    normals = np.zeros((height, width, 3), dtype=np.float32)
    with csv_path.open("r", encoding="utf-8-sig", newline="") as handle:
        for row in csv.DictReader(handle):
            x = int(row["x"])
            y = int(row["y"])
            if not (0 <= x < width and 0 <= y < height):
                continue
            normals[y, x] = (
                safe_float(row.get("first_accepted_normal_x", row.get("normal_x"))),
                safe_float(row.get("first_accepted_normal_y", row.get("normal_y"))),
                safe_float(row.get("first_accepted_normal_z", row.get("normal_z"))),
            )
    length = np.linalg.norm(normals, axis=2, keepdims=True)
    return np.divide(normals, np.maximum(length, 1e-9), out=np.zeros_like(normals), where=length > 1e-9)


def neighbor_normal_delta(normal: np.ndarray) -> np.ndarray:
    height, width, _ = normal.shape
    delta = np.zeros((height, width), dtype=np.float32)
    count = np.zeros((height, width), dtype=np.float32)
    right = 1.0 - np.sum(normal[:, :-1] * normal[:, 1:], axis=2)
    down = 1.0 - np.sum(normal[:-1, :] * normal[1:, :], axis=2)
    delta[:, :-1] += right
    delta[:, 1:] += right
    count[:, :-1] += 1
    count[:, 1:] += 1
    delta[:-1, :] += down
    delta[1:, :] += down
    count[:-1, :] += 1
    count[1:, :] += 1
    return np.divide(delta, np.maximum(count, 1.0))


def load_phase_maps(phase_summary: dict, checkpoint: str, width: int, height: int) -> tuple[np.ndarray, np.ndarray]:
    row = next(item for item in phase_summary["checkpoints"] if item["checkpoint"] == checkpoint)
    phase = np.zeros((height, width), dtype=np.float32)
    normal_coherence = np.zeros((height, width), dtype=np.float32)
    for tile in row["tiles"]:
        x0 = int(tile["x"])
        y0 = int(tile["y"])
        x1 = min(width, x0 + int(tile["w"]))
        y1 = min(height, y0 + int(tile["h"]))
        phase[y0:y1, x0:x1] = float(tile["phase_coherence"])
        normal_coherence[y0:y1, x0:x1] = 1.0 - float(tile["normal_variance"])
    return np.clip(phase, 0.0, 1.0), np.clip(normal_coherence, 0.0, 1.0)


def edge_fields(image_path: Path) -> tuple[np.ndarray, np.ndarray, np.ndarray, Image.Image]:
    with Image.open(image_path) as image:
        base = image.convert("RGB")
    rgb = np.asarray(base)
    gray = cv2.cvtColor(rgb, cv2.COLOR_RGB2GRAY)
    blur = cv2.GaussianBlur(gray, (5, 5), 0)
    gx = cv2.Sobel(blur, cv2.CV_32F, 1, 0, ksize=3)
    gy = cv2.Sobel(blur, cv2.CV_32F, 0, 1, ksize=3)
    mag = cv2.magnitude(gx, gy)
    weight = normalize01(mag)
    length = np.maximum(mag, 1e-9)
    return gx / length, gy / length, weight, base


def domain_boundary(assignments: np.ndarray) -> np.ndarray:
    boundary = np.zeros(assignments.shape, dtype=bool)
    boundary[:, 1:] |= assignments[:, 1:] != assignments[:, :-1]
    boundary[1:, :] |= assignments[1:, :] != assignments[:-1, :]
    return boundary


def dilate(mask: np.ndarray, radius: int) -> np.ndarray:
    if radius <= 0:
        return mask
    kernel = np.ones((radius * 2 + 1, radius * 2 + 1), dtype=np.uint8)
    return cv2.dilate(mask.astype(np.uint8), kernel, iterations=1) > 0


def transition_normal_delta(normal: np.ndarray, assignments: np.ndarray) -> tuple[float, float]:
    cross_values: list[float] = []
    inside_values: list[float] = []
    right_delta = 1.0 - np.sum(normal[:, :-1] * normal[:, 1:], axis=2)
    right_cross = assignments[:, :-1] != assignments[:, 1:]
    cross_values.extend(right_delta[right_cross].astype(float).tolist())
    inside_values.extend(right_delta[~right_cross].astype(float).tolist())
    down_delta = 1.0 - np.sum(normal[:-1, :] * normal[1:, :], axis=2)
    down_cross = assignments[:-1, :] != assignments[1:, :]
    cross_values.extend(down_delta[down_cross].astype(float).tolist())
    inside_values.extend(down_delta[~down_cross].astype(float).tolist())
    cross = float(np.mean(cross_values)) if cross_values else 0.0
    inside = float(np.mean(inside_values)) if inside_values else 0.0
    return cross, inside


def render_domain(assignments: np.ndarray, confidence: np.ndarray, centers: list[dict], path: Path, label: str) -> None:
    rgb = PALETTE[np.mod(assignments, len(PALETTE))].copy()
    shaded = (rgb.astype(np.float32) * (0.45 + 0.55 * confidence[..., None])).astype(np.uint8)
    image = Image.fromarray(shaded, "RGB")
    draw = ImageDraw.Draw(image)
    for idx, center in enumerate(centers):
        x = float(center["x"])
        y = float(center["y"])
        color = tuple(int(v) for v in PALETTE[idx % len(PALETTE)])
        draw.ellipse((x - 5, y - 5, x + 5, y + 5), outline=(255, 255, 255), width=2)
        draw.text((x + 7, y - 8), str(idx + 1), fill=color)
    draw.rectangle((0, 0, image.width, 22), fill=(0, 0, 0))
    draw.text((6, 5), label, fill=(255, 255, 255))
    image.save(path)


def render_overlay(base: Image.Image, boundary: np.ndarray, visible: np.ndarray, centers: list[dict], path: Path, label: str) -> None:
    rgb = np.asarray(base.convert("RGB")).copy()
    band = visible > 0.125
    rgb[band] = (0.65 * rgb[band] + np.array([0, 255, 255]) * 0.35).astype(np.uint8)
    rgb[boundary] = (255, 50, 30)
    image = Image.fromarray(rgb, "RGB")
    draw = ImageDraw.Draw(image)
    for idx, center in enumerate(centers):
        x = float(center["x"])
        y = float(center["y"])
        color = tuple(int(v) for v in PALETTE[idx % len(PALETTE)])
        draw.ellipse((x - 5, y - 5, x + 5, y + 5), outline=color, width=2)
        draw.text((x + 7, y - 8), str(idx + 1), fill=color)
    draw.rectangle((0, 0, image.width, 22), fill=(0, 0, 0))
    draw.text((6, 5), label, fill=(255, 255, 255))
    image.save(path)


def analyze_checkpoint(
    checkpoint: str,
    prefix: str,
    diagnostics_dir: Path,
    visible_mask_dir: Path,
    curvature_summary: dict,
    phase_summary: dict,
    output_dir: Path,
    top_n: int,
) -> dict:
    tile_summary = json.loads((diagnostics_dir / f"{prefix}_tile_summary.json").read_text(encoding="utf-8"))
    width = int(tile_summary["width"])
    height = int(tile_summary["height"])
    image_path = Path(tile_summary["debug_image_path"])
    csv_path = Path(tile_summary["csv_path"])
    visible_path = visible_mask_dir / f"{checkpoint}_visible_band_mask.png"

    center_row = next(item for item in curvature_summary["checkpoints"] if item["checkpoint"] == checkpoint)
    centers = center_row["candidates"][: max(2, min(top_n, 4))]
    grad_x, grad_y, edge_weight, base = edge_fields(image_path)
    visible = load_gray(visible_path, (width, height))
    normals = load_normals(csv_path, width, height)
    n_delta = neighbor_normal_delta(normals)
    phase_coherence, normal_coherence = load_phase_maps(phase_summary, checkpoint, width, height)
    confidence = np.clip(edge_weight * (0.50 + 0.25 * phase_coherence + 0.25 * normal_coherence), 0.0, 1.0)

    yy, xx = np.indices((height, width), dtype=np.float32)
    scores = []
    for center in centers:
        dx = xx - float(center["x"])
        dy = yy - float(center["y"])
        radius = np.sqrt((dx * dx) + (dy * dy))
        ux = dx / np.maximum(radius, 1e-6)
        uy = dy / np.maximum(radius, 1e-6)
        radial_similarity = np.abs((grad_x * ux) + (grad_y * uy))
        distance_bias = 1.0 - np.clip(radius / max(math.hypot(width, height), 1e-6), 0.0, 1.0)
        scores.append((0.95 * radial_similarity + 0.05 * distance_bias) * (0.35 + 0.65 * confidence))
    score_stack = np.stack(scores, axis=0)
    assignments = np.argmax(score_stack, axis=0).astype(np.uint8)
    best_score = np.max(score_stack, axis=0)
    boundary = domain_boundary(assignments)
    boundary_near = dilate(boundary, 2)
    visible_band = visible > 0.125
    visible_near = dilate(visible_band, 2)

    intersection = np.logical_and(boundary, visible_band)
    union = np.logical_or(boundary, visible_band)
    dilated_intersection = np.logical_and(boundary_near, visible_band)
    dilated_union = np.logical_or(boundary_near, visible_band)
    cross_delta, inside_delta = transition_normal_delta(normals, assignments)

    domain_path = output_dir / f"{prefix}_domain_segmentation.png"
    overlay_path = output_dir / f"{prefix}_domain_boundary_overlay.png"
    render_domain(assignments, normalize01(best_score), centers, domain_path, f"{checkpoint}: curvature-domain ownership")
    render_overlay(base, boundary, visible, centers, overlay_path, f"{checkpoint}: domain boundaries vs visible bands")

    return {
        "checkpoint": checkpoint,
        "prefix": prefix,
        "inputs": {
            "curvature_summary": str(output_dir.parent / "curvature_center_polar_2026-04-25" / "curvature_center_summary.json"),
            "debug_image": str(image_path),
            "hit_diagnostics": str(csv_path),
            "visible_band_mask": str(visible_path),
            "phase_coherence_summary": str(output_dir.parent / "phase_coherence_field_2026-04-25" / "phase_coherence_summary.json"),
        },
        "centers": [
            {
                "rank": idx + 1,
                "x": float(center["x"]),
                "y": float(center["y"]),
                "radius": float(center.get("radius", 0.0)),
                "source": center.get("source", "unknown"),
                "candidate_score": float(center.get("candidate_score", 0.0)),
                "prior_direction_similarity": float(center.get("metrics", {}).get("gradient_direction_similarity", 0.0)),
                "prior_distance_score": float(center.get("metrics", {}).get("symmetric_edge_distance_score", 0.0)),
            }
            for idx, center in enumerate(centers)
        ],
        "method": {
            "assignment": "argmax over curvature centers of radial edge-gradient similarity weighted by local edge strength, tile phase coherence, and tile normal coherence; small center-distance term only breaks flat ties",
            "smoothing": "none; Gaussian blur is used only inside Sobel edge-gradient extraction",
            "boundary": "domain boundary pixels are 4-neighbor assignment transitions",
        },
        "metrics": {
            "center_count": len(centers),
            "domain_boundary_pixels": int(np.count_nonzero(boundary)),
            "visible_band_pixels": int(np.count_nonzero(visible_band)),
            "domain_boundary_visible_band_iou_exact": float(np.count_nonzero(intersection) / max(1, np.count_nonzero(union))),
            "domain_boundary_visible_band_iou_boundary_dilated_r2": float(
                np.count_nonzero(dilated_intersection) / max(1, np.count_nonzero(dilated_union))
            ),
            "bands_explained_by_domain_transitions_fraction_r2": float(
                np.count_nonzero(np.logical_and(visible_band, boundary_near)) / max(1, np.count_nonzero(visible_band))
            ),
            "domain_boundaries_near_visible_bands_fraction_r2": float(
                np.count_nonzero(np.logical_and(boundary, visible_near)) / max(1, np.count_nonzero(boundary))
            ),
            "average_normal_delta_across_domain_boundaries": cross_delta,
            "average_normal_delta_inside_domains": inside_delta,
            "normal_delta_boundary_inside_ratio": float(cross_delta / inside_delta) if inside_delta > 1e-12 else 0.0,
            "mean_assignment_confidence": float(np.mean(confidence)),
            "mean_best_center_score": float(np.mean(best_score)),
            "mean_neighbor_normal_delta_on_domain_boundary": float(np.mean(n_delta[boundary])) if np.any(boundary) else 0.0,
            "mean_neighbor_normal_delta_inside_domain_pixels": float(np.mean(n_delta[~boundary])) if np.any(~boundary) else 0.0,
        },
        "outputs": {
            "domain_segmentation": str(domain_path),
            "domain_boundary_overlay": str(overlay_path),
        },
    }


def build_sheet(results: list[dict], output_key: str, path: Path) -> None:
    cells = []
    for result in results:
        with Image.open(result["outputs"][output_key]) as image:
            cells.append(image.convert("RGB").resize((480, 270), Image.Resampling.BILINEAR))
    sheet = Image.new("RGB", (480, 270 * len(cells)), (18, 18, 18))
    for idx, cell in enumerate(cells):
        sheet.paste(cell, (0, idx * 270))
    sheet.save(path)


def write_metric_plot(results: list[dict], path: Path) -> None:
    labels = [row["checkpoint"] for row in results]
    explained = [row["metrics"]["bands_explained_by_domain_transitions_fraction_r2"] for row in results]
    iou = [row["metrics"]["domain_boundary_visible_band_iou_boundary_dilated_r2"] for row in results]
    ratio = [row["metrics"]["normal_delta_boundary_inside_ratio"] for row in results]
    fig, axes = plt.subplots(1, 3, figsize=(9, 3), dpi=160, constrained_layout=True)
    for ax, values, title in zip(
        axes,
        [explained, iou, ratio],
        ["Band pixels near domain boundary", "Boundary-band IoU r2", "Normal delta boundary/inside"],
    ):
        ax.bar(labels, values, color=["#ef5350", "#42a5f5"])
        ax.set_title(title)
        ax.tick_params(axis="x", rotation=25)
        ax.set_ylim(0, max(1.0, max(values) * 1.15 if values else 1.0))
    fig.savefig(path, facecolor="white")
    plt.close(fig)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--diagnostics-dir", type=Path, required=True)
    parser.add_argument("--visible-mask-dir", type=Path, required=True)
    parser.add_argument("--curvature-summary", type=Path, required=True)
    parser.add_argument("--phase-summary", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path)
    parser.add_argument("--top-n", type=int, default=4)
    args = parser.parse_args()

    output_dir = args.output_dir or (args.diagnostics_dir / "curvature_domain_ownership_2026-04-25")
    output_dir.mkdir(parents=True, exist_ok=True)
    curvature_summary = json.loads(args.curvature_summary.read_text(encoding="utf-8"))
    phase_summary = json.loads(args.phase_summary.read_text(encoding="utf-8"))
    results = [
        analyze_checkpoint(
            checkpoint,
            prefix,
            args.diagnostics_dir,
            args.visible_mask_dir,
            curvature_summary,
            phase_summary,
            output_dir,
            args.top_n,
        )
        for checkpoint, prefix in CHECKPOINTS
    ]

    domain_sheet = output_dir / "domain_segmentation.png"
    overlay_sheet = output_dir / "domain_boundary_overlay.png"
    plot_path = output_dir / "domain_alignment_metrics.png"
    build_sheet(results, "domain_segmentation", domain_sheet)
    build_sheet(results, "domain_boundary_overlay", overlay_sheet)
    write_metric_plot(results, plot_path)
    summary = {
        "analysis_label": "exploratory_curvature_domain_ownership",
        "constraints": "analysis-only; no renderer changes; no hit-selection changes; no simulation reruns; existing artifacts only",
        "top_n_curvature_centers": max(2, min(args.top_n, 4)),
        "outputs": {
            "domain_segmentation": str(domain_sheet),
            "domain_boundary_overlay": str(overlay_sheet),
            "domain_alignment_metrics": str(plot_path),
            "summary_json": str(output_dir / "domain_alignment_summary.json"),
        },
        "checkpoints": results,
    }
    (output_dir / "domain_alignment_summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary["outputs"], indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
