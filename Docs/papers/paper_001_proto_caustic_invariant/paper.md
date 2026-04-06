---
title: Proto-Caustic Invariant in Geometry-Aware Wormhole Transport
authors:
date: 2026-04-05
invariant: proto_caustic_annulus_layer_1_radial_bin_3
status: validated
related_fixtures:
  - wormhole_validation_harness
---

# Paper 001: Proto-Caustic Invariant in Geometry-Aware Wormhole Transport

## Abstract

We define a proto-caustic invariant for a deterministic wormhole rendering harness in which curved transport, topological remap, and observer-side film formation are validated as a single optical process. The invariant is not a generic image-space score; it is a portal-local annular structure characterized by hit density, angular continuity, positive-overlap continuity, and a radial transition strength that remains stable under controlled runs. Conventional ray tracing lacks such a constraint because its validation language is usually framed in pixel agreement, stochastic convergence, or isolated hit statistics, rather than in the preservation of structured focusing regions tied to transport geometry. We demonstrate that the wormhole prototype in `GD_xPRIMEray` admits a preserved destination-side annulus under deterministic validation, and that this annulus can serve as both a correctness condition and a guide for geometry-aware performance work. The result is a rendering contract in which optical structure is treated as primary evidence rather than as an accidental by-product of computation.

## 1. Motivation

The classical rendering imagination still leans, even when it becomes numerically elaborate, upon a largely straight-ray picture. Rays are sampled, traced, classified, and accumulated. When the geometry is mild, and when the topology is fixed, such an account is often sufficient. A wormhole scene, however, makes this language inadequate in two related ways. First, the ray does not merely move through a field; it can cross a shell, undergo a scene remap, and continue in a different spatial context. Second, the observer does not merely receive isolated samples; the image acquires structure through coherent families of rays whose organization matters.

In such a system, purely stochastic validation is too weak. One may know that some rays remap, that some queries occur, or even that some film writes appear, yet still fail to know whether the optical structure being formed is the one the geometry demands. The question is therefore not only whether the pipeline is alive, but whether the image contains regions whose stability reveals that the transport is behaving as a geometrically coherent whole.

Not all image regions are equal in such a setting. Some sectors are costly but largely sterile. Others encode a concentrating structure: a stable annulus, a continuity of hit class, a distinct radial transition. These are not mere numerical conveniences. They are signatures that the geometry is selecting certain optical pathways more strongly than others. The need for an invariant arises here. Without it, optimization remains blind and correctness remains under-specified.

<!--
Perspective Alignment Notes
- Penrose: the emphasis should fall on geometry as the source of visible structure, not on computation as an end in itself.
- Bandyopadhyay: propagation should be read as temporally coherent organization, not as a bag of independent events.
- Orch OR: the observer-facing image is treated as a structured reduction of competing possibilities, but only in a disciplined physical sense.
-->

## 2. Concept: Proto-Caustic Invariant

The proto-caustic invariant is defined as a portal-local, ring-indexed optical constraint. In the present wormhole harness, the target object is the destination-side annulus identified by:

- `layer = 1`
- `radial_bin = 3`

This ring is not selected arbitrarily. Portal-centric sector aggregation reveals that it carries a stable high-density band, complete angular occupancy, strong hit continuity, strong positive-overlap continuity, and a pronounced radial transition relative to the adjacent inner ring. It is therefore reasonable to treat it as a proto-caustic structure: not a formal catastrophe-theoretic caustic in the strongest analytic sense, but a robust focusing signature in the measured transport.

Operationally, the invariant is expressed through four quantities:

- hit density in the target annulus
- hit continuity ratio across neighboring theta sectors
- positive-overlap continuity ratio across neighboring theta sectors
- radial hit gradient from the preceding ring

These quantities together form a geometric-statistical object. Density alone would not suffice, since density can be inflated or flattened by unrelated scene changes. Continuity alone would not suffice, since a uniformly weak ring could still be continuous. The radial gradient matters because a caustic-like structure is not merely present; it differentiates itself from its surroundings. The invariant therefore asks not only whether a ring is occupied, but whether it stands out in the right way.

Why should an annular structure appear at all? Once transport bends around a mouth, crosses a remapping surface, and resolves against downstream geometry, neighboring rays do not populate the image plane uniformly. They arrive in portal-relative families. The ring is the observer-side trace of that family structure. It records a focusing tendency without pretending that the present prototype has solved the full analytic classification of wormhole caustics.

