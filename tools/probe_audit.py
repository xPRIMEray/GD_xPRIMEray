#!/usr/bin/env python3
"""
probe_audit.py — Audit step-convergence probe maps from a sconv_on DOE cell.

Usage:
    python3 tools/probe_audit.py <sconv_on_dir> [<sconv_on_dir2> ...] [--out <dir>]

Each <sconv_on_dir> should be a DOE sconv_on cell folder (e.g.
output/doe_sensitivity/<timestamp>/step_0.0125/sconv_on/).

Outputs (written to --out dir, or first sconv_on_dir if not specified):
    raw_probe_debug.md         — per-run statistics and root-cause analysis
    probe_histograms.png       — histogram grid of all probe map channels
"""

import argparse
import os
import sys

import numpy as np
from PIL import Image

try:
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    HAS_MPL = True
except ImportError:
    HAS_MPL = False

# ---------------------------------------------------------------------------
# Map discovery
# ---------------------------------------------------------------------------

MAP_SUFFIXES = {
    "step_sensitivity":           "step_sensitivity",
    "step_convergence_confidence": "step_convergence_confidence",
    "precision_required":         "precision_required",
    "probe_hit_distance_delta":   "probe_hit_distance_delta",
    "probe_normal_delta":         "probe_normal_delta",
    "probe_collider_mismatch":    "probe_collider_mismatch",
    "boundary_confidence":        "boundary_confidence",
    "selection_flip":             "selection_flip",
}

BEAUTY_EXCLUSIONS = tuple(f".{s}.png" for s in MAP_SUFFIXES.values())


def find_map(folder: str, suffix: str) -> str | None:
    target = f".{suffix}.png"
    for name in sorted(os.listdir(folder)):
        if name.endswith(target):
            return os.path.join(folder, name)
    return None


def find_beauty(folder: str) -> str | None:
    for name in sorted(os.listdir(folder)):
        if name.endswith(".png") and not any(name.endswith(s) for s in BEAUTY_EXCLUSIONS):
            return os.path.join(folder, name)
    return None


def load_gray(path: str) -> np.ndarray:
    return np.array(Image.open(path).convert("L"), dtype=np.float32) / 255.0


# ---------------------------------------------------------------------------
# Per-cell audit
# ---------------------------------------------------------------------------

def audit_cell(cell_dir: str) -> dict:
    """
    Load all probe maps from a sconv_on cell dir, compute statistics,
    diagnose whether the probe is functioning.
    """
    label = os.path.basename(os.path.dirname(cell_dir)) + "/" + os.path.basename(cell_dir)
    step_length = None
    parent = os.path.basename(os.path.dirname(cell_dir))
    if parent.startswith("step_"):
        step_length = parent[len("step_"):]

    result = {
        "label": label,
        "step_length": step_length,
        "cell_dir": cell_dir,
        "maps": {},
        "diagnoses": [],
    }

    for key, suffix in MAP_SUFFIXES.items():
        p = find_map(cell_dir, suffix)
        if p is None:
            result["diagnoses"].append(f"MISSING map: {suffix}")
            continue
        arr = load_gray(p)
        stats = {
            "path": p,
            "mean": float(arr.mean()),
            "std": float(arr.std()),
            "max": float(arr.max()),
            "min": float(arr.min()),
            "nonzero_frac": float((arr > 0.01).mean()),
            "arr": arr,
        }
        result["maps"][key] = stats

    _diagnose(result)
    return result


