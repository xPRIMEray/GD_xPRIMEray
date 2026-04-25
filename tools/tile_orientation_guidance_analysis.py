#!/usr/bin/env python3
"""Tile-level orientation guidance and segment-selection mismatch analysis."""

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


def normalize_gray(gray: np.ndarray) -> np.ndarray:
    return cv2.normalize(gray, None, 0, 255, cv2.NORM_MINMAX).astype(np.uint8)


def undirected_delta(a: np.ndarray, b: np.ndarray) -> np.ndarray:
    return np.abs((a - b + np.pi / 2.0) % np.pi - np.pi / 2.0)


def circular_mean_undirected(angles: np.ndarray, weights: np.ndarray) -> float | None:
    if angles.size == 0 or float(weights.sum()) <= 1e-9:
        return None
    z = np.sum(weights * np.exp(2j * angles)) / float(weights.sum())
    return float((np.angle(z) / 2.0) % np.pi)


def visible_mask_path(visible_mask_dir: Path, checkpoint: str) -> Path:
    return visible_mask_dir / f"{checkpoint}_visible_band_mask.png"


def load_segment_index(csv_path: Path, width: int, height: int) -> np.ndarray:
    field = np.full((height, width), np.nan, dtype=np.float32)
    with csv_path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        for row in reader:
            x = int(row["x"])
            y = int(row["y"])
            raw = row.get("first_accepted_segment_index", "")
            if raw == "":
                raw = row.get("segment_count", "")
            try:
                value = float(raw)
            except ValueError:
                continue
            if 0 <= x < width and 0 <= y < height:
                field[y, x] = value
    fill = float(np.nanmedian(field)) if np.isfinite(field).any() else 0.0
    return np.nan_to_num(field, nan=fill)


def center_from_mask(mask: np.ndarray, width: int, height: int) -> tuple[float, float, str]:
    yy, xx = np.indices(mask.shape)
    weights = (mask > 32).astype(np.float64)
    total = float(weights.sum())
    if total <= 1e-9:
        return width / 2.0, height / 2.0, "image_center_fallback"
    return float((xx * weights).sum() / total), float((yy * weights).sum() / total), "visible_band_centroid"


def classify_orientation(tile_angle: float, tile_cx: float, tile_cy: float, center: tuple[float, float]) -> tuple[str, float]:
    radial_angle = math.atan2(tile_cy - center[1], tile_cx - center[0]) % math.pi
    delta = float(undirected_delta(np.array([tile_angle]), np.array([radial_angle]))[0])
    delta_deg = math.degrees(delta)
    if delta_deg <= 30.0:
        return "radial", delta_deg
    if delta_deg >= 60.0:
        return "tangential", delta_deg
    return "oblique", delta_deg


def orientation_rgb(angle: np.ndarray, confidence: np.ndarray) -> np.ndarray:
    hue = (angle % np.pi) / np.pi
    hsv = np.zeros((*angle.shape, 3), dtype=np.float32)
    hsv[..., 0] = hue
    hsv[..., 1] = np.clip(confidence, 0.0, 1.0)
    hsv[..., 2] = np.where(confidence > 0, 0.95, 0.08)
    rgb = cv2.cvtColor((hsv * 255).astype(np.uint8), cv2.COLOR_HSV2RGB)
    return rgb


