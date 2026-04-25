#!/usr/bin/env python3
"""Log-polar edge-orientation persistence analysis for wormhole ladder artifacts."""

from __future__ import annotations

import argparse
import json
import math
from pathlib import Path

import cv2
import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
from PIL import Image, ImageDraw


CHECKPOINT_ORDER = [
    "mouth",
    "mouth_to_throat_approach",
    "throat",
    "post_throat_backstep_01",
    "post_throat_exit_approach",
    "exit_lookback",
]

DISPLAY = {
    "mouth": "mouth",
    "mouth_to_throat_approach": "mouth->throat",
    "throat": "throat",
    "post_throat_backstep_01": "bridge",
    "post_throat_exit_approach": "post-exit",
    "exit_lookback": "exit",
}


def load_summary(summary_path: Path) -> list[dict]:
    payload = json.loads(summary_path.read_text(encoding="utf-8"))
    return payload.get("Checkpoints", [])


def normalize_gray(gray: np.ndarray) -> np.ndarray:
    return cv2.normalize(gray, None, 0, 255, cv2.NORM_MINMAX).astype(np.uint8)


def circular_angle_delta(a: np.ndarray, b: np.ndarray) -> np.ndarray:
    """Return undirected angular distance in radians, constrained to [0, pi/2]."""
    delta = np.abs((a - b + np.pi / 2.0) % np.pi - np.pi / 2.0)
    return delta


def weighted_center(edges: np.ndarray, gradient_mag: np.ndarray, fallback: tuple[float, float]) -> tuple[float, float, str]:
    weights = np.where(edges > 0, gradient_mag, 0.0).astype(np.float64)
    total = float(weights.sum())
    if total <= 1e-6:
        return fallback[0], fallback[1], "image_center_fallback"
    yy, xx = np.indices(edges.shape)
    cx = float((xx * weights).sum() / total)
    cy = float((yy * weights).sum() / total)
    return cx, cy, "edge_energy_centroid"


def primary_image_path(ladder_dir: Path, checkpoint: str, index: int) -> Path:
    prefix = f"{index:02d}_{checkpoint}"
    normal = ladder_dir / f"{prefix}_debug_normal_rgb.png"
    debug = ladder_dir / f"{prefix}_debug.png"
    return normal if normal.exists() else debug


