#!/usr/bin/env python3
"""Adaptive-tile phase coherence field analysis against visible bands."""

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


def safe_float(value: str, fallback: float = 0.0) -> float:
    try:
        return float(value)
    except (TypeError, ValueError):
        return fallback


def load_fields(csv_path: Path, width: int, height: int) -> dict[str, np.ndarray]:
    normal = np.zeros((height, width, 3), dtype=np.float32)
    segment = np.zeros((height, width), dtype=np.float32)
    collider = np.zeros((height, width), dtype=np.int64)
    with csv_path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        for row in reader:
            x = int(row["x"])
            y = int(row["y"])
            if not (0 <= x < width and 0 <= y < height):
                continue
            nx = safe_float(row.get("first_accepted_normal_x", row.get("normal_x", "0")))
            ny = safe_float(row.get("first_accepted_normal_y", row.get("normal_y", "0")))
            nz = safe_float(row.get("first_accepted_normal_z", row.get("normal_z", "0")))
            normal[y, x] = (nx, ny, nz)
            segment[y, x] = safe_float(row.get("first_accepted_segment_index", row.get("segment_count", "0")))
            collider[y, x] = int(safe_float(row.get("first_accepted_collider_id", row.get("collider_id", "0"))))
    norm = np.linalg.norm(normal, axis=2, keepdims=True)
    normal = np.divide(normal, np.maximum(norm, 1e-9), out=np.zeros_like(normal), where=norm > 1e-9)
    return {"normal": normal, "segment": segment, "collider": collider}


def neighbor_normal_delta(normal: np.ndarray) -> np.ndarray:
    h, w, _ = normal.shape
    delta = np.zeros((h, w), dtype=np.float32)
    count = np.zeros((h, w), dtype=np.float32)
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


def circular_variance(angles: np.ndarray, weights: np.ndarray) -> float:
    if angles.size == 0 or float(weights.sum()) <= 1e-9:
        return 0.0
    z = np.sum(weights * np.exp(2j * angles)) / float(weights.sum())
    return float(1.0 - np.abs(z))


def percentile_normalize(values: np.ndarray) -> np.ndarray:
    hi = float(np.percentile(values, 95)) if values.size else 1.0
    if hi <= 1e-9:
        return np.zeros_like(values, dtype=np.float32)
    return np.clip(values / hi, 0.0, 1.0).astype(np.float32)


def pearson(a: np.ndarray, b: np.ndarray) -> float:
    av = a.ravel().astype(np.float64)
    bv = b.ravel().astype(np.float64)
    if float(np.std(av)) <= 1e-12 or float(np.std(bv)) <= 1e-12:
        return 0.0
    return float(np.corrcoef(av, bv)[0, 1])


def make_heatmap(value: np.ndarray, path: Path, title: str, contour: np.ndarray | None = None) -> None:
    fig, ax = plt.subplots(figsize=(7.2, 4.05), dpi=180, constrained_layout=True)
    ax.imshow(value, cmap="viridis", vmin=0.0, vmax=1.0)
    if contour is not None:
        ax.contour((contour > 32).astype(float), levels=[0.5], colors="white", linewidths=0.5)
    ax.set_title(title)
    ax.axis("off")
    fig.savefig(path, facecolor="white")
    plt.close(fig)