def _diagnose(result: dict):
    d = result["diagnoses"]
    maps = result["maps"]

    # Check whether probe redesign is working
    if "step_sensitivity" in maps:
        ss_mean = maps["step_sensitivity"]["mean"]
        if ss_mean < 0.001:
            d.append(
                f"PROBE_ZERO: step_sensitivity_mean={ss_mean:.5f} — "
                "probe is still returning all-zero sensitivity. "
                "Position-shifted probes may not be finding different colliders."
            )
        else:
            d.append(f"OK: step_sensitivity_mean={ss_mean:.5f} (non-zero, probe is active)")

    if "probe_collider_mismatch" in maps:
        pcm = maps["probe_collider_mismatch"]["mean"]
        pcm_max = maps["probe_collider_mismatch"]["max"]
        pcm_nz = maps["probe_collider_mismatch"]["nonzero_frac"]
        if pcm < 0.001:
            d.append(
                f"PROBE_ZERO: probe_collider_mismatch_mean={pcm:.5f} — "
                "no position-shifted probes found a different collider anywhere in the image."
            )
        else:
            d.append(
                f"OK: probe_collider_mismatch_mean={pcm:.5f} max={pcm_max:.3f} "
                f"nonzero_frac={pcm_nz:.3f}"
            )

    if "probe_hit_distance_delta" in maps:
        phdd = maps["probe_hit_distance_delta"]["mean"]
        d.append(f"probe_hit_distance_delta_mean={phdd:.5f}")

    if "probe_normal_delta" in maps:
        pnd = maps["probe_normal_delta"]["mean"]
        d.append(f"probe_normal_delta_mean={pnd:.5f}")

    # Correlation: does probe_collider_mismatch track step_sensitivity?
    if "probe_collider_mismatch" in maps and "step_sensitivity" in maps:
        a = maps["probe_collider_mismatch"]["arr"].ravel()
        b = maps["step_sensitivity"]["arr"].ravel()
        if a.std() > 1e-6 and b.std() > 1e-6:
            corr = float(np.corrcoef(a, b)[0, 1])
            d.append(f"pearson(probe_collider_mismatch, step_sensitivity)={corr:.4f}")
        else:
            d.append("pearson: one or both arrays are constant, skipping correlation")

    # Check precision_required distribution
    if "precision_required" in maps:
        pr = maps["precision_required"]["arr"]
        n_half = int((pr > 0.45).sum())
        n_full = int((pr > 0.95).sum())
        total = pr.size
        d.append(
            f"precision_required: {n_half}/{total} pixels ≥0.5 ({100*n_half/total:.1f}%), "
            f"{n_full}/{total} pixels ≥1.0 ({100*n_full/total:.1f}%)"
        )


# ---------------------------------------------------------------------------
# Markdown report
# ---------------------------------------------------------------------------

def write_report(cells: list[dict], out_path: str):
    lines = [
        "# Step-Convergence Probe Audit",
        "",
        "## Root-Cause Analysis",
        "",
        "### Probe Redesign: Position-Shifted Step Windows",
        "",
        "The original `ComputeStepConvergenceProbe` subdivided the SAME segment `[A,B]`",
        "with 2×/4×/0.5× substep counts. Since the physics hit exists within `[A,B]`,",
        "all probes found the same collider → confidence=1, sensitivity=0, precisionRequired=0.",
        "",
        "**Root cause:** substep-count variation on a known-hit segment is blind to",
        "step-position sensitivity. The fix uses four position-shifted probes at",
        "±0.125s and ±0.5s offsets (where s=segLen). A shifted probe that finds a",
        "different collider means the pixel result is sensitive to step-grid alignment.",
        "",
        "### Expected Behavior After Fix",
        "",
        "- `step_sensitivity_mean > 0` in sconv_on runs where banding is visible",
        "- `probe_collider_mismatch_mean` scales with banding intensity",
        "- `precision_required` shows ≥0.5 in boundary-region pixels",
        "- `pearson(probe_collider_mismatch, step_sensitivity) ≈ 1.0` (they're derived together)",
        "",
    ]

    for cell in cells:
        lines += [
            f"## Cell: {cell['label']}",
            "",
            f"**Step length:** `{cell['step_length']}`  ",
            f"**Dir:** `{cell['cell_dir']}`",
            "",
            "### Maps present",
            "",
        ]
        for key in MAP_SUFFIXES:
            if key in cell["maps"]:
                m = cell["maps"][key]
                lines.append(
                    f"- `{key}`: mean={m['mean']:.5f}  std={m['std']:.5f}  "
                    f"max={m['max']:.3f}  nonzero={m['nonzero_frac']:.3f}"
                )
            else:
                lines.append(f"- `{key}`: **MISSING**")

        lines += ["", "### Diagnoses", ""]
        for diag in cell["diagnoses"]:
            prefix = "⚠️" if any(tag in diag for tag in ("MISSING", "PROBE_ZERO")) else "✓"
            lines.append(f"- {prefix} {diag}")
        lines.append("")

    with open(out_path, "w") as f:
        f.write("\n".join(lines) + "\n")
    print(f"[probe-audit] wrote {out_path}")


