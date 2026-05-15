from __future__ import annotations

import json
from dataclasses import asdict, dataclass
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
from scipy.signal import find_peaks, savgol_filter


ROOT = Path(__file__).resolve().parents[4]
RUN_DIR = ROOT / "output" / "fixture_runs" / "fixture_011_wormhole_checkpoint_sequence" / "2026-04-20T22-26-39"
MORPHOLOGY_SUMMARY_PATH = Path(__file__).resolve().parent / "morphology" / "morphology_summary.json"
OUTPUT_DIR = Path(__file__).resolve().parent / "radial_structure"
FIG_DIR = OUTPUT_DIR / "figures"
SUMMARY_MD = OUTPUT_DIR / "summary.md"
SUMMARY_JSON = OUTPUT_DIR / "radial_structure_summary.json"

ORDER = [
    ("mouth", RUN_DIR / "00_mouth_debug.png"),
    ("mouth_to_throat_approach", RUN_DIR / "01_mouth_to_throat_approach_debug.png"),
    ("throat", RUN_DIR / "02_throat_debug.png"),
    ("post_throat_backstep_01", RUN_DIR / "03_post_throat_backstep_01_debug.png"),
    ("post_throat_exit_approach", RUN_DIR / "04_post_throat_exit_approach_debug.png"),
    ("exit_lookback", RUN_DIR / "05_exit_lookback_debug.png"),
]

DISPLAY = {
    "mouth": "Mouth",
    "mouth_to_throat_approach": "Mouth→Throat",
    "throat": "Throat",
    "post_throat_backstep_01": "Bridge",
    "post_throat_exit_approach": "Post-Exit",
    "exit_lookback": "Exit",
}


@dataclass
class RadialStructureResult:
    checkpoint: str
    apparent_radius_px: float
    feature_radius_px: float | None
    gradient_sign_change_radius_px: float | None
    peak_count: int
    inflection_count: int
    peak_minus_apparent_px: float | None
    slope_steepness_near_apparent: float


def load_morphology() -> dict[str, dict]:
    payload = json.loads(MORPHOLOGY_SUMMARY_PATH.read_text(encoding="utf-8"))
    return {row["checkpoint"]: row for row in payload["results"]}


def radial_profile(gray: np.ndarray, cx: float, cy: float, max_radius: int = 360) -> np.ndarray:
    yy, xx = np.indices(gray.shape)
    radii = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    bins = np.clip(radii.astype(int), 0, max_radius)
    sums = np.bincount(bins.ravel(), weights=gray.ravel(), minlength=max_radius + 1)
    counts = np.bincount(bins.ravel(), minlength=max_radius + 1)
    return sums / np.maximum(counts, 1)


def zero_crossings(values: np.ndarray) -> np.ndarray:
    s = np.sign(values)
    s[s == 0] = 1
    idx = np.where(np.diff(s) != 0)[0]
    return idx.astype(int)


def nearest_radius(candidates: np.ndarray, target: float, max_delta: float | None = None) -> float | None:
    if candidates.size == 0:
        return None
    nearest = float(candidates[np.argmin(np.abs(candidates - target))])
    if max_delta is not None and abs(nearest - target) > max_delta:
        return None
    return nearest


def analyze_checkpoint(checkpoint: str, image_path: Path, morphology: dict[str, dict]) -> tuple[RadialStructureResult, np.ndarray, np.ndarray, np.ndarray, np.ndarray]:
    image = plt.imread(image_path)
    if image.ndim == 3:
        if image.shape[2] == 4:
            image = image[:, :, :3]
        gray = image.mean(axis=2)
    else:
        gray = image.astype(float)
    if gray.max() > 1.0:
        gray = gray / 255.0

    meta = morphology[checkpoint]
    profile = radial_profile(gray, meta["center_x"], meta["center_y"])
    smooth = savgol_filter(profile, 21, 3, mode="interp")
    norm = (smooth - smooth.min()) / max(float(smooth.max() - smooth.min()), 1e-9)
    first = np.gradient(norm)
    second = np.gradient(first)

    peaks, _ = find_peaks(norm, prominence=0.03, distance=8)
    infl = zero_crossings(second)
    apparent = float(meta["apparent_radius_px"])
    grad_zero = zero_crossings(first)
    near_grad_zero = nearest_radius(grad_zero, apparent, max_delta=45.0)
    near_peak = nearest_radius(peaks, apparent, max_delta=70.0)
    feature_radius = near_peak if near_peak is not None else near_grad_zero
    peak_minus_apparent = (feature_radius - apparent) if feature_radius is not None else None

    lo = max(0, int(round(apparent - 20)))
    hi = min(len(first), int(round(apparent + 21)))
    slope_steepness = float(np.max(np.abs(first[lo:hi]))) if hi > lo else 0.0

    result = RadialStructureResult(
        checkpoint=checkpoint,
        apparent_radius_px=apparent,
        feature_radius_px=feature_radius,
        gradient_sign_change_radius_px=near_grad_zero,
        peak_count=int(len(peaks)),
        inflection_count=int(len(infl)),
        peak_minus_apparent_px=peak_minus_apparent,
        slope_steepness_near_apparent=slope_steepness,
    )
    return result, norm, first, second, peaks


