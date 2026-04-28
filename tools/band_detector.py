#!/usr/bin/env python3
"""
band_detector.py — Detect banding artifacts in xPRIMEray renders and emit band_tile_signals.json.

Banding pixels are identified by looking for abrupt local luminance discontinuities that form
horizontal streaks. The output links detected band loci to tile grid cells so that
TileMetricsBandSeed can pre-seed TileMetricPersistentPrior entries before the next render pass.

Output schema (band_tile_signals.json):
{
  "image_path": str,
  "image_width": int,
  "image_height": int,
  "tile_width": int,
  "tile_height": int,
  "tile_grid_w": int,
  "tile_grid_h": int,
  "band_threshold": float,
  "total_band_pixels": int,
  "total_pixels": int,
  "global_band_fraction": float,
  "tiles": [
    {
      "tile_col": int,
      "tile_row": int,
      "tile_x": int,
      "tile_y": int,
      "tile_w": int,
      "tile_h": int,
      "total_pixels": int,
      "band_pixel_count": int,
      "band_pixel_fraction": float,
      "mean_band_score": float
    },
    ...
  ]
}
"""

import argparse
import json
import os
import sys

import numpy as np
from PIL import Image


def detect_band_pixels(img_array: np.ndarray, sensitivity: float = 1.5, min_score: float = 0.05) -> np.ndarray:
    """Return a float32 band-score array the same shape as the image (H, W).

    Band pixels are those with high vertical luminance gradient relative to
    their local horizontal neighbourhood. A high score means abrupt row-to-row
    change that is spatially localised (characteristic of rendering banding).
    Score is in [0, 1]; pixels below min_score are treated as non-band.
    """
    if img_array.ndim == 3:
        luma = 0.299 * img_array[:, :, 0] + 0.587 * img_array[:, :, 1] + 0.114 * img_array[:, :, 2]
    else:
        luma = img_array.astype(np.float32)

    luma = luma.astype(np.float32) / 255.0

    # Vertical gradient magnitude (finite difference, both directions, take max).
    vgrad_fwd = np.abs(np.diff(luma, axis=0, append=luma[-1:, :]))
    vgrad_bwd = np.abs(np.diff(luma, axis=0, prepend=luma[:1, :]))
    vgrad = np.maximum(vgrad_fwd, vgrad_bwd)

    # Local horizontal smoothing of the vertical gradient to suppress noise.
    # Use a simple box filter of width 5.
    from scipy.ndimage import uniform_filter1d
    smooth_h = uniform_filter1d(vgrad, size=5, axis=1)

    # Normalise per-row: score is how much higher the local vgrad is vs row median.
    row_median = np.median(smooth_h, axis=1, keepdims=True)
    row_median = np.maximum(row_median, 1e-4)
    score = (smooth_h / row_median - 1.0) / sensitivity
    score = np.clip(score, 0.0, 1.0).astype(np.float32)
    score[score < min_score] = 0.0
    return score


def compute_tile_signals(
    score: np.ndarray,
    tile_w: int,
    tile_h: int,
    band_threshold: float,
) -> list[dict]:
    """Aggregate per-pixel band scores into tile grid cells."""
    H, W = score.shape
    tile_grid_w = (W + tile_w - 1) // tile_w
    tile_grid_h = (H + tile_h - 1) // tile_h
    tiles = []
    for row in range(tile_grid_h):
        for col in range(tile_grid_w):
            x0 = col * tile_w
            y0 = row * tile_h
            x1 = min(x0 + tile_w, W)
            y1 = min(y0 + tile_h, H)
            patch = score[y0:y1, x0:x1]
            total_px = patch.size
            band_mask = patch > 0.0
            band_px = int(band_mask.sum())
            band_fraction = band_px / total_px if total_px > 0 else 0.0
            mean_score = float(patch[band_mask].mean()) if band_px > 0 else 0.0
            tiles.append({
                "tile_col": col,
                "tile_row": row,
                "tile_x": x0,
                "tile_y": y0,
                "tile_w": x1 - x0,
                "tile_h": y1 - y0,
                "total_pixels": total_px,
                "band_pixel_count": band_px,
                "band_pixel_fraction": round(band_fraction, 6),
                "mean_band_score": round(mean_score, 6),
            })
    return tiles


