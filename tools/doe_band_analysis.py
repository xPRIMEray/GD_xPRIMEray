#!/usr/bin/env python3
"""
doe_band_analysis.py — Analyse DOE sensitivity experiment outputs.

Usage:
    python3 tools/doe_band_analysis.py <doe_dir> [--sensitivity FLOAT] [--min-score FLOAT]

Expects layout:
    <doe_dir>/step_<SL>/{off,telemetry_on,sconv_on,resolver_on}/

Outputs (written to <doe_dir>/):
    DOE_summary.csv
    DOE_summary.md
    (optional) DOE_sensitivity_plot.png  (when matplotlib available)
"""

import argparse
import csv
import hashlib
import json
import os
import sys

import numpy as np
from PIL import Image
from scipy.ndimage import uniform_filter1d

# ---------------------------------------------------------------------------
# Band detection (same kernel as band_detector.py / band_correlation_analysis.py)
# ---------------------------------------------------------------------------

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
    smooth = uniform_filter1d(vgrad, size=5, axis=1)
    row_med = np.maximum(np.median(smooth, axis=1, keepdims=True), 1e-4)
    score = np.clip((smooth / row_med - 1.0) / sensitivity, 0.0, 1.0).astype(np.float32)
    score[score < min_score] = 0.0
    return score


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def load_rgb(path: str) -> np.ndarray:
    return np.array(Image.open(path).convert("RGB"), dtype=np.uint8)


def load_gray_float(path: str) -> np.ndarray:
    return np.array(Image.open(path).convert("L"), dtype=np.float32) / 255.0


def sha256_file(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def find_beauty_png(folder: str) -> str | None:
    """Return path of the first .png that looks like a beauty render (not a map)."""
    map_suffixes = (
        ".boundary_confidence.png", ".domain_confidence.png", ".domain_id.png",
        ".normal_discontinuity.png", ".selection_flip.png",
        ".step_convergence_confidence.png", ".step_sensitivity.png",
        ".precision_required.png",
        ".probe_hit_distance_delta.png", ".probe_normal_delta.png",
        ".probe_collider_mismatch.png",
    )
    if not os.path.isdir(folder):
        return None
    for name in sorted(os.listdir(folder)):
        if name.endswith(".png") and not any(name.endswith(s) for s in map_suffixes):
            return os.path.join(folder, name)
    return None


def find_map_png(folder: str, suffix: str) -> str | None:
    if not os.path.isdir(folder):
        return None
    for name in sorted(os.listdir(folder)):
        if name.endswith(f".{suffix}.png"):
            return os.path.join(folder, name)
    return None


def overlap(band: np.ndarray, map_f: np.ndarray, threshold: float = 0.1) -> dict:
    high = map_f > threshold
    band_px = int(band.sum())
    high_px = int(high.sum())
    inter = int((band & high).sum())
    total = band_px + high_px - inter
    return {
        "band_px": band_px,
        "high_map_px": high_px,
        "intersection": inter,
        "precision": round(inter / band_px, 4) if band_px > 0 else 0.0,
        "recall": round(inter / high_px, 4) if high_px > 0 else 0.0,
        "iou": round(inter / total, 4) if total > 0 else 0.0,
        "mean_map_at_band": round(float(map_f[band].mean()), 4) if band_px > 0 else 0.0,
    }


def changed_pixels_bbox(a: np.ndarray, b: np.ndarray):
    diff = np.any(a.astype(np.int16) != b.astype(np.int16), axis=2)
    n = int(diff.sum())
    if n == 0:
        return n, None
    ys, xs = np.where(diff)
    return n, (int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max()))


# ---------------------------------------------------------------------------
# Per-cell analysis
# ---------------------------------------------------------------------------

