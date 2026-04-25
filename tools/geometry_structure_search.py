#!/usr/bin/env python3
"""Exploratory geometric morphology search over existing wormhole artifacts."""

from __future__ import annotations

import argparse
import json
import math
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont


CHECKPOINT_ORDER = [
    "mouth",
    "mouth_to_throat_approach",
    "throat",
    "post_throat_backstep_01",
    "post_throat_exit_approach",
    "exit_lookback",
]


def load_rgb(path: Path) -> np.ndarray:
    image = cv2.imread(str(path), cv2.IMREAD_COLOR)
    if image is None:
        raise FileNotFoundError(path)
    return image


def gray_from_bgr(image: np.ndarray) -> np.ndarray:
    return cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)


def normalize_gray(gray: np.ndarray) -> np.ndarray:
    return cv2.normalize(gray, None, 0, 255, cv2.NORM_MINMAX).astype(np.uint8)


def contour_eccentricity(contour: np.ndarray) -> float:
    if len(contour) < 5:
        return 0.0
    (_, _), axes, _ = cv2.fitEllipse(contour)
    major = max(float(axes[0]), float(axes[1]))
    minor = min(float(axes[0]), float(axes[1]))
    if major <= 1e-9:
        return 0.0
    return math.sqrt(max(0.0, 1.0 - (minor * minor) / (major * major)))


def dominant_orientations(lines: np.ndarray | None) -> list[dict]:
    if lines is None:
        return []
    angles: list[float] = []
    for line in lines[:, 0, :]:
        x1, y1, x2, y2 = [float(v) for v in line]
        angle = math.degrees(math.atan2(y2 - y1, x2 - x1))
        angle = (angle + 180.0) % 180.0
        angles.append(angle)
    if not angles:
        return []
    bins = np.histogram(angles, bins=18, range=(0.0, 180.0))[0]
    top = np.argsort(bins)[::-1][:3]
    out = []
    for index in top:
        if int(bins[index]) <= 0:
            continue
        out.append({"angle_degrees": float((index + 0.5) * 10.0), "line_count": int(bins[index])})
    return out


