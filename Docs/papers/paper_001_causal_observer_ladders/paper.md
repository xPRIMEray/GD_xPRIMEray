# Causal Observer Ladders for Wormhole Ray Transport  
### Fresh-Instance Validation, Regime Structure, and Bridge Anomalies

---

## Abstract

We present a fixture-based method for validating observer-dependent ray transport through wormhole-like topological structures. Using a fresh-instance observer ladder with per-checkpoint causal verification, we identify distinct transport regimes spanning near-side, throat, bridge, and far-side observer states. 

We demonstrate that world-space interpolation fails across the topological transition, while a mixed strategy of interpolation and discovered checkpoints yields a coherent traversal path. Derived metrics including optical path length (OPL), interaction density, and transport cost reveal a previously uncharacterized bridge regime in which interaction density collapses while per-crossing cost peaks. These results suggest that observer position defines transport regime and that naive coordinate interpolation is insufficient across non-trivial topology.

---

## 1. Introduction

Simulating ray transport through curved or topologically non-trivial spaces presents challenges beyond standard geometric rendering. In particular, observer-dependent effects may induce discontinuities that are not captured by naive interpolation in world-space coordinates.

This work introduces a **causal observer ladder** methodology: a sequence of validated observer states, each computed under fresh-instance conditions, ensuring that transport metrics reflect true causal structure rather than accumulated simulation artifacts.

We apply this method to a wormhole-like system and show that traversal naturally separates into multiple regimes with distinct transport characteristics.

---

## 2. Method

### 2.1 Fresh-Instance Observer Ladder

Each checkpoint is evaluated using a fresh instance of the renderer, ensuring:
- full classified pixel coverage
- zero budget exhaustion
- zero inferred classification artifacts

This guarantees that differences between checkpoints arise from transport structure rather than simulation state.

### 2.2 Ladder Construction

The observer ladder consists of six validated checkpoints:

- `mouth`
- `mouth_to_throat_approach`
- `throat`
- `post_throat_backstep_01`
- `post_throat_exit_approach`
- `exit_lookback`

Near-side checkpoints were densified via interpolation, while hard-leg checkpoints were discovered via guided search.

### 2.3 Derived Metrics

For each checkpoint, we compute:

- Optical Path Length (OPL mean, max)
- Portal-hit density
- Throat-event density
- Crossings per pixel
- Segments per crossing
- Average segments per ray

---

## 3. Results

All checkpoints satisfy full classified coverage, zero budget exhaustion, and zero inferred throat classification, ensuring that reported differences arise from transport structure rather than sampling artifacts.

### 3.1 Near-Side Regime

From `mouth` to `throat`, interaction density increases while transport cost decreases:

- portal-hit density: 0.1465 → 0.1750  
- throat-event density: 0.0969 → 0.1139  
- crossings per pixel: 0.6495 → 0.7479  
- OPL mean: 9.9599 → 9.5078  
- segments per crossing: 153.26 → 128.17  

This indicates a smooth, interpolation-friendly regime.

### 3.2 Throat as Transition Hinge

The throat behaves as a **transition hinge rather than a discontinuity**, extending the near-side trend instead of breaking it.

### 3.3 Bridge Regime

The checkpoint `post_throat_backstep_01` exhibits a distinct transport state:

- portal-hit density: 0.0964  
- throat-event density: 0.0555  
- crossings per pixel: 0.2098  
- segments per crossing: 366.03 (maximum)  
- OPL mean: 7.5908 (minimum)  

This indicates a **sparse, high-cost transport regime** where interactions are rare but expensive.

### 3.4 Far-Side Re-Densification

The far-side regime re-densifies:

- throat-event density peaks at 0.2111  
- crossings per pixel peaks at 1.6544  
- segments per crossing drops to 50.31 (minimum)  

At `exit_lookback`, portal density reaches its maximum (0.2557), and OPL max reaches 16.3070.

---

## 4. Key Findings

- Interpolation validity is regime-dependent  
- The throat is not the primary discontinuity  
- The bridge is the dominant transport anomaly  
- OPL and interaction density decouple sharply at the bridge  

---

## 5. Advanced Analysis of Observer Regimes

### 5.1 Regime Clustering Results

Artifact-only clustering recovered the large-scale observer-regime structure directly from the validated ladder metrics. Using standardized checkpoint features derived from optical path length, interaction density, crossing density, and transport cost, both agglomerative clustering and k-means achieved their best agreement with the manual regime labels at `k = 3` (`ARI = 0.5946`, `silhouette = 0.5547`). In this best-performing automatic partition, the near-side checkpoints `mouth`, `mouth_to_throat_approach`, and `throat` were grouped together, the bridge checkpoint `post_throat_backstep_01` was isolated as a singleton cluster, and the far-side checkpoints `post_throat_exit_approach` and `exit_lookback` formed a separate cluster. Thus, the bridge emerges automatically as a distinct transport state, whereas the throat is grouped more naturally with the near-side progression than with the bridge or far-side states.