def analyse_cell(sl: str, mode: str, cell_dir: str, sens: float, min_score: float) -> dict:
    """Return a flat dict of metrics for one DOE cell (one step-length × one mode)."""
    rec = {
        "step_length": sl,
        "mode": mode,
        "folder": cell_dir,
        "beauty_path": "",
        "beauty_hash": "",
        "hash_matches_off": "",    # filled in attach_cross_metrics()
        "changed_pixels": "",      # filled in attach_cross_metrics()
        "changed_bbox": "",        # filled in attach_cross_metrics()
        "effective_status": -1,
        "band_pixels": -1,
        "band_percent": float("nan"),
        "sconv_confidence_mean": float("nan"),
        "step_sensitivity_mean": float("nan"),
        "precision_required_mean": float("nan"),
        "probe_dist_delta_mean": float("nan"),
        "probe_normal_delta_mean": float("nan"),
        "probe_collider_mismatch_mean": float("nan"),
        "boundary_mean": float("nan"),
        "selection_flip_mean": float("nan"),
        "band_vs_step_sensitivity_precision": float("nan"),
        "band_vs_step_sensitivity_iou": float("nan"),
        "band_vs_precision_required_precision": float("nan"),
        "band_vs_precision_required_iou": float("nan"),
        "band_vs_probe_dist_delta_precision": float("nan"),
        "band_vs_probe_dist_delta_iou": float("nan"),
        "band_vs_probe_collider_mismatch_precision": float("nan"),
        "band_vs_probe_collider_mismatch_iou": float("nan"),
        "band_vs_selection_flip_precision": float("nan"),
        "band_vs_selection_flip_iou": float("nan"),
        "band_vs_boundary_precision": float("nan"),
        "band_vs_boundary_iou": float("nan"),
    }

    status_file = os.path.join(cell_dir, "effective_status.txt")
    if os.path.exists(status_file):
        try:
            rec["effective_status"] = int(open(status_file).read().strip())
        except Exception:
            pass

    beauty = find_beauty_png(cell_dir)
    if beauty is None:
        return rec
    rec["beauty_path"] = beauty
    rec["beauty_hash"] = sha256_file(beauty)

    img = load_rgb(beauty)
    H, W = img.shape[:2]
    scores = detect_band_scores(img, sens, min_score)
    band = scores > 0.0
    rec["band_pixels"] = int(band.sum())
    rec["band_percent"] = round(100.0 * rec["band_pixels"] / (H * W), 3)

    # (file_suffix, mean_rec_key, overlap_prefix or None)
    map_specs = [
        ("step_sensitivity",           "step_sensitivity_mean",        "band_vs_step_sensitivity"),
        ("step_convergence_confidence", "sconv_confidence_mean",        None),
        ("precision_required",         "precision_required_mean",       "band_vs_precision_required"),
        ("probe_hit_distance_delta",   "probe_dist_delta_mean",        "band_vs_probe_dist_delta"),
        ("probe_normal_delta",         "probe_normal_delta_mean",      None),
        ("probe_collider_mismatch",    "probe_collider_mismatch_mean", "band_vs_probe_collider_mismatch"),
        ("boundary_confidence",        "boundary_mean",                "band_vs_boundary"),
        ("selection_flip",             "selection_flip_mean",          "band_vs_selection_flip"),
    ]
    for suffix, mean_key, overlap_prefix in map_specs:
        mp = find_map_png(cell_dir, suffix)
        if mp is None:
            continue
        mf = load_gray_float(mp)
        rec[mean_key] = round(float(mf.mean()), 4)
        if overlap_prefix is not None:
            ov = overlap(band, mf)
            rec[f"{overlap_prefix}_precision"] = ov["precision"]
            rec[f"{overlap_prefix}_iou"] = ov["iou"]

    return rec


# ---------------------------------------------------------------------------
# Cross-cell metrics — attach per-row after all cells are computed
# ---------------------------------------------------------------------------

