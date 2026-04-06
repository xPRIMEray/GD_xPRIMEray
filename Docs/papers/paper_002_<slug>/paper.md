---
title: Low-Value Sector Budget as a Negative Invariant in Geometry-Aware Wormhole Transport
authors: <fill or placeholder>
date: 2026-04-05
invariant: low_value_sector_budget
status: draft
related_fixtures: wormhole_prototype
---

# Paper 002: Low-Value Sector Budget as a Negative Invariant in Geometry-Aware Wormhole Transport

## Abstract

We define a low-value sector budget as a negative invariant for deterministic wormhole rendering, complementing the proto-caustic annulus introduced in Paper 001. Whereas the proto-caustic invariant preserves a destination-side high-value optical structure, the present contract limits how much overlap-query work may continue to accumulate in portal-local sectors shown to have negligible contribution to the preserved image. Traditional ray tracing has little language for such a condition, since waste is usually measured globally through timing counters rather than geometrically through persistent low-yield regions. We demonstrate that the wormhole harness in `GD_xPRIMEray` admits a stable budget contract on a non-invariant outer-ring family and that a modest deterministic throttle can reduce pass-2 query cost while preserving hits, final writes, and the annular structure defined previously. The result is a paired validation system in which geometry dictates not only what must be preserved, but also where expenditure should remain bounded.

## 1. Motivation

Paper 001 established that wormhole transport in this harness should not be judged merely by whether rays survive remap and reach film. It showed that a portal-local annulus on the destination side carries structured optical value: continuity, density, and radial separation that remain stable under deterministic runs. That result immediately raises a complementary question. If some regions are demonstrably rich in optical significance, are there others whose cost can be bounded without injury to the preserved structure?

Classical rendering practice often answers such a question only indirectly. One measures global timing, samples fewer rays, or prunes heuristically, then checks whether the image appears worse. Such a method is too blunt for a wormhole scene. The transport is curved, remapped, and observer-dependent; it is therefore easy to save time in the wrong place while leaving total correctness under-specified. One needs a notion of negative structure: a region whose repeated expenditure can be shown to have low value relative to the maintained optical signature.

The deterministic harness already suggests such a region. Portal-sector usefulness mapping shows that a subset of outer-ring sectors on `layer = 0`, `radial_bin = 3` accumulates heavy query traffic while contributing little or nothing to the preserved annulus and, in the strongest cases, no final hits at all. The question is not whether these sectors are metaphysically unimportant. It is whether the geometry permits their budget to be constrained without corrupting the observer-facing structure that the transport is supposed to preserve.

The motivation of the present paper is therefore complementary to that of Paper 001. There the task was to identify a positive invariant: a high-value annulus that must survive. Here the task is to identify a negative invariant: a low-value region whose query share must not be allowed to grow beyond an explicit bound. Together they begin to define not only where wormhole optical reality concentrates, but also where computation may be prevented from dissipating into regions that do not materially support that concentration.

<!--
Perspective Alignment Notes
- Penrose: the emphasis should remain on geometry as the source of both value and non-value, not on computation as an autonomous criterion.
- Bandyopadhyay: repeated low-yield sectors matter because their behavior is temporally coherent across deterministic runs, not because of a single noisy snapshot.
- Orch OR: the observer-facing image again appears as a disciplined reduction of possibilities, but now the reduction includes the refusal to over-invest in sectors that do not support the stable structure.
-->

## 2. Concept: Low-Value Sector Budget

The low-value sector budget is a portal-local, region-specific constraint on query allocation. In the present harness, the budget applies to the outer-ring family identified by:

- `layer = 0`
- `radial_bin = 3`

Within this family, usefulness mapping further identifies a throttle profile that is currently retained:

- `theta bins = {13,14,15,0}`
- `period = 2`

The conceptual object is not a generic timing budget. It is a geometry-aware negative invariant: a bound on the fraction of overlap-query work that may be spent in a region whose persistent yield has been shown to be low relative to the preserved annulus. Operationally, the contract is expressed by a maximum allowed query share derived from a deterministic baseline:

- baseline low-value query share: `0.4011`
- maximum allowed scale: `0.9`
- maximum allowed low-value query share: `0.361`

This object is negative in the technical sense that it constrains waste rather than preserving a directly visible concentration. Yet it remains geometric and statistical, not merely administrative. The region is defined in portal-local coordinates. The budget is measured through actual query share. Its legitimacy depends upon the stability of the low-yield classification across deterministic runs and upon the continued preservation of the positive annulus invariant.

Why is such a budget meaningful? Because the wormhole scene exhibits an asymmetry of value. Some post-remap sectors support the destination-side annulus. Others consume pass-2 work while contributing little to hits, final writes, or the preserved ring metrics. The budget therefore does not declare these sectors nonexistent. It declares instead that their cost must remain subordinate to the structure that the geometry has already revealed to be important.