<!--
Perspective Alignment Notes
- Penrose: caustic structure is treated as a natural consequence of geometry and congruence behavior.
- Bandyopadhyay: the invariant is described as a stable temporal attractor across repeated deterministic runs.
- Orch OR: the invariant is a structured selection principle, but it remains tied to measurable continuity and gradient conditions.
-->

## 3. Method

The method is intentionally concrete. The wormhole harness runs as a deterministic static scene with fixed camera transform, locked input, stable framing, and repeatable film-path capture. Pass-1 transport integrates rays through a GRIN field around the portal mouth. Boundary-shell interaction triggers scene remap. Pass-2 then performs geometry candidate generation and `OverlapOnly` query dispatch in the remapped scene before narrowphase hit resolution and film write.

Measurement is observer-centric but portal-local. Post-remap segments are aggregated into sector bins indexed by:

- portal layer
- radial bin
- theta sector
- direction bucket

This sector map supports three distinct but connected tasks:

1. congruence inspection  
   Neighboring post-remap rays can be evaluated as coherent families rather than as isolated segments.

2. ring-density analysis  
   Portal-local annuli can be examined for continuity, density, and radial transition behavior.

3. contract evaluation  
   The proto-caustic invariant can be checked directly against deterministic thresholds after each validation run.

The method also uses a figure quartet so that visual, geometric, and quantitative evidence remain coupled:

- Figure A: raw film-buffer result
- Figure B: composed result with research overlay
- Figure C: portal-centric ring-density map
- Figure D: compact contract and performance summary

GRIN transport, portal mapping, and sector binning are thus treated as parts of a single validation language. The observer image is not detached from the geometry; it is the geometry's report in observer coordinates.

<!--
Perspective Alignment Notes
- Keep this section concrete and implementation-facing.
- The language should avoid metaphor and let the structure of the harness carry the argument.
- Observer-centric interpretation is methodological here, not mystical.
-->

## 4. Results

The four figures should be read as four projections of one structure rather than as separate diagnostics.

### Figure A

![Figure A — Main Render](../../../output/wormhole_test/figures/figure_A_main_render.png)

Figure A isolates the accumulated film result. It answers the narrow question: what does the observer-facing film actually contain once remap, hit resolution, and film write have completed? No explanatory overlay is present, so the image remains a pure rendering artifact.

### Figure B

![Figure B — Composed Render with Research Inset](../../../output/wormhole_test/figures/figure_B_composed_overlay.png)

Figure B restores context. The composed render preserves the main film image while adding a research-view inset, a mode label, and contract status text. The inset makes the relation between the visible film structure and the underlying wormhole layout legible without disturbing the main image.

### Figure C

![Figure C — Ring Density](../../../output/wormhole_test/figures/figure_C_ring_density.png)

Figure C reveals what Figures A and B imply but do not quantify directly: the optical bundle has portal-local structure. The destination-side outer ring stands out as a stable annular band with strong continuity and a marked radial separation from its interior neighbor. This is the image of the invariant in sector space.

### Figure D

![Figure D — Metrics Table](../../../output/wormhole_test/figures/figure_D_metrics_table.png)

Figure D closes the loop by presenting the contract and timing state compactly. The proto-caustic invariant is shown as an explicit pass condition, the low-value budget is shown alongside it, and the active low-value throttle profile is made visible rather than implicit. The same artifact places optical metrics and pass-2 performance metrics in the same frame, which is essential if geometry-aware optimization is to remain accountable to geometry-aware correctness.

The four figures therefore do not compete. Figure A shows what is seen. Figure B shows where it comes from. Figure C shows how it organizes. Figure D shows whether that organization remains preserved under the current operating profile.

<!--
Perspective Alignment Notes
- The reader should feel that the quartet is one object seen from four coordinate systems.
- Penrose-style emphasis: visual appearance is a projection of geometry.
- Keep the prose tight enough that the figures appear as evidence, not decoration.
-->

## 5. Validation

The correctness claim is intentionally falsifiable. The proto-caustic invariant passes only if the designated annulus satisfies all threshold conditions:

- minimum hit density
- minimum hit continuity ratio
- minimum positive-overlap continuity ratio
- minimum radial gradient