This clustering result supports a physically interpretable regime decomposition. The near-side and throat checkpoints behave as a continuous interaction-rich family, while the bridge is separated by its sparse and inefficient transport signature rather than by a purely geometric label. The far-side states then regroup into a higher-density family with strong interaction load but lower transport cost per crossing.

Relevant figures:
- [cluster_pca_scatter.png](./figures/cluster_pca_scatter.png)
- [cluster_dendrogram.png](./figures/cluster_dendrogram.png)
- [regime_clustering.png](./analysis/figures/regime_clustering.png)

### 5.2 Bridge Anomaly Quantification

Three independent anomaly measures were applied to the same normalized checkpoint feature table: Euclidean z-score distance in standardized feature space, isolation forest, and local outlier factor. All three ranked `post_throat_backstep_01` as the strongest anomaly in the ladder. In the combined ranking, the bridge checkpoint achieved the top position with `z = 4.3999`, `isolation forest = 0.6160`, and `LOF = 1.3496`, exceeding all other checkpoints across the joint anomaly assessment. The next-ranked checkpoints, such as `exit_lookback` and `mouth`, remained distinctly less extreme in the combined score.

The anomaly ranking provides quantitative support for the bridge interpretation already suggested by the observer-ladder characterization. The bridge is not merely a low-density checkpoint; it is a multi-metric outlier defined simultaneously by suppressed interaction density, depressed mean optical path length, and unusually high cost per crossing. In physical terms, this supports treating the bridge as a transitional transport anomaly rather than as a weak member of either the near-side or far-side regime.

Relevant figures:
- [bridge_anomaly_scores.png](./analysis/figures/bridge_anomaly_scores.png)
- [checkpoint_anomaly_scores.png](./analysis/anomaly_detection/figures/checkpoint_anomaly_scores.png)

### 5.3 Radial Structure and Horizon-Like Features

Image-derived radial structure analysis was performed using the approved debug captures and the previously estimated aperture centers. For each checkpoint, radial intensity profiles were reconstructed and differentiated to obtain first- and second-derivative structure, allowing detection of local peaks, inflection points, and sign changes near the apparent aperture radius. The resulting feature-radius comparison yielded a mixed global verdict: a single consistent feature radius does not persist across the entire ladder. However, near-side checkpoints remain relatively close to the marked aperture radius, and the throat checkpoint shows the strongest local slope magnitude near the apparent radius (`0.0251`), exceeding all other checkpoints.

This pattern suggests that a horizon-like radial feature is most sharply expressed at the throat rather than at the bridge. The near-side leg preserves a relatively stable radial boundary interpretation, while post-throat checkpoints increasingly shift away from a simple single-ring model. The bridge therefore appears not as the sharpest horizon-like state, but as the point where the visible radial structure becomes harder to represent with a single near-aperture feature radius.

Relevant figures:
- [normalized_profile_overlay.png](./analysis/radial_structure/figures/normalized_profile_overlay.png)
- [radial_derivative_panels.png](./analysis/radial_structure/figures/radial_derivative_panels.png)

### 5.4 Spectral / Periodicity Analysis

FFT analysis of the ordered ladder sequences was applied to `OPL mean`, `throat_event_density`, `crossings_per_pixel`, and `segments_per_crossing`. Across the first three sequences, the dominant frequency was the lowest nonzero mode (`1/6` cycles per checkpoint), indicating a slow regime-scale drift rather than a repeating oscillation. `segments_per_crossing` showed a different dominant frequency (`1/3` cycles per checkpoint), but the bridge residual remained large and localized, especially for `segments_per_crossing`, where the bridge deviation from neighbor interpolation was strongly positive. Wavelet analysis of the debug-image radial profiles also showed structured multiscale content, but the dominant scales changed across checkpoints rather than locking into a persistent repeating cadence.

Taken together, the spectral evidence argues against interpreting the ladder as an oscillatory family. Instead, the observer path is better described as a sequence of regime transitions with one strongly singular bridge excursion. In physical terms, the bridge behaves like a localized topological transition state rather than one phase of a smooth repeating pattern.

Relevant figures:
- [sequence_fft.png](./analysis/periodicity/figures/sequence_fft.png)
- [radial_profile_wavelets.png](./analysis/periodicity/figures/radial_profile_wavelets.png)

### 5.5 Geometric Sampling Texture and Orientation Persistence