def analyze_checkpoint(checkpoint: str, prefix: str, diagnostics_dir: Path, visible_mask_dir: Path, output_dir: Path) -> dict:
    summary_path = diagnostics_dir / f"{prefix}_tile_summary.json"
    summary = json.loads(summary_path.read_text(encoding="utf-8"))
    image_path = Path(summary["debug_image_path"])
    csv_path = Path(summary["csv_path"])
    tile_size = int(summary["tile_size"])
    width = int(summary["width"])
    height = int(summary["height"])

    bgr = cv2.imread(str(image_path), cv2.IMREAD_COLOR)
    if bgr is None:
        raise FileNotFoundError(image_path)
    gray = normalize_gray(cv2.cvtColor(bgr, cv2.COLOR_BGR2GRAY))
    blurred = cv2.GaussianBlur(gray, (5, 5), 0)
    edges = cv2.Canny(blurred, 50, 150)
    sobel_x = cv2.Sobel(blurred, cv2.CV_32F, 1, 0, ksize=3)
    sobel_y = cv2.Sobel(blurred, cv2.CV_32F, 0, 1, ksize=3)
    edge_tangent = (np.arctan2(sobel_y, sobel_x) + np.pi / 2.0) % np.pi
    edge_weight = cv2.magnitude(sobel_x, sobel_y)

    mask_path = visible_mask_path(visible_mask_dir, checkpoint)
    mask = cv2.imread(str(mask_path), cv2.IMREAD_GRAYSCALE)
    if mask is None:
        mask = np.zeros((height, width), dtype=np.uint8)
    center_x, center_y, center_source = center_from_mask(mask, width, height)
    center = (center_x, center_y)

    tile_angle = np.zeros((height, width), dtype=np.float32)
    tile_conf = np.zeros((height, width), dtype=np.float32)
    tile_class = np.full((height, width), 0, dtype=np.uint8)
    tile_records = []
    class_to_id = {"radial": 1, "oblique": 2, "tangential": 3}
    for y0 in range(0, height, tile_size):
        for x0 in range(0, width, tile_size):
            y1 = min(height, y0 + tile_size)
            x1 = min(width, x0 + tile_size)
            local_edges = edges[y0:y1, x0:x1] > 0
            local_weights = np.where(local_edges, edge_weight[y0:y1, x0:x1], 0.0)
            local_angles = edge_tangent[y0:y1, x0:x1][local_edges]
            local_edge_weights = local_weights[local_edges]
            angle = circular_mean_undirected(local_angles, local_edge_weights)
            if angle is None:
                angle = math.atan2((y0 + y1) / 2.0 - center_y, (x0 + x1) / 2.0 - center_x) % math.pi
                confidence = 0.0
            else:
                confidence = min(1.0, float(local_edge_weights.sum()) / 6000.0)
            cls, radial_delta = classify_orientation(angle, (x0 + x1) / 2.0, (y0 + y1) / 2.0, center)
            tile_angle[y0:y1, x0:x1] = angle
            tile_conf[y0:y1, x0:x1] = confidence
            tile_class[y0:y1, x0:x1] = class_to_id[cls]
            tile_records.append(
                {
                    "x": x0,
                    "y": y0,
                    "w": x1 - x0,
                    "h": y1 - y0,
                    "dominant_edge_orientation_degrees": math.degrees(angle),
                    "orientation_confidence": confidence,
                    "radial_delta_degrees": radial_delta,
                    "orientation_class": cls,
                }
            )

    segment_index = load_segment_index(csv_path, width, height)
    seg_blur = cv2.GaussianBlur(segment_index, (5, 5), 0)
    seg_dx = cv2.Sobel(seg_blur, cv2.CV_32F, 1, 0, ksize=3)
    seg_dy = cv2.Sobel(seg_blur, cv2.CV_32F, 0, 1, ksize=3)
    seg_grad_mag = cv2.magnitude(seg_dx, seg_dy)
    segment_orientation = (np.arctan2(seg_dy, seg_dx) + np.pi / 2.0) % np.pi
    mismatch = undirected_delta(segment_orientation, tile_angle) / (np.pi / 2.0)
    valid = (tile_conf > 0) & (seg_grad_mag > 1e-3)
    mismatch_valid = np.where(valid, mismatch, np.nan)
    heatmap = np.nan_to_num(mismatch_valid, nan=0.0)

    band = mask > 32
    finite = np.isfinite(mismatch_valid)
    values = np.nan_to_num(mismatch_valid, nan=0.0).ravel()
    band_values = band.astype(np.float32).ravel()
    if float(np.std(values)) > 1e-9 and float(np.std(band_values)) > 1e-9:
        pearson = float(np.corrcoef(values, band_values)[0, 1])
    else:
        pearson = 0.0
    in_band = mismatch_valid[band & finite]
    out_band = mismatch_valid[(~band) & finite]
    mean_in_band = float(np.nanmean(in_band)) if in_band.size else 0.0
    mean_out_band = float(np.nanmean(out_band)) if out_band.size else 0.0
    high_threshold = 0.66
    high = (heatmap >= high_threshold) & finite
    high_in_band_fraction = float(np.count_nonzero(high & band) / max(np.count_nonzero(high), 1))
    band_coverage_by_high = float(np.count_nonzero(high & band) / max(np.count_nonzero(band), 1))

    field_rgb = orientation_rgb(tile_angle, np.maximum(tile_conf, 0.25 * (tile_class > 0)))
    field_rgb[mask > 32] = (0.65 * field_rgb[mask > 32] + np.array([255, 255, 255]) * 0.35).astype(np.uint8)
    cv2.drawMarker(field_rgb, (int(round(center_x)), int(round(center_y))), (255, 255, 255), cv2.MARKER_CROSS, 18, 2)
    field_path = output_dir / f"{prefix}_tile_orientation_field.png"
    Image.fromarray(field_rgb).save(field_path)

    mismatch_path = output_dir / f"{prefix}_segment_orientation_mismatch_heatmap.png"
    fig, ax = plt.subplots(figsize=(7.2, 4.05), dpi=180, constrained_layout=True)
    ax.imshow(heatmap, cmap="magma", vmin=0.0, vmax=1.0)
    ax.contour(band.astype(float), levels=[0.5], colors="cyan", linewidths=0.5)
    ax.set_title(f"{checkpoint}: segment-selection mismatch vs tile orientation")
    ax.axis("off")
    fig.savefig(mismatch_path, facecolor="white")
    plt.close(fig)

    return {
        "checkpoint": checkpoint,
        "prefix": prefix,
        "inputs": {
            "tile_summary": str(summary_path),
            "csv_path": str(csv_path),
            "image_path": str(image_path),
            "visible_band_mask": str(mask_path),
        },
        "outputs": {
            "tile_orientation_field": str(field_path),
            "segment_orientation_mismatch_heatmap": str(mismatch_path),
        },
        "center": {"x": center_x, "y": center_y, "source": center_source},
        "tile_size": tile_size,
        "tile_count": len(tile_records),
        "tile_class_counts": {
            "radial": sum(1 for t in tile_records if t["orientation_class"] == "radial"),
            "oblique": sum(1 for t in tile_records if t["orientation_class"] == "oblique"),
            "tangential": sum(1 for t in tile_records if t["orientation_class"] == "tangential"),
        },
        "mismatch": {
            "definition": "undirected angular mismatch between tile dominant edge tangent and first_accepted_segment_index isoline tangent; 0 aligned, 1 orthogonal",
            "valid_pixel_count": int(np.count_nonzero(finite)),
            "mean": float(np.nanmean(mismatch_valid)) if np.count_nonzero(finite) else 0.0,
            "p95": float(np.nanpercentile(mismatch_valid, 95)) if np.count_nonzero(finite) else 0.0,
            "mean_in_visible_band": mean_in_band,
            "mean_outside_visible_band": mean_out_band,
            "in_minus_out": mean_in_band - mean_out_band,
        },
        "correlation_vs_visible_bands": {
            "pearson_pixel_mismatch_vs_visible_band_mask": pearson,
            "high_mismatch_threshold": high_threshold,
            "high_mismatch_pixels_in_visible_band_fraction": high_in_band_fraction,
            "visible_band_pixels_covered_by_high_mismatch_fraction": band_coverage_by_high,
        },
        "tiles": tile_records,
    }