def attach_cross_metrics(cells_by_sl: dict) -> dict:
    """
    For each step-length group, compare every non-OFF mode against OFF:
      - set hash_matches_off, changed_pixels, changed_bbox on the non-OFF record
    Returns a separate cross summary dict (for markdown tables).
    """
    cross = {}
    for sl, cells in cells_by_sl.items():
        off_rec = cells.get("off")
        entry = {}
        for mode, rec in cells.items():
            if mode == "off":
                rec["hash_matches_off"] = "true"
                rec["changed_pixels"] = 0
                rec["changed_bbox"] = "none"
                continue
            if off_rec is None:
                continue
            a_path = off_rec.get("beauty_path", "")
            b_path = rec.get("beauty_path", "")
            if not a_path or not b_path or not os.path.exists(a_path) or not os.path.exists(b_path):
                continue
            a_img = load_rgb(a_path)
            b_img = load_rgb(b_path)
            n, bbox = changed_pixels_bbox(a_img, b_img)
            matches = off_rec.get("beauty_hash", "?") == rec.get("beauty_hash", "??")
            rec["hash_matches_off"] = "true" if matches else "false"
            rec["changed_pixels"] = n
            rec["changed_bbox"] = str(bbox) if bbox else "none"
            entry[f"off_vs_{mode}_changed_px"] = n
            entry[f"off_vs_{mode}_hash_match"] = matches
        cross[sl] = entry
    return cross


# ---------------------------------------------------------------------------
# CSV / Markdown output
# ---------------------------------------------------------------------------

CSV_COLS = [
    "step_length", "mode", "effective_status",
    "beauty_hash", "hash_matches_off", "changed_pixels", "changed_bbox",
    "band_pixels", "band_percent",
    "boundary_mean", "selection_flip_mean",
    "step_sensitivity_mean", "precision_required_mean", "sconv_confidence_mean",
    "probe_dist_delta_mean", "probe_normal_delta_mean", "probe_collider_mismatch_mean",
    "band_vs_boundary_precision", "band_vs_boundary_iou",
    "band_vs_selection_flip_precision", "band_vs_selection_flip_iou",
    "band_vs_step_sensitivity_precision", "band_vs_step_sensitivity_iou",
    "band_vs_precision_required_precision", "band_vs_precision_required_iou",
    "band_vs_probe_dist_delta_precision", "band_vs_probe_dist_delta_iou",
    "band_vs_probe_collider_mismatch_precision", "band_vs_probe_collider_mismatch_iou",
]

LABELS_ORDER = ["off", "telemetry_on", "sconv_on", "resolver_on"]
STEP_LENGTHS_ORDER = ["0.025", "0.0125", "0.00625"]


def fmt(v):
    if isinstance(v, float):
        if v != v:  # nan
            return ""
        return f"{v:.4f}"
    return str(v) if v is not None else ""


def write_csv(path: str, rows: list[dict]):
    with open(path, "w", newline="") as f:
        w = csv.DictWriter(f, fieldnames=CSV_COLS, extrasaction="ignore")
        w.writeheader()
        for row in rows:
            w.writerow({k: fmt(row.get(k, "")) for k in CSV_COLS})
    print(f"[doe-analysis] wrote {path}")