The low-value sector budget adds a second gate. It asks whether the known low-value region retains a bounded query share relative to its baseline. Together these two contracts define a positive condition and a negative condition: preserve the annulus that matters, and prevent wasted work from re-expanding where it has already been shown to be unproductive.

Failure is therefore easy to state. A run fails if the annulus weakens below threshold, if the low-value region reclaims too much query share, or if an apparent timing gain is purchased by drift in `geom_hits` or `final_write_px`. In practice, a useful rejected case has already occurred: increasing the low-value throttle from period `2` to period `3` preserved the formal contracts, yet weakened the annulus metrics and reduced hits and final writes while worsening the target timing buckets. This is precisely the sort of case that a serious validation language must be able to reject.

Determinism is central here. A contract that passes only in distribution, or only under operator-dependent framing, is too weak for this kind of work. The same scene, same camera, same portal framing, and same harness conditions must recur if the invariant is to serve as truth condition rather than as suggestion.

<!--
Perspective Alignment Notes
- Penrose: the question is not whether the system can narrate a success, but whether the geometry makes a true demand that can be failed.
- Bandyopadhyay: stability must be exhibited across repeated temporal realizations of the run.
- Determinism should be treated as part of the epistemology, not just a testing convenience.
-->

## 6. Discussion

The proto-caustic invariant may be understood in two simultaneous ways. It is first a geometric constraint: a preserved annulus in portal-local coordinates whose density and continuity indicate that the wormhole transport has produced a stable optical concentration. It is second a computational guide: a statement about where work matters and where it does not.

This matters for adaptive rendering. If adaptive logic sees only cost, it may mistakenly suppress the very regions in which geometry concentrates visible structure. If it sees only hits, it may fail to distinguish between diffuse support and organized annular concentration. The invariant provides a more disciplined target. It tells the system that some observer-facing regions are not merely productive, but structurally informative.

There is, accordingly, an information-flow interpretation available, though it must be stated carefully. The renderer does not literally acquire knowledge in the human sense. What it does acquire is a stable separation between sectors that preserve the optical signature and sectors that consume work without materially supporting it. In that limited but useful sense, the system learns where reality concentrates. The phrase is defensible only because the concentration is made explicit by deterministic structure and measured thresholds, not because we have imported a metaphor of intelligence where none is warranted.

This is also where observer-centric language becomes legitimate. The observer is not introduced as a metaphysical extra. The observer is simply the image-plane frame in which continuity, density, and ring transition become measurable. The coupling is therefore between transport geometry and observer-visible structure. That coupling is exactly what a wormhole renderer must preserve if it is to remain accountable both as a physical model and as a rendering system.

<!--
Perspective Alignment Notes
- Introduce observer-system coupling gently and only through measurable structure.
- Bandyopadhyay influence: emphasize coherence and temporal persistence without invoking unverifiable mechanism.
- Orch OR influence: permit structured emergence as a framing idea, but refuse any claim not secured by the current measurements.
-->

## 7. Conclusion

We defined a proto-caustic invariant as a portal-local annular constraint expressed through density, continuity, and radial transition metrics.  
We showed that the deterministic wormhole harness preserves this annulus under the current kept geometry-aware profile while also supporting performance-aware regression gates.  
This matters because wormhole rendering can now be validated not only by whether it produces pixels, but by whether it preserves the optical structure those pixels are meant to represent.

## Appendix A

Current deterministic harness configuration for the active invariant and throttle profile:

- proto-caustic target ring: `layer = 1`, `radial_bin = 3`
- invariant thresholds:
  - `min_hit_density = 800`
  - `min_hit_continuity_ratio = 0.95`
  - `min_positive_overlap_continuity_ratio = 0.95`
  - `min_radial_gradient = 600`
- low-value budget:
  - `layer = 0`
  - `radial_bin = 3`
  - `baseline_query_share = 0.4011`
  - `max_query_share_scale = 0.9`
  - `maximum_allowed_query_share = 0.361`
- active kept low-value throttle profile:
  - `layer = 0`
  - `radial_bin = 3`
  - `theta bins = {13,14,15,0}`
  - `period = 2`

<!--
Perspective Alignment Notes
- Appendix remains purely operational.
- No interpretation should be smuggled into the config summary.
-->

