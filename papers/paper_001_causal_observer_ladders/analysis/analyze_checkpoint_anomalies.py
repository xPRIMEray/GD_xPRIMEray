from __future__ import annotations

import json
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
from sklearn.ensemble import IsolationForest
from sklearn.neighbors import LocalOutlierFactor
from sklearn.preprocessing import StandardScaler


ROOT = Path(__file__).resolve().parents[4]
SUMMARY_PATH = ROOT / "output" / "fixture_runs" / "fixture_011_wormhole_checkpoint_sequence" / "2026-04-20T22-26-39" / "checkpoint_sequence_summary.json"
ANALYSIS_DIR = Path(__file__).resolve().parent / "anomaly_detection"
FIG_DIR = ANALYSIS_DIR / "figures"
SUMMARY_MD = ANALYSIS_DIR / "summary.md"
SUMMARY_JSON = ANALYSIS_DIR / "scores.json"

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


def load_feature_table() -> pd.DataFrame:
    payload = json.loads(SUMMARY_PATH.read_text(encoding="utf-8"))
    checkpoints = {cp["Name"]: cp for cp in payload["Checkpoints"]}
    rows: list[dict] = []
    for name in ORDER:
        cp = checkpoints[name]
        total_pixels = cp["TotalPixels"]
        boundary_crossings = cp["BoundaryCrossingsTotal"]
        total_segments = cp["AdaptiveDiagnostics"]["TotalEmittedRaySegCount"]
        rows.append(
            {
                "checkpoint": name,
                "opl_mean": cp["OpticalPathLengthMean"],
                "opl_max": cp["OpticalPathLengthMax"],
                "portal_hit_density": cp["PortalHitPixels"] / total_pixels,
                "throat_event_density": cp["ThroatEventPixels"] / total_pixels,
                "crossings_per_pixel": boundary_crossings / total_pixels,
                "segments_per_crossing": (total_segments / boundary_crossings) if boundary_crossings > 0 else 0.0,
                "average_segments_per_ray": cp["AdaptiveDiagnostics"]["AverageSegmentsPerRay"],
            }
        )
    return pd.DataFrame(rows)


def minmax(values: np.ndarray) -> np.ndarray:
    low = float(values.min())
    high = float(values.max())
    if high - low < 1e-12:
        return np.zeros_like(values, dtype=float)
    return (values - low) / (high - low)


def compute_scores(df: pd.DataFrame) -> pd.DataFrame:
    scaler = StandardScaler()
    X = scaler.fit_transform(df[FEATURE_COLUMNS].to_numpy(dtype=float))

    zscore_distance = np.linalg.norm(X, axis=1)

    iso = IsolationForest(
        n_estimators=256,
        contamination="auto",
        random_state=7,
    )
    iso.fit(X)
    isolation_score = -iso.score_samples(X)

    lof = LocalOutlierFactor(n_neighbors=min(3, len(df) - 1), contamination="auto")
    lof.fit_predict(X)
    lof_score = -lof.negative_outlier_factor_

    out = df.copy()
    out["zscore_distance"] = zscore_distance
    out["isolation_forest_score"] = isolation_score
    out["local_outlier_factor_score"] = lof_score
    out["zscore_distance_norm"] = minmax(zscore_distance)
    out["isolation_forest_score_norm"] = minmax(isolation_score)
    out["local_outlier_factor_score_norm"] = minmax(lof_score)
    out["mean_rank_score"] = (
        out["zscore_distance"].rank(ascending=False, method="min")
        + out["isolation_forest_score"].rank(ascending=False, method="min")
        + out["local_outlier_factor_score"].rank(ascending=False, method="min")
    ) / 3.0
    out = out.sort_values("mean_rank_score").reset_index(drop=True)
    return out