Artifact-only geometric sampling analysis compared adaptive square tiles, polar/radial tiles, OpenCV/scikit-image morphology detections, and log-polar edge-orientation histograms. Adaptive square tile boundaries were the stronger directional boundary match: for the available `mouth` and `post_throat_backstep_01` diagnostics, adaptive boundary overlays reached gradient-direction similarity `0.836` and `0.875`, while polar boundary overlays reached lower direction similarity `0.628` and `0.642`. Polar/radial tiles, however, gave near-perfect visible-edge recall (`1.000` and `0.9997`), making them useful high-recall aperture diagnostics even when they sacrifice direction fidelity.

The bridge remains morphologically distinct. The geometric structure search found `214` Hough line detections at `post_throat_backstep_01`, compared with `71-106` for most other checkpoints; its mean contour eccentricity dropped to `0.663`; and its visible-band mask split into `190` connected components compared with `91` at the mouth. Log-polar orientation analysis did not show a global bridge-only breakdown: the bridge-to-near-side mean orientation cosine was `0.988`, and bridge-to-rest cosine was `0.972`. Instead, near-side, throat, and bridge checkpoints remain radial-dominant, while the far-side checkpoints shift tangential (`radial/tangential = 0.754` at `post_throat_exit_approach`, `0.581` at `exit_lookback`).

These results motivate a hybrid sampling texture architecture: retain raw row passes as scout truth, use adaptive square tiles for local coherence and direction-preserving boundary previews, use polar/radial tiles for high-recall aperture diagnostics, and keep future triangle, diagonal, and annular textures as separate diagnostic layers rather than replacements for raw validation.

Relevant analysis:
- [geometric_sampling_texture.md](./analysis/geometric_sampling_texture.md)
- [orientation_histograms.png](../../../output/fixture_runs/fixture_011_wormhole_checkpoint_sequence/2026-04-21T20-24-39/log_polar_orientation_2026-04-25/orientation_histograms.png)
- [annotated_shape_search_contact_sheet.png](../../../output/fixture_runs/fixture_011_wormhole_checkpoint_sequence/2026-04-21T20-24-39/geometry_structure_search_2026-04-25/annotated_shape_search_contact_sheet.png)

### 5.6 Phase Coherence and Temporal Structure in Observer-Ladder Sampling

Recent work by Anirban Bandyopadhyay on phase-coherent biological computation and time-crystal-like oscillatory systems suggests that complex systems maintain stability not through purely local rules, but through phase-aligned coherence across interacting elements.

In this framework:

Computation emerges from phase relationships
Stability emerges from coherence persistence
Discontinuities emerge from phase boundary transitions

We apply this interpretation to the observed banding artifacts in xPRIMEray.

🧪 Empirical Mapping

From our diagnostics:

Neighbor-normal discontinuity strongly aligns with visible bands
First-hit divergence originates before stored-hit refinement
Orientation fields remain globally persistent
Morphology fragments locally
Phase-coherence field shows reduced coherence at band locations

This suggests:

Banding artifacts correspond to phase-coherence boundaries in the ray-field

📊 Supporting Results (Your actual data 🔥)
checkpoint	band coherence	outside coherence	incoh vs band r
mouth	0.639	0.801	0.309
post_throat_backstep_01	0.764	0.796	0.071

Interpretation:

Lower coherence correlates with visible bands
Stronger effect near observer (mouth)
Bridge region shows weaker but still positive coherence disruption
🧠 Interpretation

Unlike classical ray tracing artifacts caused by:

insufficient sampling
numerical precision
shading discontinuities

the observed banding here appears to arise from:

multiple locally valid ray-path solutions coexisting, with spatial sampling collapsing inconsistently across phase boundaries

This is consistent with:

multi-solution geodesic fields
interference-like domain partitioning
phase-coherent computation models
🔮 Implication

This reframes the renderer:

from a deterministic ray solver
to a phase-selection system over a multi-solution field

🧬 Forward Direction

Future work should explore:

phase-coherence-guided hit selection
tile-level phase memory propagation
multi-scale coherence enforcement
temporal persistence across frames

---

## 6. Discussion

These results suggest that observer traversal through wormhole-like topology cannot be treated as a continuous world-space path. Instead, traversal must be constructed as a sequence of causally valid states.

The existence of a bridge regime indicates that transport efficiency and interaction density may decouple in transitional regions, revealing structure not visible through naive sampling.

---

## 7. Conclusion

We introduce a validated observer ladder framework for wormhole ray transport and demonstrate that traversal naturally decomposes into distinct regimes. This provides a foundation for future work in curved-ray rendering, topological optics, and causal transport analysis.

---

## Figures (Proposed)

- Ladder diagram (observer positions)
- Interaction density vs checkpoint
- Segments-per-crossing spike at bridge
- OPL vs interaction density phase plot

---

## Data Source

All results derived from:
output/fixture_runs/fixture_011_wormhole_checkpoint_sequence/2026-04-20T22-26-39/


---
