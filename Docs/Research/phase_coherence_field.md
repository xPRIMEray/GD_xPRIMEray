# Phase Coherence Field

**Status:** Analysis and design documentation only. No renderer changes. All mappings are analogical design grammar, not physical or biological equivalence claims.

This note synthesises the phase-coherence analysis for xPRIMEray and describes the proposed geometric phase memory layer as a future computational extension.

Source: [papers/paper_001_causal_observer_ladders/paper.md §5.6](../papers/paper_001_causal_observer_ladders/paper.md), [papers/paper_001_causal_observer_ladders/analysis/geometric_phase_memory.md](../papers/paper_001_causal_observer_ladders/analysis/geometric_phase_memory.md)

---

## What Phase Coherence Measures

The phase-coherence field assigns a scalar score to each pixel, estimating whether that pixel's transport solution is consistent with its immediate 3 × 3 neighbourhood. High coherence indicates smooth variation — adjacent pixels reached their hits via similar ray paths. Low coherence indicates a phase boundary — a location where two distinct solution families meet in pixel space.

The score is constructed from:

1. **Neighbour-normal delta** — angular deviation between a pixel's stored-hit normal and its neighbourhood average. Large deviations indicate adjacent rays hitting structurally different surfaces.
2. **Phase-incoherence field** — a composite of neighbour-normal delta and first-hit divergence, summarised as a scalar per-pixel incoherence score.

---

## Empirical Results

Coherence was measured at two checkpoints (mouth and bridge):

| Checkpoint | Band-region coherence | Outside-band coherence | Gap |
|---|---|---|---|
| mouth | 0.639 | 0.801 | 0.162 |
| bridge (post-throat backstep) | 0.764 | 0.796 | 0.032 |

**At the mouth:** visible banding corresponds to a substantial coherence reduction. The 0.162 gap indicates that band locations are structurally different from their surroundings in the transport-solution field, not just in pixel intensity.

**At the bridge:** the gap collapses to 0.032. The bridge is broadly disordered — high Hough-line count, fragmented components, low contour eccentricity — rather than sharply partitioned into two coherent families. The bridge does not exhibit clean phase boundaries; it exhibits diffuse incoherence.

---

## Proposed Mechanism for Banding

The coherence correlation supports a specific mechanism:

> In regions where multiple locally valid ray-path solutions coexist (multi-solution geodesic field), spatial sampling collapses inconsistently across phase boundaries. Each side of a boundary selects a different dominant solution, producing a visible band.

This is distinct from classical rendering artifacts (insufficient samples, numerical precision, shading discontinuities) in a testable way: classical artifacts would not show the coherence-gap pattern — they would produce either uniformly low coherence (noise) or checkpoint-independent artifacts.

The mechanism is hypothesised, not confirmed. A definitive test would require controlled variation of the candidate-hit selection policy while holding the field and observer position fixed.

---

## Geometric Phase Memory: Design Framework

The phase-coherence diagnostics suggest a future computational layer: **geometric phase memory**. This is a proposed extension — not a current implementation — that would use persistent phase-organisation across frames and observer positions to guide sampling texture selection.

The framework borrows design grammar (not physical claims) from Anirban Bandyopadhyay's work on phase-coherent biological computation, specifically the Geometric Musical Language (GML) and Phase Prime Metric (PPM). These are adopted as an organising vocabulary, not as a model of biological or physical equivalence.

### Conceptual mapping

| Bandyopadhyay construct | xPRIMEray equivalent |
|---|---|
| GML geometric event | Detected primitive: Hough line, arc, circle, corner, annular sector |
| Phase tag | Per-event coherence score from phase-incoherence field |
| Phase Prime Metric | Per-checkpoint coherence score and cross-checkpoint coherence delta |
| Node of silence | Curvature-centre candidate, line-intersection cluster, band-boundary junction |
| Phase attractor | Geometric event grammar that recurs across checkpoints (e.g. radial dominance) |
| Phase attractor basin | Set of observer positions producing the same dominant event grammar |
| Time-crystal recurrence | Attractor-basin membership recurring without physical position repeat |
| Phase transition | Departure from one attractor basin to another (near-side → bridge → far-side) |

### Five-phase implementation roadmap

| Phase | Goal |
|---|---|
| A — Primitive extraction | Build per-checkpoint inventory of GML-equivalent events (Hough lines, arcs, corners) with coherence tags |
| B — Attractor / node detection | Cluster line intersections and arc centres; score candidates by coherence gradient |
| C — Persistence tracking | Match anchor candidates across checkpoints; compute persistence scores; flag attractors vs transition events |
| D — Domain-aware render guidance | Use persistence map to recommend sampling texture per observer position (human-review artifact, not automatic directive) |
| E — Validation separation | Label all phase-memory outputs `[PHASE-MEMORY PREVIEW]`; never mix with Pass 1 truth |

### Epistemic posture

This framework is adopted as a productive design grammar. Bandyopadhyay's GML and PPM provide a structured vocabulary for describing what the diagnostics already show. The claim is not that xPRIMEray instantiates a biological time crystal or that wormhole ray transport is equivalent to phase-coherent neural computation. The claim is that the vocabulary is useful for organising the diagnostic evidence and motivating a concrete implementation roadmap.

---

## Current Evidence Quality

| Concept | Best existing artifact | Coverage |
|---|---|---|
| GML line events | Hough detections, geometry structure contact sheet | 6 checkpoints |
| GML arc/circle events | Curvature-centre polar candidates | 2 checkpoints |
| PPM coherence scores | Band/outside coherence table | 2 checkpoints |
| Nodes of silence | Curvature-centre candidates, line intersections | 2 checkpoints |
| Attractor-basin decomposition | Regime clustering (k=3, ARI=0.5946) | 6 checkpoints |
| Orientation persistence | Log-polar histograms | 6 checkpoints |

Full six-checkpoint coherence scoring is the highest-priority evidence gap.

---

## Cross-References

- Diagnostics page: [../diagnostics/phase_coherence.md](../diagnostics/phase_coherence.md)
- Geometric phase memory full framework: [../papers/paper_001_causal_observer_ladders/analysis/geometric_phase_memory.md](../papers/paper_001_causal_observer_ladders/analysis/geometric_phase_memory.md)
- Phase sampling domain update: [../papers/paper_001_causal_observer_ladders/analysis/phase_sampling_domain_update.md](../papers/paper_001_causal_observer_ladders/analysis/phase_sampling_domain_update.md)
- Paper 001 §5.6–5.7: [../papers/paper_001_causal_observer_ladders/paper.md](../papers/paper_001_causal_observer_ladders/paper.md)
