---
title: Low-Value Sector Budget as a Negative Invariant in Geometry-Aware Wormhole Transport
authors: <fill or placeholder>
date: 2026-04-05
invariant: low_value_sector_budget
status: draft
related_fixtures: wormhole_prototype
---

# Paper 002: Low-Value Sector Budget as a Negative Invariant in Geometry-Aware Wormhole Transport

![Wormhole DualRealityTransport](../../../output/dual_reality/wormhole_inset_baseline.png)

*Current wormhole DualRealityTransport capture showing the curved main view, straight transport reference panel, and diagnostic overlays.*

## Abstract

We define a low-value sector budget as a negative invariant for deterministic wormhole rendering, complementing the proto-caustic annulus introduced in Paper 001. Whereas the proto-caustic invariant preserves a destination-side high-value optical structure, the present contract limits how much overlap-query work may continue to accumulate in portal-local sectors shown to have negligible contribution to the preserved image. Traditional ray tracing has little language for such a condition, since waste is usually measured globally through timing counters rather than geometrically through persistent low-yield regions. We demonstrate that the wormhole harness in `GD_xPRIMEray` admits a stable budget contract on a non-invariant outer-ring family and that a modest deterministic throttle reduces pass-2 query cost while preserving hits, final writes, and the annular structure defined previously. The result is a paired validation system in which geometry dictates not only what must be preserved, but also where expenditure should remain bounded.

## 1. Motivation

Paper 001 established that wormhole transport in this harness should not be judged merely by whether rays survive remap and reach film. It showed that a portal-local annulus on the destination side carries structured optical value: continuity, density, and radial separation that remain stable under deterministic runs. That result immediately raises a complementary question. If some regions are demonstrably rich in optical significance, are there others whose cost can be bounded without injury to the preserved structure?

Classical rendering practice usually answers such a question only indirectly. One measures global timing, samples fewer rays, or prunes heuristically, then asks whether the image appears worse. Such a method is too blunt for a wormhole scene. The transport is curved, remapped, and observer-dependent; it is easy to save time in the wrong place while leaving correctness under-specified. One needs a notion of negative structure: a region whose repeated expenditure can be shown to have low value relative to the maintained optical signature.

The deterministic harness already suggests such a region. Portal-sector usefulness mapping shows that a subset of outer-ring sectors on `layer = 0`, `radial_bin = 3` accumulates heavy query traffic while contributing little or nothing to the preserved annulus and, in the strongest cases, no final hits at all. The question is not whether these sectors are metaphysically unimportant. It is whether the geometry permits their budget to be constrained without corrupting the observer-facing structure that transport is supposed to preserve.

The motivation of the present paper is therefore complementary to that of Paper 001. There the task was to identify a positive invariant: a high-value annulus that must survive. Here the task is to identify a negative invariant: a low-value region whose query share must not be allowed to grow beyond an explicit bound. Meaningful rendering requires both. Together they begin to define not only where wormhole optical reality concentrates, but also where computation may be prevented from dissipating into regions that do not materially support that concentration.

<!--
Perspective Alignment Notes
- Penrose: geometry defines the importance distribution; computational value is derivative, not primary.
- Bandyopadhyay: the low-yield classification matters because it persists coherently across repeated deterministic runs.
- Orch OR: the observer sees stability because low-significance fluctuations are bounded rather than allowed to dominate the visible result.
-->

## 2. Concept: Low-Value Sector Budget

The low-value sector budget is a portal-local, region-specific constraint on query allocation. The scene is partitioned into sectors by portal-local coordinates, in particular:

- `layer`
- `radial_bin`
- `theta_bin`

Within this partition, the low-value family of interest is the outer-ring region:

- `layer = 0`
- `radial_bin = 3`

Usefulness mapping then identifies a retained throttle profile inside that family:

- `theta bins = {13,14,15,0}`
- `period = 2`

The negative invariant is defined by a simple contract:

- `actual_query_share <= maximum_allowed_query_share`

where the allowed share is derived from a deterministic baseline:

- `baseline_query_share = 0.4011`
- `max_query_share_scale = 0.9`
- `maximum_allowed_query_share = 0.361`

This is not merely a timing budget renamed in geometric language. It is a geometric-statistical constraint. The sectors are portal-local. The classification is grounded in measured query work and measured yield. The contract is evaluated only in tandem with preservation of the positive annulus invariant from Paper 001.

One may therefore describe this as geometric pruning in a disciplined sense. The system does not discard a region because it looks empty in one frame. It bounds expenditure because a sector family has been shown, across deterministic runs, to contribute disproportionately little to the preserved optical structure. The suppression is structural rather than heuristic.

