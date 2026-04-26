# Curvature Heat Maps

The curvature heat map is a per-pixel transport diagnostic. It maps the **cumulative absolute turn angle** accumulated by each camera ray along its Pass 1 path, before any classified terminal event.

This is a transport-side geometric diagnostic, not an observable flux or brightness image.

---

## What the Heat Map Measures

For each pixel, the renderer integrates the ray from the camera outward through the GRIN field. At each integration step, the instantaneous directional change $\Delta\theta$ is recorded. The heat map value for that pixel is

$$H = \sum_{\text{steps}} |\Delta\theta|$$

This answers the question: *how much total directional bending did this ray accumulate before it reached a classified terminal event?*

High values appear where rays:
- skim the wormhole throat or an effective photon sphere
- spend significant path length in high-curvature field regions
- pass near separatrices between ray families with qualitatively different behaviour

Low values appear where rays travel approximately straight, with little field influence.

---

## What the Heat Map Is Not

| It is | It is not |
|---|---|
| A transport-complexity map | An observable brightness or flux image |
| A map of path-geometry stress | A radiative transfer result |
| A precursor diagnostic for ring structure | A direct measurement of Einstein-ring positions |
| A camera-space turn-angle summary | A redshift-weighted or emissivity-weighted image |

This distinction matters when comparing against the astrophysics literature. Published wormhole and black-hole imaging papers typically plot quantities such as observed intensity, thin-disk emissivity, transfer function decompositions, or photon-ring groups. Those are observer-facing image products derived from an emitting source model. The heat map is a transport-side state variable, not a substitute for those quantities.

---

## Qualitative Alignment with Literature

Despite the above distinction, the wormhole-side heat map exhibits structure that is **qualitatively consistent** with published wormhole ring and strong-lensing phenomenology:

- **Annular organisation** — nested high-curvature bands around the wormhole mouth, rather than diffuse unstructured bending.
- **Nested zones** — multiple concentric transition rings, consistent with the multi-photon-sphere / multiple-Einstein-ring structure reported by Shaikh et al. (2019) and Huang et al. (2021).
- **Strong radial transitions** — sharp separatrices between ray families with different throat-interaction histories, consistent with the shadow-boundary and lensing-band structure in thin-shell wormhole image papers.

These structural similarities support the interpretation that the transport stack is generating organised, nontrivial lensing-like structure — not generic curved-ray smear. They do not constitute a claim of observational equivalence with any published wormhole image.

---

## Reading the Heat Map Alongside Other Diagnostics

The heat map is most informative when read alongside:

| Companion diagnostic | What it adds |
|---|---|
| [Domain ownership](domain_ownership.md) | Which annuli correspond to domain-boundary transitions vs continuous regime interiors |
| [Tile coherence](tile_coherence.md) | Whether the banding visible in the heat map aligns with coherent tile-boundary orientations |
| [Phase coherence](phase_coherence.md) | Whether high-curvature regions correspond to phase-boundary locations |
| Portal-hit density overlay | Which annuli correspond to active portal interaction vs geometry-only paths |

---

## Key Data Points (Wormhole Checkpoint Ladder)

From the six-checkpoint wormhole observer ladder:

| Checkpoint | OPL mean | Transport character |
|---|---|---|
| mouth | 9.96 | Dense interaction, high turn accumulation near aperture |
| mouth-to-throat | 9.73 | Smooth near-side progression |
| throat | 9.51 | Strongest local slope at apparent aperture radius (slope magnitude 0.025) |
| bridge (backstep) | 7.59 (minimum) | Sparse transport; low turn accumulation per pixel, but 366 segments/crossing |
| exit-approach | 8.12 | Re-densification begins; tangential shift |
| exit-lookback | 8.43 | Maximum OPL max (16.31); portal density peaks at 0.256 |

The throat shows the strongest horizon-like radial feature (steepest radial intensity gradient at the apparent aperture radius), not the bridge. The bridge is anomalous not because it bends rays sharply, but because it transports them expensively through sparse geometry.

---

## Next Diagnostic Steps (Recommended)

1. Add masked SSIM and MAD comparisons for the curved aperture region vs HUD/panel regions, to avoid treating overlay clutter as primary signal.
2. Add optional transfer-function-style overlays (impact-parameter class, side-of-throat origin, path-family label) beside the scalar heat map.
3. Add hit-density or remap-density overlays to separate "high bending" from "high observable contribution."
4. If a thin-disk or background-sky source model is added, compare curvature maxima against emergent brightness rings rather than assuming they coincide.

---

## Cross-References

- [domain_ownership.md](domain_ownership.md) — how heat-map annuli relate to transport domains
- [phase_coherence.md](phase_coherence.md) — coherence structure at band locations
- Literature crosswalk: [Research/wormhole_curvature_heatmap_literature_crosswalk.md](../Research/wormhole_curvature_heatmap_literature_crosswalk.md)
- Paper 001 radial structure: [papers/paper_001_causal_observer_ladders/analysis/radial_structure/summary.md](../papers/paper_001_causal_observer_ladders/analysis/radial_structure/summary.md)