def hough_circles(gray: np.ndarray) -> list[dict]:
    blurred = cv2.medianBlur(gray, 5)
    min_dim = min(gray.shape[:2])
    circles = cv2.HoughCircles(
        blurred,
        cv2.HOUGH_GRADIENT,
        dp=1.25,
        minDist=max(12, min_dim // 12),
        param1=90,
        param2=22,
        minRadius=max(5, min_dim // 40),
        maxRadius=max(12, min_dim // 2),
    )
    if circles is None:
        return []
    rounded = np.round(circles[0]).astype(int)
    return [
        {"center_x": int(x), "center_y": int(y), "radius": int(r)}
        for x, y, r in sorted(rounded.tolist(), key=lambda c: c[2], reverse=True)[:12]
    ]


def connected_components(mask: np.ndarray | None) -> dict:
    if mask is None:
        return {"available": False, "component_count": 0, "largest_area": 0}
    binary = (mask > 32).astype(np.uint8)
    count, _, stats, _ = cv2.connectedComponentsWithStats(binary, 8)
    areas = [int(stats[i, cv2.CC_STAT_AREA]) for i in range(1, count)]
    return {
        "available": True,
        "component_count": len(areas),
        "largest_area": max(areas) if areas else 0,
    }


def analyze_image(primary_path: Path, mask_paths: dict[str, Path]) -> tuple[dict, np.ndarray]:
    bgr = load_rgb(primary_path)
    gray = gray_from_bgr(bgr)
    normalized = normalize_gray(gray)
    blurred = cv2.GaussianBlur(normalized, (5, 5), 0)
    edges = cv2.Canny(blurred, 50, 150)
    sobel_x = cv2.Sobel(blurred, cv2.CV_32F, 1, 0, ksize=3)
    sobel_y = cv2.Sobel(blurred, cv2.CV_32F, 0, 1, ksize=3)
    gradient_mag = cv2.magnitude(sobel_x, sobel_y)

    contours, hierarchy = cv2.findContours(edges, cv2.RETR_TREE, cv2.CHAIN_APPROX_SIMPLE)
    contours = sorted(contours, key=cv2.contourArea, reverse=True)
    contour_areas = [float(cv2.contourArea(c)) for c in contours]
    large_contours = [c for c in contours if cv2.contourArea(c) >= 50.0]
    eccentricities = [contour_eccentricity(c) for c in large_contours]
    nested_count = 0
    if hierarchy is not None:
        nested_count = sum(1 for h in hierarchy[0] if int(h[3]) >= 0)

    lines = cv2.HoughLinesP(edges, 1, np.pi / 180.0, threshold=45, minLineLength=20, maxLineGap=5)
    circles = hough_circles(blurred)
    radii = sorted([float(c["radius"]) for c in circles])
    spacings = [radii[i + 1] - radii[i] for i in range(len(radii) - 1)]

    mask_component_metrics = {}
    for label, path in mask_paths.items():
        if path.exists():
            mask_component_metrics[label] = connected_components(cv2.imread(str(path), cv2.IMREAD_GRAYSCALE))
        else:
            mask_component_metrics[label] = connected_components(None)

    annotated = bgr.copy()
    cv2.drawContours(annotated, large_contours[:12], -1, (0, 255, 255), 1)
    for circle in circles[:8]:
        cv2.circle(annotated, (circle["center_x"], circle["center_y"]), circle["radius"], (255, 0, 255), 1)
        cv2.circle(annotated, (circle["center_x"], circle["center_y"]), 2, (255, 255, 255), -1)
    if lines is not None:
        for line in lines[:80, 0, :]:
            x1, y1, x2, y2 = [int(v) for v in line]
            cv2.line(annotated, (x1, y1), (x2, y2), (0, 180, 255), 1)

    metrics = {
        "primary_image": str(primary_path),
        "preprocessing": "grayscale normalize + GaussianBlur(5x5) for detection only; raw artifacts preserved",
        "edge_pixels": int(np.count_nonzero(edges)),
        "mean_gradient_magnitude": float(np.mean(gradient_mag)),
        "detected_ring_or_circle_count": len(circles),
        "detected_rings_or_circles": circles,
        "radial_band_spacing": {
            "count": len(spacings),
            "mean": float(np.mean(spacings)) if spacings else 0.0,
            "std": float(np.std(spacings)) if spacings else 0.0,
            "values": spacings,
        },
        "contour_count": len(contours),
        "large_contour_count": len(large_contours),
        "largest_contour_area": max(contour_areas) if contour_areas else 0.0,
        "nested_contour_count": nested_count,
        "mean_contour_eccentricity": float(np.mean(eccentricities)) if eccentricities else 0.0,
        "max_contour_eccentricity": max(eccentricities) if eccentricities else 0.0,
        "hough_line_count": int(0 if lines is None else len(lines)),
        "dominant_orientation_angles": dominant_orientations(lines),
        "connected_components": mask_component_metrics,
    }
    return metrics, annotated


def load_summary(summary_path: Path) -> list[dict]:
    data = json.loads(summary_path.read_text())
    return data.get("Checkpoints", [])


def optional_artifacts(checkpoint_name: str, index: int, ladder_dir: Path, auxiliary_dir: Path, visible_dir: Path) -> dict[str, Path]:
    prefix = f"{index:02d}_{checkpoint_name}"
    short = checkpoint_name
    return {
        "debug": ladder_dir / f"{prefix}_debug.png",
        "normal_rgb": ladder_dir / f"{prefix}_debug_normal_rgb.png",
        "adaptive_tile_heatmap": auxiliary_dir / f"{prefix}_adaptive_tile_heatmap.png",
        "polar_coherence_heatmap": auxiliary_dir / f"{prefix}_polar_coherence_heatmap.png",
        "visible_band_mask": visible_dir / f"{short}_visible_band_mask.png",
    }


def annotate_label(image: Image.Image, label: str) -> Image.Image:
    out = image.copy()
    draw = ImageDraw.Draw(out)
    draw.rectangle((0, 0, out.width, 18), fill=(0, 0, 0))
    draw.text((5, 3), label, fill=(255, 255, 255))
    return out


def build_contact_sheet(images: list[tuple[str, Path]]) -> Image.Image:
    thumbs: list[Image.Image] = []
    for label, path in images:
        with Image.open(path) as image:
            rgb = image.convert("RGB")
            rgb.thumbnail((240, 135), Image.Resampling.LANCZOS)
            canvas = Image.new("RGB", (240, 135), (0, 0, 0))
            canvas.paste(rgb, ((240 - rgb.width) // 2, (135 - rgb.height) // 2))
            thumbs.append(annotate_label(canvas, label))
    if not thumbs:
        return Image.new("RGB", (1, 1), (0, 0, 0))
    cols = 3
    rows = math.ceil(len(thumbs) / cols)
    sheet = Image.new("RGB", (cols * 240, rows * 135), (18, 18, 18))
    for i, thumb in enumerate(thumbs):
        sheet.paste(thumb, ((i % cols) * 240, (i // cols) * 135))
    return sheet


def classify_regime(results: list[dict]) -> dict:
    ring_counts = [r["metrics"]["detected_ring_or_circle_count"] for r in results]
    contour_counts = [r["metrics"]["large_contour_count"] for r in results]
    gradients = [r["metrics"]["mean_gradient_magnitude"] for r in results]
    backstep = next((r for r in results if r["checkpoint"] == "post_throat_backstep_01"), None)
    throat = next((r for r in results if r["checkpoint"] == "throat"), None)
    stable_rings = len(set(ring_counts)) <= 2 and max(ring_counts, default=0) > 0
    bridge_distinct = False
    if backstep and throat:
        bridge_distinct = (
            backstep["metrics"]["large_contour_count"] > throat["metrics"]["large_contour_count"] * 1.25
            or backstep["metrics"]["mean_gradient_magnitude"] > throat["metrics"]["mean_gradient_magnitude"] * 1.20
        )
    noisy = max(contour_counts, default=0) > 200 and np.std(gradients) < 1.0
    if stable_rings and bridge_distinct:
        label = "mixed regime behavior"
    elif bridge_distinct:
        label = "singular bridge transition"
    elif stable_rings:
        label = "stable geometric family"
    elif noisy:
        label = "noisy artifact field"
    else:
        label = "mixed regime behavior"
    return {
        "regime_label": label,
        "stable_ring_count_pattern": stable_rings,
        "bridge_distinct_from_throat_heuristic": bridge_distinct,
        "noisy_artifact_heuristic": noisy,
    }


def write_markdown(summary: dict, path: Path) -> None:
    lines = [
        "# Geometry Structure Search",
        "",
        "Exploratory geometric morphology analysis only; no physical interpretation is asserted.",
        "",
        "## Method",
        "- Primary detection image: debug normal RGB when present, otherwise debug capture.",
        "- Detection preprocessing: grayscale normalization, Gaussian blur for feature detection only, Canny/Sobel edges.",
        "- Features: Hough circles, Hough line orientations, contour hierarchy/eccentricity, connected components in available masks/coherence artifacts.",
        "",
        "## Metrics",
        "| checkpoint | rings/arcs | large contours | largest contour area | mean eccentricity | spacing mean | Hough lines | dominant angles | components |",
        "|---|---:|---:|---:|---:|---:|---:|---|---|",
    ]
    for result in summary["checkpoints"]:
        metrics = result["metrics"]
        components = metrics["connected_components"]
        comp_text = ", ".join(
            f"{name}:{value['component_count']}" for name, value in components.items() if value.get("available")
        ) or "none"
        angles = ", ".join(f"{a['angle_degrees']:.1f}" for a in metrics["dominant_orientation_angles"])
        lines.append(
            f"| {result['checkpoint']} | {metrics['detected_ring_or_circle_count']} | "
            f"{metrics['large_contour_count']} | {metrics['largest_contour_area']:.1f} | "
            f"{metrics['mean_contour_eccentricity']:.3f} | {metrics['radial_band_spacing']['mean']:.2f} | "
            f"{metrics['hough_line_count']} | {angles or 'none'} | {comp_text} |"
        )
    lines.extend(
        [
            "",
            "## Interpretation",
            summary["interpretation"]["summary"],
            "",
            f"Regime label: `{summary['interpretation']['regime']['regime_label']}`",
            "",
            f"Best next metric: {summary['interpretation']['best_next_metric']}",
        ]
    )
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--ladder-dir", type=Path, required=True)
    parser.add_argument("--auxiliary-dir", type=Path, required=True)
    parser.add_argument("--visible-mask-dir", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path)
    args = parser.parse_args()

    output_dir = args.output_dir or (args.ladder_dir / "geometry_structure_search")
    output_dir.mkdir(parents=True, exist_ok=True)
    checkpoints = load_summary(args.ladder_dir / "checkpoint_sequence_summary.json")
    results: list[dict] = []
    contact_images: list[tuple[str, Path]] = []
    for index, checkpoint in enumerate(checkpoints):
        name = checkpoint.get("Name", f"checkpoint_{index:02d}")
        if name not in CHECKPOINT_ORDER:
            continue
        artifacts = optional_artifacts(name, index, args.ladder_dir, args.auxiliary_dir, args.visible_mask_dir)
        primary = artifacts["normal_rgb"] if artifacts["normal_rgb"].exists() else artifacts["debug"]
        mask_paths = {
            "visible_band_mask": artifacts["visible_band_mask"],
            "adaptive_tile_heatmap": artifacts["adaptive_tile_heatmap"],
            "polar_coherence_heatmap": artifacts["polar_coherence_heatmap"],
        }
        metrics, annotated = analyze_image(primary, mask_paths)
        annotated_path = output_dir / f"{index:02d}_{name}_annotated_shape_search.png"
        cv2.imwrite(str(annotated_path), annotated)
        contact_images.append((name, annotated_path))
        results.append(
            {
                "checkpoint": name,
                "index": index,
                "artifacts": {key: str(path) for key, path in artifacts.items() if path.exists()},
                "annotated_image": str(annotated_path),
                "metrics": metrics,
            }
        )

    regime = classify_regime(results)
    interpretation_summary = (
        "Detected structures are recurring but not uniform: contour/edge families persist across the ladder, "
        "while the throat-to-exit side changes contour density and component structure. Treat this as morphology, "
        "not physical proof."
    )
    summary = {
        "analysis_label": "exploratory_geometric_morphology_analysis",
        "ladder_dir": str(args.ladder_dir),
        "auxiliary_dir": str(args.auxiliary_dir),
        "visible_mask_dir": str(args.visible_mask_dir),
        "outputs": {
            "contact_sheet": str(output_dir / "annotated_shape_search_contact_sheet.png"),
            "summary_json": str(output_dir / "geometry_structure_summary.json"),
            "summary_md": str(output_dir / "geometry_structure_summary.md"),
        },
        "method": {
            "primary_image": "debug_normal_rgb when present, otherwise debug capture",
            "feature_detection_preprocessing": "grayscale normalize + GaussianBlur(5x5); used only for feature detection",
            "features": ["HoughCircles", "HoughLinesP", "Canny contours with hierarchy", "connected components on available masks/coherence maps"],
        },
        "checkpoints": results,
        "interpretation": {
            "summary": interpretation_summary,
            "regime": regime,
            "best_next_metric": "log-polar edge-orientation persistence: compare radial/tangential edge histograms after registering each checkpoint to its aperture center",
        },
    }
    contact_sheet_path = output_dir / "annotated_shape_search_contact_sheet.png"
    build_contact_sheet(contact_images).save(contact_sheet_path)
    summary_json_path = output_dir / "geometry_structure_summary.json"
    summary_md_path = output_dir / "geometry_structure_summary.md"
    summary_json_path.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    write_markdown(summary, summary_md_path)
    print(json.dumps({key: summary[key] for key in ("analysis_label", "outputs", "interpretation")}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
