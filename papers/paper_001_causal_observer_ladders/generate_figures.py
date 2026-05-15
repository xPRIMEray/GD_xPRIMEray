from __future__ import annotations

import json
from pathlib import Path

import matplotlib.pyplot as plt


ROOT = Path(__file__).resolve().parents[3]
SUMMARY_PATH = ROOT / "output/fixture_runs/fixture_011_wormhole_checkpoint_sequence/2026-04-20T22-26-39/checkpoint_sequence_summary.json"
FIG_DIR = Path(__file__).resolve().parent / "figures"

CHECKPOINT_ORDER = [
    "mouth",
    "mouth_to_throat_approach",
    "throat",
    "post_throat_backstep_01",
    "post_throat_exit_approach",
    "exit_lookback",
]

DISPLAY_LABELS = {
    "mouth": "Mouth",
    "mouth_to_throat_approach": "Mouth→Throat",
    "throat": "Throat",
    "post_throat_backstep_01": "Bridge",
    "post_throat_exit_approach": "Post-Exit",
    "exit_lookback": "Exit Look-Back",
}


def load_checkpoints() -> list[dict]:
    data = json.loads(SUMMARY_PATH.read_text(encoding="utf-8"))
    by_name = {cp["Name"]: cp for cp in data["Checkpoints"]}
    return [by_name[name] for name in CHECKPOINT_ORDER]


def portal_hit_density(cp: dict) -> float:
    return cp["PortalHitPixels"] / cp["TotalPixels"]


def throat_event_density(cp: dict) -> float:
    return cp["ThroatEventPixels"] / cp["TotalPixels"]


def crossings_per_pixel(cp: dict) -> float:
    return cp["BoundaryCrossingsTotal"] / cp["TotalPixels"]


def segments_per_crossing(cp: dict) -> float:
    crossings = cp["BoundaryCrossingsTotal"]
    if crossings <= 0:
        return 0.0
    return cp["AdaptiveDiagnostics"]["TotalEmittedRaySegCount"] / crossings


def opl_mean(cp: dict) -> float:
    return cp["OpticalPathLengthMean"]


def style_axes(ax: plt.Axes, title: str, ylabel: str) -> None:
    ax.set_facecolor("white")
    ax.set_title(title, fontsize=14, pad=12)
    ax.set_xlabel("Observer checkpoint", fontsize=11)
    ax.set_ylabel(ylabel, fontsize=11)
    ax.grid(True, axis="y", color="#d8dde6", linewidth=0.9)
    ax.grid(False, axis="x")
    for spine in ("top", "right"):
        ax.spines[spine].set_visible(False)
    ax.spines["left"].set_color("#7d8696")
    ax.spines["bottom"].set_color("#7d8696")
    ax.tick_params(axis="x", rotation=22, labelsize=10)
    ax.tick_params(axis="y", labelsize=10)


def make_plot(filename: str, title: str, ylabel: str, values: list[float], color: str) -> None:
    labels = [DISPLAY_LABELS[name] for name in CHECKPOINT_ORDER]
    xs = list(range(len(labels)))

    fig, ax = plt.subplots(figsize=(8.8, 4.8), dpi=220, constrained_layout=True)
    fig.patch.set_facecolor("white")
    style_axes(ax, title, ylabel)

    ax.plot(
        xs,
        values,
        color=color,
        linewidth=2.4,
        marker="o",
        markersize=6.5,
        markerfacecolor="white",
        markeredgecolor=color,
        markeredgewidth=2.0,
    )
    ax.set_xticks(xs, labels)
    ax.set_xlim(-0.2, len(xs) - 0.8)
    ymin = 0.0 if min(values) >= 0 else min(values) * 1.08
    ymax = max(values) * 1.12 if max(values) > 0 else 1.0
    if ymax <= ymin:
        ymax = ymin + 1.0
    ax.set_ylim(ymin, ymax)

    for x, y in zip(xs, values):
        label = f"{y:.3f}" if abs(y) < 10 else f"{y:.1f}"
        ax.annotate(
            label,
            (x, y),
            textcoords="offset points",
            xytext=(0, 8),
            ha="center",
            fontsize=8.5,
            color="#30343b",
        )

    FIG_DIR.mkdir(parents=True, exist_ok=True)
    fig.savefig(FIG_DIR / filename, facecolor="white")
    plt.close(fig)


def main() -> None:
    checkpoints = load_checkpoints()
    make_plot(
        "portal_hit_density_vs_checkpoint.png",
        "Portal-hit density vs checkpoint",
        "Portal-hit density",
        [portal_hit_density(cp) for cp in checkpoints],
        "#1f77b4",
    )
    make_plot(
        "throat_event_density_vs_checkpoint.png",
        "Throat-event density vs checkpoint",
        "Throat-event density",
        [throat_event_density(cp) for cp in checkpoints],
        "#d62728",
    )
    make_plot(
        "crossings_per_pixel_vs_checkpoint.png",
        "Crossings per pixel vs checkpoint",
        "Crossings per pixel",
        [crossings_per_pixel(cp) for cp in checkpoints],
        "#ff7f0e",
    )
    make_plot(
        "segments_per_crossing_vs_checkpoint.png",
        "Segments per crossing vs checkpoint",
        "Segments per crossing",
        [segments_per_crossing(cp) for cp in checkpoints],
        "#2ca02c",
    )
    make_plot(
        "opl_mean_vs_checkpoint.png",
        "Optical path length mean vs checkpoint",
        "OPL mean",
        [opl_mean(cp) for cp in checkpoints],
        "#9467bd",
    )


if __name__ == "__main__":
    main()
