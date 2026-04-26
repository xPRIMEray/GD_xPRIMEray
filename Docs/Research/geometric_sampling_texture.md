# Geometric Sampling Texture

**Status:** Analysis-only. No renderer changes. No simulation reruns. Raw validation truth remains separate from coherence previews.

This note synthesises the sampling-texture analysis performed on the wormhole checkpoint ladder (fixture_011, runs 2026-04-21 and 2026-04-24). It consolidates findings from adaptive tiling, polar tiling, edge-alignment measurements, geometric structure search, and log-polar orientation histograms into a unified recommendation.

Source: [papers/paper_001_causal_observer_ladders/analysis/geometric_sampling_texture.md](../papers/paper_001_causal_observer_ladders/analysis/geometric_sampling_texture.md), [papers/paper_001_causal_observer_ladders/analysis/phase_sampling_domain_update.md](../papers/paper_001_causal_observer_ladders/analysis/phase_sampling_domain_update.md)

---

## Central Result

Two independent diagnostic objectives require different tile geometries:

| Objective | Best tile geometry | Key metric |
|---|---|---|
| Preserve local edge direction | **Adaptive square** | direction similarity: 0.836 (mouth), 0.875 (bridge) |
| Maximise aperture edge recall | **Aperture polar** | recall: 1.000 / 1.000 |
| Balance bridge direction + recall | Incoherence or curvature polar | direction: 0.656–0.660; recall near 1.000 |

No single geometry achieves both objectives. This is a fundamental trade-off, not a calibration issue.

---

## Adaptive Square Tiles

Adaptive square tiles divide the image into tiles whose size and alignment respond to the local intensity-gradient field. Their primary advantage is **direction fidelity**: the tile boundaries align with locally dominant edge orientations.

Measured results:
- mouth: visible-edge recall 0.712, gradient-direction similarity **0.836**
- bridge: visible-edge recall 0.731, gradient-direction similarity **0.875**

The bridge achieves *higher* direction similarity than the mouth despite being the more morphologically disrupted checkpoint. This suggests that the bridge's line-rich, radial-dominant edge field is well-matched to a gradient-aligned tile grid, even though its absolute coherence is lower.

---

## Polar and Radial Tiles

Polar tiles divide the image into radial and angular sectors from a fixed centre. Their primary advantage is **boundary recall**: because any roughly circular or radial edge structure is guaranteed to fall near a tile boundary, they are difficult to fool.

Measured results (aperture-centred):
- mouth: recall **1.000**, direction 0.628
- bridge: recall **1.000**, direction 0.642

Polar tiles are appropriate for aperture diagnostics where missing an edge is worse than having spurious matches. They are not appropriate as a replacement for adaptive tiles when directional accuracy matters.

### Polar centre variants

| Centre type | mouth direction | bridge direction | Notes |
|---|---|---|---|
| Aperture centre | 0.628 | 0.642 | Global wormhole mouth estimate |
| Incoherence centre | 0.624 | **0.656** | Centroid of visible-band / phase-incoherence signal |
| Curvature centre | **0.656** | **0.660** | Hough circle/arc-fit centre candidates |

Curvature-centred polar tiling gives the best polar direction results at both checkpoints, but still falls well below adaptive-square direction fidelity and reduces symmetric edge-distance alignment relative to aperture-centred polar.

---

## Bridge Morphology

The bridge checkpoint is morphologically distinct from all others by three independent measures:

| Measure | Bridge | Near-side range | Far-side range |
|---|---|---|---|
| Hough line detections | **214** | 71–86 | 90–106 |
| Mean contour eccentricity | **0.663** | 0.909–0.962 | 0.909–0.929 |
| Visible-band components | **190** | 91 | 56–107 |

The bridge has three times as many Hough line detections as the next-highest checkpoint, much rounder contours, and a highly fragmented visible-band mask. This is consistent with the transport finding that the bridge is a disordered, sparse regime rather than a clean photon-sphere-adjacent transition.

---

## Orientation Persistence

Log-polar orientation histograms were computed for all six checkpoints. Key finding: orientation persistence does **not** break down at the bridge.

| Checkpoint pair | Cosine similarity |
|---|---|
| Bridge vs near-side mean | **0.988** |
| Bridge vs all-others mean | **0.972** |
| Non-bridge pairwise mean | 0.961 |

The bridge remains radial-dominant (radial/tangential = 2.03), slightly more so than the near-side. The orientation transition occurs *after* the bridge: far-side checkpoints shift to tangential dominance (radial/tangential = 0.75 at exit-approach, 0.58 at exit-lookback).

This has a direct implication for render guidance: the far-side domain, not the bridge, is the location where an adaptive tile grid needs to be reoriented for tangential features.

---

## Hybrid Architecture Recommendation

The evidence supports a layered rather than substituted architecture:

```
Raw row pass (scout truth)
    ↓ never overridden
Adaptive square tiles (local direction, coherence previews)
    + Aperture polar tiles (high-recall boundary diagnostics)
    + [Optional] Curvature-centred polar (geometric probe refinement)
    + [Future] Triangle/diagonal/annular (oblique edge families, ring persistence)
```

Coherence previews from any tiled layer must be labelled as interpretive and kept separate from Pass 1 classification truth.

---

## Connection to Phase Sampling

The phase sampling domain update (2026-04-25) tested three polar recentring strategies as alternatives to aperture-centred polar. The key result: **adaptive-square tiles remain the best directional match regardless of polar centre choice**. Polar recentring improves geometric probe alignment modestly, especially for bridge-direction fidelity, but cannot close the gap to adaptive direction performance.

The next useful sampling texture is likely hybrid: adaptive square for local direction, plus multi-centre annular/curvature probes for high-recall geometry diagnostics.

---

## Cross-References

- Diagnostics page: [../diagnostics/tile_coherence.md](../diagnostics/tile_coherence.md)
- Phase coherence field: [phase_coherence_field.md](phase_coherence_field.md)
- Source analysis notes: [../papers/paper_001_causal_observer_ladders/analysis/geometric_sampling_texture.md](../papers/paper_001_causal_observer_ladders/analysis/geometric_sampling_texture.md)
- Paper 001 §5.5: [../papers/paper_001_causal_observer_ladders/paper.md](../papers/paper_001_causal_observer_ladders/paper.md)