# ---------------------------------------------------------------------------
# Histogram plots
# ---------------------------------------------------------------------------

PLOT_MAPS = [
    ("step_sensitivity",           "Step Sensitivity",           "steelblue"),
    ("probe_collider_mismatch",    "Probe Collider Mismatch",    "tomato"),
    ("probe_hit_distance_delta",   "Probe Hit Distance Delta",   "darkorange"),
    ("probe_normal_delta",         "Probe Normal Delta",         "mediumseagreen"),
    ("precision_required",         "Precision Required",         "mediumpurple"),
    ("step_convergence_confidence","Step Conv Confidence",       "slategray"),
]


def write_histograms(cells: list[dict], out_path: str):
    if not HAS_MPL:
        print("[probe-audit] matplotlib not available — skipping histogram plot")
        return

    n_cells = len(cells)
    n_maps = len(PLOT_MAPS)
    fig, axes = plt.subplots(n_cells, n_maps, figsize=(3.5 * n_maps, 3.0 * n_cells), squeeze=False)
    fig.suptitle("Step-Convergence Probe Histograms (sconv_on cells)", fontsize=12)

    for ci, cell in enumerate(cells):
        sl = cell.get("step_length", "?")
        for mi, (key, title, color) in enumerate(PLOT_MAPS):
            ax = axes[ci][mi]
            if key in cell["maps"]:
                arr = cell["maps"][key]["arr"].ravel()
                ax.hist(arr, bins=64, range=(0, 1), color=color, alpha=0.8, density=True)
                mean_v = cell["maps"][key]["mean"]
                ax.axvline(mean_v, color="black", linewidth=1.0, linestyle="--")
                ax.set_title(f"{title}\nsl={sl} μ={mean_v:.4f}", fontsize=8)
            else:
                ax.text(0.5, 0.5, "MISSING", ha="center", va="center",
                        transform=ax.transAxes, color="red", fontsize=10)
                ax.set_title(f"{title}\nsl={sl}", fontsize=8)
            ax.set_xlabel("value", fontsize=7)
            if mi == 0:
                ax.set_ylabel("density", fontsize=7)
            ax.tick_params(labelsize=6)

    plt.tight_layout()
    plt.savefig(out_path, dpi=110)
    plt.close()
    print(f"[probe-audit] wrote {out_path}")


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(description="Audit step-convergence probe maps")
    parser.add_argument("cell_dirs", nargs="+", help="One or more sconv_on cell dirs")
    parser.add_argument("--out", default=None,
                        help="Output directory (default: first cell_dir)")
    args = parser.parse_args()

    cell_dirs = [d.rstrip("/") for d in args.cell_dirs]
    out_dir = args.out if args.out else cell_dirs[0]
    os.makedirs(out_dir, exist_ok=True)

    cells = []
    for d in cell_dirs:
        if not os.path.isdir(d):
            print(f"[probe-audit] WARNING: {d} is not a directory, skipping", file=sys.stderr)
            continue
        print(f"[probe-audit] auditing {d}")
        cells.append(audit_cell(d))

    if not cells:
        print("[probe-audit] no valid cell dirs found", file=sys.stderr)
        return 1

    report_path = os.path.join(out_dir, "raw_probe_debug.md")
    hist_path = os.path.join(out_dir, "probe_histograms.png")

    write_report(cells, report_path)
    write_histograms(cells, hist_path)

    print(f"[probe-audit] complete — output in {out_dir}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
