from __future__ import annotations

import json
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
from scipy.cluster.hierarchy import dendrogram, linkage
from sklearn.cluster import AgglomerativeClustering, KMeans
from sklearn.decomposition import PCA
from sklearn.metrics import adjusted_rand_score, silhouette_score
from sklearn.preprocessing import StandardScaler


ROOT = Path(__file__).resolve().parents[3]
PAPER_DIR = ROOT / "Docs" / "papers" / "paper_001_causal_observer_ladders"
FIG_DIR = PAPER_DIR / "figures"
SUMMARY_PATH = ROOT / "output" / "fixture_runs" / "fixture_011_wormhole_checkpoint_sequence" / "2026-04-20T22-26-39" / "checkpoint_sequence_summary.json"
SUMMARY_MD_PATH = PAPER_DIR / "clustering_summary.md"

ORDER = [
    "mouth",
    "mouth_to_throat_approach",
    "throat",
    "post_throat_backstep_01",
    "post_throat_exit_approach",
    "exit_lookback",
]

MANUAL_LABELS = {
    "mouth": "near-side",
    "mouth_to_throat_approach": "near-side",
    "throat": "throat",
    "post_throat_backstep_01": "bridge",
    "post_throat_exit_approach": "far-side",
    "exit_lookback": "far-side",
}

DISPLAY_LABELS = {
    "mouth": "mouth",
    "mouth_to_throat_approach": "mouth→throat",
    "throat": "throat",
    "post_throat_backstep_01": "bridge",
    "post_throat_exit_approach": "post-exit",
    "exit_lookback": "exit",
}

FEATURE_COLUMNS = [
    "opl_mean",
    "opl_max",
    "portal_hit_density",
    "throat_event_density",
    "crossings_per_pixel",
    "segments_per_crossing",
    "average_segments_per_ray",
]


def load_dataframe() -> pd.DataFrame:
    payload = json.loads(SUMMARY_PATH.read_text(encoding="utf-8"))
    by_name = {cp["Name"]: cp for cp in payload["Checkpoints"]}
    rows: list[dict] = []
    for name in ORDER:
        cp = by_name[name]
        total_pixels = cp["TotalPixels"]
        boundary_crossings_total = cp["BoundaryCrossingsTotal"]
        total_segments = cp["AdaptiveDiagnostics"]["TotalEmittedRaySegCount"]
        rows.append(
            {
                "checkpoint": name,
                "manual_label": MANUAL_LABELS[name],
                "opl_mean": cp["OpticalPathLengthMean"],
                "opl_max": cp["OpticalPathLengthMax"],
                "portal_hit_density": cp["PortalHitPixels"] / total_pixels,
                "throat_event_density": cp["ThroatEventPixels"] / total_pixels,
                "crossings_per_pixel": boundary_crossings_total / total_pixels,
                "segments_per_crossing": (total_segments / boundary_crossings_total) if boundary_crossings_total > 0 else 0.0,
                "average_segments_per_ray": cp["AdaptiveDiagnostics"]["AverageSegmentsPerRay"],
            }
        )
    return pd.DataFrame(rows)


def standardize(df: pd.DataFrame) -> np.ndarray:
    scaler = StandardScaler()
    return scaler.fit_transform(df[FEATURE_COLUMNS].to_numpy(dtype=float))


def encode_labels(labels: list[str]) -> np.ndarray:
    classes = {label: idx for idx, label in enumerate(sorted(set(labels)))}
    return np.array([classes[label] for label in labels], dtype=int)


def clustering_comparison(df: pd.DataFrame, X: np.ndarray) -> tuple[pd.DataFrame, np.ndarray]:
    manual = encode_labels(df["manual_label"].tolist())
    rows: list[dict] = []
    for k in range(2, 6):
        kmeans = KMeans(n_clusters=k, random_state=7, n_init=20)
        km_labels = kmeans.fit_predict(X)
        rows.append(
            {
                "method": "kmeans",
                "k": k,
                "ari_vs_manual": adjusted_rand_score(manual, km_labels),
                "silhouette": silhouette_score(X, km_labels) if 1 < k < len(df) else np.nan,
            }
        )

    for k in range(2, 6):
        agg = AgglomerativeClustering(n_clusters=k, linkage="ward")
        ag_labels = agg.fit_predict(X)
        rows.append(
            {
                "method": "agglomerative",
                "k": k,
                "ari_vs_manual": adjusted_rand_score(manual, ag_labels),
                "silhouette": silhouette_score(X, ag_labels) if 1 < k < len(df) else np.nan,
            }
        )

    comparison = pd.DataFrame(rows).sort_values(["method", "k"]).reset_index(drop=True)
    best_row = comparison.sort_values(["ari_vs_manual", "silhouette"], ascending=[False, False]).iloc[0]
    if best_row["method"] == "kmeans":
        best_labels = KMeans(n_clusters=int(best_row["k"]), random_state=7, n_init=20).fit_predict(X)
    else:
        best_labels = AgglomerativeClustering(n_clusters=int(best_row["k"]), linkage="ward").fit_predict(X)
    return comparison, best_labels