def analyze_checkpoint(checkpoint: str, index: int, image_path: Path, output_dir: Path) -> dict:
    bgr = cv2.imread(str(image_path), cv2.IMREAD_COLOR)
    if bgr is None:
        raise FileNotFoundError(image_path)
    gray = cv2.cvtColor(bgr, cv2.COLOR_BGR2GRAY)
    normalized = normalize_gray(gray)
    blurred = cv2.GaussianBlur(normalized, (5, 5), 0)
    edges = cv2.Canny(blurred, 50, 150)
    sobel_x = cv2.Sobel(blurred, cv2.CV_32F, 1, 0, ksize=3)
    sobel_y = cv2.Sobel(blurred, cv2.CV_32F, 0, 1, ksize=3)
    gradient_mag = cv2.magnitude(sobel_x, sobel_y)
    gradient_angle = np.arctan2(sobel_y, sobel_x)

    h, w = gray.shape
    cx, cy, center_method = weighted_center(edges, gradient_mag, (w / 2.0, h / 2.0))
    yy, xx = np.indices(gray.shape)
    radial_angle = np.arctan2(yy - cy, xx - cx)
    edge_tangent_angle = gradient_angle + np.pi / 2.0
    delta = circular_angle_delta(edge_tangent_angle, radial_angle)

    edge_mask = (edges > 0) & (gradient_mag > 0)
    edge_weights = gradient_mag[edge_mask].astype(np.float64)
    delta_deg = np.degrees(delta[edge_mask])
    bins = np.linspace(0.0, 90.0, 19)
    histogram, _ = np.histogram(delta_deg, bins=bins, weights=edge_weights)
    hist_sum = float(histogram.sum())
    normalized_hist = histogram / hist_sum if hist_sum > 0 else histogram

    radial_weight = float(edge_weights[delta_deg <= 22.5].sum()) if edge_weights.size else 0.0
    tangential_weight = float(edge_weights[delta_deg >= 67.5].sum()) if edge_weights.size else 0.0
    oblique_weight = max(0.0, hist_sum - radial_weight - tangential_weight)
    radial_fraction = radial_weight / hist_sum if hist_sum > 0 else 0.0
    tangential_fraction = tangential_weight / hist_sum if hist_sum > 0 else 0.0
    rt_ratio = radial_fraction / max(tangential_fraction, 1e-9)

    max_radius = max(2.0, min(cx, cy, w - cx, h - cy))
    log_polar = cv2.warpPolar(
        edges,
        (360, 192),
        (cx, cy),
        max_radius,
        cv2.WARP_POLAR_LOG | cv2.INTER_NEAREST,
    )
    log_polar_path = output_dir / f"{index:02d}_{checkpoint}_log_polar_edges.png"
    cv2.imwrite(str(log_polar_path), log_polar)

    overlay = bgr.copy()
    overlay[edges > 0] = (0, 210, 255)
    cv2.drawMarker(
        overlay,
        (int(round(cx)), int(round(cy))),
        (255, 255, 255),
        markerType=cv2.MARKER_CROSS,
        markerSize=18,
        thickness=2,
    )
    cv2.circle(overlay, (int(round(cx)), int(round(cy))), int(round(max_radius)), (255, 0, 255), 1)
    overlay_path = output_dir / f"{index:02d}_{checkpoint}_registered_edges.png"
    cv2.imwrite(str(overlay_path), overlay)

    return {
        "checkpoint": checkpoint,
        "index": index,
        "primary_image": str(image_path),
        "registered_edge_overlay": str(overlay_path),
        "log_polar_edge_image": str(log_polar_path),
        "center": {"x": cx, "y": cy, "method": center_method, "max_radius": float(max_radius)},
        "edge_pixel_count": int(np.count_nonzero(edges)),
        "weighted_edge_count": hist_sum,
        "orientation_histogram": {
            "bins_degrees": [float(v) for v in bins.tolist()],
            "weighted_counts": [float(v) for v in histogram.tolist()],
            "normalized": [float(v) for v in normalized_hist.tolist()],
            "definition": "edge tangent vs aperture radial direction; 0 degrees radial, 90 degrees tangential",
        },
        "radial_fraction": radial_fraction,
        "tangential_fraction": tangential_fraction,
        "oblique_fraction": oblique_weight / hist_sum if hist_sum > 0 else 0.0,
        "radial_to_tangential_ratio": rt_ratio,
    }


def cosine_similarity(a: np.ndarray, b: np.ndarray) -> float:
    denom = float(np.linalg.norm(a) * np.linalg.norm(b))
    if denom <= 1e-12:
        return 0.0
    return float(np.dot(a, b) / denom)


def persistence_metrics(results: list[dict]) -> dict:
    hists = {r["checkpoint"]: np.array(r["orientation_histogram"]["normalized"], dtype=np.float64) for r in results}
    names = [r["checkpoint"] for r in results]
    adjacent = []
    for a, b in zip(names, names[1:]):
        adjacent.append({"a": a, "b": b, "cosine_similarity": cosine_similarity(hists[a], hists[b])})

    bridge_name = "post_throat_backstep_01"
    near = ["mouth", "mouth_to_throat_approach", "throat"]
    far = ["post_throat_exit_approach", "exit_lookback"]
    near_hists = [hists[n] for n in near if n in hists]
    far_hists = [hists[n] for n in far if n in hists]
    bridge_hist = hists.get(bridge_name)
    near_mean = np.mean(near_hists, axis=0) if near_hists else np.zeros(18)
    far_mean = np.mean(far_hists, axis=0) if far_hists else np.zeros(18)
    rest_hists = [hists[n] for n in names if n != bridge_name]
    rest_mean = np.mean(rest_hists, axis=0) if rest_hists else np.zeros(18)
    bridge_to_near = cosine_similarity(bridge_hist, near_mean) if bridge_hist is not None else 0.0
    bridge_to_far = cosine_similarity(bridge_hist, far_mean) if bridge_hist is not None else 0.0
    bridge_to_rest = cosine_similarity(bridge_hist, rest_mean) if bridge_hist is not None else 0.0
    rest_pairwise = [
        cosine_similarity(hists[a], hists[b])
        for i, a in enumerate(names)
        for b in names[i + 1 :]
        if a != bridge_name and b != bridge_name
    ]
    rest_pairwise_mean = float(np.mean(rest_pairwise)) if rest_pairwise else 0.0
    bridge_drop = rest_pairwise_mean - bridge_to_rest
    disrupts = bool(bridge_drop > 0.08 and bridge_to_rest < 0.92)
    return {
        "adjacent_cosine_similarity": adjacent,
        "near_side": near,
        "bridge": bridge_name,
        "far_side": far,
        "bridge_to_near_mean_cosine": bridge_to_near,
        "bridge_to_far_mean_cosine": bridge_to_far,
        "bridge_to_rest_mean_cosine": bridge_to_rest,
        "non_bridge_pairwise_mean_cosine": rest_pairwise_mean,
        "bridge_persistence_drop_vs_non_bridge_mean": bridge_drop,
        "bridge_disrupts_persistence": disrupts,
    }


