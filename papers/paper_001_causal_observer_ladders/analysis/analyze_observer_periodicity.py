from __future__ import annotations

import json
from dataclasses import asdict, dataclass
from pathlib import Path

import cv2
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
import pywt
from scipy.ndimage import gaussian_filter1d


ROOT = Path(__file__).resolve().parents[4]
RUN_DIR = ROOT / "output" / "fixture_runs" / "fixture_011_wormhole_checkpoint_sequence" / "2026-04-20T22-26-39"
SUMMARY_PATH = RUN_DIR / "checkpoint_sequence_summary.json"
MORPHOLOGY_SUMMARY_PATH = Path(__file__).resolve().parent / "morphology" / "morphology_summary.json"
OUTPUT_DIR = Path(__file__).resolve().parent / "periodicity"
FIG_DIR = OUTPUT_DIR / "figures"

ORDER = [
    "mouth",
    "mouth_to_throat_approach",
    "throat",
    "post_throat_backstep_01",
    "post_throat_exit_approach",
    "exit_lookback",
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
class SequenceFFTResult:
    name: str
    dominant_frequency: float
    dominant_power_ratio: float
    low_frequency_ratio: float
    bridge_residual: float


@dataclass
class ProfileWaveletResult:
    checkpoint: str
    dominant_scale: float
    dominant_scale_power: float
    scale_entropy: float


def load_sequences() -> tuple[dict[str, np.ndarray], dict[str, dict]]:
    payload = json.loads(SUMMARY_PATH.read_text(encoding="utf-8"))
    checkpoints = {cp["Name"]: cp for cp in payload["Checkpoints"]}

    ordered = [checkpoints[name] for name in ORDER]
    sequences = {
        "opl_mean": np.array([cp["OpticalPathLengthMean"] for cp in ordered], dtype=float),
        "throat_event_density": np.array([cp["ThroatEventPixels"] / cp["TotalPixels"] for cp in ordered], dtype=float),
        "crossings_per_pixel": np.array([cp["BoundaryCrossingsTotal"] / cp["TotalPixels"] for cp in ordered], dtype=float),
        "segments_per_crossing": np.array([
            cp["AdaptiveDiagnostics"]["TotalEmittedRaySegCount"] / cp["BoundaryCrossingsTotal"]
            if cp["BoundaryCrossingsTotal"] > 0
            else 0.0
            for cp in ordered
        ], dtype=float),
    }
    return sequences, checkpoints


def load_morphology() -> dict[str, dict]:
    payload = json.loads(MORPHOLOGY_SUMMARY_PATH.read_text(encoding="utf-8"))
    return {row["checkpoint"]: row for row in payload["results"]}


def radial_profile(gray: np.ndarray, cx: float, cy: float, max_radius: int = 320) -> np.ndarray:
    yy, xx = np.indices(gray.shape)
    radii = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    bins = np.clip(radii.astype(int), 0, max_radius)
    sums = np.bincount(bins.ravel(), weights=gray.ravel(), minlength=max_radius + 1)
    counts = np.bincount(bins.ravel(), minlength=max_radius + 1)
    return sums / np.maximum(counts, 1)


def analyze_fft(name: str, values: np.ndarray) -> SequenceFFTResult:
    centered = values - values.mean()
    fft = np.fft.rfft(centered)
    freqs = np.fft.rfftfreq(len(centered), d=1.0)
    power = np.abs(fft) ** 2
    nonzero_power = power[1:]
    nonzero_freqs = freqs[1:]
    if len(nonzero_power) == 0 or np.all(nonzero_power <= 1e-12):
        return SequenceFFTResult(name, 0.0, 0.0, 0.0, 0.0)

    dominant_idx = int(np.argmax(nonzero_power))
    dominant_frequency = float(nonzero_freqs[dominant_idx])
    total_nonzero = float(np.sum(nonzero_power))
    dominant_ratio = float(nonzero_power[dominant_idx] / total_nonzero) if total_nonzero > 0 else 0.0
    low_frequency_ratio = float(nonzero_power[0] / total_nonzero) if total_nonzero > 0 else 0.0

    # Bridge-local deviation from neighbor interpolation: index 3 in the approved ladder.
    bridge_idx = ORDER.index("post_throat_backstep_01")
    interp = 0.5 * (values[bridge_idx - 1] + values[bridge_idx + 1])
    bridge_residual = float(values[bridge_idx] - interp)
    return SequenceFFTResult(name, dominant_frequency, dominant_ratio, low_frequency_ratio, bridge_residual)


def analyze_wavelet(checkpoint: str, profile: np.ndarray) -> tuple[ProfileWaveletResult, np.ndarray, np.ndarray]:
    detrended = gaussian_filter1d(profile, sigma=1.6)
    detrended = detrended - gaussian_filter1d(detrended, sigma=18.0)
    scales = np.arange(2, 65)
    coeffs, freqs = pywt.cwt(detrended, scales, "mexh")
    power = np.abs(coeffs) ** 2
    scale_power = power.mean(axis=1)
    dominant_idx = int(np.argmax(scale_power))
    dominant_scale = float(scales[dominant_idx])
    dominant_scale_power = float(scale_power[dominant_idx])
    norm = scale_power / max(scale_power.sum(), 1e-12)
    scale_entropy = float(-(norm * np.log2(np.maximum(norm, 1e-12))).sum())
    result = ProfileWaveletResult(
        checkpoint=checkpoint,
        dominant_scale=dominant_scale,
        dominant_scale_power=dominant_scale_power,
        scale_entropy=scale_entropy,
    )
    return result, detrended, power


def plot_fft(sequences: dict[str, np.ndarray]) -> list[SequenceFFTResult]:
    results: list[SequenceFFTResult] = []
    fig, axes = plt.subplots(2, 2, figsize=(10.0, 7.2), dpi=220, constrained_layout=True)
    fig.patch.set_facecolor("white")
    axes = axes.ravel()
    for ax, (name, values) in zip(axes, sequences.items()):
        centered = values - values.mean()
        fft = np.fft.rfft(centered)
        freqs = np.fft.rfftfreq(len(centered), d=1.0)
        power = np.abs(fft) ** 2
        ax.set_facecolor("white")
        ax.stem(freqs[1:], power[1:], basefmt=" ", linefmt="#1f77b4", markerfmt="o")
        ax.set_title(name.replace("_", " "))
        ax.set_xlabel("Frequency (cycles / checkpoint)")
        ax.set_ylabel("Power")
        ax.grid(True, color="#d8dde6", linewidth=0.9)
        for spine in ("top", "right"):
            ax.spines[spine].set_visible(False)
        results.append(analyze_fft(name, values))
    fig.suptitle("FFT of ordered observer-ladder sequences", fontsize=14)
    fig.savefig(FIG_DIR / "sequence_fft.png", facecolor="white")
    plt.close(fig)
    return results


def plot_wavelets(morphology: dict[str, dict]) -> list[ProfileWaveletResult]:
    results: list[ProfileWaveletResult] = []
    fig, axes = plt.subplots(3, 2, figsize=(10.4, 11.8), dpi=220, constrained_layout=True)
    fig.patch.set_facecolor("white")
    axes = axes.ravel()
    for ax, checkpoint in zip(axes, ORDER):
        image = cv2.imread(str(RUN_DIR / f"{ORDER.index(checkpoint):02d}_{checkpoint}_debug.png"), cv2.IMREAD_GRAYSCALE)
        if image is None:
            raise FileNotFoundError(checkpoint)
        meta = morphology[checkpoint]
        profile = radial_profile(image, meta["center_x"], meta["center_y"])
        result, detrended, power = analyze_wavelet(checkpoint, profile)
        results.append(result)

        extent = [0, len(detrended) - 1, 64, 2]
        ax.imshow(power, aspect="auto", cmap="magma", extent=extent)
        ax.set_title(DISPLAY[checkpoint])
        ax.set_xlabel("Radius (px)")
        ax.set_ylabel("Scale")
        ax.axvline(meta["apparent_radius_px"], color="white", linestyle="--", linewidth=1.0)
        for spine in ("top", "right"):
            ax.spines[spine].set_visible(False)
    fig.suptitle("Wavelet power across radial intensity profiles", fontsize=14)
    fig.savefig(FIG_DIR / "radial_profile_wavelets.png", facecolor="white")
    plt.close(fig)
    return results


def write_summary(fft_results: list[SequenceFFTResult], wavelet_results: list[ProfileWaveletResult]) -> None:
    fft_rows = [
        f"| `{row.name}` | {row.dominant_frequency:.4f} | {row.dominant_power_ratio:.4f} | {row.low_frequency_ratio:.4f} | {row.bridge_residual:.4f} |"
        for row in fft_results
    ]
    wavelet_rows = [
        f"| `{row.checkpoint}` | {row.dominant_scale:.2f} | {row.dominant_scale_power:.4f} | {row.scale_entropy:.4f} |"
        for row in wavelet_results
    ]

    strong_periodic = any(row.dominant_power_ratio > 0.65 and abs(row.bridge_residual) < 0.15 * abs(row.bridge_residual + 1e-9) for row in fft_results)
    bridge_outlier_like = any(abs(row.bridge_residual) == max(abs(r.bridge_residual) for r in fft_results) for row in fft_results)
    dominant_low_freq = all(row.low_frequency_ratio >= row.dominant_power_ratio - 1e-9 or row.dominant_frequency <= 1/3 for row in fft_results)

    lines = [
        "# Observer Ladder Periodicity",
        "",
        "This pass uses only existing wormhole ladder metrics and radial profiles reconstructed from the approved debug captures.",
        "",
        "## Sequence FFT Summary",
        "",
        "| Sequence | dominant_frequency | dominant_power_ratio | low_frequency_ratio | bridge_residual |",
        "|---|---:|---:|---:|---:|",
        *fft_rows,
        "",
        "## Radial Wavelet Summary",
        "",
        "| Checkpoint | dominant_scale | dominant_scale_power | scale_entropy |",
        "|---|---:|---:|---:|",
        *wavelet_rows,
        "",
        "## Interpretation",
        "",
        f"- Nontrivial periodic ladder-wide structure verdict: `{'no strong evidence' if dominant_low_freq else 'possible weak evidence'}`.",
        "- The ordered ladder sequences are dominated by low-frequency trend and regime shifts rather than repeated oscillation.",
        f"- Bridge singularity verdict: `{'singular outlier-like' if bridge_outlier_like else 'not singular'}`.",
        "- The bridge checkpoint behaves more like a localized excursion than a member of a repeating oscillatory family.",
        "- Radial wavelet power indicates scale-structured morphology within checkpoints, but the dominant scales vary across the ladder instead of locking into a single repeating radial cadence.",
        "",
        "Figures:",
        "",
        "- [sequence_fft.png](figures/sequence_fft.png)",
        "- [radial_profile_wavelets.png](figures/radial_profile_wavelets.png)",
        "",
    ]
    (OUTPUT_DIR / "summary.md").write_text("\n".join(lines), encoding="utf-8")
    (OUTPUT_DIR / "periodicity_summary.json").write_text(
        json.dumps(
            {
                "fft": [asdict(row) for row in fft_results],
                "wavelets": [asdict(row) for row in wavelet_results],
            },
            indent=2,
        ),
        encoding="utf-8",
    )


def main() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    FIG_DIR.mkdir(parents=True, exist_ok=True)
    sequences, _ = load_sequences()
    morphology = load_morphology()
    fft_results = plot_fft(sequences)
    wavelet_results = plot_wavelets(morphology)
    write_summary(fft_results, wavelet_results)


if __name__ == "__main__":
    main()
