#!/usr/bin/env python3
"""Analyze deterministic pass1/pass2 traversal comparison cells."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
from collections import deque
from pathlib import Path
from typing import Any

import numpy as np
from PIL import Image, ImageChops, ImageDraw


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
    "beauty_png_count",
    "beauty_exact_once",
    "beauty_expected_pixels",
    "beauty_pixels_written_once",
    "beauty_pixels_unwritten",
    "beauty_pixels_multi_written",
    "traversal_expected_pixels",
    "traversal_pixels_once",
    "traversal_pixels_unwritten",
    "traversal_pixels_multi_written",
    "traversal_exact_once",
    "changed_pixels_vs_row",
    "band_pixels",
    "band_percent",
    "horizontal_band_score",
    "vertical_band_score",
    "max_horizontal_run_length",
    "local_artifact_cluster_count",
    "max_tile_local_run_length",
    "corner_roi_required_precision",
    "corner_roi_ownership_change_samples",
    "corner_roi_collider_flip_samples",
    "corner_roi_mean_max_risk",
    "notes",
]


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def find_beauty_pngs(folder: Path) -> list[Path]:
    result: list[Path] = []
    for path in sorted(folder.glob("*.png")):
        if path.name.startswith("corner_"):
            continue
        if path.name.endswith(DIAGNOSTIC_SUFFIXES):
            continue
        result.append(path)
    return result


def load_rgb(path: Path) -> np.ndarray:
    return np.array(Image.open(path).convert("RGB"), dtype=np.uint8)


def band_mask(img: np.ndarray, axis: int, sensitivity: float, min_score: float) -> np.ndarray:
    luma = (0.299 * img[:, :, 0] + 0.587 * img[:, :, 1] + 0.114 * img[:, :, 2]).astype(np.float32) / 255.0
    if axis == 0:
        grad = np.maximum(
            np.abs(np.diff(luma, axis=0, append=luma[-1:, :])),
            np.abs(np.diff(luma, axis=0, prepend=luma[:1, :])),
        )
        med = np.maximum(np.median(grad, axis=1, keepdims=True), 1e-4)
    else:
        grad = np.maximum(
            np.abs(np.diff(luma, axis=1, append=luma[:, -1:])),
            np.abs(np.diff(luma, axis=1, prepend=luma[:, :1])),
        )
        med = np.maximum(np.median(grad, axis=0, keepdims=True), 1e-4)
    score = np.clip((grad / med - 1.0) / sensitivity, 0.0, 1.0)
    return score >= min_score


def max_run(mask: np.ndarray, axis: int) -> int:
    best = 0
    lines = mask if axis == 1 else mask.T
    for line in lines:
        cur = 0
        for value in line:
            if value:
                cur += 1
                best = max(best, cur)
            else:
                cur = 0
    return int(best)


def connected_components(mask: np.ndarray) -> int:
    h, w = mask.shape
    seen = np.zeros_like(mask, dtype=bool)
    count = 0
    for y in range(h):
        for x in range(w):
            if not mask[y, x] or seen[y, x]:
                continue
            count += 1
            q: deque[tuple[int, int]] = deque([(x, y)])
            seen[y, x] = True
            while q:
                cx, cy = q.popleft()
                for nx, ny in ((cx - 1, cy), (cx + 1, cy), (cx, cy - 1), (cx, cy + 1)):
                    if 0 <= nx < w and 0 <= ny < h and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        q.append((nx, ny))
    return count


def max_tile_local_run(mask: np.ndarray, tile_size: int = 16) -> int:
    h, w = mask.shape
    best = 0
    for y0 in range(0, h, tile_size):
        for x0 in range(0, w, tile_size):
            tile = mask[y0:min(h, y0 + tile_size), x0:min(w, x0 + tile_size)]
            best = max(best, max_run(tile, axis=1))
    return int(best)


def analyse_image(img: np.ndarray, sensitivity: float, min_score: float) -> dict[str, Any]:
    h, w = img.shape[:2]
    hmask = band_mask(img, axis=0, sensitivity=sensitivity, min_score=min_score)
    vmask = band_mask(img, axis=1, sensitivity=sensitivity, min_score=min_score)
    row_cov = hmask.sum(axis=1) / max(1, w)
    col_cov = vmask.sum(axis=0) / max(1, h)
    active_rows = row_cov > 0
    active_cols = col_cov > 0
    band_pixels = int(hmask.sum())
    return {
        "band_pixels": band_pixels,
        "band_percent": round(100.0 * band_pixels / max(1, h * w), 4),
        "horizontal_band_score": round(float(np.mean(row_cov[active_rows])) if np.any(active_rows) else 0.0, 6),
        "vertical_band_score": round(float(np.mean(col_cov[active_cols])) if np.any(active_cols) else 0.0, 6),
        "max_horizontal_run_length": max_run(hmask, axis=1),
        "local_artifact_cluster_count": connected_components(hmask),
        "max_tile_local_run_length": max_tile_local_run(hmask),
        "_band_mask": hmask,
    }


def load_metadata(path: Path) -> dict[str, Any]:
    try:
        return json.loads(path.read_text())
    except Exception as exc:
        return {"cell_dir": str(path.parent), "notes": f"metadata_error:{exc}"}


def parse_beauty_audit(cell_dir: Path) -> dict[str, Any]:
    log_path = cell_dir / "run.log"
    if not log_path.exists():
        return {}
    text = log_path.read_text(errors="replace")
    matches = re.findall(
        r"beautyExpected=(\d+) beautyOnce=(\d+) beautyUnwritten=(\d+) beautyMulti=(\d+) beautyExactOnce=(\d+)",
        text,
    )
    if not matches:
        return {}
    expected, once, unwritten, multi, exact = matches[-1]
    return {
        "beauty_expected_pixels": int(expected),
        "beauty_pixels_written_once": int(once),
        "beauty_pixels_unwritten": int(unwritten),
        "beauty_pixels_multi_written": int(multi),
        "beauty_exact_once": int(exact),
    }


def traversal_coverage(width: int, height: int, stride: int, traversal: str, tile_size: int = 16) -> dict[str, Any]:
    """Audit the deterministic full-frame traversal schedule, independent of render budget."""
    safe_stride = max(1, int(stride or 1))
    mode = (traversal or "row").strip().lower()
    if mode in ("row_baseline", "baseline"):
        mode = "row"
    elif mode in ("column_major", "column-major"):
        mode = "column"
    elif mode in ("square_tile", "square-tile"):
        mode = "tile"
    elif mode in ("checkerboard_tile", "checkerboard-tile"):
        mode = "checkerboard"
    counts = np.zeros((height, width), dtype=np.uint16)

    def fill_origin(x: int, y: int) -> None:
        y1 = min(height, y + safe_stride)
        x1 = min(width, x + safe_stride)
        if 0 <= x < width and 0 <= y < height:
            counts[y:y1, x:x1] += 1

    if mode == "column":
        for x in range(0, width, safe_stride):
            for y in range(0, height, safe_stride):
                fill_origin(x, y)
    elif mode in ("tile", "checkerboard"):
        tile_cols = max(1, (width + tile_size - 1) // tile_size)
        tile_rows = max(1, (height + tile_size - 1) // tile_size)
        phases = (0, 1) if mode == "checkerboard" else (0,)
        for phase in phases:
            for tile_y in range(tile_rows):
                for tile_x in range(tile_cols):
                    if mode == "checkerboard" and ((tile_x + tile_y) & 1) != phase:
                        continue
                    x0 = tile_x * tile_size
                    y0 = tile_y * tile_size
                    for y in range(y0, min(height, y0 + tile_size), safe_stride):
                        for x in range(x0, min(width, x0 + tile_size), safe_stride):
                            fill_origin(x, y)
    else:
        for y in range(0, height, safe_stride):
            for x in range(0, width, safe_stride):
                fill_origin(x, y)

    expected = width * height
    once = int((counts == 1).sum())
    unwritten = int((counts == 0).sum())
    multi = int((counts > 1).sum())
    return {
        "traversal_expected_pixels": expected,
        "traversal_pixels_once": once,
        "traversal_pixels_unwritten": unwritten,
        "traversal_pixels_multi_written": multi,
        "traversal_exact_once": int(expected > 0 and once == expected and unwritten == 0 and multi == 0),
    }


def corner_summary(root: Path, step: str, traversal: str) -> dict[str, Any]:
    path = root / "corner_probe_after_beauty" / f"step_{step}" / traversal / "corner_transport_probe.json"
    if not path.exists():
        return {}
    try:
        data = json.loads(path.read_text())
    except Exception:
        return {}
    rois = data.get("roi_summary") or data.get("roi_summaries") or []
    if not rois:
        return {}
    return {
        "corner_roi_required_precision": max(float(r.get("required_precision", 0) or 0) for r in rois),
        "corner_roi_ownership_change_samples": sum(int(r.get("ownership_change_samples", 0) or 0) for r in rois),
        "corner_roi_collider_flip_samples": sum(int(r.get("collider_flip_samples", 0) or 0) for r in rois),
        "corner_roi_mean_max_risk": round(float(np.mean([float(r.get("mean_max_risk", 0) or 0) for r in rois])), 6),
    }


def discover(root: Path, sensitivity: float, min_score: float) -> tuple[list[dict[str, Any]], dict[tuple[str, str], Path]]:
    rows: list[dict[str, Any]] = []
    images: dict[tuple[str, str], Path] = {}
    row_baselines: dict[str, np.ndarray] = {}
    pending: list[tuple[dict[str, Any], np.ndarray | None]] = []
    for meta_path in sorted(root.glob("beauty/step_*/*/metadata.json")):
        meta = load_metadata(meta_path)
        cell_dir = Path(meta.get("cell_dir") or meta_path.parent)
        step = str(meta.get("step_length", ""))
        traversal = str(meta.get("traversal", ""))
        row = {col: "" for col in CSV_COLS}
        row.update({
            "timestamp": meta.get("timestamp", ""),
            "step_length": step,
            "traversal": traversal,
            "cell_dir": str(cell_dir),
            "effective_status": meta.get("effective_status", ""),
            "notes": meta.get("notes", ""),
        })
        beauty_pngs = find_beauty_pngs(cell_dir)
        row["beauty_png_count"] = len(beauty_pngs)
        img = None
        if beauty_pngs:
            path = beauty_pngs[0]
            img = load_rgb(path)
            h, w = img.shape[:2]
            images[(step, traversal)] = path
            row["beauty_hash"] = sha256_file(path)
            row.update(traversal_coverage(w, h, int(meta.get("stride", 1) or 1), traversal))
            metrics = analyse_image(img, sensitivity, min_score)
            metrics.pop("_band_mask", None)
            row.update(metrics)
            if traversal == "row":
                row_baselines[step] = img
        row.update(parse_beauty_audit(cell_dir))
        row.update(corner_summary(root, step, traversal))
        pending.append((row, img))
    for row, img in pending:
        baseline = row_baselines.get(row["step_length"])
        if img is not None and baseline is not None and baseline.shape == img.shape:
            row["changed_pixels_vs_row"] = int(np.any(img != baseline, axis=2).sum())
        rows.append(row)
    return rows, images


def write_csv(path: Path, rows: list[dict[str, Any]]) -> None:
    with path.open("w", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=CSV_COLS)
        writer.writeheader()
        for row in rows:
            writer.writerow({col: row.get(col, "") for col in CSV_COLS})


def write_md(path: Path, rows: list[dict[str, Any]]) -> None:
    lines = [
        "# Tile Commit Traversal Summary",
        "",
        "The traversal flag controls render-test pass1 acquisition and pass2 beauty commit/write order. Beauty cells keep diagnostics disabled; corner ROI probes are aggregated from separate post-beauty cells when present.",
        "",
        "| step | mode | status | hash | traversal_once | runtime_once | band_% | h_score | v_score | changed_vs_row | max_h_run | clusters | max_tile_run | corner_precision | corner_ownership |",
        "|---:|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|",
    ]
    for row in rows:
        lines.append(
            f"| {row.get('step_length','')} | `{row.get('traversal','')}` | {row.get('effective_status','')} | "
            f"`{str(row.get('beauty_hash',''))[:12]}` | {row.get('traversal_exact_once','')} | {row.get('beauty_exact_once','')} | {row.get('band_percent','')} | "
            f"{row.get('horizontal_band_score','')} | {row.get('vertical_band_score','')} | {row.get('changed_pixels_vs_row','')} | "
            f"{row.get('max_horizontal_run_length','')} | {row.get('local_artifact_cluster_count','')} | "
            f"{row.get('max_tile_local_run_length','')} | {row.get('corner_roi_required_precision','')} | "
            f"{row.get('corner_roi_ownership_change_samples','')} |"
        )
    path.write_text("\n".join(lines) + "\n")


def make_contact(root: Path, rows: list[dict[str, Any]], images: dict[tuple[str, str], Path]) -> None:
    steps = sorted({r["step_length"] for r in rows}, key=float)
    modes = [m for m in ("row", "column", "tile", "checkerboard") if any(r["traversal"] == m for r in rows)]
    if not steps or not modes:
        return
    tw, th, lh = 240, 135, 28
    sheet = Image.new("RGB", (tw * len(modes), (th + lh) * len(steps)), "white")
    draw = ImageDraw.Draw(sheet)
    for yi, step in enumerate(steps):
        for xi, mode in enumerate(modes):
            x0, y0 = xi * tw, yi * (th + lh)
            draw.text((x0 + 6, y0 + 6), f"{step} {mode}", fill=(0, 0, 0))
            path = images.get((step, mode))
            if path:
                thumb = Image.open(path).convert("RGB")
                thumb.thumbnail((tw, th), Image.Resampling.LANCZOS)
                sheet.paste(thumb, (x0, y0 + lh))
    sheet.save(root / "traversal_contact_sheet.png")


def make_diff(root: Path, images: dict[tuple[str, str], Path], mode: str, out_name: str) -> None:
    steps = sorted({s for s, m in images if m == "row"} & {s for s, m in images if m == mode}, key=float)
    if not steps:
        return
    step = steps[0]
    diff = ImageChops.difference(Image.open(images[(step, "row")]).convert("RGB"), Image.open(images[(step, mode)]).convert("RGB"))
    diff.save(root / out_name)


def make_band_plot(root: Path, rows: list[dict[str, Any]]) -> None:
    usable = [r for r in rows if str(r.get("band_percent", "")) != ""]
    if not usable:
        return
    w, h = max(720, len(usable) * 70), 420
    img = Image.new("RGB", (w, h), "white")
    draw = ImageDraw.Draw(img)
    ml, mt, mb = 60, 36, 80
    ph = h - mt - mb
    vals = [float(r["band_percent"]) for r in usable]
    vmax = max(vals) or 1.0
    step_w = max(28, (w - ml - 20) // len(usable))
    for idx, row in enumerate(usable):
        val = float(row["band_percent"])
        x = ml + idx * step_w
        bh = int((val / vmax) * ph)
        draw.rectangle((x, mt + ph - bh, x + 20, mt + ph), fill=(50, 120, 200))
        draw.text((x - 8, mt + ph + 8), row["traversal"][:4], fill=(0, 0, 0))
        draw.text((x - 10, mt + ph + 25), row["step_length"], fill=(0, 0, 0))
    draw.text((ml, 10), "Band support by traversal mode", fill=(0, 0, 0))
    img.save(root / "band_support_by_mode.png")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("root", type=Path)
    parser.add_argument("--sensitivity", type=float, default=1.5)
    parser.add_argument("--min-score", type=float, default=0.4)
    args = parser.parse_args()
    rows, images = discover(args.root, args.sensitivity, args.min_score)
    write_csv(args.root / "tile_commit_traversal_summary.csv", rows)
    (args.root / "tile_commit_traversal_summary.json").write_text(json.dumps(rows, indent=2, sort_keys=True) + "\n")
    write_md(args.root / "tile_commit_traversal_summary.md", rows)
    make_contact(args.root, rows, images)
    make_diff(args.root, images, "tile", "row_vs_tile_diff.png")
    make_diff(args.root, images, "checkerboard", "row_vs_checkerboard_diff.png")
    make_band_plot(args.root, rows)
    print(f"[tile-commit-analysis] rows={len(rows)} out={args.root}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
