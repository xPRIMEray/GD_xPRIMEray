# Phase Coherence Field

The phase-coherence diagnostic estimates, for each pixel, whether the local transport solution is consistent with its immediate neighbours. High coherence indicates that adjacent pixels share a smoothly varying ray-path solution. Low coherence indicates a phase boundary — a location where the transport field is transitioning between two distinct solution families.

This diagnostic is a sampling-domain interpretive layer. It does not modify Pass 1 classifications.

---

## Construction

The coherence field is constructed from two signals:

1. **Neighbour-normal delta** — the angular deviation between the surface normal of a pixel's stored hit and the average normal of its 3 × 3 neighbourhood. Large deltas indicate that neighbouring pixels hit surface patches with sharply different orientations, which correlates with transport-path discontinuities rather than smooth geometry.

2. **Phase-incoherence signal** — a derived signal that combines neighbour-normal delta with first-hit divergence and orientation persistence to produce a scalar per-pixel incoherence score.

The coherence score is the complement: `coherence = 1 − incoherence`. It ranges from 0 (maximally incoherent) to 1 (fully coherent).

---

## Measured Values (Wormhole Checkpoint Ladder)

Phase-coherence scores were computed for the mouth and bridge checkpoints:

| Checkpoint | Band-region coherence | Outside-band coherence | PPM gap |
|---|---|---|---|
| mouth | 0.639 | 0.801 | **0.162** |
| post-throat backstep (bridge) | 0.764 | 0.796 | **0.032** |

**Interpretation:**

- At the mouth, visible banding correlates with a substantial coherence drop (0.162 gap). The band-region coherence score (0.639) is significantly lower than the surrounding non-band score (0.801), suggesting that bands at the near-side observer position correspond to phase-boundary transitions in the ray field.

- At the bridge, the coherence gap is smaller (0.032). This is consistent with the bridge's morphological character: the bridge is not a two-phase system with clean boundaries, but a fragmented, line-rich geometry where the ray field is broadly disordered rather than sharply partitioned.

---

## Relation to Visible Banding

Visible banding in xPRIMEray renders has been associated with regions of reduced phase coherence rather than with numerical artifacts from insufficient sampling, precision errors, or shading discontinuities.

The proposed mechanism: in regions where multiple locally valid ray-path solutions coexist, spatial sampling collapses inconsistently across phase boundaries, producing bands where one solution family dominates on each side. This is consistent with multi-solution geodesic fields and interference-like domain partitioning — not with classical ray-tracing artifacts.

**Caution:** This interpretation is a hypothesis grounded in the coherence-score correlations observed at two checkpoints. It is not yet confirmed by a controlled experiment that isolates the candidate-hit selection mechanism from other contributors to banding.

---

## Phase-Guided Hit Selection

A phase-guided first-hit proxy was tested as a band-reduction signal (2026-04-25 analysis). Results:

- Mean local selection score reduced by 0.040 on average.
- Visible-band / incoherence correlation reduced by −0.033.
- Visible-band phase-boundary fraction changed by only −0.007.

This positions the phase-coherence signal as a **tie-breaker** in hit selection — useful as an additional constraint, not as a standalone band-reduction mechanism.

---

## Geometric Phase Memory (Future Layer)

The phase-coherence field provides the empirical foundation for a proposed **geometric phase memory** layer: a persistent record of phase-organisation across frames and observer positions, analogous (as design grammar) to the Phase Prime Metric in Anirban Bandyopadhyay's work on phase-coherent computation.

In this framework:
- **High-coherence regions** correspond to stable phase attractors (the near-side and far-side basins).
- **Phase boundaries** (low-coherence band locations) correspond to transitions between attractor basins.
- **Curvature centres** (Hough arc-fit candidates) are candidate nodes of silence — structural anchors from which phase relationships radiate.

This is explicitly an analogy and a design grammar, not a physical or biological equivalence claim. See [Research/phase_coherence_field.md](../Research/phase_coherence_field.md) and [papers/paper_001_causal_observer_ladders/analysis/geometric_phase_memory.md](../papers/paper_001_causal_observer_ladders/analysis/geometric_phase_memory.md) for the full framework.

---

## Cross-References

- Full analysis in paper 001: [papers/paper_001_causal_observer_ladders/paper.md](../papers/paper_001_causal_observer_ladders/paper.md) §5.6
- Phase sampling domain update: [papers/paper_001_causal_observer_ladders/analysis/phase_sampling_domain_update.md](../papers/paper_001_causal_observer_ladders/analysis/phase_sampling_domain_update.md)
- Geometric phase memory framework: [papers/paper_001_causal_observer_ladders/analysis/geometric_phase_memory.md](../papers/paper_001_causal_observer_ladders/analysis/geometric_phase_memory.md)
- Research synthesis: [Research/phase_coherence_field.md](../Research/phase_coherence_field.md)
- Tile coherence interaction: [tile_coherence.md](tile_coherence.md)
