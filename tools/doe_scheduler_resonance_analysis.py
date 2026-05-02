#!/usr/bin/env python3
"""
Incremental analysis for scheduler/stride resonance DOE cells.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
from pathlib import Path
from typing import Any

import numpy as np
from PIL import Image

try:
    from scipy.ndimage import uniform_filter1d
except Exception:
    uniform_filter1d = None


MAP_SUFFIXES = (
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
    "phase",
    "step_length",
    "mode",
    "stride",
    "cell_dir",
    "effective_status",
    "beauty_hash",
    "band_pixels",
    "band_percent",
    "horizontal_band_score",
    "mean_band_run_length_by_row",
    "max_contiguous_horizontal_band_width",
    "row_band_coverage_percent",
    "band_pixels_by_y_mod_stride",
    "band_pixels_by_row_group",
    "corner_edge_fill_proxy",
    "notes",
]


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def find_beauty_png(folder: Path) -> Path | None:
    for path in sorted(folder.glob("*.png")):
        if not any(path.name.endswith(suffix) for suffix in MAP_SUFFIXES):
            return path
    return None


def load_rgb(path: Path) -> np.ndarray:
    return np.array(Image.open(path).convert("RGB"), dtype=np.uint8)


def detect_band_scores(img: np.ndarray, sensitivity: float, min_score: float) -> np.ndarray:
    luma = 0.299 * img[:, :, 0] + 0.587 * img[:, :, 1] + 0.114 * img[:, :, 2]
    luma = luma.astype(np.float32) / 255.0
    vgrad = np.maximum(
        np.abs(np.diff(luma, axis=0, append=luma[-1:, :])),
        np.abs(np.diff(luma, axis=0, prepend=luma[:1, :])),
    )
    if uniform_filter1d is not None:
        smooth = uniform_filter1d(vgrad, size=5, axis=1)
    else:
        pad = np.pad(vgrad, ((0, 0), (2, 2)), mode="edge")
        smooth = (
            pad[:, 0:-4] + pad[:, 1:-3] + pad[:, 2:-2] + pad[:, 3:-1] + pad[:, 4:]
        ) / 5.0
    row_med = np.maximum(np.median(smooth, axis=1, keepdims=True), 1e-4)
    score = np.clip((smooth / row_med - 1.0) / sensitivity, 0.0, 1.0).astype(np.float32)
    score[score < min_score] = 0.0
    return score


def run_lengths(mask_row: np.ndarray) -> list[int]:
    runs: list[int] = []
    current = 0
    for value in mask_row:
        if value:
            current += 1
        elif current:
            runs.append(current)
            current = 0
    if current:
        runs.append(current)
    return runs


def corner_edge_fill_proxy(img: np.ndarray) -> float:
    h, w = img.shape[:2]
    patch = max(4, min(24, h // 8, w // 8))
    corners = [
        img[:patch, :patch],
        img[:patch, w - patch:],
        img[h - patch:, :patch],
        img[h - patch:, w - patch:],
    ]
    vals = []
    for c in corners:
        luma = 0.299 * c[:, :, 0] + 0.587 * c[:, :, 1] + 0.114 * c[:, :, 2]
        vals.append(float(np.std(luma) / 255.0))
    return round(float(np.mean(vals)), 5)


def analyse_image(img: np.ndarray, stride: int, sensitivity: float, min_score: float) -> dict[str, Any]:
    h, w = img.shape[:2]
    band = detect_band_scores(img, sensitivity, min_score) > 0.0
    band_pixels = int(band.sum())
    row_counts = band.sum(axis=1).astype(np.float32)
    row_coverage = row_counts / max(1, w)
    active_rows = row_coverage > 0.0

    all_runs: list[int] = []
    max_run = 0
    for y in range(h):
        runs = run_lengths(band[y])
        if runs:
            all_runs.extend(runs)
            max_run = max(max_run, max(runs))

    horizontal_band_score = float(np.mean(row_coverage[active_rows])) if np.any(active_rows) else 0.0
    mean_run = float(np.mean(all_runs)) if all_runs else 0.0
    row_band_coverage_percent = 100.0 * float(np.mean(active_rows))

    safe_stride = max(1, int(stride))
    by_mod = [int(row_counts[y::safe_stride].sum()) for y in range(safe_stride)]
    group_count = 12
    by_group = []
    for gi in range(group_count):
        y0 = int(round(gi * h / group_count))
        y1 = int(round((gi + 1) * h / group_count))
        by_group.append(int(row_counts[y0:y1].sum()))

    return {
        "band_pixels": band_pixels,
        "band_percent": round(100.0 * band_pixels / max(1, h * w), 4),
        "horizontal_band_score": round(horizontal_band_score, 5),
        "mean_band_run_length_by_row": round(mean_run, 3),
        "max_contiguous_horizontal_band_width": int(max_run),
        "row_band_coverage_percent": round(row_band_coverage_percent, 4),
        "band_pixels_by_y_mod_stride": json.dumps(by_mod, separators=(",", ":")),
        "band_pixels_by_row_group": json.dumps(by_group, separators=(",", ":")),
        "corner_edge_fill_proxy": corner_edge_fill_proxy(img),
    }


def load_metadata(path: Path) -> dict[str, Any]:
    try:
        with path.open() as f:
            return json.load(f)
    except Exception as exc:
        return {"cell_dir": str(path.parent), "notes": f"metadata_error:{exc}"}


def discover_rows(root: Path, sensitivity: float, min_score: float) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for meta_path in sorted(root.glob("phase_*/step_*/*/metadata.json")):
        meta = load_metadata(meta_path)
        cell_dir = Path(meta.get("cell_dir") or meta_path.parent)
        row = {col: "" for col in CSV_COLS}
        row.update({
            "timestamp": meta.get("timestamp", ""),
            "phase": meta.get("phase", ""),
            "step_length": meta.get("step_length", ""),
            "mode": meta.get("mode", ""),
            "stride": meta.get("stride", ""),
            "cell_dir": str(cell_dir),
            "effective_status": meta.get("effective_status", ""),
            "notes": meta.get("notes", ""),
        })
        beauty = find_beauty_png(cell_dir)
        if beauty is not None:
            row["beauty_hash"] = sha256_file(beauty)
            img = load_rgb(beauty)
            row.update(analyse_image(img, int(meta.get("stride") or 1), sensitivity, min_score))
        rows.append(row)
    return rows


def fmt(value: Any) -> str:
    if value is None:
        return ""
    if isinstance(value, float):
        if math.isnan(value):
            return ""
        return f"{value:.5f}".rstrip("0").rstrip(".")
    return str(value)


def write_csv(path: Path, rows: list[dict[str, Any]]) -> None:
    with path.open("w", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=CSV_COLS, extrasaction="ignore")
        writer.writeheader()
        for row in rows:
            writer.writerow({key: fmt(row.get(key, "")) for key in CSV_COLS})


def write_json(path: Path, rows: list[dict[str, Any]], root: Path) -> None:
    with path.open("w") as f:
        json.dump({"root": str(root), "rows": [{k: r.get(k, "") for k in CSV_COLS} for r in rows]}, f, indent=2)


def write_markdown(path: Path, rows: list[dict[str, Any]], root: Path) -> None:
    lines = [
        "# Scheduler Resonance DOE Summary",
        "",
        f"**Experiment dir:** `{root}`",
        "",
        f"Completed/effective cells: {sum(str(r.get('effective_status')) == '0' for r in rows)} / {len(rows)}",
        "",
        "| timestamp | phase | step_length | mode | stride | status | band_% | horizontal_score | mean_run | max_width | row_cov_% | notes |",
        "| --------- | ----- | ----------- | ---- | ------ | ------ | ------ | ---------------- | -------- | --------- | --------- | ----- |",
    ]
    for row in rows:
        keys = (
            "timestamp",
            "phase",
            "step_length",
            "mode",
            "stride",
            "effective_status",
            "band_percent",
            "horizontal_band_score",
            "mean_band_run_length_by_row",
            "max_contiguous_horizontal_band_width",
            "row_band_coverage_percent",
            "notes",
        )
        lines.append("| " + " | ".join(fmt(row.get(k, "")) for k in keys) + " |")
    with path.open("w") as f:
        f.write("\n".join(lines) + "\n")


def try_plots(root: Path, rows: list[dict[str, Any]]) -> None:
    try:
        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
    except Exception:
        return

    usable = [r for r in rows if r.get("band_percent") not in ("", None)]
    if not usable:
        return

    labels = [f"{r['step_length']} s{r['stride']} {r['mode']}" for r in usable]
    x = np.arange(len(usable))

    plt.figure(figsize=(max(10, len(usable) * 0.45), 6))
    plt.bar(x, [float(r["band_percent"]) for r in usable], color="steelblue")
    plt.xticks(x, labels, rotation=75, ha="right", fontsize=7)
    plt.ylabel("Band pixel %")
    plt.title("Scheduler Resonance DOE - Band %")
    plt.tight_layout()
    plt.savefig(root / "scheduler_stride_plot.png", dpi=120)
    plt.close()

    plt.figure(figsize=(max(10, len(usable) * 0.45), 6))
    plt.bar(x, [float(r["horizontal_band_score"]) for r in usable], color="tomato")
    plt.xticks(x, labels, rotation=75, ha="right", fontsize=7)
    plt.ylabel("Horizontal band score")
    plt.title("Horizontal Band Score")
    plt.tight_layout()
    plt.savefig(root / "horizontal_band_score_plot.png", dpi=120)
    plt.close()

    max_stride = max(int(r.get("stride") or 1) for r in usable)
    heat = np.full((len(usable), max_stride), np.nan, dtype=np.float32)
    for i, row in enumerate(usable):
        vals = json.loads(row.get("band_pixels_by_y_mod_stride") or "[]")
        total = max(1, sum(vals))
        for j, v in enumerate(vals):
            heat[i, j] = v / total
    plt.figure(figsize=(max(8, max_stride), max(5, len(usable) * 0.25)))
    plt.imshow(heat, aspect="auto", interpolation="nearest", cmap="magma")
    plt.colorbar(label="Fraction of band pixels")
    plt.yticks(np.arange(len(usable)), labels, fontsize=6)
    plt.xticks(np.arange(max_stride), [str(i) for i in range(max_stride)])
    plt.xlabel("y mod stride")
    plt.title("Band Pixels by Row Mod Stride")
    plt.tight_layout()
    plt.savefig(root / "band_by_row_mod_stride_heatmap.png", dpi=120)
    plt.close()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("root")
    parser.add_argument("--sensitivity", type=float, default=1.5)
    parser.add_argument("--min-score", type=float, default=0.05)
    args = parser.parse_args()

    root = Path(args.root).resolve()
    rows = discover_rows(root, args.sensitivity, args.min_score)
    write_csv(root / "scheduler_DOE_summary.csv", rows)
    write_json(root / "scheduler_DOE_summary.json", rows, root)
    write_markdown(root / "scheduler_DOE_summary.md", rows, root)
    try_plots(root, rows)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