def label_image(path: Path, label: str, size: tuple[int, int]) -> Image.Image:
    with Image.open(path) as image:
        rgb = image.convert("RGB")
        rgb.thumbnail(size, Image.Resampling.LANCZOS)
        canvas = Image.new("RGB", size, (12, 12, 12))
        canvas.paste(rgb, ((size[0] - rgb.width) // 2, (size[1] - rgb.height) // 2))
    draw = ImageDraw.Draw(canvas)
    draw.rectangle((0, 0, size[0], 20), fill=(0, 0, 0))
    draw.text((6, 4), label, fill=(255, 255, 255))
    return canvas


def build_contact_sheet(results: list[dict], key: str, output_path: Path) -> None:
    cell = (480, 270)
    sheet = Image.new("RGB", (cell[0], cell[1] * len(results)), (18, 18, 18))
    for i, result in enumerate(results):
        image = label_image(Path(result["outputs"][key]), result["checkpoint"], cell)
        sheet.paste(image, (0, i * cell[1]))
    sheet.save(output_path)


def write_markdown(summary: dict, output_path: Path) -> None:
    lines = [
        "# Tile Orientation Guidance Field",
        "",
        "Exploratory analysis only. No renderer changes, hit-selection changes, or simulation reruns were performed.",
        "",
        "## Method",
        "- Adaptive tile summaries define the tile lattice.",
        "- Dominant tile edge orientation is computed from Canny/Sobel edge tangents inside each tile.",
        "- Tiles are classified as radial, tangential, or oblique relative to the visible-band centroid.",
        "- Segment direction is approximated from the local image-space isoline tangent of `first_accepted_segment_index`, because the CSV artifacts do not store literal ray-segment direction vectors.",
        "- Mismatch is the undirected angular difference between segment-selection orientation and tile orientation, normalized so `0` is aligned and `1` is orthogonal.",
        "",
        "## Results",
        "| checkpoint | radial tiles | oblique tiles | tangential tiles | mean mismatch | in-band mismatch | out-band mismatch | Pearson vs band | high mismatch in band | band covered by high mismatch |",
        "|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|",
    ]
    for result in summary["checkpoints"]:
        c = result["tile_class_counts"]
        m = result["mismatch"]
        corr = result["correlation_vs_visible_bands"]
        lines.append(
            f"| `{result['checkpoint']}` | {c['radial']} | {c['oblique']} | {c['tangential']} | "
            f"{m['mean']:.3f} | {m['mean_in_visible_band']:.3f} | {m['mean_outside_visible_band']:.3f} | "
            f"{corr['pearson_pixel_mismatch_vs_visible_band_mask']:.3f} | "
            f"{corr['high_mismatch_pixels_in_visible_band_fraction']:.3f} | "
            f"{corr['visible_band_pixels_covered_by_high_mismatch_fraction']:.3f} |"
        )
    lines.extend(
        [
            "",
            "## Verdict",
            summary["verdict"],
            "",
            "Outputs:",
            f"- [{Path(summary['outputs']['tile_orientation_field']).name}]({Path(summary['outputs']['tile_orientation_field']).name})",
            f"- [{Path(summary['outputs']['segment_orientation_mismatch_heatmap']).name}]({Path(summary['outputs']['segment_orientation_mismatch_heatmap']).name})",
            f"- [{Path(summary['outputs']['summary_json']).name}]({Path(summary['outputs']['summary_json']).name})",
        ]
    )
    output_path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--diagnostics-dir", type=Path, required=True)
    parser.add_argument("--visible-mask-dir", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path)
    args = parser.parse_args()

    output_dir = args.output_dir or (args.diagnostics_dir / "tile_orientation_guidance")
    output_dir.mkdir(parents=True, exist_ok=True)
    results = [
        analyze_checkpoint(checkpoint, prefix, args.diagnostics_dir, args.visible_mask_dir, output_dir)
        for checkpoint, prefix in CHECKPOINTS
    ]
    field_sheet = output_dir / "tile_orientation_field.png"
    mismatch_sheet = output_dir / "segment_orientation_mismatch_heatmap.png"
    build_contact_sheet(results, "tile_orientation_field", field_sheet)
    build_contact_sheet(results, "segment_orientation_mismatch_heatmap", mismatch_sheet)

    avg_corr = float(np.mean([r["correlation_vs_visible_bands"]["pearson_pixel_mismatch_vs_visible_band_mask"] for r in results]))
    avg_in_minus_out = float(np.mean([r["mismatch"]["in_minus_out"] for r in results]))
    if avg_corr > 0.10 and avg_in_minus_out > 0.02:
        verdict = "Visible bands show a positive association with tile/segment orientation mismatch in this proxy analysis."
    elif avg_corr < -0.10 and avg_in_minus_out < -0.02:
        verdict = "Visible bands are lower-mismatch than their surroundings in this proxy analysis; banding is not explained by orientation mismatch."
    else:
        verdict = "Only a weak or mixed association was found; banding is not strongly explained by tile/segment orientation mismatch under this proxy."

    outputs = {
        "tile_orientation_field": str(field_sheet),
        "segment_orientation_mismatch_heatmap": str(mismatch_sheet),
        "summary_json": str(output_dir / "tile_orientation_guidance_summary.json"),
        "summary_md": str(output_dir / "tile_orientation_guidance_summary.md"),
    }
    summary = {
        "analysis_label": "exploratory_tile_orientation_guidance_field",
        "diagnostics_dir": str(args.diagnostics_dir),
        "visible_mask_dir": str(args.visible_mask_dir),
        "outputs": outputs,
        "method": {
            "tile_orientation": "dominant Canny/Sobel edge tangent per adaptive tile",
            "tile_classification": "radial/tangential/oblique relative to visible-band centroid",
            "segment_direction_proxy": "local isoline tangent of first_accepted_segment_index; literal segment direction vectors are not present in the CSV artifacts",
            "mismatch": "undirected angular difference normalized to [0, 1]",
        },
        "checkpoints": results,
        "aggregate": {
            "mean_pearson_vs_visible_band": avg_corr,
            "mean_in_band_minus_out_band_mismatch": avg_in_minus_out,
        },
        "verdict": verdict,
    }
    Path(outputs["summary_json"]).write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    write_markdown(summary, Path(outputs["summary_md"]))
    print(json.dumps({"outputs": outputs, "aggregate": summary["aggregate"], "verdict": verdict}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