def analyze_checkpoint(checkpoint: str, prefix: str, diagnostics_dir: Path, visible_mask_dir: Path, output_dir: Path) -> dict:
    summary_path = diagnostics_dir / f"{prefix}_tile_summary.json"
    summary = json.loads(summary_path.read_text(encoding="utf-8"))
    width = int(summary["width"])
    height = int(summary["height"])
    tile_size = int(summary["tile_size"])
    csv_path = Path(summary["csv_path"])
    image_path = Path(summary["debug_image_path"])
    mask_path = visible_mask_dir / f"{checkpoint}_visible_band_mask.png"
    mask = cv2.imread(str(mask_path), cv2.IMREAD_GRAYSCALE)
    if mask is None:
        mask = np.zeros((height, width), dtype=np.uint8)

    fields = load_fields(csv_path, width, height)
    n_delta = neighbor_normal_delta(fields["normal"])

    bgr = cv2.imread(str(image_path), cv2.IMREAD_COLOR)
    if bgr is None:
        raise FileNotFoundError(image_path)
    gray = normalize_gray(cv2.cvtColor(bgr, cv2.COLOR_BGR2GRAY))
    blur = cv2.GaussianBlur(gray, (5, 5), 0)
    edges = cv2.Canny(blur, 50, 150)
    sx = cv2.Sobel(blur, cv2.CV_32F, 1, 0, ksize=3)
    sy = cv2.Sobel(blur, cv2.CV_32F, 0, 1, ksize=3)
    edge_tangent = (np.arctan2(sy, sx) + np.pi / 2.0) % np.pi
    edge_weight = cv2.magnitude(sx, sy)

    tile_rows = []
    normal_var_raw = []
    segment_var_raw = []
    orientation_var_raw = []
    collider_switch_raw = []
    tiles = []
    for y0 in range(0, height, tile_size):
        for x0 in range(0, width, tile_size):
            y1 = min(height, y0 + tile_size)
            x1 = min(width, x0 + tile_size)
            normals = fields["normal"][y0:y1, x0:x1].reshape(-1, 3)
            mean_norm = np.linalg.norm(np.mean(normals, axis=0))
            normal_variance = float(np.clip(1.0 - mean_norm, 0.0, 1.0))

            segment_values = fields["segment"][y0:y1, x0:x1]
            segment_variance = float(np.var(segment_values))

            local_edges = edges[y0:y1, x0:x1] > 0
            orientation_variance = circular_variance(
                edge_tangent[y0:y1, x0:x1][local_edges],
                edge_weight[y0:y1, x0:x1][local_edges],
            )

            col = fields["collider"][y0:y1, x0:x1]
            horizontal = col[:, :-1] != col[:, 1:] if col.shape[1] > 1 else np.zeros((col.shape[0], 0), dtype=bool)
            vertical = col[:-1, :] != col[1:, :] if col.shape[0] > 1 else np.zeros((0, col.shape[1]), dtype=bool)
            denom = horizontal.size + vertical.size
            collider_switch_rate = float((np.count_nonzero(horizontal) + np.count_nonzero(vertical)) / max(denom, 1))

            tile = {
                "x": x0,
                "y": y0,
                "w": x1 - x0,
                "h": y1 - y0,
                "normal_variance_raw": normal_variance,
                "segment_variance_raw": segment_variance,
                "orientation_variance_raw": orientation_variance,
                "collider_switch_rate_raw": collider_switch_rate,
            }
            tiles.append(tile)
            normal_var_raw.append(normal_variance)
            segment_var_raw.append(segment_variance)
            orientation_var_raw.append(orientation_variance)
            collider_switch_raw.append(collider_switch_rate)

    nv = percentile_normalize(np.array(normal_var_raw, dtype=np.float32))
    sv = percentile_normalize(np.array(segment_var_raw, dtype=np.float32))
    ov = percentile_normalize(np.array(orientation_var_raw, dtype=np.float32))
    cv = percentile_normalize(np.array(collider_switch_raw, dtype=np.float32))

    coherence = np.zeros((height, width), dtype=np.float32)
    incoherence = np.zeros((height, width), dtype=np.float32)
    for i, tile in enumerate(tiles):
        score_incoherent = float(np.mean([nv[i], sv[i], ov[i], cv[i]]))
        score_coherent = 1.0 - score_incoherent
        x0, y0, x1, y1 = tile["x"], tile["y"], tile["x"] + tile["w"], tile["y"] + tile["h"]
        coherence[y0:y1, x0:x1] = score_coherent
        incoherence[y0:y1, x0:x1] = score_incoherent
        tile.update(
            {
                "normal_variance": float(nv[i]),
                "segment_variance": float(sv[i]),
                "orientation_variance": float(ov[i]),
                "collider_switch_rate": float(cv[i]),
                "phase_incoherence": score_incoherent,
                "phase_coherence": score_coherent,
            }
        )
        tile_rows.append(tile)

    heatmap_path = output_dir / f"{prefix}_phase_coherence_heatmap.png"
    make_heatmap(coherence, heatmap_path, f"{checkpoint}: phase coherence", mask)

    boundary = incoherence >= float(np.percentile(incoherence, 85))
    overlay = cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB)
    overlay[boundary] = (255, 60, 20)
    overlay[mask > 32] = (0.65 * overlay[mask > 32] + np.array([0, 255, 255]) * 0.35).astype(np.uint8)
    overlay_path = output_dir / f"{prefix}_phase_boundary_overlay.png"
    Image.fromarray(overlay).save(overlay_path)

    neighbor_norm = np.clip(n_delta / max(float(np.percentile(n_delta, 95)), 1e-9), 0.0, 1.0)
    neighbor_path = output_dir / f"{prefix}_neighbor_normal_delta.png"
    make_heatmap(neighbor_norm, neighbor_path, f"{checkpoint}: neighbor normal delta", mask)

    band = (mask > 32).astype(np.float32)
    low_coherence = 1.0 - coherence
    band_bool = band > 0
    low_boundary = low_coherence >= float(np.percentile(low_coherence, 85))
    corr_band_coherence = pearson(coherence, band)
    corr_band_incoherence = pearson(low_coherence, band)
    corr_neighbor_coherence = pearson(coherence, neighbor_norm)
    corr_neighbor_incoherence = pearson(low_coherence, neighbor_norm)

    return {
        "checkpoint": checkpoint,
        "prefix": prefix,
        "inputs": {
            "tile_summary": str(summary_path),
            "csv_path": str(csv_path),
            "debug_normal_rgb": str(image_path),
            "visible_band_mask": str(mask_path),
        },
        "outputs": {
            "phase_coherence_heatmap": str(heatmap_path),
            "phase_boundary_overlay": str(overlay_path),
            "neighbor_normal_delta": str(neighbor_path),
        },
        "tile_size": tile_size,
        "tile_count": len(tile_rows),
        "normalization": "Each component normalized by its checkpoint 95th percentile, clipped to [0,1]; coherence = 1 - mean(normal, segment, orientation, collider incoherence).",
        "metrics": {
            "mean_phase_coherence": float(np.mean(coherence)),
            "mean_phase_incoherence": float(np.mean(low_coherence)),
            "mean_coherence_in_visible_band": float(np.mean(coherence[band_bool])) if np.any(band_bool) else 0.0,
            "mean_coherence_outside_visible_band": float(np.mean(coherence[~band_bool])) if np.any(~band_bool) else 0.0,
            "correlation_coherence_vs_visible_band": corr_band_coherence,
            "correlation_incoherence_vs_visible_band": corr_band_incoherence,
            "correlation_coherence_vs_neighbor_normal_delta": corr_neighbor_coherence,
            "correlation_incoherence_vs_neighbor_normal_delta": corr_neighbor_incoherence,
            "phase_boundary_pixels_in_visible_band_fraction": float(np.count_nonzero(low_boundary & band_bool) / max(np.count_nonzero(low_boundary), 1)),
            "visible_band_pixels_on_phase_boundary_fraction": float(np.count_nonzero(low_boundary & band_bool) / max(np.count_nonzero(band_bool), 1)),
        },
        "tiles": tile_rows,
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


def build_sheet(results: list[dict], output_key: str, path: Path) -> None:
    cell = (480, 270)
    sheet = Image.new("RGB", (cell[0], cell[1] * len(results)), (18, 18, 18))
    for i, result in enumerate(results):
        sheet.paste(label_image(Path(result["outputs"][output_key]), result["checkpoint"], cell), (0, i * cell[1]))
    sheet.save(path)


def write_markdown(summary: dict, path: Path) -> None:
    lines = [
        "# Phase Coherence Field",
        "",
        "Exploratory analysis only. No renderer changes, hit-selection changes, or simulation reruns were performed.",
        "",
        "## Method",
        "- Used existing adaptive tile summaries and hit diagnostic CSVs.",
        "- Per tile, computed normal variance, first-accepted segment-index variance, Canny/Sobel edge-orientation circular variance, and collider switch rate.",
        "- Each component was normalized to `[0, 1]` by checkpoint 95th percentile clipping.",
        "- Phase coherence is `1 - mean(component incoherence)`; low coherence is treated as a phase-boundary proxy.",
        "- Neighbor-normal delta was derived from adjacent first-accepted normal vectors in the CSV artifacts.",
        "",
        "## Results",
        "| checkpoint | mean coherence | band coherence | outside coherence | incoh vs band r | incoh vs normal-delta r | boundary in band | band on boundary |",
        "|---|---:|---:|---:|---:|---:|---:|---:|",
    ]
    for result in summary["checkpoints"]:
        m = result["metrics"]
        lines.append(
            f"| `{result['checkpoint']}` | {m['mean_phase_coherence']:.3f} | "
            f"{m['mean_coherence_in_visible_band']:.3f} | {m['mean_coherence_outside_visible_band']:.3f} | "
            f"{m['correlation_incoherence_vs_visible_band']:.3f} | "
            f"{m['correlation_incoherence_vs_neighbor_normal_delta']:.3f} | "
            f"{m['phase_boundary_pixels_in_visible_band_fraction']:.3f} | "
            f"{m['visible_band_pixels_on_phase_boundary_fraction']:.3f} |"
        )
    lines.extend(
        [
            "",
            "## Verdict",
            summary["verdict"],
            "",
            "Outputs:",
            f"- [{Path(summary['outputs']['phase_coherence_heatmap']).name}]({Path(summary['outputs']['phase_coherence_heatmap']).name})",
            f"- [{Path(summary['outputs']['phase_boundary_overlay']).name}]({Path(summary['outputs']['phase_boundary_overlay']).name})",
            f"- [{Path(summary['outputs']['summary_json']).name}]({Path(summary['outputs']['summary_json']).name})",
        ]
    )
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--diagnostics-dir", type=Path, required=True)
    parser.add_argument("--visible-mask-dir", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path)
    args = parser.parse_args()

    output_dir = args.output_dir or (args.diagnostics_dir / "phase_coherence_field")
    output_dir.mkdir(parents=True, exist_ok=True)
    results = [analyze_checkpoint(name, prefix, args.diagnostics_dir, args.visible_mask_dir, output_dir) for name, prefix in CHECKPOINTS]

    heatmap_sheet = output_dir / "phase_coherence_heatmap.png"
    overlay_sheet = output_dir / "phase_boundary_overlay.png"
    build_sheet(results, "phase_coherence_heatmap", heatmap_sheet)
    build_sheet(results, "phase_boundary_overlay", overlay_sheet)

    mean_incoh_band_corr = float(np.mean([r["metrics"]["correlation_incoherence_vs_visible_band"] for r in results]))
    mean_incoh_normal_corr = float(np.mean([r["metrics"]["correlation_incoherence_vs_neighbor_normal_delta"] for r in results]))
    mean_band_minus_out = float(
        np.mean(
            [
                (1.0 - r["metrics"]["mean_coherence_in_visible_band"])
                - (1.0 - r["metrics"]["mean_coherence_outside_visible_band"])
                for r in results
            ]
        )
    )
    if mean_incoh_band_corr > 0.15 and mean_band_minus_out > 0.02:
        verdict = "Visible bands align with low-coherence phase-boundary structure in this adaptive-tile field."
    elif mean_incoh_band_corr < -0.10 and mean_band_minus_out < -0.02:
        verdict = "Visible bands are more coherent than their surroundings in this adaptive-tile field; low-coherence boundaries do not explain the bands."
    else:
        verdict = "Band alignment with low-coherence phase boundaries is weak or mixed in this adaptive-tile field."

    outputs = {
        "phase_coherence_heatmap": str(heatmap_sheet),
        "phase_boundary_overlay": str(overlay_sheet),
        "summary_json": str(output_dir / "phase_coherence_summary.json"),
        "summary_md": str(output_dir / "phase_coherence_summary.md"),
    }
    summary = {
        "analysis_label": "exploratory_adaptive_tile_phase_coherence_field",
        "diagnostics_dir": str(args.diagnostics_dir),
        "visible_mask_dir": str(args.visible_mask_dir),
        "outputs": outputs,
        "checkpoints": results,
        "aggregate": {
            "mean_correlation_incoherence_vs_visible_band": mean_incoh_band_corr,
            "mean_correlation_incoherence_vs_neighbor_normal_delta": mean_incoh_normal_corr,
            "mean_visible_band_incoherence_minus_outside": mean_band_minus_out,
        },
        "verdict": verdict,
    }
    Path(outputs["summary_json"]).write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    write_markdown(summary, Path(outputs["summary_md"]))
    print(json.dumps({"outputs": outputs, "aggregate": summary["aggregate"], "verdict": verdict}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
