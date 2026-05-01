#!/usr/bin/env python3
"""
band_correlation_analysis.py — Cross-correlate band mask with domain telemetry maps.

Usage:
    python3 tools/band_correlation_analysis.py <audit_dir> [--sensitivity FLOAT] [--min-score FLOAT]

Outputs (written to <audit_dir>/banding_analysis/):
    band_overlay.png
    band_vs_boundary_overlay.png
    band_vs_step_sensitivity_overlay.png   (skipped if maps not present)
    band_vs_precision_required_overlay.png (skipped if maps not present)
    band_correlation_summary.json
    band_correlation_summary.md
"""

import argparse
import hashlib
import json
import os
import sys
import textwrap

import numpy as np
from PIL import Image
from scipy.ndimage import uniform_filter1d

# ---------------------------------------------------------------------------
# Band detection (same algorithm as band_detector.py)
# ---------------------------------------------------------------------------

def detect_band_scores(img_array: np.ndarray, sensitivity: float = 1.5, min_score: float = 0.05) -> np.ndarray:
    if img_array.ndim == 3:
        luma = 0.299 * img_array[:, :, 0] + 0.587 * img_array[:, :, 1] + 0.114 * img_array[:, :, 2]
    else:
        luma = img_array.astype(np.float32)
    luma = luma.astype(np.float32) / 255.0
    vgrad_fwd = np.abs(np.diff(luma, axis=0, append=luma[-1:, :]))
    vgrad_bwd = np.abs(np.diff(luma, axis=0, prepend=luma[:1, :]))
    vgrad = np.maximum(vgrad_fwd, vgrad_bwd)
    smooth_h = uniform_filter1d(vgrad, size=5, axis=1)
    row_median = np.median(smooth_h, axis=1, keepdims=True)
    row_median = np.maximum(row_median, 1e-4)
    score = (smooth_h / row_median - 1.0) / sensitivity
    score = np.clip(score, 0.0, 1.0).astype(np.float32)
    score[score < min_score] = 0.0
    return score


def band_mask(scores: np.ndarray) -> np.ndarray:
    """Boolean mask: True where score > 0."""
    return scores > 0.0


# ---------------------------------------------------------------------------
# Image helpers
# ---------------------------------------------------------------------------

def load_rgb(path: str) -> np.ndarray:
    img = Image.open(path).convert("RGB")
    return np.array(img, dtype=np.uint8)


def load_gray_float(path: str) -> np.ndarray:
    """Load a grayscale image as float32 [0,1]."""
    img = Image.open(path).convert("L")
    return np.array(img, dtype=np.float32) / 255.0


def sha256_file(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def save_overlay(beauty: np.ndarray, mask: np.ndarray, color_rgb: tuple, path: str, alpha: float = 0.6):
    """Overlay a boolean mask on the beauty image using a solid color."""
    out = beauty.astype(np.float32).copy()
    c = np.array(color_rgb, dtype=np.float32)
    out[mask] = out[mask] * (1.0 - alpha) + c * alpha
    Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).save(path)


def save_dual_overlay(beauty: np.ndarray, mask_a: np.ndarray, color_a: tuple,
                      mask_b: np.ndarray, color_b: tuple, path: str, alpha: float = 0.55):
    """Overlay two boolean masks on the beauty image with different colors."""
    out = beauty.astype(np.float32).copy()
    ca = np.array(color_a, dtype=np.float32)
    cb = np.array(color_b, dtype=np.float32)
    # Regions only in A
    only_a = mask_a & ~mask_b
    out[only_a] = out[only_a] * (1.0 - alpha) + ca * alpha
    # Regions only in B
    only_b = ~mask_a & mask_b
    out[only_b] = out[only_b] * (1.0 - alpha) + cb * alpha
    # Intersection
    both = mask_a & mask_b
    cm = (ca + cb) * 0.5
    out[both] = out[both] * (1.0 - alpha) + cm * alpha
    Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).save(path)