In this sense, the low-value sector budget is the complement of the proto-caustic invariant. One says: this annulus must remain. The other says: this low-yield expenditure must not re-expand beyond a defined bound. The pair forms a more complete rendering logic than either would alone.

<!--
Perspective Alignment Notes
- Penrose: the budget should read as a consequence of portal-local structure, not as an arbitrary computational convenience.
- Bandyopadhyay: the low-value classification is meaningful only because it persists across repeated deterministic realizations.
- Orch OR: this is a structured exclusion principle, but it stays grounded in measured query share, hit yield, and preserved annular structure.
-->

## 3. Method

The method remains the deterministic wormhole harness established previously. Camera transform and input are fixed. Pass-1 transport advances rays through the GRIN field. Boundary interaction triggers remap between scenes. Pass-2 performs candidate generation and `OverlapOnly` query dispatch before narrowphase hit resolution and film write. No change to transport law, remap logic, broadphase policy, or hit acceptance is introduced for the purposes of the present paper.

The crucial addition is usefulness classification. Post-remap segments are aggregated in portal-local bins indexed by:

- layer
- radial bin
- theta sector
- direction bucket when available

For each class, the harness records:

- query count
- geometry hit count
- final-write relevance
- relation to the invariant annulus
- share of total query work

This makes it possible to identify high-cost, low-yield sectors and to distinguish them from sectors that materially support the preserved annulus. The low-value sector budget is then evaluated as a contract after each deterministic run. The contract passes only if the measured query share of the designated low-value family remains below its maximum allowed share, while the proto-caustic annulus continues to satisfy its own thresholds.

The active kept throttle profile is deliberately narrow. It does not remove the low-value sectors. It reduces overlap-query frequency deterministically within a known outer-ring family:

- `layer = 0`
- `radial_bin = 3`
- `theta bins = {13,14,15,0}`
- `period = 2`

As in Paper 001, the figure quartet serves as the common evidence frame:

- Figure A: raw film-buffer result
- Figure B: composed render with research inset
- Figure C: ring-density map
- Figure D: compact metrics and contract summary

The method is therefore not a heuristic attached after the fact. It is a direct continuation of the same portal-centric measurement language used to define the positive annular invariant.

<!--
Perspective Alignment Notes
- Keep the language concrete and implementation-facing.
- Avoid any suggestion that the budget is a purely aesthetic choice; it is measured and enforced through the same deterministic harness.
- Let the shared figure quartet make the continuity with Paper 001 obvious.
-->

## 4. Results

The results should again be read as coordinated views of the same optical-computational structure.

### Figure A

![Figure A — Main Render](../../../output/wormhole_test/figures/figure_A_main_render.png)

Figure A shows the raw film result under the active kept throttle profile. The main point is not that the image merely exists, but that the observer-facing structure remains stable while a bounded reduction is applied to a designated low-value region.

### Figure B

![Figure B — Composed Render with Research Inset](../../../output/wormhole_test/figures/figure_B_composed_overlay.png)

Figure B places the image in explanatory context. The research overlay indicates the active mode, the contract status, and the highlighted invariant ring. This matters because the budget contract must be interpreted relative to a preserved structure, not in isolation from it.

### Figure C

![Figure C — Ring Density](../../../output/wormhole_test/figures/figure_C_ring_density.png)

Figure C confirms that the destination-side annulus remains the dominant ring-like concentration. The negative invariant is therefore not winning by flattening the geometry; it is winning, when it does win, by bounding expenditure outside the preserved annular structure.

### Figure D

![Figure D — Metrics Table](../../../output/wormhole_test/figures/figure_D_metrics_table.png)

Figure D is especially important in the present paper because it places both contracts in the same compact frame. It records the proto-caustic invariant status, the low-value sector budget status, the active throttle profile, and the key pass-2 timing metrics. It therefore shows whether the retained geometry-aware throttle is actually functioning as intended: lower pass-2 cost without loss of hits, final writes, or annular continuity.

The central empirical result is that the kept profile for the low-value outer-ring family improves the target timing buckets while preserving the annulus and leaving `geom_hits` and `final_write_px` unchanged. A stronger throttle on the same region, however, fails to maintain this balance. The result is thus not simply that a budget can be imposed, but that the budget has a safe boundary.

<!--
Perspective Alignment Notes
- The reader should feel continuity with Paper 001: the same quartet, the same scene, the same geometry, but a new complementary claim.
- Penrose-style emphasis: the low-value budget is legible only because the figures remain projections of one structure.
- Keep the prose tightly evidential.
-->

## 5. Validation