def write_markdown(path: str, rows: list[dict], cross: dict, doe_dir: str):
    lines = [
        "# DOE Sensitivity Experiment — Band Analysis",
        f"",
        f"**Experiment dir:** `{doe_dir}`",
        f"",
        "## Design",
        f"",
        "| Factor | Levels |",
        "| ------ | ------ |",
        "| A — Base step length | 0.025, 0.0125, 0.00625 |",
        "| B — Resolver | off, on |",
        "| C — Step-conv telemetry | off, on (only when B=off) |",
        "",
        "Fixed: 320×180, 90 frames, domain_resolver_stress fixture, camera fixed.",
        "",
        "## Beauty Hash Validity",
        "",
        "| Step length | OFF vs tel | OFF vs sconv | OFF vs resolver |",
        "| ----------- | ---------- | ------------ | --------------- |",
    ]
    for sl in STEP_LENGTHS_ORDER:
        c = cross.get(sl, {})
        off_tel = "MATCH ✓" if c.get("off_vs_tel_hash_match") else (
            f"DIFF ({c['off_vs_tel_changed_px']}px)" if "off_vs_tel_changed_px" in c else "—")
        off_sconv = "MATCH ✓" if c.get("off_vs_sconv_hash_match") else (
            f"DIFF ({c['off_vs_sconv_changed_px']}px)" if "off_vs_sconv_changed_px" in c else "—")
        off_res = "MATCH ✓" if c.get("off_vs_resolver_hash_match") else (
            f"DIFF ({c['off_vs_resolver_changed_px']}px)" if "off_vs_resolver_changed_px" in c else "—")
        lines.append(f"| {sl} | {off_tel} | {off_sconv} | {off_res} |")

    lines += [
        "",
        "## Band Pixel Count by Step Length and Mode",
        "",
        "| Step length | off | telemetry_on | sconv_on | resolver_on |",
        "| ----------- | --- | ------------ | -------- | ----------- |",
    ]
    by_sl_mode = {r["step_length"]: {} for r in rows}
    for r in rows:
        by_sl_mode[r["step_length"]][r["mode"]] = r

    for sl in STEP_LENGTHS_ORDER:
        cells = by_sl_mode.get(sl, {})
        def bp(mode):
            r = cells.get(mode, {})
            bp_ = r.get("band_pixels", -1)
            pct_ = r.get("band_percent", float("nan"))
            if bp_ < 0:
                return "—"
            return f"{bp_} ({pct_:.1f}%)"
        lines.append(f"| {sl} | {bp('off')} | {bp('telemetry_on')} | {bp('sconv_on')} | {bp('resolver_on')} |")

    lines += [
        "",
        "## Changed Pixels vs Baseline (OFF)",
        "",
        "| Step length | OFF→tel | OFF→sconv | OFF→resolver |",
        "| ----------- | ------- | --------- | ------------ |",
    ]
    for sl in STEP_LENGTHS_ORDER:
        c = cross.get(sl, {})
        def cx(key):
            v = c.get(key)
            return str(v) if v is not None else "—"
        lines.append(f"| {sl} | {cx('off_vs_telemetry_on_changed_px')} | {cx('off_vs_sconv_on_changed_px')} | {cx('off_vs_resolver_on_changed_px')} |")

    lines += [
        "",
        "## Mean Telemetry Map Values",
        "",
        "| Step length | mode | boundary_mean | selection_flip | step_sensitivity | precision_req | sconv_conf |",
        "| ----------- | ---- | ------------- | -------------- | ---------------- | ------------- | ---------- |",
    ]
    for sl in STEP_LENGTHS_ORDER:
        cells = by_sl_mode.get(sl, {})
        for mode in LABELS_ORDER:
            r = cells.get(mode, {})
            if not r:
                continue
            def fv(k): return fmt(r.get(k, float("nan")))
            lines.append(
                f"| {sl} | {mode} | {fv('boundary_mean')} | {fv('selection_flip_mean')} "
                f"| {fv('step_sensitivity_mean')} | {fv('precision_required_mean')} | {fv('sconv_confidence_mean')} |"
            )

    lines += [
        "",
        "## Probe Diagnostic Map Values (sconv_on mode)",
        "",
        "| Step length | probe_dist_delta | probe_normal_delta | probe_collider_mismatch |",
        "| ----------- | ---------------- | ------------------ | ----------------------- |",
    ]
    for sl in STEP_LENGTHS_ORDER:
        cells = by_sl_mode.get(sl, {})
        r = cells.get("sconv_on", {})
        def fv2(k): return fmt(r.get(k, float("nan"))) if r else "—"
        lines.append(
            f"| {sl} | {fv2('probe_dist_delta_mean')} | {fv2('probe_normal_delta_mean')} "
            f"| {fv2('probe_collider_mismatch_mean')} |"
        )

    lines += [
        "",
        "## Band vs Map Overlap (Precision / IoU)",
        "",
        "| Step length | mode | vs boundary | vs sel-flip | vs step-sens | vs prec-req | vs probe-mismatch |",
        "| ----------- | ---- | ----------- | ----------- | ------------ | ----------- | ----------------- |",
    ]
    for sl in STEP_LENGTHS_ORDER:
        cells = by_sl_mode.get(sl, {})
        for mode in LABELS_ORDER:
            r = cells.get(mode, {})
            if not r:
                continue
            def po(prec_k, iou_k):
                p = r.get(prec_k, float("nan"))
                u = r.get(iou_k, float("nan"))
                if p != p:
                    return "—"
                return f"{p:.2%} / {u:.2%}"
            lines.append(
                f"| {sl} | {mode} "
                f"| {po('band_vs_boundary_precision','band_vs_boundary_iou')} "
                f"| {po('band_vs_selection_flip_precision','band_vs_selection_flip_iou')} "
                f"| {po('band_vs_step_sensitivity_precision','band_vs_step_sensitivity_iou')} "
                f"| {po('band_vs_precision_required_precision','band_vs_precision_required_iou')} "
                f"| {po('band_vs_probe_collider_mismatch_precision','band_vs_probe_collider_mismatch_iou')} |"
            )

    lines += [
        "",
        "## Interpretation Guide",
        "",
        "- **Does smaller step size reduce banding?** → Compare `band_pixels` across step lengths for the `off` mode.",
        "- **Does resolver amplify instability?** → Compare `resolver_on` band_pixels vs `off` at same step length.",
        "- **Do convergence maps predict banding?** → Check `band_vs_step_sensitivity_precision` and `band_vs_precision_required_precision` for `sconv_on` rows.",
        "- **Probe collider mismatch vs banding?** → `band_vs_probe_collider_mismatch_precision` — if high, position-shifted probes correctly identify banding pixels.",
        "- **Convergence stabilization threshold?** → Find the step length where `precision_required_mean` drops to near zero.",
        "- `hash_matches_off=false` rows indicate the telemetry is non-passive (flag run invalid).",
        "- `—` means map not generated for that mode (resolver_on never gets step-conv maps).",
        "- `probe_dist_delta_mean` and `probe_collider_mismatch_mean` should be non-zero in sconv_on runs if probe redesign is working.",
    ]

    with open(path, "w") as f:
        f.write("\n".join(lines) + "\n")
    print(f"[doe-analysis] wrote {path}")