def plot_overlay(results: list[RadialStructureResult], profiles: dict[str, np.ndarray]) -> None:
    fig, ax = plt.subplots(figsize=(8.8, 5.2), dpi=220, constrained_layout=True)
    fig.patch.set_facecolor("white")
    ax.set_facecolor("white")
    for checkpoint, profile in profiles.items():
        ax.plot(profile, linewidth=2.0, label=DISPLAY[checkpoint])
    ax.set_title("Normalized radial intensity profiles across the observer ladder")
    ax.set_xlabel("Radius (pixels)")
    ax.set_ylabel("Normalized intensity")
    ax.grid(True, color="#d8dde6", linewidth=0.9)
    for spine in ("top", "right"):
        ax.spines[spine].set_visible(False)
    ax.legend(frameon=False, ncol=2)
    fig.savefig(FIG_DIR / "normalized_profile_overlay.png", facecolor="white")
    plt.close(fig)


def plot_derivatives(results: list[RadialStructureResult], profiles: dict[str, np.ndarray], firsts: dict[str, np.ndarray], seconds: dict[str, np.ndarray]) -> None:
    fig, axes = plt.subplots(3, 2, figsize=(10.8, 11.6), dpi=220, constrained_layout=True)
    fig.patch.set_facecolor("white")
    axes = axes.ravel()
    for ax, result in zip(axes, results):
        checkpoint = result.checkpoint
        r = np.arange(len(profiles[checkpoint]))
        ax.set_facecolor("white")
        ax.plot(r, profiles[checkpoint], color="#1f77b4", linewidth=1.8, label="I(r)")
        ax.plot(r, firsts[checkpoint], color="#d62728", linewidth=1.4, label="dI/dr")
        ax.plot(r, seconds[checkpoint], color="#2ca02c", linewidth=1.2, label="d²I/dr²")
        ax.axvline(result.apparent_radius_px, color="#444444", linestyle="--", linewidth=1.2, label="apparent radius")
        if result.feature_radius_px is not None:
            ax.axvline(result.feature_radius_px, color="#9467bd", linestyle=":", linewidth=1.6, label="feature radius")
        ax.set_title(DISPLAY[checkpoint])
        ax.set_xlabel("Radius (pixels)")
        ax.grid(True, color="#d8dde6", linewidth=0.8)
        for spine in ("top", "right"):
            ax.spines[spine].set_visible(False)
    handles, labels = axes[0].get_legend_handles_labels()
    fig.legend(handles, labels, loc="upper center", ncol=4, frameon=False)
    fig.savefig(FIG_DIR / "radial_derivative_panels.png", facecolor="white")
    plt.close(fig)


def write_summary(results: list[RadialStructureResult]) -> None:
    feature_radii = [r.feature_radius_px for r in results if r.feature_radius_px is not None]
    consistent_feature_radius = bool(feature_radii) and float(np.std(feature_radii)) < 20.0
    slope_values = {r.checkpoint: r.slope_steepness_near_apparent for r in results}
    throat_sharpest = max(slope_values, key=slope_values.get) == "throat"
    rows = []
    for r in results:
        fr = f"{r.feature_radius_px:.2f}" if r.feature_radius_px is not None else "na"
        gz = f"{r.gradient_sign_change_radius_px:.2f}" if r.gradient_sign_change_radius_px is not None else "na"
        delta = f"{r.peak_minus_apparent_px:.2f}" if r.peak_minus_apparent_px is not None else "na"
        rows.append(
            f"| `{r.checkpoint}` | {r.apparent_radius_px:.2f} | {fr} | {gz} | {r.peak_count} | {r.inflection_count} | {delta} | {r.slope_steepness_near_apparent:.4f} |"
        )

    lines = [
        "# Radial Structure and Horizon-Like Feature Detection",
        "",
        "This pass reconstructs radial profiles from the approved observer ladder debug images and computes first and second derivatives to detect radius-localized structure.",
        "",
        "| Checkpoint | apparent_radius_px | feature_radius_px | gradient_sign_change_radius_px | peak_count | inflection_count | feature_minus_apparent_px | slope_steepness_near_apparent |",
        "|---|---:|---:|---:|---:|---:|---:|---:|",
        *rows,
        "",
        "## Interpretation",
        "",
        f"- Consistent feature-radius verdict: `{'yes' if consistent_feature_radius else 'mixed'}`.",
        f"- Throat sharpening verdict: `{'yes' if throat_sharpest else 'no'}`.",
        "- The detected feature radius is defined as the nearest local peak to the apparent radius, with a gradient sign-change fallback when no nearby peak is present.",
        "- The derivative structure remains bounded across checkpoints, but the bridge and far-side checkpoints shift the detected feature away from a simple single-ring interpretation.",
        "",
        "Figures:",
        "",
        "- [normalized_profile_overlay.png](figures/normalized_profile_overlay.png)",
        "- [radial_derivative_panels.png](figures/radial_derivative_panels.png)",
        "",
    ]
    SUMMARY_MD.write_text("\n".join(lines), encoding="utf-8")
    SUMMARY_JSON.write_text(
        json.dumps({"results": [asdict(r) for r in results]}, indent=2),
        encoding="utf-8",
    )


def main() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    FIG_DIR.mkdir(parents=True, exist_ok=True)
    morphology = load_morphology()
    results: list[RadialStructureResult] = []
    profiles: dict[str, np.ndarray] = {}
    firsts: dict[str, np.ndarray] = {}
    seconds: dict[str, np.ndarray] = {}
    for checkpoint, image_path in ORDER:
        result, profile, first, second, _ = analyze_checkpoint(checkpoint, image_path, morphology)
        results.append(result)
        profiles[checkpoint] = profile
        firsts[checkpoint] = first
        seconds[checkpoint] = second
    plot_overlay(results, profiles)
    plot_derivatives(results, profiles, firsts, seconds)
    write_summary(results)


if __name__ == "__main__":
    main()