Validation in this paper is necessarily two-sided. The proto-caustic annulus remains the positive condition. The low-value sector budget adds the complementary negative condition. A run is acceptable only if both are satisfied.

The positive condition requires the destination-side annulus to preserve:

- minimum hit density
- minimum hit continuity ratio
- minimum positive-overlap continuity ratio
- minimum radial gradient

The negative condition requires the designated low-value family to remain below its maximum allowed query share. This produces a strict asymmetry in the validation language: the system must preserve what geometry has shown to be valuable, and it must also prevent waste from reclaiming too large a portion of the pass-2 budget.

The kept profile satisfies both conditions:

- low-value throttle region: `layer = 0`, `radial_bin = 3`, `theta bins = {13,14,15,0}`
- throttle period: `2`
- proto-caustic invariant: pass
- low-value budget: pass
- `geom_hits`: unchanged
- `final_write_px`: unchanged
- `pass2.query`: improved
- `pass2.physics`: improved

The rejected boundary case is equally instructive:

- same low-value region
- throttle period: `3`

This stronger throttle still passed the formal contracts, yet it weakened annular metrics, worsened the target timing buckets, and reduced `geom_hits` and `final_write_px`. It is therefore rejected. This case makes clear that a contract can remain technically satisfied while the overall operating point becomes worse. For that reason, retained changes must satisfy not only invariant pass conditions but also stable hit/write behavior and actual performance improvement.

Determinism is again essential. Without fixed framing and repeatable capture, one could mistake a fragile gain for a meaningful reduction of low-value expenditure. The negative invariant is credible only because the low-yield classification and the preserved annulus recur under the same conditions.

<!--
Perspective Alignment Notes
- Penrose: validation is about truth conditions, not merely numerical optimism.
- Bandyopadhyay: the budget matters because the low-yield region is stable across repeated temporal realizations.
- Keep the section explicitly falsifiable and deterministic.
-->

## 6. Discussion

The low-value sector budget shifts geometry-aware optimization into a more disciplined phase. Earlier micro-optimizations attempted to reduce cost by manipulating per-segment reuse, overlap result handling, or candidate bookkeeping. Many of these efforts either failed to help or introduced drift. The present budget is different in kind. It does not begin by asking how to reuse more aggressively. It begins by asking where the geometry itself indicates that expenditure is persistently low in value.

This matters for future adaptive rendering. A system that sees only positive structure may preserve an annulus and still squander work elsewhere. A system that sees only negative pressure may save time by eroding the very ring that makes the wormhole image geometrically meaningful. The combination of positive and negative invariants suggests a more balanced principle: adaptive logic should preserve concentrations of structured optical reality while suppressing the recurrent expansion of low-yield sectors.

There is an observer-aware interpretation available here as well, though it should remain modest. The renderer is not merely tracing arbitrary rays. It is separating portal-local regions into those that materially support the observer-facing image and those that do not. In that limited sense, it acquires a structured relation between optical significance and computational effort. One may say, cautiously, that the system learns not only where reality concentrates, but also where it does not repay further expenditure. The formulation is useful so long as it remains subordinate to deterministic evidence and portal-local measurement.

The present result also suggests a broader methodological lesson. Negative invariants may be just as important as positive ones in non-Euclidean or topologically complex rendering systems. It is not enough to preserve a structure of value. One must also bound the regions in which recurrent cost fails to support that value. This paired logic may be especially important wherever curved transport causes some observer-local structures to become privileged while others remain geometrically peripheral.

<!--
Perspective Alignment Notes
- Introduce the observer-system coupling only through measured allocation and visible consequence.
- Bandyopadhyay influence: coherence includes the persistence of low-yield classification, not only the persistence of high-value rings.
- Avoid over-claiming; keep the argument tied to this harness and these measurements.
-->

## 7. Conclusion

We defined a low-value sector budget as a negative invariant that constrains pass-2 expenditure in a portal-local outer-ring family.  
We showed that the deterministic wormhole harness can retain a modest geometry-aware throttle that improves the target timing buckets while preserving hits, final writes, and the proto-caustic annulus.  
This matters because geometry-aware wormhole validation can now regulate not only what optical structure must survive, but also where computational effort must remain bounded.

## Appendix A

Current deterministic harness configuration for the active low-value sector budget and companion annulus invariant:

- proto-caustic target ring:
  - `layer = 1`
  - `radial_bin = 3`
- proto-caustic thresholds:
  - `min_hit_density = 800`
  - `min_hit_continuity_ratio = 0.95`
  - `min_positive_overlap_continuity_ratio = 0.95`
  - `min_radial_gradient = 600`
- low-value sector budget:
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
- rejected boundary profile:
  - same region
  - `period = 3`

<!--
Perspective Alignment Notes
- Appendix remains purely operational.
- No interpretation should be imported into the configuration summary.
-->
