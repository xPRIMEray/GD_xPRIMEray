#!/usr/bin/env python3
"""Compare curvature-center polar tiling against existing sampling textures."""

from __future__ import annotations

import argparse
import json
import math
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw

from incoherence_centered_polar_tiling import compare_edges, load_gray, load_rgb


CHECKPOINTS = [
    ("mouth", "00_mouth"),
    ("post_throat_backstep_01", "01_post_throat_backstep_01"),
]


def normalize_gray_u8(gray: np.ndarray) -> np.ndarray:
    return cv2.normalize(gray, None, 0, 255, cv2.NORM_MINMAX).astype(np.uint8)


def detect_candidates(image_path: Path, visible_mask_path: Path, max_candidates: int = 5) -> list[dict]:
    bgr = cv2.imread(str(image_path), cv2.IMREAD_COLOR)
    if bgr is None:
        raise FileNotFoundError(image_path)
    gray = normalize_gray_u8(cv2.cvtColor(bgr, cv2.COLOR_BGR2GRAY))
    mask = cv2.imread(str(visible_mask_path), cv2.IMREAD_GRAYSCALE)
    if mask is None:
        mask = np.zeros_like(gray)
    source = cv2.max(gray, mask)
    blur = cv2.GaussianBlur(source, (5, 5), 0)
    edges = cv2.Canny(blur, 50, 150)
    h, w = gray.shape
    candidates: list[dict] = []

    circles = cv2.HoughCircles(
        blur,
        cv2.HOUGH_GRADIENT,
        dp=1.25,
        minDist=max(18, min(h, w) // 10),
        param1=90,
        param2=24,
        minRadius=max(8, min(h, w) // 18),
        maxRadius=max(16, min(h, w) // 2),
    )
    if circles is not None:
        for x, y, r in np.round(circles[0]).astype(int).tolist():
            if -w * 0.2 <= x <= w * 1.2 and -h * 0.2 <= y <= h * 1.2:
                candidates.append({"x": float(x), "y": float(y), "radius": float(r), "source": "hough_circle"})

    contours, _ = cv2.findContours(edges, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    contours = sorted(contours, key=cv2.contourArea, reverse=True)
    for contour in contours[:12]:
        if len(contour) < 12 or cv2.arcLength(contour, False) < 24:
            continue
        (x, y), r = cv2.minEnclosingCircle(contour)
        if r >= 8:
            candidates.append({"x": float(x), "y": float(y), "radius": float(r), "source": "contour_min_enclosing_circle"})

    if not candidates:
        candidates.append({"x": w / 2.0, "y": h / 2.0, "radius": min(w, h) / 3.0, "source": "image_center_fallback"})

    deduped: list[dict] = []
    for cand in candidates:
        if any(math.hypot(cand["x"] - prev["x"], cand["y"] - prev["y"]) < 12 for prev in deduped):
            continue
        center_bias = math.hypot(cand["x"] - w / 2.0, cand["y"] - h / 2.0)
        cand["candidate_score"] = float(cand["radius"] - 0.15 * center_bias)
        deduped.append(cand)
    return sorted(deduped, key=lambda row: row["candidate_score"], reverse=True)[:max_candidates]


def polar_boundary_image(width: int, height: int, center: tuple[float, float], radial_bins: int = 24, angular_sectors: int = 48) -> np.ndarray:
    cx, cy = center
    yy, xx = np.indices((height, width))
    r = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    theta = (np.arctan2(yy - cy, xx - cx) + 2 * np.pi) % (2 * np.pi)
    max_radius = math.sqrt(max(cx, width - cx) ** 2 + max(cy, height - cy) ** 2)
    rb = np.floor(r / max_radius * radial_bins)
    ab = np.floor(theta / (2 * np.pi) * angular_sectors)
    boundary = np.zeros((height, width), dtype=bool)
    boundary[:, 1:] |= rb[:, 1:] != rb[:, :-1]
    boundary[1:, :] |= rb[1:, :] != rb[:-1, :]
    boundary[:, 1:] |= ab[:, 1:] != ab[:, :-1]
    boundary[1:, :] |= ab[1:, :] != ab[:-1, :]
    return boundary.astype(np.float32)


def overlay_candidates(base: Image.Image, candidates: list[dict], best: dict | None, path: Path, label: str) -> None:
    image = base.copy()
    draw = ImageDraw.Draw(image)
    for idx, cand in enumerate(candidates, start=1):
        x, y = cand["x"], cand["y"]
        color = (255, 255, 0) if best is not None and cand["x"] == best["x"] and cand["y"] == best["y"] else (255, 80, 180)
        draw.ellipse((x - 5, y - 5, x + 5, y + 5), outline=color, width=2)
        draw.text((x + 7, y - 7), str(idx), fill=color)
    draw.rectangle((0, 0, image.width, 20), fill=(0, 0, 0))
    draw.text((6, 4), label, fill=(255, 255, 255))
    image.save(path)


def overlay_boundary(base: Image.Image, boundary: np.ndarray, center: tuple[float, float], path: Path, label: str) -> None:
    rgb = np.asarray(base.convert("RGB")).copy()
    rgb[boundary > 0.5] = (255, 60, 20)
    image = Image.fromarray(rgb, "RGB")
    draw = ImageDraw.Draw(image)
    cx, cy = center
    draw.ellipse((cx - 5, cy - 5, cx + 5, cy + 5), outline=(255, 255, 0), width=2)
    draw.rectangle((0, 0, image.width, 20), fill=(0, 0, 0))
    draw.text((6, 4), label, fill=(255, 255, 255))
    image.save(path)


def analyze_checkpoint(name: str, prefix: str, diagnostics_dir: Path, visible_mask_dir: Path, output_dir: Path, prior_polar: dict, prior_incoherence: dict) -> dict:
    tile_summary = json.loads((diagnostics_dir / f"{prefix}_tile_summary.json").read_text(encoding="utf-8"))
    image_path = Path(tile_summary["debug_image_path"])
    visible_path = visible_mask_dir / f"{name}_visible_band_mask.png"
    base = load_rgb(image_path)
    visible = load_gray(visible_path, size=base.size)
    width, height = base.size

    candidates = detect_candidates(image_path, visible_path)
    scored = []
    for cand in candidates:
        boundary = polar_boundary_image(width, height, (cand["x"], cand["y"]))
        metrics = compare_edges(visible, boundary)
        scored.append({**cand, "metrics": {"gradient_direction_similarity": metrics["gradient_direction_similarity"], "symmetric_edge_distance_score": metrics["symmetric_edge_distance_score"]}})
    best = max(scored, key=lambda row: (row["metrics"]["gradient_direction_similarity"], row["metrics"]["symmetric_edge_distance_score"]))
    boundary = polar_boundary_image(width, height, (best["x"], best["y"]))

    cand_path = output_dir / f"{prefix}_curvature_center_candidates.png"
    best_path = output_dir / f"{prefix}_best_center_overlay.png"
    overlay_candidates(base, scored, best, cand_path, f"{name}: curvature center candidates")
    overlay_boundary(base, boundary, (best["x"], best["y"]), best_path, f"{name}: best curvature-center polar")

    aperture = prior_polar["polar"][name]["edge_alignment"]["polar_boundary_overlay"]
    adaptive = prior_polar["adaptive"][name]
    incoh = prior_incoherence[name]["metrics"]["new_incoherence_centered_polar"]
    return {
        "checkpoint": name,
        "candidates": scored,
        "best_center": {"x": best["x"], "y": best["y"], "radius": best["radius"], "source": best["source"]},
        "outputs": {"curvature_center_candidates": str(cand_path), "best_center_overlay": str(best_path)},
        "comparison": {
            "curvature_center_polar": best["metrics"],
            "aperture_centered_polar": {
                "gradient_direction_similarity": aperture["gradient_direction_similarity"],
                "symmetric_edge_distance_score": aperture["symmetric_edge_distance_score"],
            },
            "incoherence_centered_polar": {
                "gradient_direction_similarity": incoh["gradient_direction_similarity"],
                "symmetric_edge_distance_score": incoh["symmetric_edge_distance_score"],
            },
            "adaptive_square": {
                "gradient_direction_similarity": adaptive["gradient_direction_similarity"],
                "symmetric_edge_distance_score": adaptive["symmetric_edge_distance_score"],
            },
        },
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
    parser.add_argument("--incoherence-summary", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path)
    args = parser.parse_args()

    output_dir = args.output_dir or (args.diagnostics_dir / "curvature_center_polar")
    output_dir.mkdir(parents=True, exist_ok=True)
    polar_summary = json.loads((args.diagnostics_dir / "polar_edge_comparison_summary.json").read_text(encoding="utf-8"))
    prior_polar = {
        "polar": {row["checkpoint"]: row for row in polar_summary["checkpoints"]},
        "adaptive": polar_summary["adaptive_square_edge_baseline"],
    }
    incoh_summary = json.loads(args.incoherence_summary.read_text(encoding="utf-8"))
    prior_incoherence = {row["checkpoint"]: row for row in incoh_summary["checkpoints"]}
    results = [
        analyze_checkpoint(name, prefix, args.diagnostics_dir, args.visible_mask_dir, output_dir, prior_polar, prior_incoherence)
        for name, prefix in CHECKPOINTS
    ]
    cand_sheet = output_dir / "curvature_center_candidates.png"
    best_sheet = output_dir / "best_center_overlay.png"
    build_sheet(results, "curvature_center_candidates", cand_sheet)
    build_sheet(results, "best_center_overlay", best_sheet)
    summary = {
        "analysis_label": "exploratory_curvature_center_polar_tiling_compare",
        "constraints": "analysis-only; no renderer changes; no reruns; metrics limited to gradient direction similarity and symmetric edge distance score",
        "outputs": {
            "curvature_center_candidates": str(cand_sheet),
            "best_center_overlay": str(best_sheet),
            "summary_json": str(output_dir / "curvature_center_summary.json"),
        },
        "checkpoints": results,
    }
    (output_dir / "curvature_center_summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary["outputs"], indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
