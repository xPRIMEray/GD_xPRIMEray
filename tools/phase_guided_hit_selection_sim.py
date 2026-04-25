#!/usr/bin/env python3
"""Analysis-only proxy simulation for phase-coherence-guided hit selection."""

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


def safe_float(value: str, fallback: float = 0.0) -> float:
    try:
        return float(value)
    except (TypeError, ValueError):
        return fallback


def load_fields(csv_path: Path, width: int, height: int) -> dict[str, np.ndarray]:
    normal = np.zeros((height, width, 3), dtype=np.float32)
    segment = np.zeros((height, width), dtype=np.float32)
    distance = np.zeros((height, width), dtype=np.float32)
    collider = np.zeros((height, width), dtype=np.int64)
    candidate_count = np.ones((height, width), dtype=np.float32)
    with csv_path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        for row in reader:
            x = int(row["x"])
            y = int(row["y"])
            if not (0 <= x < width and 0 <= y < height):
                continue
            normal[y, x] = (
                safe_float(row.get("first_accepted_normal_x", row.get("normal_x", "0"))),
                safe_float(row.get("first_accepted_normal_y", row.get("normal_y", "0"))),
                safe_float(row.get("first_accepted_normal_z", row.get("normal_z", "0"))),
            )
            segment[y, x] = safe_float(row.get("first_accepted_segment_index", row.get("segment_count", "0")))
            distance[y, x] = safe_float(row.get("first_accepted_hit_distance", row.get("hit_distance", "0")))
            collider[y, x] = int(safe_float(row.get("first_accepted_collider_id", row.get("collider_id", "0"))))
            candidate_count[y, x] = safe_float(row.get("first_accepted_candidate_count", "1"), 1.0)
    norm = np.linalg.norm(normal, axis=2, keepdims=True)
    normal = np.divide(normal, np.maximum(norm, 1e-9), out=np.zeros_like(normal), where=norm > 1e-9)
    return {"normal": normal, "segment": segment, "distance": distance, "collider": collider, "candidate_count": candidate_count}


def normalize_gray(gray: np.ndarray) -> np.ndarray:
    return cv2.normalize(gray, None, 0, 255, cv2.NORM_MINMAX).astype(np.uint8)


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


def compute_phase(
    fields: dict[str, np.ndarray],
    image_path: Path,
    tile_size: int,
) -> tuple[np.ndarray, np.ndarray, dict[tuple[int, int], dict]]:
    normal = fields["normal"]
    segment = fields["segment"]
    distance = fields["distance"]
    collider = fields["collider"]
    height, width = segment.shape

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

    raw = []
    tile_records = {}
    for y0 in range(0, height, tile_size):
        for x0 in range(0, width, tile_size):
            y1 = min(height, y0 + tile_size)
            x1 = min(width, x0 + tile_size)
            normals = normal[y0:y1, x0:x1].reshape(-1, 3)
            mean_normal = np.mean(normals, axis=0)
            mean_norm = float(np.linalg.norm(mean_normal))
            proto_normal = mean_normal / max(mean_norm, 1e-9)
            normal_var = float(np.clip(1.0 - mean_norm, 0.0, 1.0))
            seg = segment[y0:y1, x0:x1]
            seg_median = float(np.median(seg))
            seg_var = float(np.var(seg))
            dist = distance[y0:y1, x0:x1]
            dist_median = float(np.median(dist))
            dist_scale = max(float(np.percentile(np.abs(dist - dist_median), 90)), 1.0)
            local_edges = edges[y0:y1, x0:x1] > 0
            orient_var = circular_variance(edge_tangent[y0:y1, x0:x1][local_edges], edge_weight[y0:y1, x0:x1][local_edges])
            col = collider[y0:y1, x0:x1]
            values, counts = np.unique(col, return_counts=True)
            dominant_collider = int(values[np.argmax(counts)]) if values.size else 0
            horizontal = col[:, :-1] != col[:, 1:] if col.shape[1] > 1 else np.zeros((col.shape[0], 0), dtype=bool)
            vertical = col[:-1, :] != col[1:, :] if col.shape[0] > 1 else np.zeros((0, col.shape[1]), dtype=bool)
            collider_switch = float((np.count_nonzero(horizontal) + np.count_nonzero(vertical)) / max(horizontal.size + vertical.size, 1))
            rec = {
                "x": x0,
                "y": y0,
                "w": x1 - x0,
                "h": y1 - y0,
                "normal_var_raw": normal_var,
                "segment_var_raw": seg_var,
                "orientation_var_raw": orient_var,
                "collider_switch_raw": collider_switch,
                "proto_normal": proto_normal,
                "segment_median": seg_median,
                "distance_median": dist_median,
                "distance_scale": dist_scale,
                "dominant_collider": dominant_collider,
            }
            raw.append(rec)
            tile_records[(x0, y0)] = rec

    nv = percentile_normalize(np.array([r["normal_var_raw"] for r in raw], dtype=np.float32))
    sv = percentile_normalize(np.array([r["segment_var_raw"] for r in raw], dtype=np.float32))
    ov = percentile_normalize(np.array([r["orientation_var_raw"] for r in raw], dtype=np.float32))
    cv = percentile_normalize(np.array([r["collider_switch_raw"] for r in raw], dtype=np.float32))
    coherence = np.zeros((height, width), dtype=np.float32)
    incoherence = np.zeros((height, width), dtype=np.float32)
    for i, rec in enumerate(raw):
        inc = float(np.mean([nv[i], sv[i], ov[i], cv[i]]))
        coh = 1.0 - inc
        x0, y0, x1, y1 = rec["x"], rec["y"], rec["x"] + rec["w"], rec["y"] + rec["h"]
        rec["phase_incoherence"] = inc
        rec["phase_coherence"] = coh
        rec["segment_scale"] = max(float(np.sqrt(rec["segment_var_raw"])), 1.0)
        coherence[y0:y1, x0:x1] = coh
        incoherence[y0:y1, x0:x1] = inc
    return coherence, incoherence, tile_records


