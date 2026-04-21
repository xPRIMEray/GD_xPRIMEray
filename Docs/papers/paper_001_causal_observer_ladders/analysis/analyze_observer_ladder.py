from __future__ import annotations

import json
from dataclasses import asdict, dataclass
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np


ROOT = Path(__file__).resolve().parents[4]
PAPER_DIR = ROOT / "Docs" / "papers" / "paper_001_causal_observer_ladders"
ANALYSIS_DIR = PAPER_DIR / "analysis"
FIG_DIR = ANALYSIS_DIR / "figures"
SUMMARY_PATH = ROOT / "output" / "fixture_runs" / "fixture_011_wormhole_checkpoint_sequence" / "2026-04-20T22-26-39" / "checkpoint_sequence_summary.json"

ORDER = [
    "mouth",
    "mouth_to_throat_approach",
    "throat",
    "post_throat_backstep_01",
    "post_throat_exit_approach",
    "exit_lookback",
]

DISPLAY = {
    "mouth": "mouth",
    "mouth_to_throat_approach": "mouth_to_throat_approach",
    "throat": "throat",
    "post_throat_backstep_01": "post_throat_backstep_01",
    "post_throat_exit_approach": "post_throat_exit_approach",
    "exit_lookback": "exit_lookback",
}


@dataclass
class CheckpointMetrics:
    checkpoint: str
    portal_hit_density: float
    throat_event_density: float
    crossings_per_pixel: float
    segments_per_crossing: float
    average_segments_per_ray: float
    opl_mean: float
    opl_max: float
    run_verified: bool


def load_metrics() -> list[CheckpointMetrics]:
    data = json.loads(SUMMARY_PATH.read_text(encoding="utf-8"))
    by_name = {cp["Name"]: cp for cp in data["Checkpoints"]}
    metrics: list[CheckpointMetrics] = []
    for name in ORDER:
        cp = by_name[name]
        total = cp["TotalPixels"]
        crossings = cp["BoundaryCrossingsTotal"]
        segs = cp["AdaptiveDiagnostics"]["TotalEmittedRaySegCount"]
        metrics.append(
            CheckpointMetrics(
                checkpoint=name,
                portal_hit_density=cp["PortalHitPixels"] / total,
                throat_event_density=cp["ThroatEventPixels"] / total,
                crossings_per_pixel=crossings / total,
                segments_per_crossing=(segs / crossings) if crossings > 0 else 0.0,
                average_segments_per_ray=cp["AdaptiveDiagnostics"]["AverageSegmentsPerRay"],
                opl_mean=cp["OpticalPathLengthMean"],
                opl_max=cp["OpticalPathLengthMax"],
                run_verified=bool(cp["RunVerified"]),
            )
        )
    return metrics


def deterministic_kmeans(features: np.ndarray, k: int) -> tuple[np.ndarray, np.ndarray]:
    sums = features.sum(axis=1)
    seed_indices = [int(np.argmin(sums)), int(np.argmax(sums))]
    remaining = [i for i in range(len(features)) if i not in seed_indices]
    if k > 2:
        center = features.mean(axis=0)
        mid = min(remaining, key=lambda idx: float(np.linalg.norm(features[idx] - center)))
        seed_indices.append(mid)
    centroids = features[seed_indices[:k]].copy()

    labels = np.zeros(len(features), dtype=int)
    for _ in range(32):
        distances = np.linalg.norm(features[:, None, :] - centroids[None, :, :], axis=2)
        new_labels = distances.argmin(axis=1)
        if np.array_equal(new_labels, labels):
            break
        labels = new_labels
        for c in range(k):
            members = features[labels == c]
            if len(members) > 0:
                centroids[c] = members.mean(axis=0)
    return labels, centroids


def remap_cluster_labels(labels: np.ndarray, metrics: list[CheckpointMetrics]) -> tuple[list[str], dict[int, str]]:
    cluster_rows: dict[int, list[CheckpointMetrics]] = {}
    for label, row in zip(labels, metrics):
        cluster_rows.setdefault(int(label), []).append(row)

    mapping: dict[int, str] = {}
    for cluster_id, rows in cluster_rows.items():
        mean_spc = float(np.mean([r.segments_per_crossing for r in rows]))
        mean_throat = float(np.mean([r.throat_event_density for r in rows]))
        if len(rows) == 1 and mean_spc == max(
            float(np.mean([r.segments_per_crossing for r in rs])) for rs in cluster_rows.values()
        ):
            mapping[cluster_id] = "bridge"
        elif mean_throat == max(float(np.mean([r.throat_event_density for r in rs])) for rs in cluster_rows.values()):
            mapping[cluster_id] = "far-side"
        else:
            mapping[cluster_id] = "near-side/throat"
    return [mapping[int(label)] for label in labels], mapping