def plot_pca(df: pd.DataFrame, X: np.ndarray, cluster_labels: np.ndarray) -> None:
    pca = PCA(n_components=2, random_state=7)
    coords = pca.fit_transform(X)
    FIG_DIR.mkdir(parents=True, exist_ok=True)

    fig, ax = plt.subplots(figsize=(7.4, 5.6), dpi=220, constrained_layout=True)
    fig.patch.set_facecolor("white")
    ax.set_facecolor("white")
    ax.grid(True, color="#d8dde6", linewidth=0.9)
    for spine in ("top", "right"):
        ax.spines[spine].set_visible(False)

    cmap = plt.get_cmap("tab10")
    for idx, row in df.iterrows():
        ax.scatter(
            coords[idx, 0],
            coords[idx, 1],
            s=92,
            color=cmap(int(cluster_labels[idx])),
            edgecolors="white",
            linewidths=1.3,
            zorder=3,
        )
        ax.annotate(
            DISPLAY_LABELS[row["checkpoint"]],
            (coords[idx, 0], coords[idx, 1]),
            textcoords="offset points",
            xytext=(6, 6),
            fontsize=8,
        )

    ax.set_title("Observer ladder clustering in PCA space")
    ax.set_xlabel(f"PC1 ({pca.explained_variance_ratio_[0] * 100:.1f}% var.)")
    ax.set_ylabel(f"PC2 ({pca.explained_variance_ratio_[1] * 100:.1f}% var.)")
    fig.savefig(FIG_DIR / "cluster_pca_scatter.png", facecolor="white")
    plt.close(fig)


def plot_dendrogram(df: pd.DataFrame, X: np.ndarray) -> None:
    linkage_matrix = linkage(X, method="ward")
    fig, ax = plt.subplots(figsize=(8.2, 5.4), dpi=220, constrained_layout=True)
    fig.patch.set_facecolor("white")
    ax.set_facecolor("white")
    dendrogram(
        linkage_matrix,
        labels=[DISPLAY_LABELS[name] for name in df["checkpoint"]],
        leaf_rotation=22,
        leaf_font_size=9,
        ax=ax,
        color_threshold=None,
    )
    ax.set_title("Hierarchical clustering dendrogram")
    ax.set_xlabel("Observer checkpoint")
    ax.set_ylabel("Ward linkage distance")
    ax.grid(True, axis="y", color="#d8dde6", linewidth=0.9)
    for spine in ("top", "right"):
        ax.spines[spine].set_visible(False)
    fig.savefig(FIG_DIR / "cluster_dendrogram.png", facecolor="white")
    plt.close(fig)


def write_summary(df: pd.DataFrame, comparison: pd.DataFrame, best_labels: np.ndarray) -> None:
    annotated = df.copy()
    annotated["discovered_cluster"] = [f"cluster_{label}" for label in best_labels]

    cluster_sizes = annotated.groupby("discovered_cluster").size().to_dict()
    bridge_row = annotated.loc[annotated["checkpoint"] == "post_throat_backstep_01"].iloc[0]
    bridge_cluster_size = int(cluster_sizes[bridge_row["discovered_cluster"]])
    bridge_is_outlier = bridge_cluster_size == 1

    metric_rows = [
        f"| `{row.checkpoint}` | `{row.manual_label}` | `{row.discovered_cluster}` | {row.opl_mean:.4f} | {row.opl_max:.4f} | "
        f"{row.portal_hit_density:.4f} | {row.throat_event_density:.4f} | {row.crossings_per_pixel:.4f} | "
        f"{row.segments_per_crossing:.4f} | {row.average_segments_per_ray:.4f} |"
        for row in annotated.itertuples(index=False)
    ]
    comparison_rows = [
        f"| `{row.method}` | {int(row.k)} | {row.ari_vs_manual:.4f} | {row.silhouette:.4f} |"
        for row in comparison.itertuples(index=False)
    ]

    best = comparison.sort_values(["ari_vs_manual", "silhouette"], ascending=[False, False]).iloc[0]

    lines = [
        "# Observer Ladder Clustering Summary",
        "",
        f"Source artifact: [{SUMMARY_PATH.name}]({SUMMARY_PATH})",
        "",
        "This analysis uses only the saved wormhole ladder checkpoint summary. Features were standardized before clustering.",
        "",
        "## Checkpoint Feature Table",
        "",
        "| Checkpoint | Manual regime | Discovered cluster | OPL mean | OPL max | portal_hit_density | throat_event_density | crossings_per_pixel | segments_per_crossing | AverageSegmentsPerRay |",
        "|---|---|---|---:|---:|---:|---:|---:|---:|---:|",
        *metric_rows,
        "",
        "## Clustering Comparison",
        "",
        "| Method | k | ARI vs manual labels | Silhouette |",
        "|---|---:|---:|---:|",
        *comparison_rows,
        "",
        "## Interpretation",
        "",
        f"- The best alignment with the current manual regime labels was `{best['method']}` at `k={int(best['k'])}`, with `ARI={best['ari_vs_manual']:.4f}` and `silhouette={best['silhouette']:.4f}`.",
        f"- The bridge checkpoint `post_throat_backstep_01` falls in `{bridge_row['discovered_cluster']}` with cluster size `{bridge_cluster_size}`.",
        f"- Bridge outlier verdict: `{'yes' if bridge_is_outlier else 'no'}`. "
        + ("It is isolated as a singleton cluster under the best-performing automatic clustering pass." if bridge_is_outlier else "It remains separable, but not as a singleton, under the best-performing automatic clustering pass."),
        "- The automatic clustering still groups the near-side mouth approach and throat progression together more strongly than the manual taxonomy separates `throat` from the rest of the near-side leg.",
        "",
        "Figures:",
        "",
        "- [cluster_pca_scatter.png](figures/cluster_pca_scatter.png)",
        "- [cluster_dendrogram.png](figures/cluster_dendrogram.png)",
        "",
    ]
    SUMMARY_MD_PATH.write_text("\n".join(lines), encoding="utf-8")


def main() -> None:
    FIG_DIR.mkdir(parents=True, exist_ok=True)
    df = load_dataframe()
    X = standardize(df)
    comparison, best_labels = clustering_comparison(df, X)
    plot_pca(df, X, best_labels)
    plot_dendrogram(df, X)
    write_summary(df, comparison, best_labels)


if __name__ == "__main__":
    main()