def tile_origin(x: int, y: int, tile_size: int) -> tuple[int, int]:
    return (x // tile_size) * tile_size, (y // tile_size) * tile_size


def phase_score(normal: np.ndarray, segment: float, collider: int, proto: dict) -> float:
    normal_mismatch = float(np.clip((1.0 - np.dot(normal, proto["proto_normal"])) / 2.0, 0.0, 1.0))
    segment_mismatch = float(min(1.0, abs(segment - proto["segment_median"]) / proto["segment_scale"]))
    collider_mismatch = 0.0 if int(collider) == int(proto["dominant_collider"]) else 1.0
    return 0.45 * normal_mismatch + 0.35 * segment_mismatch + 0.20 * collider_mismatch


def distance_score(distance: float, proto: dict) -> float:
    return float(min(1.0, max(0.0, distance - proto["distance_median"]) / proto["distance_scale"]))


def selection_score(normal: np.ndarray, segment: float, distance: float, collider: int, proto: dict, phase_lambda: float) -> tuple[float, float, float]:
    d_score = distance_score(distance, proto)
    p_score = phase_score(normal, segment, collider, proto)
    return d_score + phase_lambda * p_score, d_score, p_score


def simulate_guided_selection(
    fields: dict[str, np.ndarray],
    tile_records: dict[tuple[int, int], dict],
    tile_size: int,
    phase_lambda: float,
) -> tuple[dict[str, np.ndarray], np.ndarray, np.ndarray, np.ndarray, np.ndarray]:
    normal = fields["normal"]
    segment = fields["segment"]
    distance = fields["distance"]
    collider = fields["collider"]
    candidate_count = fields["candidate_count"]
    height, width = segment.shape
    new_normal = normal.copy()
    new_segment = segment.copy()
    new_distance = distance.copy()
    new_collider = collider.copy()
    original_score = np.zeros((height, width), dtype=np.float32)
    guided_score = np.zeros((height, width), dtype=np.float32)
    original_phase = np.zeros((height, width), dtype=np.float32)
    guided_phase = np.zeros((height, width), dtype=np.float32)

    for y in range(height):
        y0 = max(0, y - 1)
        y1 = min(height, y + 2)
        for x in range(width):
            proto = tile_records[tile_origin(x, y, tile_size)]
            original, _, original_p = selection_score(
                normal[y, x],
                float(segment[y, x]),
                float(distance[y, x]),
                int(collider[y, x]),
                proto,
                phase_lambda,
            )
            original_score[y, x] = original
            original_phase[y, x] = original_p
            if candidate_count[y, x] <= 1:
                guided_score[y, x] = original
                guided_phase[y, x] = original_p
                continue
            x0 = max(0, x - 1)
            x1 = min(width, x + 2)
            best = (original, original_p, y, x)
            for cy in range(y0, y1):
                for cx in range(x0, x1):
                    score, _, phase = selection_score(
                        normal[cy, cx],
                        float(segment[cy, cx]),
                        float(distance[cy, cx]),
                        int(collider[cy, cx]),
                        proto,
                        phase_lambda,
                    )
                    if score < best[0]:
                        best = (score, phase, cy, cx)
            guided_score[y, x] = best[0]
            guided_phase[y, x] = best[1]
            by, bx = best[2], best[3]
            new_normal[y, x] = normal[by, bx]
            new_segment[y, x] = segment[by, bx]
            new_distance[y, x] = distance[by, bx]
            new_collider[y, x] = collider[by, bx]
    return (
        {"normal": new_normal, "segment": new_segment, "distance": new_distance, "collider": new_collider, "candidate_count": candidate_count},
        original_score,
        guided_score,
        original_phase,
        guided_phase,
    )


def normal_preview(normal: np.ndarray, changed: np.ndarray, mask: np.ndarray, path: Path, title: str) -> None:
    rgb = ((normal + 1.0) * 0.5 * 255.0).clip(0, 255).astype(np.uint8)
    rgb[mask > 32] = (0.70 * rgb[mask > 32] + np.array([0, 255, 255]) * 0.30).astype(np.uint8)
    rgb[changed] = (0.55 * rgb[changed] + np.array([255, 40, 20]) * 0.45).astype(np.uint8)
    fig, ax = plt.subplots(figsize=(7.2, 4.05), dpi=180, constrained_layout=True)
    ax.imshow(rgb)
    ax.set_title(title)
    ax.axis("off")
    fig.savefig(path, facecolor="white")
    plt.close(fig)


def render_heatmap(value: np.ndarray, path: Path, title: str, mask: np.ndarray | None = None, cmap: str = "viridis") -> None:
    fig, ax = plt.subplots(figsize=(7.2, 4.05), dpi=180, constrained_layout=True)
    ax.imshow(value, cmap=cmap, vmin=0.0, vmax=1.0)
    if mask is not None:
        ax.contour((mask > 32).astype(float), levels=[0.5], colors="cyan", linewidths=0.5)
    ax.set_title(title)
    ax.axis("off")
    fig.savefig(path, facecolor="white")
    plt.close(fig)


def analyze_checkpoint(name: str, prefix: str, diagnostics_dir: Path, visible_mask_dir: Path, output_dir: Path, phase_lambda: float) -> dict:
    summary_path = diagnostics_dir / f"{prefix}_tile_summary.json"
    tile_summary = json.loads(summary_path.read_text(encoding="utf-8"))
    width = int(tile_summary["width"])
    height = int(tile_summary["height"])
    tile_size = int(tile_summary["tile_size"])
    image_path = Path(tile_summary["debug_image_path"])
    csv_path = Path(tile_summary["csv_path"])
    mask_path = visible_mask_dir / f"{name}_visible_band_mask.png"
    mask = cv2.imread(str(mask_path), cv2.IMREAD_GRAYSCALE)
    if mask is None:
        mask = np.zeros((height, width), dtype=np.uint8)

    fields = load_fields(csv_path, width, height)
    coherence, incoherence, tile_records = compute_phase(fields, image_path, tile_size)
    guided_fields, original_score, guided_score, original_phase, guided_phase = simulate_guided_selection(fields, tile_records, tile_size, phase_lambda)
    guided_coherence, guided_incoherence, _ = compute_phase(guided_fields, image_path, tile_size)

    band = mask > 32
    original_boundary = incoherence >= float(np.percentile(incoherence, 85))
    guided_boundary = guided_incoherence >= float(np.percentile(guided_incoherence, 85))
    score_reduction = np.clip(original_score - guided_score, 0.0, 1.0)
    changed = (
        (np.abs(fields["segment"] - guided_fields["segment"]) > 1e-6)
        | (fields["collider"] != guided_fields["collider"])
        | (np.sum(fields["normal"] * guided_fields["normal"], axis=2) < 0.999)
    )

    heatmap_path = output_dir / f"{prefix}_phase_coherence_map.png"
    diff_path = output_dir / f"{prefix}_phase_guided_hit_selection_diff.png"
    preview_path = output_dir / f"{prefix}_phase_guided_render_preview.png"
    render_heatmap(coherence, heatmap_path, f"{name}: phase coherence map", mask, "viridis")
    render_heatmap(score_reduction, diff_path, f"{name}: phase-guided proxy score reduction", mask, "magma")
    normal_preview(guided_fields["normal"], changed, mask, preview_path, f"{name}: phase-guided first-hit preview")

    original_corr = pearson(incoherence, band.astype(np.float32))
    guided_corr = pearson(guided_incoherence, band.astype(np.float32))
    original_band_boundary = float(np.count_nonzero(original_boundary & band) / max(np.count_nonzero(band), 1))
    guided_band_boundary = float(np.count_nonzero(guided_boundary & band) / max(np.count_nonzero(band), 1))
    original_normal_corr = pearson(incoherence, np.clip(neighbor_normal_delta(fields["normal"]) / max(float(np.percentile(neighbor_normal_delta(fields["normal"]), 95)), 1e-9), 0, 1))
    guided_nd = neighbor_normal_delta(guided_fields["normal"])
    guided_normal_corr = pearson(guided_incoherence, np.clip(guided_nd / max(float(np.percentile(guided_nd, 95)), 1e-9), 0, 1))

    return {
        "checkpoint": name,
        "inputs": {
            "tile_summary": str(summary_path),
            "hit_diagnostics_csv": str(csv_path),
            "visible_band_mask": str(mask_path),
        },
        "outputs": {
            "phase_coherence_map": str(heatmap_path),
            "phase_guided_hit_selection_diff": str(diff_path),
            "phase_guided_render_preview": str(preview_path),
        },
        "simulation_limits": "CSV artifacts do not contain actual candidate hit lists. Candidate set is proxied by accepted hits in the local 3x3 neighborhood when first_accepted_candidate_count > 1.",
        "metrics": {
            "lambda": phase_lambda,
            "candidate_proxy_pixels": int(np.count_nonzero(fields["candidate_count"] > 1)),
            "changed_pixel_fraction": float(np.count_nonzero(changed) / changed.size),
            "mean_selection_score_before": float(np.mean(original_score)),
            "mean_selection_score_after": float(np.mean(guided_score)),
            "mean_selection_score_reduction": float(np.mean(score_reduction)),
            "mean_phase_mismatch_before": float(np.mean(original_phase)),
            "mean_phase_mismatch_after": float(np.mean(guided_phase)),
            "mean_phase_coherence_before": float(np.mean(coherence)),
            "mean_phase_coherence_after": float(np.mean(guided_coherence)),
            "visible_band_incoherence_corr_before": original_corr,
            "visible_band_incoherence_corr_after": guided_corr,
            "visible_band_incoherence_corr_delta": guided_corr - original_corr,
            "visible_band_boundary_fraction_before": original_band_boundary,
            "visible_band_boundary_fraction_after": guided_band_boundary,
            "visible_band_boundary_fraction_delta": guided_band_boundary - original_band_boundary,
            "neighbor_normal_delta_corr_before": original_normal_corr,
            "neighbor_normal_delta_corr_after": guided_normal_corr,
            "neighbor_normal_delta_corr_delta": guided_normal_corr - original_normal_corr,
        },
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
        "# Phase-Coherence-Guided Hit Selection Proxy",
        "",
        "Analysis-only simulation. No geometry, renderer, hit-selection, or simulation rerun changes were made.",
        "",
        "## Limitation",
        "The available CSV artifacts do not contain actual candidate hit lists. This proxy lets each pixel choose among accepted hits in its local 3x3 neighborhood when `first_accepted_candidate_count > 1`, minimizing mismatch to the adaptive tile phase prototype.",
        "",
        "## Results",
        "| checkpoint | changed px frac | phase score before | phase score after | band corr before | band corr after | band boundary before | band boundary after |",
        "|---|---:|---:|---:|---:|---:|---:|---:|",
    ]
    for result in summary["checkpoints"]:
        m = result["metrics"]
        lines.append(
            f"| `{result['checkpoint']}` | {m['changed_pixel_fraction']:.3f} | "
            f"{m['mean_selection_score_before']:.3f} | {m['mean_selection_score_after']:.3f} | "
            f"{m['visible_band_incoherence_corr_before']:.3f} | {m['visible_band_incoherence_corr_after']:.3f} | "
            f"{m['visible_band_boundary_fraction_before']:.3f} | {m['visible_band_boundary_fraction_after']:.3f} |"
        )
    lines.extend(
        [
            "",
            "## Verdict",
            summary["verdict"],
            "",
            "Outputs:",
            f"- [{Path(summary['outputs']['phase_coherence_map']).name}]({Path(summary['outputs']['phase_coherence_map']).name})",
            f"- [{Path(summary['outputs']['phase_guided_render_preview']).name}]({Path(summary['outputs']['phase_guided_render_preview']).name})",
            f"- [{Path(summary['outputs']['phase_guided_hit_selection_diff']).name}]({Path(summary['outputs']['phase_guided_hit_selection_diff']).name})",
            f"- [{Path(summary['outputs']['band_reduction_estimate']).name}]({Path(summary['outputs']['band_reduction_estimate']).name})",
        ]
    )
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--diagnostics-dir", type=Path, required=True)
    parser.add_argument("--visible-mask-dir", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path)
    parser.add_argument("--lambda", dest="phase_lambda", type=float, default=0.35)
    args = parser.parse_args()

    output_dir = args.output_dir or (args.diagnostics_dir / "phase_guided_hit_selection")
    output_dir.mkdir(parents=True, exist_ok=True)
    results = [analyze_checkpoint(name, prefix, args.diagnostics_dir, args.visible_mask_dir, output_dir, args.phase_lambda) for name, prefix in CHECKPOINTS]

    map_sheet = output_dir / "phase_coherence_map.png"
    diff_sheet = output_dir / "phase_guided_hit_selection_diff.png"
    preview_sheet = output_dir / "phase_guided_render_preview.png"
    build_sheet(results, "phase_coherence_map", map_sheet)
    build_sheet(results, "phase_guided_hit_selection_diff", diff_sheet)
    build_sheet(results, "phase_guided_render_preview", preview_sheet)

    mean_corr_delta = float(np.mean([r["metrics"]["visible_band_incoherence_corr_delta"] for r in results]))
    mean_boundary_delta = float(np.mean([r["metrics"]["visible_band_boundary_fraction_delta"] for r in results]))
    mean_score_reduction = float(np.mean([r["metrics"]["mean_selection_score_reduction"] for r in results]))
    if mean_corr_delta < -0.03 and mean_boundary_delta < -0.02:
        verdict = "Proxy phase-guided selection reduces visible-band alignment with low-coherence boundaries."
    elif mean_score_reduction > 0.0 and (mean_corr_delta >= 0.0 or mean_boundary_delta >= 0.0):
        verdict = "Proxy phase-guided selection improves local phase score but does not reduce visible-band alignment."
    else:
        verdict = "Proxy phase-guided selection shows weak or inconclusive band-reduction evidence."

    outputs = {
        "phase_coherence_map": str(map_sheet),
        "phase_guided_render_preview": str(preview_sheet),
        "phase_guided_hit_selection_diff": str(diff_sheet),
        "band_reduction_estimate": str(output_dir / "band_reduction_metrics.json"),
        "summary_md": str(output_dir / "phase_guided_hit_selection_summary.md"),
    }
    summary = {
        "analysis_label": "analysis_only_phase_coherence_guided_hit_selection_proxy",
        "diagnostics_dir": str(args.diagnostics_dir),
        "visible_mask_dir": str(args.visible_mask_dir),
        "outputs": outputs,
        "method": {
            "phase_map": "adaptive tile phase coherence from normal variance, segment variance, edge orientation variance, and collider switch rate",
            "candidate_proxy": "local 3x3 accepted hits gated by first_accepted_candidate_count > 1; actual candidate lists are unavailable",
            "selection_score": "normalized distance + lambda * phase mismatch; phase mismatch weights normal 0.45, segment index 0.35, collider identity 0.20",
            "lambda": args.phase_lambda,
            "band_reduction": "compare visible-band correlation and visible-band phase-boundary coverage before vs after proxy selection",
        },
        "checkpoints": results,
        "aggregate": {
            "mean_visible_band_incoherence_corr_delta": mean_corr_delta,
            "mean_visible_band_boundary_fraction_delta": mean_boundary_delta,
            "mean_phase_score_reduction": mean_score_reduction,
        },
        "verdict": verdict,
    }
    Path(outputs["band_reduction_estimate"]).write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    write_markdown(summary, Path(outputs["summary_md"]))
    print(json.dumps({"outputs": outputs, "aggregate": summary["aggregate"], "verdict": verdict}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