def robust_z_scores(values: np.ndarray) -> np.ndarray:
    median = np.median(values, axis=0)
    mad = np.median(np.abs(values - median), axis=0)
    mad = np.where(mad < 1e-9, 1.0, mad)
    return 0.6745 * (values - median) / mad


def compute_bridge_anomaly_scores(metrics: list[CheckpointMetrics]) -> tuple[np.ndarray, np.ndarray]:
    feature_matrix = np.array([
        [
            m.portal_hit_density,
            m.throat_event_density,
            m.crossings_per_pixel,
            m.segments_per_crossing,
            m.opl_mean,
        ]
        for m in metrics
    ], dtype=float)
    z = robust_z_scores(feature_matrix)
    overall = np.sqrt(np.mean(z ** 2, axis=1))
    bridge_signature = (
        np.maximum(-z[:, 0], 0.0)
        + np.maximum(-z[:, 1], 0.0)
        + np.maximum(-z[:, 2], 0.0)
        + np.maximum(z[:, 3], 0.0)
        + np.maximum(-z[:, 4], 0.0)
    ) / 5.0
    return overall, bridge_signature


def write_json(path: Path, payload: dict) -> None:
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def plot_regime_clusters(metrics: list[CheckpointMetrics], labels: list[str]) -> None:
    x = [m.crossings_per_pixel for m in metrics]
    y = [m.segments_per_crossing for m in metrics]
    color_map = {
        "near-side/throat": "#1f77b4",
        "bridge": "#d62728",
        "far-side": "#2ca02c",
    }
    plt.figure(figsize=(7.4, 5.2), dpi=220)
    ax = plt.gca()
    ax.set_facecolor("white")
    for spine in ("top", "right"):
        ax.spines[spine].set_visible(False)
    ax.grid(True, color="#d8dde6", linewidth=0.9)
    for metric, label in zip(metrics, labels):
        ax.scatter(
            metric.crossings_per_pixel,
            metric.segments_per_crossing,
            s=90,
            color=color_map[label],
            edgecolors="white",
            linewidths=1.4,
            zorder=3,
        )
        ax.annotate(
            DISPLAY[metric.checkpoint],
            (metric.crossings_per_pixel, metric.segments_per_crossing),
            textcoords="offset points",
            xytext=(6, 6),
            fontsize=8,
        )
    ax.set_title("Regime clustering of the observer ladder")
    ax.set_xlabel("Crossings per pixel")
    ax.set_ylabel("Segments per crossing")
    handles = [
        plt.Line2D([0], [0], marker="o", color="w", label=label, markerfacecolor=color, markersize=8)
        for label, color in color_map.items()
    ]
    ax.legend(handles=handles, frameon=False)
    plt.tight_layout()
    plt.savefig(FIG_DIR / "regime_clustering.png", facecolor="white")
    plt.close()


def plot_bridge_anomaly(metrics: list[CheckpointMetrics], overall: np.ndarray, bridge: np.ndarray) -> None:
    labels = [DISPLAY[m.checkpoint] for m in metrics]
    xs = np.arange(len(labels))
    width = 0.36
    plt.figure(figsize=(8.6, 5.0), dpi=220)
    ax = plt.gca()
    ax.set_facecolor("white")
    for spine in ("top", "right"):
        ax.spines[spine].set_visible(False)
    ax.grid(True, axis="y", color="#d8dde6", linewidth=0.9)
    ax.bar(xs - width / 2, overall, width=width, color="#7f7f7f", label="overall anomaly score")
    ax.bar(xs + width / 2, bridge, width=width, color="#d62728", label="bridge signature score")
    ax.set_title("Bridge anomaly scoring across observer checkpoints")
    ax.set_xlabel("Observer checkpoint")
    ax.set_ylabel("Score")
    ax.set_xticks(xs)
    ax.set_xticklabels(labels, rotation=22, ha="right")
    ax.legend(frameon=False)
    plt.tight_layout()
    plt.savefig(FIG_DIR / "bridge_anomaly_scores.png", facecolor="white")
    plt.close()