# ---------------------------------------------------------------------------
# Overlap metrics
# ---------------------------------------------------------------------------

def overlap_metrics(band: np.ndarray, other: np.ndarray) -> dict:
    """
    band: boolean H×W — band pixels
    other: float32 H×W in [0,1] — telemetry map
    Returns: band_pixels, high_other_pixels (>0.1), intersection, precision, recall, iou, mean_other_at_band
    """
    band_px = int(band.sum())
    high_other = other > 0.1
    high_other_px = int(high_other.sum())
    inter = int((band & high_other).sum())
    precision = inter / band_px if band_px > 0 else 0.0
    recall = inter / high_other_px if high_other_px > 0 else 0.0
    iou = inter / (band_px + high_other_px - inter) if (band_px + high_other_px - inter) > 0 else 0.0
    mean_at_band = float(other[band].mean()) if band_px > 0 else 0.0
    return {
        "band_pixels": band_px,
        "high_map_pixels": high_other_px,
        "intersection": inter,
        "precision": round(precision, 4),
        "recall": round(recall, 4),
        "iou": round(iou, 4),
        "mean_map_at_band": round(mean_at_band, 4),
    }


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

STEM = "domain_resolver_stress__{label}__baseline_prune_off__scheduler-baseline__targetms-1000__stride-2__runid-1"


def stem(label: str) -> str:
    return STEM.format(label=label)


def beauty_path(base: str, label: str) -> str:
    return os.path.join(base, label, stem(label) + ".png")


def map_path(base: str, label: str, suffix: str) -> str:
    return os.path.join(base, label, stem(label) + f".{suffix}.png")


