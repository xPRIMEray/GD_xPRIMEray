#!/usr/bin/env python3
"""
run_band_comparison.py — Compare band_summary.json outputs across fixture variants.

Usage:
    python tools/run_band_comparison.py \\
        --baseline  output/fixture_runs/curved_minimal/<run_A>/band_tile_signals.json \\
        --treatment output/fixture_runs/tile_priors_active/<run_B>/band_tile_signals.json \\
        [--output comparison.json]

Computes per-variant band statistics and emits a comparison JSON:
{
  "baseline": {
    "path": str,
    "global_band_fraction": float,
    "high_energy_tiles": int,
    "mean_band_fraction": float
  },
  "treatment": { ... },
  "delta_global_band_fraction": float,   // treatment - baseline (negative = improvement)
  "relative_reduction_pct": float,       // (baseline - treatment) / baseline * 100
  "high_energy_tile_delta": int,
  "verdict": "improvement" | "regression" | "neutral"
}
"""

import argparse
import json
import os
import sys


def load_band_signals(path: str) -> dict:
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def summarise(data: dict, band_threshold: float = 0.10) -> dict:
    tiles = data.get("tiles", [])
    fractions = [t["band_pixel_fraction"] for t in tiles]
    high_energy = [t for t in tiles if t["band_pixel_fraction"] > band_threshold]
    mean_frac = sum(fractions) / len(fractions) if fractions else 0.0
    return {
        "path": data.get("image_path", ""),
        "image_width": data.get("image_width", 0),
        "image_height": data.get("image_height", 0),
        "tile_width": data.get("tile_width", 64),
        "tile_height": data.get("tile_height", 64),
        "global_band_fraction": data.get("global_band_fraction", 0.0),
        "total_band_pixels": data.get("total_band_pixels", 0),
        "total_pixels": data.get("total_pixels", 0),
        "tile_count": len(tiles),
        "high_energy_tiles": len(high_energy),
        "mean_band_fraction": round(mean_frac, 6),
    }


def main() -> None:
    ap = argparse.ArgumentParser(description="Compare band_tile_signals.json outputs across fixture variants.")
    ap.add_argument("--baseline", required=True, help="Path to baseline band_tile_signals.json.")
    ap.add_argument("--treatment", required=True, help="Path to treatment (tile_priors_active) band_tile_signals.json.")
    ap.add_argument("--output", default="", help="Output path for comparison JSON. Defaults to stdout.")
    ap.add_argument("--band-threshold", type=float, default=0.10, help="Threshold for 'high energy' tiles (default 0.10).")
    args = ap.parse_args()

    baseline_path = os.path.abspath(args.baseline)
    treatment_path = os.path.abspath(args.treatment)

    if not os.path.isfile(baseline_path):
        print(f"[run_band_comparison] ERROR: baseline not found: {baseline_path}", file=sys.stderr)
        sys.exit(1)
    if not os.path.isfile(treatment_path):
        print(f"[run_band_comparison] ERROR: treatment not found: {treatment_path}", file=sys.stderr)
        sys.exit(1)

    baseline_data = load_band_signals(baseline_path)
    treatment_data = load_band_signals(treatment_path)

    baseline_summary = summarise(baseline_data, args.band_threshold)
    treatment_summary = summarise(treatment_data, args.band_threshold)

    b_frac = baseline_summary["global_band_fraction"]
    t_frac = treatment_summary["global_band_fraction"]
    delta = t_frac - b_frac
    rel_reduction = ((b_frac - t_frac) / b_frac * 100.0) if b_frac > 0.0 else 0.0
    tile_delta = treatment_summary["high_energy_tiles"] - baseline_summary["high_energy_tiles"]

    if delta < -1e-4:
        verdict = "improvement"
    elif delta > 1e-4:
        verdict = "regression"
    else:
        verdict = "neutral"

    result = {
        "baseline": baseline_summary,
        "treatment": treatment_summary,
        "delta_global_band_fraction": round(delta, 6),
        "relative_reduction_pct": round(rel_reduction, 3),
        "high_energy_tile_delta": tile_delta,
        "verdict": verdict,
    }

    out_str = json.dumps(result, indent=2)

    if args.output:
        out_path = os.path.abspath(args.output)
        os.makedirs(os.path.dirname(out_path) or ".", exist_ok=True)
        with open(out_path, "w", encoding="utf-8") as f:
            f.write(out_str)
        print(f"[run_band_comparison] wrote: {out_path}")

    print(out_str)
    print(
        f"\n[run_band_comparison] verdict={verdict} "
        f"baseline={b_frac:.4f} treatment={t_frac:.4f} "
        f"delta={delta:+.4f} reduction={rel_reduction:+.2f}% "
        f"high_energy_tile_delta={tile_delta:+d}"
    )


if __name__ == "__main__":
    main()
