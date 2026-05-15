# Tile Coherence

Tile coherence diagnostics compare different sampling-tile geometries against the visible-edge structure of each checkpoint capture. The goal is to understand which tile shape best preserves local direction information and which best maximises geometric recall — and to establish where those goals conflict.

All numbers below are from artifact-only analysis of the wormhole checkpoint ladder. No renderer changes were made to produce these results.

---

## Tile Geometries

Three tile geometries have been evaluated:

| Geometry | Description | Primary strength |
|---|---|---|
| **Adaptive square** | Tiles sized and aligned to local intensity gradients | Direction fidelity — best gradient-direction similarity |
| **Aperture-centred polar** | Radial/angular sectors from estimated wormhole mouth centre | High recall — rarely misses aperture-adjacent edges |
| **Curvature-centred polar** | Radial/angular sectors from Hough circle/arc-fit centres | Slightly better direction fidelity than aperture polar; weaker distance alignment |
| **Incoherence-centred polar** | Sectors centred on phase-incoherence field centroid | Slight bridge-direction improvement over aperture polar |

---

## Direction Fidelity vs Recall Trade-Off

Edge metrics measured as `visible-edge recall / gradient-direction similarity` within a 6-pixel matching radius:

| Tile geometry | mouth recall / direction | bridge recall / direction |
|---|---|---|
| Adaptive square | 0.712 / **0.836** | 0.731 / **0.875** |
| Aperture polar | **1.000** / 0.628 | **1.000** / 0.642 |
| Incoherence polar | 1.000 / 0.624 | 1.000 / 0.656 |
| Curvature-centre polar | — / 0.656 | — / 0.660 |

**Key finding:** Adaptive square tiles and polar tiles optimise different objectives. Polar tiles achieve near-perfect recall (they almost never miss an aperture edge) but sacrifice direction fidelity. Adaptive tiles sacrifice some recall to preserve local gradient direction significantly better.

No single tile geometry dominates across both objectives.

---

## Per-Checkpoint Morphology

The bridge checkpoint (`post_throat_backstep_01`) is morphologically distinct from all others:

| Metric | Bridge value | Typical non-bridge range |
|---|---|---|
| Hough line detections | **214** | 71–106 |
| Mean contour eccentricity | **0.663** | 0.909–0.962 |
| Visible-band connected components | **190** | 56–107 |

The bridge has more lines, rounder contours, and a more fragmented visible-band mask. This is consistent with sparse, disordered transport geometry — not with an orientation collapse. The bridge remains **radial-dominant** in log-polar orientation (radial/tangential ratio = 2.03), like the near-side and throat checkpoints.

The orientation transition occurs *after* the bridge, on the far side:

| Checkpoint | Radial fraction | Tangential fraction | Radial/tangential |
|---|---|---|---|
| mouth | 0.312 | 0.202 | 1.54 |
| throat | 0.335 | 0.187 | 1.80 |
| post-throat backstep (bridge) | 0.336 | 0.166 | **2.03** |
| exit-approach | 0.194 | 0.257 | 0.75 |
| exit-lookback | 0.177 | 0.305 | 0.58 |

Far-side checkpoints become tangential-dominant (ratio < 1.0), while near-side, throat, and bridge remain radial-dominant.

---

## Hybrid Architecture Recommendation

No single tile geometry is appropriate for all diagnostic goals. The recommended hybrid:

1. **Raw row pass** — scout truth. The non-tiled row-scan baseline is the validation reference. It must not be replaced or overridden by any tiled diagnostic.

2. **Adaptive square tiles** — local coherence and direction-preserving boundary previews. Use when the goal is to understand local edge orientation or to generate coherence maps that must agree with visible-band edge directions.

3. **Aperture-centred polar tiles** — high-recall aperture diagnostics. Use when the goal is to ensure that no aperture-adjacent edge is missed (e.g., boundary-detection passes where false negatives are costly).

4. **Curvature-centred or incoherence-centred polar** — geometric probe refinement. Use when the aperture centre is uncertain or when bridge-direction fidelity is needed. Expect weaker distance alignment.

5. **Future textures (triangle, diagonal, annular)** — diagnostic layers only. Do not replace existing textures. Add as parallel diagnostic views for oblique edge families and ring-persistence analysis.

---

## Cross-References

- Full analysis note: [papers/paper_001_causal_observer_ladders/analysis/geometric_sampling_texture.md](../papers/paper_001_causal_observer_ladders/analysis/geometric_sampling_texture.md)
- Phase sampling domain update: [papers/paper_001_causal_observer_ladders/analysis/phase_sampling_domain_update.md](../papers/paper_001_causal_observer_ladders/analysis/phase_sampling_domain_update.md)
- Research synthesis: [Research/geometric_sampling_texture.md](../Research/geometric_sampling_texture.md)
- Paper 001 §5.5: [papers/paper_001_causal_observer_ladders/paper.md](../papers/paper_001_causal_observer_ladders/paper.md)