def main():
    parser = argparse.ArgumentParser(description="Band correlation analysis for domain audit visual outputs")
    parser.add_argument("audit_dir", help="Path to timestamped audit output dir (e.g. output/domain_audit_visual/20260430T235520Z)")
    parser.add_argument("--sensitivity", type=float, default=1.5, help="Band detector sensitivity (default 1.5)")
    parser.add_argument("--min-score", type=float, default=0.05, help="Minimum band score threshold (default 0.05)")
    args = parser.parse_args()

    base = args.audit_dir.rstrip("/")
    out_dir = os.path.join(base, "banding_analysis")
    os.makedirs(out_dir, exist_ok=True)

    print(f"[band-analysis] audit_dir={base}")
    print(f"[band-analysis] output={out_dir}")

    # -----------------------------------------------------------------------
    # Load beauty images
    # -----------------------------------------------------------------------
    off_beauty_path = beauty_path(base, "off")
    tel_beauty_path = beauty_path(base, "telemetry_on")
    res_beauty_path = beauty_path(base, "resolver_on")

    off_beauty = load_rgb(off_beauty_path)
    tel_beauty = load_rgb(tel_beauty_path)
    res_beauty = load_rgb(res_beauty_path)
    H, W = off_beauty.shape[:2]

    print(f"[band-analysis] image_size={W}x{H}")

    # Beauty hashes
    off_hash = sha256_file(off_beauty_path)
    tel_hash = sha256_file(tel_beauty_path)
    res_hash = sha256_file(res_beauty_path)
    beauty_off_vs_tel = "match" if off_hash == tel_hash else "different"
    beauty_off_vs_res = "match" if off_hash == res_hash else "different"
    beauty_tel_vs_res = "match" if tel_hash == res_hash else "different"

    print(f"[band-analysis] beauty_off_vs_telemetry={beauty_off_vs_tel}")
    print(f"[band-analysis] beauty_off_vs_resolver={beauty_off_vs_res}")
    print(f"[band-analysis] beauty_telemetry_vs_resolver={beauty_tel_vs_res}")

    # -----------------------------------------------------------------------
    # Detect bands
    # -----------------------------------------------------------------------
    off_scores = detect_band_scores(off_beauty, args.sensitivity, args.min_score)
    tel_scores = detect_band_scores(tel_beauty, args.sensitivity, args.min_score)
    res_scores = detect_band_scores(res_beauty, args.sensitivity, args.min_score)

    off_band = band_mask(off_scores)
    tel_band = band_mask(tel_scores)
    res_band = band_mask(res_scores)

    print(f"[band-analysis] off_band_pixels={int(off_band.sum())}")
    print(f"[band-analysis] tel_band_pixels={int(tel_band.sum())}")
    print(f"[band-analysis] res_band_pixels={int(res_band.sum())}")

    # -----------------------------------------------------------------------
    # Load telemetry maps (from telemetry_on/ and resolver_on/)
    # -----------------------------------------------------------------------
    def load_map(label: str, suffix: str):
        p = map_path(base, label, suffix)
        if os.path.exists(p):
            return load_gray_float(p), p
        return None, None

    tel_boundary, _ = load_map("telemetry_on", "boundary_confidence")
    tel_normal, _ = load_map("telemetry_on", "normal_discontinuity")
    tel_selection_flip, _ = load_map("telemetry_on", "selection_flip")
    tel_domain_conf, _ = load_map("telemetry_on", "domain_confidence")

    res_boundary, _ = load_map("resolver_on", "boundary_confidence")
    res_normal, _ = load_map("resolver_on", "normal_discontinuity")
    res_selection_flip, _ = load_map("resolver_on", "selection_flip")

    # Step-convergence maps (may not exist yet)
    tel_sconv, _ = load_map("telemetry_on", "step_convergence_confidence")
    tel_ssens, _ = load_map("telemetry_on", "step_sensitivity")
    tel_sprec, _ = load_map("telemetry_on", "precision_required")

    # -----------------------------------------------------------------------
    # Resolver pixel diff mask
    # -----------------------------------------------------------------------
    res_diff = np.any(res_beauty.astype(np.int16) != tel_beauty.astype(np.int16), axis=2)
    res_changed_pixels = int(res_diff.sum())
    print(f"[band-analysis] resolver_changed_pixels={res_changed_pixels}")

    # -----------------------------------------------------------------------
    # Overlap metrics
    # -----------------------------------------------------------------------
    metrics = {}

    # OFF band vs telemetry maps
    if tel_boundary is not None:
        metrics["off_band_vs_tel_boundary"] = overlap_metrics(off_band, tel_boundary)
    if tel_normal is not None:
        metrics["off_band_vs_tel_normal_discontinuity"] = overlap_metrics(off_band, tel_normal)
    if tel_selection_flip is not None:
        metrics["off_band_vs_tel_selection_flip"] = overlap_metrics(off_band, tel_selection_flip)
    if tel_domain_conf is not None:
        # Low domain_confidence = instability; invert
        metrics["off_band_vs_tel_domain_instability"] = overlap_metrics(off_band, 1.0 - tel_domain_conf)

    # Step-convergence maps
    if tel_sconv is not None:
        metrics["off_band_vs_tel_step_convergence_confidence"] = overlap_metrics(off_band, tel_sconv)
    if tel_ssens is not None:
        metrics["off_band_vs_tel_step_sensitivity"] = overlap_metrics(off_band, tel_ssens)
    if tel_sprec is not None:
        metrics["off_band_vs_tel_precision_required"] = overlap_metrics(off_band, tel_sprec)

    # Resolver changed pixels vs band mask and instability maps
    if res_changed_pixels > 0:
        metrics["resolver_changed_vs_off_band"] = overlap_metrics(res_diff, off_scores)
        if res_boundary is not None:
            metrics["resolver_changed_vs_res_boundary"] = overlap_metrics(res_diff, res_boundary)
        if tel_sprec is not None:
            metrics["resolver_changed_vs_tel_precision_required"] = overlap_metrics(res_diff, tel_sprec)

    # -----------------------------------------------------------------------
    # Render-diff pixel diff
    # -----------------------------------------------------------------------
    # OFF vs telemetry_on pixel diff
    off_tel_diff = np.any(off_beauty.astype(np.int16) != tel_beauty.astype(np.int16), axis=2)
    off_tel_changed = int(off_tel_diff.sum())
    print(f"[band-analysis] off_vs_tel_changed_pixels={off_tel_changed}")

    # -----------------------------------------------------------------------
    # Generate overlay images (using OFF beauty as base)
    # -----------------------------------------------------------------------

    # 1. band_overlay.png — band mask on OFF beauty (red)
    p = os.path.join(out_dir, "band_overlay.png")
    save_overlay(off_beauty, off_band, (255, 0, 0), p)
    print(f"[band-analysis] wrote {p}")

    # 2. band_vs_boundary_overlay.png — band (red) vs high boundary confidence (blue)
    if tel_boundary is not None:
        high_boundary = tel_boundary > 0.1
        p = os.path.join(out_dir, "band_vs_boundary_overlay.png")
        save_dual_overlay(off_beauty, off_band, (255, 0, 0), high_boundary, (0, 0, 255), p)
        print(f"[band-analysis] wrote {p}")

    # 3. band_vs_step_sensitivity_overlay.png
    if tel_ssens is not None:
        high_ssens = tel_ssens > 0.1
        p = os.path.join(out_dir, "band_vs_step_sensitivity_overlay.png")
        save_dual_overlay(off_beauty, off_band, (255, 0, 0), high_ssens, (0, 200, 0), p)
        print(f"[band-analysis] wrote {p}")
    else:
        print("[band-analysis] step_sensitivity map not found — skipping band_vs_step_sensitivity_overlay.png")

    # 4. band_vs_precision_required_overlay.png
    if tel_sprec is not None:
        high_sprec = tel_sprec > 0.1
        p = os.path.join(out_dir, "band_vs_precision_required_overlay.png")
        save_dual_overlay(off_beauty, off_band, (255, 0, 0), high_sprec, (255, 165, 0), p)
        print(f"[band-analysis] wrote {p}")
    else:
        print("[band-analysis] precision_required map not found — skipping band_vs_precision_required_overlay.png")

    # 5. resolver_diff_vs_band_overlay.png — resolver changed pixels (cyan) vs band (red)
    if res_changed_pixels > 0:
        p = os.path.join(out_dir, "resolver_diff_vs_band_overlay.png")
        save_dual_overlay(tel_beauty, off_band, (255, 0, 0), res_diff, (0, 255, 255), p)
        print(f"[band-analysis] wrote {p}")

    # 6. band_vs_selection_flip_overlay.png
    if tel_selection_flip is not None:
        high_sf = tel_selection_flip > 0.1
        p = os.path.join(out_dir, "band_vs_selection_flip_overlay.png")
        save_dual_overlay(off_beauty, off_band, (255, 0, 0), high_sf, (255, 0, 255), p)
        print(f"[band-analysis] wrote {p}")

    # -----------------------------------------------------------------------
    # Assemble summary
    # -----------------------------------------------------------------------
    summary = {
        "audit_dir": base,
        "image_size": f"{W}x{H}",
        "sensitivity": args.sensitivity,
        "min_score": args.min_score,
        "beauty_hashes": {
            "off": off_hash,
            "telemetry_on": tel_hash,
            "resolver_on": res_hash,
        },
        "beauty_comparison": {
            "off_vs_telemetry_on": beauty_off_vs_tel,
            "off_vs_resolver_on": beauty_off_vs_res,
            "telemetry_on_vs_resolver_on": beauty_tel_vs_res,
        },
        "band_pixels": {
            "off": int(off_band.sum()),
            "telemetry_on": int(tel_band.sum()),
            "resolver_on": int(res_band.sum()),
        },
        "off_vs_telemetry_changed_pixels": off_tel_changed,
        "resolver_vs_telemetry_changed_pixels": res_changed_pixels,
        "telemetry_maps_present": {
            "boundary_confidence": tel_boundary is not None,
            "normal_discontinuity": tel_normal is not None,
            "selection_flip": tel_selection_flip is not None,
            "domain_confidence": tel_domain_conf is not None,
            "step_convergence_confidence": tel_sconv is not None,
            "step_sensitivity": tel_ssens is not None,
            "precision_required": tel_sprec is not None,
        },
        "overlap_metrics": metrics,
    }

    json_path = os.path.join(out_dir, "band_correlation_summary.json")
    with open(json_path, "w") as f:
        json.dump(summary, f, indent=2)
    print(f"[band-analysis] wrote {json_path}")

    # -----------------------------------------------------------------------
    # Markdown report
    # -----------------------------------------------------------------------
    def pct(v): return f"{v*100:.1f}%"

    md_lines = [
        "# Band Correlation Analysis",
        f"",
        f"**Audit dir:** `{base}`  ",
        f"**Image size:** {W}×{H}  ",
        f"**Band detector:** sensitivity={args.sensitivity}, min_score={args.min_score}",
        f"",
        "## Beauty Hash Comparison",
        f"",
        f"| Pair | Result |",
        f"|------|--------|",
        f"| OFF vs telemetry ON | `{beauty_off_vs_tel}` |",
        f"| OFF vs resolver ON | `{beauty_off_vs_res}` |",
        f"| telemetry ON vs resolver ON | `{beauty_tel_vs_res}` |",
        f"",
        "## Band Pixel Counts",
        f"",
        f"| Label | Band pixels | Fraction |",
        f"|-------|-------------|----------|",
        f"| off | {int(off_band.sum())} | {pct(off_band.sum()/(H*W))} |",
        f"| telemetry_on | {int(tel_band.sum())} | {pct(tel_band.sum()/(H*W))} |",
        f"| resolver_on | {int(res_band.sum())} | {pct(res_band.sum()/(H*W))} |",
        f"",
        f"OFF vs telemetry changed pixels: **{off_tel_changed}**  ",
        f"Resolver vs telemetry changed pixels: **{res_changed_pixels}**",
        f"",
        "## Overlap Metrics (OFF band vs telemetry maps)",
        f"",
        f"| Map | Band px | High-map px | Intersection | Precision | Recall | IoU | Mean@band |",
        f"|-----|---------|-------------|--------------|-----------|--------|-----|-----------|",
    ]

    for key, m in metrics.items():
        md_lines.append(
            f"| {key} | {m['band_pixels']} | {m['high_map_pixels']} | {m['intersection']} "
            f"| {pct(m['precision'])} | {pct(m['recall'])} | {pct(m['iou'])} | {m['mean_map_at_band']:.4f} |"
        )

    md_lines += [
        f"",
        "## Telemetry Maps Present",
        f"",
    ]
    for k, v in summary["telemetry_maps_present"].items():
        md_lines.append(f"- `{k}`: {'yes' if v else '**no** (step-convergence maps not yet generated)'}")

    md_lines += [
        f"",
        "## Interpretation",
        f"",
        f"- **Precision** (band→map): fraction of band pixels that are also high-instability in the telemetry map.",
        f"- **Recall** (map→band): fraction of high-instability map pixels that are also banding pixels.",
        f"- **IoU**: Jaccard index of band mask and high-instability region.",
        f"- High precision + recall → banding co-localises with domain instability.",
        f"- `off_vs_telemetry_on=different` is a pre-existing issue (telemetry probes alter physics query ordering).",
        f"- Step-convergence maps absent: run with `--step-convergence` to generate them.",
    ]

    md_path = os.path.join(out_dir, "band_correlation_summary.md")
    with open(md_path, "w") as f:
        f.write("\n".join(md_lines) + "\n")
    print(f"[band-analysis] wrote {md_path}")

    print(f"[band-analysis] complete output={out_dir}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
