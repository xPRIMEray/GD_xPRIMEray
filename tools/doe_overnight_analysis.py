#!/usr/bin/env python3
"""
doe_overnight_analysis.py - incremental summaries for overnight DOE runs.

The overnight runner writes one metadata.json per cell. This analyzer tolerates
missing cells and rewrites root-level CSV/JSON/Markdown summaries after every
cell so interrupted runs remain useful.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import os
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
    "subset",
    "step_length",
    "mode",
    "stride",
    "cell_dir",
    "effective_status",
    "beauty_hash",
    "hash_matches_off",
    "changed_pixels",
    "band_pixels",
    "band_percent",
    "changed_bbox",
    "basin_count",
    "stable_basin_count",
    "unstable_seam_count",
    "mean_local_coherence_score",
    "mean_transport_entropy",
    "max_manifold_fragmentation_count",
    "scheduler_resonance_seam_count",
    "minimum_precision_floor",
    "probe_sample_count",
    "probe_runtime_ms",
    "max_centers_used",
    "centers_skipped_due_to_budget",
    "rows_written",
    "notes",
]


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def find_beauty_png(folder: Path) -> Path | None:
    if not folder.is_dir():
        return None
    for path in sorted(folder.glob("*.png")):
        if not any(path.name.endswith(suffix) for suffix in MAP_SUFFIXES):
            return path
    return None


def load_rgb(path: Path) -> np.ndarray:
    return np.array(Image.open(path).convert("RGB"), dtype=np.uint8)


def changed_pixels_bbox(a: np.ndarray, b: np.ndarray) -> tuple[int, str]:
    if a.shape != b.shape:
        return -1, "shape_mismatch"
    diff = np.any(a.astype(np.int16) != b.astype(np.int16), axis=2)
    n = int(diff.sum())
    if n == 0:
        return 0, "none"
    ys, xs = np.where(diff)
    return n, f"({int(xs.min())}, {int(ys.min())}, {int(xs.max())}, {int(ys.max())})"


def detect_band_scores(img: np.ndarray, sensitivity: float, min_score: float) -> np.ndarray:
    if img.ndim == 3:
        luma = 0.299 * img[:, :, 0] + 0.587 * img[:, :, 1] + 0.114 * img[:, :, 2]
    else:
        luma = img.astype(np.float32)
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


def load_metadata(path: Path) -> dict[str, Any]:
    try:
        with path.open() as f:
            return json.load(f)
    except Exception as exc:
        return {"cell_dir": str(path.parent), "notes": f"metadata_read_error:{exc}"}


def attach_coherence_memory(row: dict[str, Any], cell_dir: Path) -> None:
    memory_path = cell_dir / "scene_transport_memory.json"
    summary_path = cell_dir / "transport_coherence_basin_summary.json"
    if not memory_path.exists():
        return
    try:
        data = json.loads(memory_path.read_text())
    except Exception as exc:
        row["notes"] = f"{row.get('notes', '')};coherence_memory_error:{exc}"
        return
    basins = data.get("stable_basins") or []
    seams = data.get("unstable_seams") or []
    summary = {}
    if summary_path.exists():
        try:
            summary = json.loads(summary_path.read_text())
        except Exception:
            summary = {}
    budget = summary.get("probe_budget") or {}
    row["basin_count"] = int(data.get("basin_count", len(basins)))
    row["stable_basin_count"] = len(basins)
    row["unstable_seam_count"] = int(data.get("unstable_seam_count", len(seams)))
    row["mean_local_coherence_score"] = round(float(np.mean([float(b.get("local_coherence_score", 0.0)) for b in basins])) if basins else 0.0, 6)
    row["mean_transport_entropy"] = round(float(np.mean([float(b.get("local_transport_entropy", 0.0)) for b in basins])) if basins else 0.0, 6)
    row["max_manifold_fragmentation_count"] = max([int(s.get("manifold_fragmentation_count", 0)) for s in seams] or [0])
    row["scheduler_resonance_seam_count"] = sum(1 for s in seams if str(s.get("seam_class")) == "SCHEDULER_RESONANCE_STRIPE")
    precision_values = [str(item.get("precision_floor", "")) for item in [*basins, *seams] if item.get("precision_floor")]
    rank = {"0.015": 0, "0.0125": 1, "0.00625": 2, "0.003125": 3, "reference": 3, "none": 4}
    row["minimum_precision_floor"] = max(precision_values, key=lambda v: rank.get(v, 5)) if precision_values else ""
    for key in ("probe_sample_count", "probe_runtime_ms", "max_centers_used", "centers_skipped_due_to_budget", "rows_written"):
        row[key] = budget.get(key, "")


def discover_rows(root: Path, sensitivity: float, min_score: float) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for meta_path in sorted(root.glob("subset_*/step_*/*/metadata.json")):
        meta = load_metadata(meta_path)
        cell_dir = Path(meta.get("cell_dir") or meta_path.parent)
        row = {col: "" for col in CSV_COLS}
        row.update({
            "timestamp": meta.get("timestamp", ""),
            "subset": meta.get("subset", ""),
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
            band = detect_band_scores(img, sensitivity, min_score) > 0.0
            row["band_pixels"] = int(band.sum())
            row["band_percent"] = round(100.0 * int(band.sum()) / (img.shape[0] * img.shape[1]), 3)
            row["_beauty_path"] = str(beauty)
        attach_coherence_memory(row, cell_dir)
        rows.append(row)
    return rows


def attach_off_comparisons(rows: list[dict[str, Any]]) -> None:
    baselines: dict[tuple[str, str], dict[str, Any]] = {}
    for row in rows:
        if str(row.get("mode", "")) == "off" and row.get("beauty_hash"):
            key = (str(row.get("step_length", "")), str(row.get("stride", "")))
            baselines.setdefault(key, row)

    for row in rows:
        if str(row.get("mode", "")) == "off":
            row["hash_matches_off"] = "true" if row.get("beauty_hash") else ""
            row["changed_pixels"] = 0 if row.get("beauty_hash") else ""
            row["changed_bbox"] = "none" if row.get("beauty_hash") else ""
            continue
        key = (str(row.get("step_length", "")), str(row.get("stride", "")))
        off = baselines.get(key)
        if not off or not row.get("beauty_hash"):
            continue
        row["hash_matches_off"] = "true" if row["beauty_hash"] == off.get("beauty_hash") else "false"
        a_path = off.get("_beauty_path")
        b_path = row.get("_beauty_path")
        if a_path and b_path:
            n, bbox = changed_pixels_bbox(load_rgb(Path(a_path)), load_rgb(Path(b_path)))
            row["changed_pixels"] = n
            row["changed_bbox"] = bbox


def fmt(value: Any) -> str:
    if value is None:
        return ""
    if isinstance(value, float):
        if math.isnan(value):
            return ""
        return f"{value:.4f}"
    return str(value)


def write_csv(path: Path, rows: list[dict[str, Any]]) -> None:
    with path.open("w", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=CSV_COLS, extrasaction="ignore")
        writer.writeheader()
        for row in rows:
            writer.writerow({key: fmt(row.get(key, "")) for key in CSV_COLS})


def write_json(path: Path, rows: list[dict[str, Any]], root: Path) -> None:
    clean_rows = [{key: row.get(key, "") for key in CSV_COLS} for row in rows]
    with path.open("w") as f:
        json.dump({"root": str(root), "rows": clean_rows}, f, indent=2)


def write_markdown(path: Path, rows: list[dict[str, Any]], root: Path) -> None:
    lines = [
        "# DOE Overnight Summary",
        "",
        f"**Experiment dir:** `{root}`",
        "",
        f"Completed/effective cells: {sum(str(r.get('effective_status')) == '0' for r in rows)} / {len(rows)}",
        "",
        "| timestamp | subset | step_length | mode | stride | status | band_pixels | band_% | hash_matches_off | changed_pixels | notes |",
        "| --------- | ------ | ----------- | ---- | ------ | ------ | ----------- | ------ | ---------------- | -------------- | ----- |",
    ]
    for row in rows:
        lines.append(
            "| "
            + " | ".join(
                fmt(row.get(key, ""))
                for key in (
                    "timestamp",
                    "subset",
                    "step_length",
                    "mode",
                    "stride",
                    "effective_status",
                    "band_pixels",
                    "band_percent",
                    "hash_matches_off",
                    "changed_pixels",
                    "notes",
                )
            )
            + " |"
        )
    with path.open("w") as f:
        f.write("\n".join(lines) + "\n")


def try_plot(path: Path, rows: list[dict[str, Any]]) -> None:
    try:
        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
    except Exception:
        return

    usable = [
        row for row in rows
        if row.get("mode") in {"off", "telemetry_on", "resolver_on", "sconv_on"}
        and row.get("band_percent") not in ("", None)
    ]
    if not usable:
        return

    labels = [f"{r['subset']} {r['step_length']} {r['mode']}" for r in usable]
    vals = [float(r["band_percent"]) for r in usable]
    fig_w = max(10, min(24, len(labels) * 0.45))
    plt.figure(figsize=(fig_w, 6))
    plt.bar(range(len(vals)), vals, color="steelblue")
    plt.xticks(range(len(vals)), labels, rotation=75, ha="right", fontsize=7)
    plt.ylabel("Band pixel %")
    plt.title("DOE Overnight Banding Summary")
    plt.tight_layout()
    plt.savefig(path, dpi=120)
    plt.close()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("root")
    parser.add_argument("--sensitivity", type=float, default=1.5)
    parser.add_argument("--min-score", type=float, default=0.05)
    args = parser.parse_args()

    root = Path(args.root).resolve()
    root.mkdir(parents=True, exist_ok=True)
    rows = discover_rows(root, args.sensitivity, args.min_score)
    attach_off_comparisons(rows)

    write_csv(root / "DOE_overnight_summary.csv", rows)
    write_json(root / "DOE_overnight_summary.json", rows, root)
    write_markdown(root / "DOE_overnight_summary.md", rows, root)
    try_plot(root / "DOE_overnight_band_plot.png", rows)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
