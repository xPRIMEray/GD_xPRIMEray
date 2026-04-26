# Curvature Domain Ownership

**Status:** Analysis-only. No renderer changes. All domain boundaries are inferred from transport-metric structure, not from scene labels.

This note synthesises the regime-clustering, anomaly-detection, and radial-structure analyses into a coherent account of how transport domains emerge from the wormhole observer ladder data and why domain boundaries — not sampling artifacts — are the primary explanation for visible banding.

Source: [papers/paper_001_causal_observer_ladders/clustering_summary.md](../papers/paper_001_causal_observer_ladders/clustering_summary.md), [papers/paper_001_causal_observer_ladders/analysis/bridge_anomaly_scoring.md](../papers/paper_001_causal_observer_ladders/analysis/bridge_anomaly_scoring.md), [papers/paper_001_causal_observer_ladders/analysis/regime_clustering.md](../papers/paper_001_causal_observer_ladders/analysis/regime_clustering.md)

---

## The Domain Decomposition

Transport domains in xPRIMEray are not assigned — they emerge. Clustering of the five-feature stored-hit metric table (OPL mean, OPL max, portal-hit density, throat-event density, crossings per pixel, segments per crossing) produces a k = 3 partition that recovers the following structure with ARI = 0.5946, silhouette = 0.5547:

| Domain | Checkpoints | Transport signature |
|---|---|---|
| **Near-side** | mouth, mouth-to-throat, throat | Dense interaction; smooth cost decrease; radial-dominant |
| **Bridge** | post-throat backstep | Sparse; maximum cost; minimum OPL; multi-metric outlier |
| **Far-side** | post-throat exit-approach, exit-lookback | Re-densification; tangential shift; maximum portal density |

Both agglomerative and k-means clustering produce identical partitions at k = 3. At k = 2, 4, or 5, alignment with the manual regime labels degrades significantly (ARI drops to 0.14 or below). k = 3 is the natural dimensionality of the observer ladder's transport-phase space.

---

## Why the Bridge Is the Anomaly, Not the Throat

A common assumption is that the wormhole throat is the primary discontinuity — the place where transport behaviour changes most sharply. The data contradict this.

The throat clusters with the near-side family, not with the bridge. It extends the near-side trend (increasing portal density, decreasing segments-per-crossing, decreasing OPL mean) rather than breaking it. The sharp discontinuity occurs at the bridge.

| Feature | Near-side trend at throat | Bridge departure |
|---|---|---|
| portal-hit density | 0.147 → 0.163 → **0.175** (increasing) | **0.096** (collapse) |
| crossings/pixel | 0.650 → 0.699 → **0.748** (increasing) | **0.210** (collapse) |
| segments/crossing | 153 → 140 → **128** (decreasing) | **366** (spike) |
| OPL mean | 9.96 → 9.73 → **9.51** (decreasing) | **7.59** (minimum) |

The bridge is not a slightly more extreme version of the throat. It is a structurally distinct transport regime defined by simultaneous collapse in density metrics and spike in per-crossing cost. This is the multi-metric character that makes it a genuine outlier.

---

## Multi-Metric Anomaly Quantification

Three independent anomaly measures all rank `post_throat_backstep_01` (the bridge) as the top anomaly:

| Measure | Bridge value | Next ranked | Method |
|---|---|---|---|
| Euclidean z-score | **4.40** | exit-lookback: 2.84 | Standardised feature-space distance from centroid |
| Isolation forest | **0.616** | exit-lookback: 0.554 | Expected path length for isolation |
| LOF | **1.35** | exit-lookback: 1.24 | Local density ratio |

The bridge anomaly score (overall: 2.195, bridge-signature: 1.953) is more than 3× larger than the next-highest bridge-signature checkpoint (mouth: 0.325). No other checkpoint is an outlier in all three measures simultaneously.

---

## Spectral and Periodicity Evidence

FFT analysis of the six-checkpoint ladder sequences rules out an oscillatory interpretation:

- OPL mean, throat-event density, and crossings-per-pixel: dominant frequency = 1/6 cycles per checkpoint (slowest possible mode — regime-scale drift, not oscillation).
- Segments-per-crossing: dominant frequency = 1/3 cycles per checkpoint, with a large bridge residual that cannot be explained by any simple periodic model.

The bridge excursion in segments-per-crossing cannot be fit by a smooth oscillatory model. It is a singular, localised transport anomaly — a transition state — rather than one phase of a repeating pattern.

---

## Radial Structure

Image-derived radial intensity profiles were reconstructed for each checkpoint. Key finding: a consistent global feature radius does not persist across the full ladder.

- Near-side checkpoints remain close to the marked aperture radius.
- The throat shows the strongest local slope magnitude at the apparent aperture radius (0.025), exceeding all other checkpoints. The throat is the sharpest horizon-like state in the ladder, not the bridge.
- Post-throat checkpoints progressively shift away from a single-ring model. The bridge is where the visible radial structure becomes harder to represent with a single near-aperture feature radius — but this fragmentation is consistent with the broader morphological disorder of the bridge (190 connected components, 214 Hough lines).

---

## Implication: Curvature Domain Boundaries as Band Cause

If banding were caused by insufficient sampling or numerical precision, the band locations would not correlate with transport-regime boundaries. But the evidence shows:

- Phase coherence is lower inside visible bands than outside (gap 0.162 at mouth).
- Neighbour-normal-delta discontinuities align with visible bands.
- First-hit divergence originates before stored-hit refinement (ruling out post-integration artifact injection).
- Band-boundary proximity and domain-boundary proximity are correlated.

This supports the hypothesis that visible bands mark locations where the transport field transitions between distinct solution families — i.e., domain-boundary transitions — rather than where sampling collapses due to renderer limitations.

**Caution:** This is a hypothesis grounded in correlations, not an experiment with a controlled intervention. A definitive test would require varying the domain-boundary location (e.g., by modifying the field profile) and checking whether band locations follow.

---

## Connection to Render Guidance

The domain decomposition has immediate practical implications:

| Domain | Interpolation | Tile recommendation | Coherence expectation |
|---|---|---|---|
| Near-side | Valid | Adaptive square (direction-preserving) | High (coherence gap > 0.16 at bands) |
| Bridge | Not valid for world-space interpolation | Multi-centre polar probes (no stable global attractor) | Low (diffuse disorder, small gap) |
| Far-side | Valid within domain | Adaptive square, reoriented for tangential features | To be measured |

---

## Cross-References

- Diagnostics page: [../diagnostics/domain_ownership.md](../diagnostics/domain_ownership.md)
- Clustering data: [../papers/paper_001_causal_observer_ladders/clustering_summary.md](../papers/paper_001_causal_observer_ladders/clustering_summary.md)
- Anomaly scores: [../papers/paper_001_causal_observer_ladders/analysis/bridge_anomaly_scoring.md](../papers/paper_001_causal_observer_ladders/analysis/bridge_anomaly_scoring.md)
- Periodicity analysis: [../papers/paper_001_causal_observer_ladders/analysis/periodicity/summary.md](../papers/paper_001_causal_observer_ladders/analysis/periodicity/summary.md)
- Radial structure: [../papers/paper_001_causal_observer_ladders/analysis/radial_structure/summary.md](../papers/paper_001_causal_observer_ladders/analysis/radial_structure/summary.md)
- Paper 001 §5.1–5.4: [../papers/paper_001_causal_observer_ladders/paper.md](../papers/paper_001_causal_observer_ladders/paper.md)