def build_histogram_plot(results: list[dict], metrics: dict, output_path: Path) -> None:
    fig, axes = plt.subplots(2, 1, figsize=(10.5, 8.0), dpi=180, constrained_layout=True)
    fig.patch.set_facecolor("white")
    bins = np.array(results[0]["orientation_histogram"]["bins_degrees"], dtype=float)
    centers = (bins[:-1] + bins[1:]) / 2.0
    for result in results:
        label = DISPLAY.get(result["checkpoint"], result["checkpoint"])
        axes[0].plot(centers, result["orientation_histogram"]["normalized"], linewidth=2.0, label=label)
    axes[0].axvspan(0, 22.5, color="#d8ecff", alpha=0.5, label="radial band")
    axes[0].axvspan(67.5, 90, color="#ffe0d2", alpha=0.5, label="tangential band")
    axes[0].set_title("Log-polar edge-tangent orientation histograms")
    axes[0].set_xlabel("Edge tangent angle relative to radial direction (degrees)")
    axes[0].set_ylabel("Weighted fraction")
    axes[0].grid(True, color="#d7dde6", linewidth=0.8)
    axes[0].legend(frameon=False, ncol=3)

    names = [DISPLAY.get(r["checkpoint"], r["checkpoint"]) for r in results]
    radial = [r["radial_fraction"] for r in results]
    tangential = [r["tangential_fraction"] for r in results]
    x = np.arange(len(results))
    width = 0.36
    axes[1].bar(x - width / 2.0, radial, width, color="#1f77b4", label="radial fraction")
    axes[1].bar(x + width / 2.0, tangential, width, color="#d95f02", label="tangential fraction")
    axes[1].set_xticks(x, names, rotation=20, ha="right")
    axes[1].set_ylabel("Weighted fraction")
    axes[1].set_title(
        "Radial/tangential balance; bridge-to-rest cosine "
        f"{metrics['bridge_to_rest_mean_cosine']:.3f}"
    )
    axes[1].grid(True, axis="y", color="#d7dde6", linewidth=0.8)
    axes[1].legend(frameon=False)
    for ax in axes:
        for spine in ("top", "right"):
            ax.spines[spine].set_visible(False)
    fig.savefig(output_path, facecolor="white")
    plt.close(fig)


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


def build_contact_sheet(results: list[dict], output_path: Path) -> None:
    cell = (240, 144)
    sheet = Image.new("RGB", (len(results) * cell[0], cell[1] * 2), (18, 18, 18))
    for i, result in enumerate(results):
        label = DISPLAY.get(result["checkpoint"], result["checkpoint"])
        top = label_image(Path(result["registered_edge_overlay"]), f"{label}: registered edges", cell)
        bottom = label_image(Path(result["log_polar_edge_image"]), f"{label}: log-polar", cell)
        sheet.paste(top, (i * cell[0], 0))
        sheet.paste(bottom, (i * cell[0], cell[1]))
    sheet.save(output_path)