def write_markdown(metrics: list[CheckpointMetrics], cluster_labels: list[str], overall: np.ndarray, bridge: np.ndarray) -> None:
    regime_rows = []
    for row, cluster in zip(metrics, cluster_labels):
        regime_rows.append(
            f"| `{row.checkpoint}` | {cluster} | {row.portal_hit_density:.4f} | {row.throat_event_density:.4f} | "
            f"{row.crossings_per_pixel:.4f} | {row.segments_per_crossing:.4f} | {row.opl_mean:.4f} |"
        )

    bridge_rank = sorted(zip(metrics, overall, bridge), key=lambda item: item[2], reverse=True)
    bridge_rows = []
    for row, overall_score, bridge_score in bridge_rank:
        bridge_rows.append(
            f"| `{row.checkpoint}` | {overall_score:.4f} | {bridge_score:.4f} | {row.segments_per_crossing:.4f} | "
            f"{row.throat_event_density:.4f} | {row.opl_mean:.4f} |"
        )

    regime_md = "\n".join([
        "# Regime Clustering",
        "",
        "This first-pass clustering uses standardized ladder metrics derived from the frozen checkpoint sequence summary. "
        "Inputs are portal-hit density, throat-event density, crossings per pixel, segments per crossing, and OPL mean.",
        "",
        "## Cluster Table",
        "",
        "| Checkpoint | Cluster | portal_hit_density | throat_event_density | crossings_per_pixel | segments_per_crossing | OPL mean |",
        "|---|---|---:|---:|---:|---:|---:|",
        *regime_rows,
        "",
        "## Summary",
        "",
        "- The clustering separates a `near-side/throat` family, a singleton `bridge` state, and a `far-side` family.",
        "- The bridge cluster is isolated by low interaction density, low OPL mean, and extremely high segments per crossing.",
        "- The far-side cluster groups the two highest-interaction checkpoints despite their different portal coverage profiles.",
        "",
        "Figure: [regime_clustering.png](figures/regime_clustering.png)",
        "",
    ])
    (ANALYSIS_DIR / "regime_clustering.md").write_text(regime_md, encoding="utf-8")

    bridge_md = "\n".join([
        "# Bridge Anomaly Scoring",
        "",
        "Two artifact-only scores are reported below:",
        "",
        "- `overall anomaly score`: root-mean-square robust z-score across the five characterization features",
        "- `bridge signature score`: a directed score favoring low densities, low OPL mean, and high segments per crossing",
        "",
        "## Score Table",
        "",
        "| Checkpoint | Overall anomaly | Bridge signature | segments_per_crossing | throat_event_density | OPL mean |",
        "|---|---:|---:|---:|---:|---:|",
        *bridge_rows,
        "",
        "## Summary",
        "",
        f"- The strongest bridge-signature checkpoint is `{bridge_rank[0][0].checkpoint}`.",
        f"- The second-highest bridge-signature checkpoint is `{bridge_rank[1][0].checkpoint}`.",
        "- The bridge score is intentionally asymmetric: it rewards sparse, expensive transport rather than generic extremeness.",
        "",
        "Figure: [bridge_anomaly_scores.png](figures/bridge_anomaly_scores.png)",
        "",
    ])
    (ANALYSIS_DIR / "bridge_anomaly_scoring.md").write_text(bridge_md, encoding="utf-8")


def main() -> None:
    ANALYSIS_DIR.mkdir(parents=True, exist_ok=True)
    FIG_DIR.mkdir(parents=True, exist_ok=True)

    metrics = load_metrics()
    feature_matrix = np.array([
        [
            m.portal_hit_density,
            m.throat_event_density,
            m.crossings_per_pixel,
            m.segments_per_crossing,
            m.opl_mean,
        ]
        for m in metrics
    ], dtype=float)
    standardized = (feature_matrix - feature_matrix.mean(axis=0)) / np.where(feature_matrix.std(axis=0) < 1e-9, 1.0, feature_matrix.std(axis=0))
    labels, centroids = deterministic_kmeans(standardized, k=3)
    cluster_labels, cluster_name_map = remap_cluster_labels(labels, metrics)
    overall_scores, bridge_scores = compute_bridge_anomaly_scores(metrics)

    write_json(
        ANALYSIS_DIR / "derived_metrics.json",
        {
            "source": str(SUMMARY_PATH),
            "checkpoints": [asdict(m) for m in metrics],
        },
    )
    write_json(
        ANALYSIS_DIR / "regime_clustering.json",
        {
            "clusters": [
                {
                    "checkpoint": row.checkpoint,
                    "cluster_id": int(label),
                    "cluster_label": cluster,
                }
                for row, label, cluster in zip(metrics, labels, cluster_labels)
            ],
            "cluster_name_map": {str(k): v for k, v in cluster_name_map.items()},
            "centroids_standardized": centroids.tolist(),
        },
    )
    write_json(
        ANALYSIS_DIR / "bridge_anomaly_scores.json",
        {
            "scores": [
                {
                    "checkpoint": row.checkpoint,
                    "overall_anomaly_score": float(overall),
                    "bridge_signature_score": float(bridge),
                }
                for row, overall, bridge in zip(metrics, overall_scores, bridge_scores)
            ]
        },
    )

    plot_regime_clusters(metrics, cluster_labels)
    plot_bridge_anomaly(metrics, overall_scores, bridge_scores)
    write_markdown(metrics, cluster_labels, overall_scores, bridge_scores)


if __name__ == "__main__":
    main()