In the present harness, the meaning of the budget is straightforward: the outer-ring low-value family must not reclaim too much of the pass-2 query budget once geometry has shown that its optical yield is weak. The constraint therefore complements the proto-caustic invariant. One preserves the annulus where signal concentrates; the other bounds the sectors where cost continues but significance does not.

<!--
Perspective Alignment Notes
- Penrose: the constraint should read as a consequence of portal-local geometry, not as an arbitrary performance trick.
- Bandyopadhyay: suppression stabilizes temporal signal by limiting recurrent low-yield expenditure.
- Orch OR: low-significance branches are not denied metaphysical existence; they are denied unbounded computational amplification.
-->

## 3. Method

The method reuses the deterministic wormhole harness established for Paper 001. Camera transform and input are fixed. Pass-1 transport advances rays through the GRIN field. Boundary interaction triggers remap between scenes. Pass-2 performs candidate generation and `OverlapOnly` query dispatch before narrowphase hit resolution and film write. No change to transport law, remap logic, broadphase policy, or hit acceptance is introduced for the purposes of the present paper.

The additional ingredient is sector heatmap measurement and budget evaluation. Post-remap segments are aggregated into portal-local bins indexed by:

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

This makes it possible to identify high-cost, low-yield sectors and to distinguish them from sectors that materially support the preserved annulus. The low-value sector budget is then evaluated as a deterministic contract after each harness run. In implementation terms, this evaluation is emitted by `WormholePrototypeRig` and written into the saved report fields:

- `low_value_sector_budget_contract`
- `low_value_sector_budget_result`

The active kept throttle profile remains deliberately narrow. It does not remove the low-value sectors. It reduces overlap-query frequency deterministically within a known outer-ring family:

- `layer = 0`
- `radial_bin = 3`
- `theta bins = {13,14,15,0}`
- `period = 2`

As in Paper 001, the figure quartet serves as the common evidence frame:

- Figure A: raw film-buffer result
- Figure B: composed render with research inset
- Figure C: portal-local density structure
- Figure D: compact metrics and contract summary

The method is therefore not a detached optimization study. It is a continuation of the same invariant-driven measurement language, now extended from preservation to bounded suppression.

<!--
Perspective Alignment Notes
- Keep this section concrete.
- Avoid mysticism; the contract lives in code, logs, and deterministic artifacts.
- Let the shared harness make the continuity with Paper 001 explicit.
-->

## 4. Results

The results should again be read as coordinated views of one underlying structure.

### Figure A

![Figure A — Main Render](../../../output/wormhole_test/figures/figure_A_main_render.png)

Figure A shows the raw film result under the active kept low-value throttle profile. Its role here is contextual. The image must remain stable not because it is pleasing in the abstract, but because the budget contract has value only if the observer-facing output remains intact.

### Figure B

![Figure B — Composed Render with Research Inset](../../../output/wormhole_test/figures/figure_B_composed_overlay.png)

Figure B restores explanatory structure. The research inset indicates the active mode, the contract status, and the highlighted invariant ring. The low-value budget is therefore shown in its proper relation to the annulus preserved by Paper 001.

### Figure C

![Figure C — Ring Density](../../../output/wormhole_test/figures/figure_C_ring_density.png)

Figure C is especially important in the present paper. It makes visible the portal-local distribution within which the negative invariant is meaningful. The outer-ring low-value sectors appear as regions in which cost is not matched by comparable contribution to the preserved annulus, while the destination-side annular concentration remains prominent. The geometry thus distinguishes between expenditure that sustains structure and expenditure that mostly accumulates outside it.

### Figure D

![Figure D — Metrics Table](../../../output/wormhole_test/figures/figure_D_metrics_table.png)

Figure D closes the loop quantitatively. It records the proto-caustic invariant status, the low-value sector budget status, the active throttle profile, and the key pass-2 timing metrics. It also makes the budget margin visible. For the current kept profile, the relevant values are:

- `actual low-value query share = 0.2888`
- `maximum allowed query share = 0.361`
- budget margin = `0.0722`

The central empirical result is therefore twofold. First, low-value regions are not merely asserted; they are measured through query share and yield. Second, bounded suppression of those regions reduces waste without disturbing the annular structure that Paper 001 treats as geometrically significant.

<!--
Perspective Alignment Notes
- The quartet should still feel like one object viewed from multiple coordinate systems.
- Penrose-style emphasis: the negative invariant is legible only because it is read against preserved geometric structure.
- Keep the prose evidence-facing and continuous with Paper 001.
-->

## 5. Validation

Validation in this paper is intentionally explicit and falsifiable. The negative invariant passes only if:

- `actual_query_share <= maximum_allowed_query_share`

