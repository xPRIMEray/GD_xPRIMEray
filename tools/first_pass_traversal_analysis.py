#!/usr/bin/env python3
"""Analyze first-pass traversal comparison cells."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
from pathlib import Path
from typing import Any

import numpy as np
from PIL import Image, ImageChops, ImageDraw

try:
    from scipy.ndimage import uniform_filter1d
except Exception:
    uniform_filter1d = None


DIAGNOSTIC_SUFFIXES = (
    ".boundary_confidence.png",
    ".domain_confidence.png",
    ".domain_id.png",
    ".normal_discontinuity.png",
    ".selection_flip.png",
    ".step_convergence_confidence.png",
    ".step_sensitivity.png",
    ".precision_required.png",
    ".probe_hit_distance_delta.png",
    ".probe_normal_delta.png",
    ".probe_collider_mismatch.png",
)

CSV_COLS = [
    "timestamp",
    "step_length",
    "traversal",
    "cell_dir",
    "effective_status",
    "beauty_hash",
    "band_pixels",
    "band_percent",
    "horizontal_band_score",
    "vertical_band_score",
    "changed_pixels_vs_row_baseline",
    "corner_roi_required_precision",
    "corner_roi_collider_flip_samples",
    "corner_roi_ownership_change_samples",
    "corner_roi_mean_max_risk",
    "notes",
]


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def load_rgb(path: Path) -> np.ndarray:
    return np.array(Image.open(path).convert("RGB"), dtype=np.uint8)


def find_beauty_png(folder: Path) -> Path | None:
    candidates = []
    for path in sorted(folder.glob("*.png")):
        if path.name.startswith("corner_"):
            continue
        if path.name.endswith(DIAGNOSTIC_SUFFIXES):
            continue
        candidates.append(path)
    return candidates[0] if candidates else None


def detect_band_scores(img: np.ndarray, axis: int, sensitivity: float, min_score: float) -> np.ndarray:
    luma = 0.299 * img[:, :, 0] + 0.587 * img[:, :, 1] + 0.114 * img[:, :, 2]
    luma = luma.astype(np.float32) / 255.0
    if axis == 0:
        grad = np.maximum(
            np.abs(np.diff(luma, axis=0, append=luma[-1:, :])),
            np.abs(np.diff(luma, axis=0, prepend=luma[:1, :])),
        )
        smooth_axis = 1
        reduce_axis = 1
    else:
        grad = np.maximum(
            np.abs(np.diff(luma, axis=1, append=luma[:, -1:])),
            np.abs(np.diff(luma, axis=1, prepend=luma[:, :1])),
        )
        smooth_axis = 0
        reduce_axis = 0
    if uniform_filter1d is not None:
        smooth = uniform_filter1d(grad, size=5, axis=smooth_axis)
    else:
        smooth = grad
    med = np.maximum(np.median(smooth, axis=reduce_axis, keepdims=True), 1e-4)
    score = np.clip((smooth / med - 1.0) / sensitivity, 0.0, 1.0).astype(np.float32)
    score[score < min_score] = 0.0
    return score


def band_metrics(img: np.ndarray, sensitivity: float, min_score: float) -> dict[str, Any]:
    h, w = img.shape[:2]
    hmask = detect_band_scores(img, axis=0, sensitivity=sensitivity, min_score=min_score) > 0.0
    vmask = detect_band_scores(img, axis=1, sensitivity=sensitivity, min_score=min_score) > 0.0
    row_coverage = hmask.sum(axis=1) / max(1, w)
    col_coverage = vmask.sum(axis=0) / max(1, h)
    active_rows = row_coverage > 0.0
    active_cols = col_coverage > 0.0
    band_pixels = int(hmask.sum())
    return {
        "band_pixels": band_pixels,
        "band_percent": round(100.0 * band_pixels / max(1, h * w), 4),
        "horizontal_band_score": round(float(np.mean(row_coverage[active_rows])) if np.any(active_rows) else 0.0, 6),
        "vertical_band_score": round(float(np.mean(col_coverage[active_cols])) if np.any(active_cols) else 0.0, 6),
    }


def load_metadata(path: Path) -> dict[str, Any]:
    try:
        return json.loads(path.read_text())
    except Exception as exc:
        return {"cell_dir": str(path.parent), "notes": f"metadata_error:{exc}"}


def corner_summary(cell_dir: Path) -> dict[str, Any]:
    path = cell_dir / "corner_transport_probe.json"
    if not path.exists():
        return {}
    try:
        data = json.loads(path.read_text())
    except Exception:
        return {}
    rois = data.get("roi_summary") or data.get("roi_summaries") or []
    if not rois:
        return {}
    precision_values = [float(r.get("required_precision", 0) or 0) for r in rois]
    return {
        "corner_roi_required_precision": max(precision_values) if precision_values else "",
        "corner_roi_collider_flip_samples": sum(int(r.get("collider_flip_samples", 0) or 0) for r in rois),
        "corner_roi_ownership_change_samples": sum(int(r.get("ownership_change_samples", 0) or 0) for r in rois),
        "corner_roi_mean_max_risk": round(float(np.mean([float(r.get("mean_max_risk", 0) or 0) for r in rois])), 6),
    }


def discover_rows(root: Path, sensitivity: float, min_score: float) -> tuple[list[dict[str, Any]], dict[tuple[str, str], Path]]:
    rows: list[dict[str, Any]] = []
    images: dict[tuple[str, str], Path] = {}
    row_images: dict[str, np.ndarray] = {}
    pending: list[tuple[dict[str, Any], np.ndarray | None]] = []
    for meta_path in sorted(root.glob("step_*/*/metadata.json")):
        meta = load_metadata(meta_path)
        cell_dir = Path(meta.get("cell_dir") or meta_path.parent)
        row = {col: "" for col in CSV_COLS}
        row.update({
            "timestamp": meta.get("timestamp", ""),
            "step_length": str(meta.get("step_length", "")),
            "traversal": str(meta.get("traversal", "")),
            "cell_dir": str(cell_dir),
            "effective_status": meta.get("effective_status", ""),
            "notes": meta.get("notes", ""),
        })
        beauty = find_beauty_png(cell_dir)
        img = None
        if beauty is not None:
            img = load_rgb(beauty)
            images[(row["step_length"], row["traversal"])] = beauty
            row["beauty_hash"] = sha256_file(beauty)
            row.update(band_metrics(img, sensitivity, min_score))
            if row["traversal"] == "row":
                row_images[row["step_length"]] = img
        row.update(corner_summary(cell_dir))
        pending.append((row, img))
    for row, img in pending:
        baseline = row_images.get(row["step_length"])
        if img is not None and baseline is not None and img.shape == baseline.shape:
            row["changed_pixels_vs_row_baseline"] = int(np.any(img != baseline, axis=2).sum())
        rows.append(row)
    return rows, images


def write_csv(path: Path, rows: list[dict[str, Any]]) -> None:
    with path.open("w", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=CSV_COLS)
        writer.writeheader()
        for row in rows:
            writer.writerow({k: row.get(k, "") for k in CSV_COLS})


def fmt(value: Any) -> str:
    if value is None:
        return ""
    if isinstance(value, float):
        if math.isnan(value):
            return ""
        return f"{value:.6f}".rstrip("0").rstrip(".")
    return str(value)


def write_markdown(path: Path, rows: list[dict[str, Any]]) -> None:
    lines = [
        "# First-Pass Traversal Comparison",
        "",
        "This compares pass1 traversal order only. Hit math, shading, resolver scoring, beauty post-processing, and scheduler decisions are unchanged.",
        "",
        "| step | traversal | status | band_% | horizontal | vertical | changed_vs_row | corner_required_precision | corner_ownership_changes |",
        "|---:|---|---:|---:|---:|---:|---:|---:|---:|",
    ]
    for row in rows:
        lines.append(
            f"| {row.get('step_length', '')} | `{row.get('traversal', '')}` | {row.get('effective_status', '')} | "
            f"{fmt(row.get('band_percent', ''))} | {fmt(row.get('horizontal_band_score', ''))} | "
            f"{fmt(row.get('vertical_band_score', ''))} | {row.get('changed_pixels_vs_row_baseline', '')} | "
            f"{fmt(row.get('corner_roi_required_precision', ''))} | {row.get('corner_roi_ownership_change_samples', '')} |"
        )
    lines.extend([
        "",
        "## Interpretation Hooks",
        "",
        "- Column traversal rotating or suppressing horizontal bands supports row traversal as an amplifier.",
        "- Tile traversal localizing artifacts supports moving toward tile/domain scheduling.",
        "- Shared corner instability across traversal modes points back to hit/geodesic precision.",
    ])
    path.write_text("\n".join(lines) + "\n")


def make_contact_sheet(root: Path, rows: list[dict[str, Any]], images: dict[tuple[str, str], Path]) -> None:
    steps = sorted({r["step_length"] for r in rows}, key=lambda s: float(s))
    traversals = [t for t in ("row", "column", "tile", "checkerboard") if any(r["traversal"] == t for r in rows)]
    if not steps or not traversals:
        return
    thumb_w, thumb_h = 240, 135
    label_h = 28
    img = Image.new("RGB", (thumb_w * len(traversals), (thumb_h + label_h) * len(steps)), "white")
    draw = ImageDraw.Draw(img)
    for yi, step in enumerate(steps):
        for xi, traversal in enumerate(traversals):
            path = images.get((step, traversal))
            x0 = xi * thumb_w
            y0 = yi * (thumb_h + label_h)
            draw.text((x0 + 6, y0 + 6), f"{step} {traversal}", fill=(0, 0, 0))
            if path is None:
                continue
            thumb = Image.open(path).convert("RGB")
            thumb.thumbnail((thumb_w, thumb_h), Image.Resampling.LANCZOS)
            img.paste(thumb, (x0, y0 + label_h))
    img.save(root / "traversal_mode_contact_sheet.png")


def make_row_column_diff(root: Path, rows: list[dict[str, Any]], images: dict[tuple[str, str], Path]) -> None:
    common_steps = sorted({r["step_length"] for r in rows if r["traversal"] == "row"} & {r["step_length"] for r in rows if r["traversal"] == "column"}, key=lambda s: float(s))
    if not common_steps:
        return
    step = common_steps[0]
    row_path = images.get((step, "row"))
    col_path = images.get((step, "column"))
    if row_path is None or col_path is None:
        return
    diff = ImageChops.difference(Image.open(row_path).convert("RGB"), Image.open(col_path).convert("RGB"))
    diff.save(root / "row_vs_column_diff.png")


def make_corner_plot(root: Path, rows: list[dict[str, Any]]) -> None:
    usable = [r for r in rows if str(r.get("corner_roi_required_precision", "")) not in {"", "nan"}]
    if not usable:
        return
    width, height = max(720, len(usable) * 72), 420
    img = Image.new("RGB", (width, height), "white")
    draw = ImageDraw.Draw(img)
    ml, mt, mb = 60, 40, 70
    ph = height - mt - mb
    steps = [float(r["corner_roi_required_precision"]) for r in usable]
    max_step = max(steps) if steps else 1.0
    for idx, row in enumerate(usable):
        x0 = ml + idx * max(28, (width - ml - 20) // max(1, len(usable)))
        val = float(row["corner_roi_required_precision"])
        bar_h = int((val / max_step) * ph)
        draw.rectangle((x0, mt + ph - bar_h, x0 + 18, mt + ph), fill=(70, 120, 210))
        draw.text((x0 - 8, mt + ph + 8), row["traversal"][:4], fill=(0, 0, 0))
        draw.text((x0 - 8, mt + ph + 24), row["step_length"], fill=(0, 0, 0))
    draw.text((ml, 12), "Corner ROI required precision by traversal", fill=(0, 0, 0))
    img.save(root / "corner_roi_convergence_by_traversal.png")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("root", type=Path)
    parser.add_argument("--sensitivity", type=float, default=1.5)
    parser.add_argument("--min-score", type=float, default=0.4)
    args = parser.parse_args()
    rows, images = discover_rows(args.root, args.sensitivity, args.min_score)
    write_csv(args.root / "traversal_comparison_summary.csv", rows)
    (args.root / "traversal_comparison_summary.json").write_text(json.dumps(rows, indent=2, sort_keys=True) + "\n")
    write_markdown(args.root / "traversal_comparison_summary.md", rows)
    make_contact_sheet(args.root, rows, images)
    make_row_column_diff(args.root, rows, images)
    make_corner_plot(args.root, rows)
    print(f"[traversal-analysis] rows={len(rows)} out={args.root}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
