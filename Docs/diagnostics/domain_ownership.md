# Curvature Domain Ownership

Domain ownership describes which transport regime — near-side, bridge, or far-side — owns each observer position on the wormhole ladder. The research analysis estimates these regimes from transport metrics; renderer-integrated domain maps are heuristic diagnostics and may include fixture/runtime signals such as hit classification, remap/crossing counts, boundary confidence, selection flips, and normal discontinuity. Treat the integrated maps as inspection aids, not proof of metric-only domain ownership.

---

## Core Finding

> **Visible banding correlates with curvature-domain boundary transitions in the audited fixtures, not with final-hit selection artifacts alone.**

Supporting evidence from the six-checkpoint wormhole observer ladder. These metrics motivate the diagnostics; they are not validation evidence for every renderer-integrated domain map:

| Evidence type | Metric | Value |
|---|---|---|
| Multi-metric anomaly rank | Bridge z-score | 4.40 (top ranked) |
| Multi-metric anomaly rank | Bridge isolation forest | 0.616 |
| Multi-metric anomaly rank | Bridge LOF | 1.35 |
| Automatic clustering | Best-fit k | 3 (ARI = 0.5946, silhouette = 0.5547) |
| Bridge isolation | Singleton cluster | yes — bridge occupies cluster 1 alone |
| Segments per crossing | Bridge value | 366 (vs 50–153 for all others) |
| Phase-coherence gap | Mouth band vs outside | 0.162 (coherence drops at band locations) |
| Radial slope at aperture | Throat value | 0.025 (strongest feature at throat, not bridge) |

---

## Regime Decomposition

The k = 3 automatic partition (agglomerative + k-means, same result) recovers the following structure:

| Cluster | Checkpoints | Transport character |
|---|---|---|
| Near-side (cluster 2) | mouth, mouth-to-throat, throat | Continuously increasing density; decreasing cost; interpolation-valid |
| Bridge (cluster 1, singleton) | post-throat backstep | Sparse, expensive; maximum segments-per-crossing; minimum OPL mean |
| Far-side (cluster 0) | exit-approach, exit-lookback | Re-densified; maximum portal density; tangential orientation dominant |

The throat is classified with the near-side family, not with the bridge. Clustering evidence disagrees with a naive spatial reading that would place the throat as the boundary between near-side and bridge. The throat extends the near-side trend; the actual transport discontinuity occurs at the bridge.

---

## Domain Boundaries vs Banding

Band-boundary alignment was measured by testing whether the visible-band mask and the phase-incoherence signal concentrate at the same locations:

- **Visible-band / incoherence correlation** was reduced by −0.033 after phase-guided hit selection, but the visible-band phase-boundary fraction changed by only −0.007. This suggests bands and incoherence share structural causes rather than one causing the other.

- **Neighbour-normal-delta discontinuity** strongly aligns with visible bands. First-hit divergence originates before stored-hit refinement. Orientation fields remain globally persistent while morphology fragments locally. These are consistent with a domain-boundary origin for banding rather than a sampling or precision origin.

---

## Checkpoint Feature Table

Full metrics from derived_metrics.json and clustering_summary.md:

| Checkpoint | Manual regime | Cluster | OPL mean | portal density | crossings/px | segments/crossing |
|---|---|---|---:|---:|---:|---:|
| mouth | near-side | 2 | 9.96 | 0.147 | 0.650 | 153 |
| mouth-to-throat | near-side | 2 | 9.73 | 0.163 | 0.699 | 140 |
| throat | throat | 2 | 9.51 | 0.175 | 0.748 | 128 |
| bridge (backstep) | bridge | **1** | **7.59** | **0.096** | **0.210** | **366** |
| exit-approach | far-side | 0 | 8.12 | 0.180 | 1.654 | 50 |
| exit-lookback | far-side | 0 | 8.43 | 0.256 | 1.420 | 61 |

The bridge is anomalous across all five characterisation features simultaneously — not merely extreme in one dimension. This multi-metric outlier character distinguishes it from a checkpoint that is simply at one end of a continuous gradient.

---

## Interpolation Validity by Domain

| Domain | Interpolation verdict | Reason |
|---|---|---|
| Near-side (mouth → throat) | Valid | Smooth continuous progression in all metrics |
| Throat | Extends near-side; interpolation-valid across throat hinge | Throat is a transition hinge, not a discontinuity |
| Bridge | Invalid for world-space interpolation | Multi-metric outlier; dense-to-sparse collapse; no smooth path from near-side metrics to bridge metrics |
| Far-side (exit-approach → exit-lookback) | Valid within far-side | Smooth re-densification |
| Bridge → far-side | Requires discovered checkpoints | No interpolation path exists through the bridge transport anomaly |

---

## Future Work

- Full six-checkpoint phase-coherence scoring (currently only mouth and bridge are measured).
- Geometric primitive inventory (Hough line/arc events) for all six checkpoints — currently only available at mouth and bridge.
- Cross-checkpoint persistence tracking: which curvature-centre candidates recur across domains (attractor behaviour) vs which are bridge-only (transition events).

---

## Cross-References

- Clustering analysis: [papers/paper_001_causal_observer_ladders/clustering_summary.md](../papers/paper_001_causal_observer_ladders/clustering_summary.md)
- Anomaly scoring: [papers/paper_001_causal_observer_ladders/analysis/bridge_anomaly_scoring.md](../papers/paper_001_causal_observer_ladders/analysis/bridge_anomaly_scoring.md)
- Regime clustering notes: [papers/paper_001_causal_observer_ladders/analysis/regime_clustering.md](../papers/paper_001_causal_observer_ladders/analysis/regime_clustering.md)
- Research synthesis: [research/curvature_domain_ownership.md](../research/curvature_domain_ownership.md)
- Paper 001 §5.1–5.2: [papers/paper_001_causal_observer_ladders/paper.md](../papers/paper_001_causal_observer_ladders/paper.md)