def write_markdown(summary: dict, output_path: Path) -> None:
    lines = [
        "# Log-Polar Edge-Orientation Persistence",
        "",
        "Exploratory geometric morphology analysis only; no physical interpretation is asserted.",
        "",
        "## Method",
        "- Registered each checkpoint to an edge-energy centroid used as the aperture/visible-band center proxy.",
        "- Built Canny edge maps from normalized grayscale debug-normal images; Gaussian blur was used only for edge detection.",
        "- Converted edge maps to log-polar coordinates for visual inspection.",
        "- Computed weighted edge-tangent orientation histograms relative to the local radial direction: 0 degrees is radial, 90 degrees is tangential.",
        "- Measured persistence with cosine similarity between normalized orientation histograms.",
        "",
        "## Metrics",
        "| checkpoint | center x | center y | edges | radial frac | tangential frac | radial/tangential |",
        "|---|---:|---:|---:|---:|---:|---:|",
    ]
    for result in summary["checkpoints"]:
        c = result["center"]
        lines.append(
            f"| `{result['checkpoint']}` | {c['x']:.1f} | {c['y']:.1f} | "
            f"{result['edge_pixel_count']} | {result['radial_fraction']:.3f} | "
            f"{result['tangential_fraction']:.3f} | {result['radial_to_tangential_ratio']:.3f} |"
        )
    p = summary["persistence"]
    lines.extend(
        [
            "",
            "## Persistence",
            f"- Non-bridge pairwise mean cosine: `{p['non_bridge_pairwise_mean_cosine']:.3f}`.",
            f"- Bridge to near-side mean cosine: `{p['bridge_to_near_mean_cosine']:.3f}`.",
            f"- Bridge to far-side mean cosine: `{p['bridge_to_far_mean_cosine']:.3f}`.",
            f"- Bridge to rest mean cosine: `{p['bridge_to_rest_mean_cosine']:.3f}`.",
            f"- Bridge persistence drop vs non-bridge mean: `{p['bridge_persistence_drop_vs_non_bridge_mean']:.3f}`.",
            "",
            "## Verdict",
            summary["verdict"],
            "",
            "Figures:",
            f"- [{Path(summary['outputs']['orientation_histograms']).name}]({Path(summary['outputs']['orientation_histograms']).name})",
            f"- [{Path(summary['outputs']['log_polar_edge_contact_sheet']).name}]({Path(summary['outputs']['log_polar_edge_contact_sheet']).name})",
        ]
    )
    output_path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--ladder-dir", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path)
    args = parser.parse_args()

    output_dir = args.output_dir or (args.ladder_dir / "log_polar_orientation")
    output_dir.mkdir(parents=True, exist_ok=True)
    checkpoints = load_summary(args.ladder_dir / "checkpoint_sequence_summary.json")
    results = []
    for index, checkpoint in enumerate(checkpoints):
        name = checkpoint.get("Name", f"checkpoint_{index:02d}")
        if name not in CHECKPOINT_ORDER:
            continue
        results.append(analyze_checkpoint(name, index, primary_image_path(args.ladder_dir, name, index), output_dir))

    persistence = persistence_metrics(results)
    verdict = (
        "Yes: the bridge disrupts radial/tangential orientation persistence relative to the rest of the ladder."
        if persistence["bridge_disrupts_persistence"]
        else "No strong disruption: the bridge changes the radial/tangential balance, but its normalized orientation histogram remains broadly persistent with the rest of the ladder."
    )
    outputs = {
        "summary_json": str(output_dir / "log_polar_orientation_summary.json"),
        "summary_md": str(output_dir / "log_polar_orientation_summary.md"),
        "orientation_histograms": str(output_dir / "orientation_histograms.png"),
        "log_polar_edge_contact_sheet": str(output_dir / "log_polar_edge_contact_sheet.png"),
    }
    summary = {
        "analysis_label": "exploratory_log_polar_edge_orientation_persistence",
        "ladder_dir": str(args.ladder_dir),
        "outputs": outputs,
        "method": {
            "registration": "edge-energy centroid of Canny/Sobel edge map as aperture/visible-band center proxy",
            "preprocessing": "grayscale normalization + GaussianBlur(5x5) for feature detection only; raw artifacts preserved",
            "log_polar": "cv2.warpPolar with WARP_POLAR_LOG for edge-map visualization",
            "orientation_histogram": "Sobel gradient normal + 90 degrees gives edge tangent; compare tangent to radial direction at each edge pixel",
            "persistence_metric": "cosine similarity between normalized weighted orientation histograms",
        },
        "checkpoints": results,
        "persistence": persistence,
        "verdict": verdict,
    }
    build_histogram_plot(results, persistence, Path(outputs["orientation_histograms"]))
    build_contact_sheet(results, Path(outputs["log_polar_edge_contact_sheet"]))
    Path(outputs["summary_json"]).write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    write_markdown(summary, Path(outputs["summary_md"]))
    print(json.dumps({"outputs": outputs, "persistence": persistence, "verdict": verdict}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