# ---------------------------------------------------------------------------
# Optional matplotlib plot
# ---------------------------------------------------------------------------

def try_plot(rows: list[dict], out_dir: str):
    try:
        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
    except ImportError:
        print("[doe-analysis] matplotlib not available — skipping plot")
        return

    sls = STEP_LENGTHS_ORDER
    by_sl_mode = {}
    for r in rows:
        by_sl_mode.setdefault(r["step_length"], {})[r["mode"]] = r

    fig, axes = plt.subplots(2, 2, figsize=(12, 8))
    fig.suptitle("DOE Sensitivity — Band Pixel % by Step Length & Mode")

    # Subplot 1: band_percent
    ax = axes[0, 0]
    x = np.arange(len(sls))
    width = 0.18
    for i, mode in enumerate(LABELS_ORDER):
        y = [by_sl_mode.get(sl, {}).get(mode, {}).get("band_percent", float("nan")) for sl in sls]
        ax.bar(x + i * width, y, width, label=mode)
    ax.set_xticks(x + width * 1.5)
    ax.set_xticklabels(sls)
    ax.set_xlabel("Step length")
    ax.set_ylabel("Band pixel %")
    ax.set_title("Band pixel %")
    ax.legend(fontsize=7)

    # Subplot 2: step_sensitivity_mean (sconv_on only)
    ax = axes[0, 1]
    y = [by_sl_mode.get(sl, {}).get("sconv_on", {}).get("step_sensitivity_mean", float("nan")) for sl in sls]
    ax.bar(sls, y, color="steelblue")
    ax.set_xlabel("Step length")
    ax.set_ylabel("Mean step sensitivity")
    ax.set_title("Mean step sensitivity (sconv_on)")

    # Subplot 3: precision_required_mean (sconv_on only)
    ax = axes[1, 0]
    y = [by_sl_mode.get(sl, {}).get("sconv_on", {}).get("precision_required_mean", float("nan")) for sl in sls]
    ax.bar(sls, y, color="tomato")
    ax.set_xlabel("Step length")
    ax.set_ylabel("Mean precision required")
    ax.set_title("Mean precision required (sconv_on)")

    # Subplot 4: band_vs_step_sensitivity IoU (sconv_on)
    ax = axes[1, 1]
    y_sens = [by_sl_mode.get(sl, {}).get("sconv_on", {}).get("band_vs_step_sensitivity_iou", float("nan")) for sl in sls]
    y_prec = [by_sl_mode.get(sl, {}).get("sconv_on", {}).get("band_vs_precision_required_iou", float("nan")) for sl in sls]
    x = np.arange(len(sls))
    ax.bar(x - 0.15, y_sens, 0.3, label="vs step_sensitivity", color="steelblue")
    ax.bar(x + 0.15, y_prec, 0.3, label="vs precision_req", color="tomato")
    ax.set_xticks(x)
    ax.set_xticklabels(sls)
    ax.set_xlabel("Step length")
    ax.set_ylabel("IoU")
    ax.set_title("Band ∩ convergence map IoU (sconv_on)")
    ax.legend(fontsize=8)

    plt.tight_layout()
    plot_path = os.path.join(out_dir, "DOE_sensitivity_plot.png")
    plt.savefig(plot_path, dpi=120)
    plt.close()
    print(f"[doe-analysis] wrote {plot_path}")


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("doe_dir", help="Path to DOE output dir (contains step_<SL>/ subdirs)")
    parser.add_argument("--sensitivity", type=float, default=1.5)
    parser.add_argument("--min-score", type=float, default=0.05)
    args = parser.parse_args()

    doe_dir = args.doe_dir.rstrip("/")
    print(f"[doe-analysis] doe_dir={doe_dir}")

    # Discover step lengths
    step_dirs = {}
    for name in sorted(os.listdir(doe_dir)):
        if name.startswith("step_") and os.path.isdir(os.path.join(doe_dir, name)):
            sl = name[len("step_"):]
            step_dirs[sl] = os.path.join(doe_dir, name)

    if not step_dirs:
        print(f"[doe-analysis] no step_<SL> subdirs found in {doe_dir}", file=sys.stderr)
        return 1

    print(f"[doe-analysis] step levels found: {list(step_dirs.keys())}")

    rows = []
    cells_by_sl = {}

    for sl, sl_dir in step_dirs.items():
        cells_by_sl[sl] = {}
        for mode in LABELS_ORDER:
            cell_dir = os.path.join(sl_dir, mode)
            print(f"[doe-analysis] analysing sl={sl} mode={mode} ...")
            rec = analyse_cell(sl, mode, cell_dir, args.sensitivity, args.min_score)
            rows.append(rec)
            cells_by_sl[sl][mode] = rec

    cross = attach_cross_metrics(cells_by_sl)

    csv_path = os.path.join(doe_dir, "DOE_summary.csv")
    md_path = os.path.join(doe_dir, "DOE_summary.md")
    json_path = os.path.join(doe_dir, "DOE_summary.json")

    write_csv(csv_path, rows)
    write_markdown(md_path, rows, cross, doe_dir)

    # Write full JSON for downstream use
    summary = {"rows": rows, "cross": cross}
    with open(json_path, "w") as f:
        json.dump(summary, f, indent=2, default=str)
    print(f"[doe-analysis] wrote {json_path}")

    try_plot(rows, doe_dir)

    print(f"[doe-analysis] complete")
    return 0


if __name__ == "__main__":
    sys.exit(main())