def main() -> None:
    ap = argparse.ArgumentParser(description="Detect banding artifacts and emit band_tile_signals.json.")
    ap.add_argument("image", help="Path to the rendered beauty image (PNG).")
    ap.add_argument("--output", default="", help="Output path for band_tile_signals.json. Defaults to <image_dir>/band_tile_signals.json.")
    ap.add_argument("--tile-width", type=int, default=64, help="Tile/subtile width in pixels (should match TileMetricsSubtileWidth, default 64).")
    ap.add_argument("--tile-height", type=int, default=64, help="Tile height in pixels (default 64).")
    ap.add_argument("--band-threshold", type=float, default=0.10, help="Band pixel fraction above which a tile is considered high-energy (default 0.10).")
    ap.add_argument("--sensitivity", type=float, default=1.5, help="Gradient sensitivity multiplier (default 1.5).")
    ap.add_argument("--min-score", type=float, default=0.05, help="Minimum per-pixel score to count as a band pixel (default 0.05).")
    args = ap.parse_args()

    img_path = os.path.abspath(args.image)
    if not os.path.isfile(img_path):
        print(f"[band_detector] ERROR: image not found: {img_path}", file=sys.stderr)
        sys.exit(1)

    output_path = args.output
    if not output_path:
        output_path = os.path.join(os.path.dirname(img_path), "band_tile_signals.json")
    output_path = os.path.abspath(output_path)

    tile_w = max(1, args.tile_width)
    tile_h = max(1, args.tile_height)

    print(f"[band_detector] loading image: {img_path}")
    img = Image.open(img_path).convert("RGB")
    img_array = np.array(img, dtype=np.float32)
    H, W = img_array.shape[:2]
    print(f"[band_detector] image size: {W}x{H}")

    print(f"[band_detector] detecting band pixels (sensitivity={args.sensitivity}, min_score={args.min_score})...")
    score = detect_band_pixels(img_array, sensitivity=args.sensitivity, min_score=args.min_score)

    total_px = H * W
    total_band_px = int((score > 0).sum())
    global_band_frac = total_band_px / total_px if total_px > 0 else 0.0
    print(f"[band_detector] band pixels: {total_band_px}/{total_px} ({global_band_frac:.3%})")

    tile_grid_w = (W + tile_w - 1) // tile_w
    tile_grid_h = (H + tile_h - 1) // tile_h
    print(f"[band_detector] tile grid: {tile_grid_w}x{tile_grid_h} ({tile_w}x{tile_h} px/tile)")

    tiles = compute_tile_signals(score, tile_w, tile_h, args.band_threshold)
    high_tiles = [t for t in tiles if t["band_pixel_fraction"] > args.band_threshold]
    print(f"[band_detector] tiles above threshold ({args.band_threshold:.0%}): {len(high_tiles)}/{len(tiles)}")

    result = {
        "image_path": img_path,
        "image_width": W,
        "image_height": H,
        "tile_width": tile_w,
        "tile_height": tile_h,
        "tile_grid_w": tile_grid_w,
        "tile_grid_h": tile_grid_h,
        "band_threshold": args.band_threshold,
        "total_band_pixels": total_band_px,
        "total_pixels": total_px,
        "global_band_fraction": round(global_band_frac, 6),
        "tiles": tiles,
    }

    os.makedirs(os.path.dirname(output_path) or ".", exist_ok=True)
    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(result, f, indent=2)
    print(f"[band_detector] wrote: {output_path}")


if __name__ == "__main__":
    main()