def plot_scores(df: pd.DataFrame) -> None:
    order_df = df.set_index("checkpoint").loc[ORDER].reset_index()
    labels = [DISPLAY[name] for name in order_df["checkpoint"]]
    x = np.arange(len(labels))
    width = 0.24

    fig, ax = plt.subplots(figsize=(9.4, 5.2), dpi=220, constrained_layout=True)
    fig.patch.set_facecolor("white")
    ax.set_facecolor("white")
    ax.grid(True, axis="y", color="#d8dde6", linewidth=0.9)
    for spine in ("top", "right"):
        ax.spines[spine].set_visible(False)

    ax.bar(x - width, order_df["zscore_distance_norm"], width=width, color="#1f77b4", label="z-score distance")
    ax.bar(x, order_df["isolation_forest_score_norm"], width=width, color="#d62728", label="isolation forest")
    ax.bar(x + width, order_df["local_outlier_factor_score_norm"], width=width, color="#2ca02c", label="local outlier factor")

    ax.set_title("Checkpoint anomaly score comparison")
    ax.set_xlabel("Observer checkpoint")
    ax.set_ylabel("Normalized anomaly score")
    ax.set_xticks(x)
    ax.set_xticklabels(labels, rotation=22, ha="right")
    ax.legend(frameon=False)

    fig.savefig(FIG_DIR / "checkpoint_anomaly_scores.png", facecolor="white")
    plt.close(fig)


def write_summary(df: pd.DataFrame) -> None:
    ranked = df.sort_values("mean_rank_score").reset_index(drop=True)
    rows = []
    for row in ranked.itertuples(index=False):
        rows.append(
            f"| `{row.checkpoint}` | {row.zscore_distance:.4f} | {row.isolation_forest_score:.4f} | "
            f"{row.local_outlier_factor_score:.4f} | {row.mean_rank_score:.2f} |"
        )

    bridge = ranked.loc[ranked["checkpoint"] == "post_throat_backstep_01"].iloc[0]
    top_row = ranked.iloc[0]
    bridge_dominant = top_row["checkpoint"] == "post_throat_backstep_01"

    lines = [
        "# Checkpoint Anomaly Scoring",
        "",
        "This analysis uses only the frozen wormhole ladder metric summary. Features were standardized before anomaly scoring.",
        "",
        "## Feature Set",
        "",
        "- OPL mean",
        "- OPL max",
        "- portal-hit density",
        "- throat-event density",
        "- crossings per pixel",
        "- segments per crossing",
        "- average segments per ray",
        "",
        "## Ranked Results",
        "",
        "| Checkpoint | z-score distance | isolation forest | local outlier factor | mean rank |",
        "|---|---:|---:|---:|---:|",
        *rows,
        "",
        "## Interpretation",
        "",
        f"- Dominant transport anomaly verdict: `{'yes' if bridge_dominant else 'no'}`.",
        f"- The top-ranked checkpoint by combined anomaly ranking is `{top_row['checkpoint']}`.",
        f"- The bridge checkpoint `post_throat_backstep_01` has scores `z={bridge['zscore_distance']:.4f}`, "
        f"`iforest={bridge['isolation_forest_score']:.4f}`, and `lof={bridge['local_outlier_factor_score']:.4f}`.",
        "- In paper-ready terms, the bridge is the strongest multi-metric outlier when anomaly is defined by simultaneous sparsity, transport inefficiency, and depressed optical path mean relative to the rest of the ladder." if bridge_dominant else "- The bridge remains anomalous, but another checkpoint exceeds it under the combined ranking.",
        "",
        "Figure:",
        "",
        "- [checkpoint_anomaly_scores.png](figures/checkpoint_anomaly_scores.png)",
        "",
    ]
    SUMMARY_MD.write_text("\n".join(lines), encoding="utf-8")

    payload = {
        "checkpoints": ranked.to_dict(orient="records"),
        "dominant_transport_anomaly": top_row["checkpoint"],
        "bridge_is_top_ranked": bool(bridge_dominant),
    }
    SUMMARY_JSON.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def main() -> None:
    ANALYSIS_DIR.mkdir(parents=True, exist_ok=True)
    FIG_DIR.mkdir(parents=True, exist_ok=True)
    df = load_feature_table()
    scored = compute_scores(df)
    plot_scores(scored)
    write_summary(scored)


if __name__ == "__main__":
    main()