For the current kept profile, the measured example is:

- `actual_query_share = 0.2888`
- `maximum_allowed_query_share = 0.361`
- budget margin = `0.0722`

This margin matters. It indicates that the retained throttle profile does not merely scrape beneath threshold; it preserves a nontrivial buffer while leaving `geom_hits` and `final_write_px` unchanged.

The negative invariant is not evaluated in isolation. It is paired with the proto-caustic annulus contract from Paper 001. A run is acceptable only if:

- the destination-side annulus preserves hit density, hit continuity, positive-overlap continuity, and radial gradient above threshold
- the low-value family remains below its maximum allowed query share

Failure therefore has a clear meaning. It would appear either as oversampling of low-significance sectors, as re-expansion of their query share, or as disturbance to the preserved annulus and the final observer-facing output. In practical terms, oversampling noise regions risks degrading the clarity of the wormhole’s structured focusing behavior even if some gross timing metric appears to improve.

The rejected stronger throttle boundary is equally informative. Increasing the same low-value family from `period = 2` to `period = 3` still passed the formal contracts, but it weakened annular metrics, worsened the target timing buckets, and reduced `geom_hits` and `final_write_px`. This shows that the negative invariant is necessary but not sufficient: retained configurations must satisfy the budget contract, preserve the positive invariant, and avoid hit/write drift.

<!--
Perspective Alignment Notes
- Penrose: truth conditions matter more than convenient stories about efficiency.
- Bandyopadhyay: stability is a temporal property of repeated deterministic realizations.
- Determinism is part of the epistemic structure of the result, not a testing afterthought.
-->

## 6. Discussion

The significance of the low-value sector budget lies in the emergence of a dual invariant system. Paper 001 supplies the positive invariant: preserve a geometrically meaningful annulus. Paper 002 supplies the negative invariant: limit expenditure in portal-local sectors whose recurrent yield is low. The pair is more powerful than either component alone.

This duality is geometric and statistical at once. Geometry defines the partition and reveals the annulus. Statistics measure density, continuity, and query share within that partition. Efficiency then emerges not from an externally imposed heuristic, but from constraints imposed by measured structure itself. One does not guess where to save work; one learns where cost is recurrently uninformative.

A restrained observer-aware reading is available here as well. The system does not become intelligent in any anthropomorphic sense. But it does begin to self-organize toward informative regions. It protects the annulus where optical significance concentrates and limits the expansion of sectors whose repeated cost does not materially support the visible result. In that narrow but meaningful sense, the rendering process becomes shaped by contracts rather than by tuning knobs.

This matters for future adaptive rendering work. A renderer that preserves signal without suppressing low-value expenditure remains incomplete. A renderer that suppresses cost without preserving optical structure is equally incomplete. The dual invariant system suggests a more disciplined path: adaptive logic should be judged by whether it keeps the geometry’s concentrations intact while bounding the branches that remain computationally active but optically weak.

<!--
Perspective Alignment Notes
- Geometric and statistical duality should be explicit here.
- Bandyopadhyay influence: coherence is preserved because recurrent low-value noise is bounded.
- Orch OR restraint: structured emergence is admissible only as a reading of measured constraint and preserved visible consequence.
-->

## 7. Conclusion

We defined a low-value sector budget as a negative invariant that constrains pass-2 expenditure in a portal-local outer-ring family.  
We showed that the deterministic wormhole harness can retain a modest geometry-aware throttle that improves the target timing buckets while preserving hits, final writes, and the proto-caustic annulus.  
This matters because invariant-driven rendering now includes both preservation of signal and suppression of waste, turning measured structure into a behavioral contract rather than a post hoc diagnostic.

## Appendix A

Current deterministic harness configuration for the active low-value sector budget and companion annulus invariant:

- `LowValueSectorBudgetLayer = 0`
- `LowValueSectorBudgetRadialBin = 3`
- `LowValueSectorBudgetBaselineQueryShare = 0.4011`
- `LowValueSectorBudgetMaxQueryShareScale = 0.9`
- `maximum_allowed_query_share = 0.361`
- active kept low-value throttle profile:
  - `layer = 0`
  - `radial_bin = 3`
  - `theta bins = {13,14,15,0}`
  - `period = 2`
- companion proto-caustic target ring:
  - `layer = 1`
  - `radial_bin = 3`
- proto-caustic thresholds:
  - `min_hit_density = 800`
  - `min_hit_continuity_ratio = 0.95`
  - `min_positive_overlap_continuity_ratio = 0.95`
  - `min_radial_gradient = 600`

<!--
Perspective Alignment Notes
- Appendix remains purely operational.
- No interpretation should be imported into the configuration summary.
-->
