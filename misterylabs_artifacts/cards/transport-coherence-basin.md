# Transport Coherence Basin — 289 Instability Regions That Won't Converge

**Hook:** The oracle found 289 regions where transport refuses to stabilize — and every single one requires the same precision to handle. That uniformity is itself a finding.

## Scientific Context

Not all pixels in a curved-ray scene are equally difficult to render correctly. Some regions sit near bifurcation points in the transport field — small changes in ray direction produce large changes in outcome. The coherence basin probe maps these instability regions by running the transport oracle at progressively finer precision until convergence or failure.

## Observation

289 UNSEALED\_NONCONVERGENT regions identified, all requiring precision=0.003125 (the finest precision floor). Zero regions sealed at any coarser level. The regions are not randomly distributed: they cluster symmetrically around pixel coordinates (128, 58) and (128, 122) — two horizontal bands centered on the GRIN field's high-curvature annulus.

The radial risk profile shows risk magnitude decaying with distance from these band centers but never reaching zero within the probed domain. The coherence decay curve is not smooth — it shows a step discontinuity consistent with a topological feature in the transport field rather than continuous degradation.

All 289 regions are unsealed, meaning the oracle's precision budget was exhausted without achieving convergence. This is not a failure of the oracle — it is evidence that the transport field has a systematic instability zone that cannot be resolved by refinement alone.

## Why It Matters

Knowing where your renderer cannot converge is as important as knowing where it can. The 289-region map identifies the scene geometry responsible for instability (the GRIN field boundary annulus) and establishes that it requires special handling — adaptive step scaling, or a modified field parameterization — not just more compute.

## Next Step

Re-run the basin probe after changing the GRIN field's boundary transition (soften the IOR gradient at the outer shell). Compare the new region count and precision requirement to establish whether the instability is in the field parameterization or in the underlying geometry topology.

---

*Source:* `output/transport_coherence_basin_smoke/20260503T001944Z/`  
*Key image:* `visuals/transport-coherence-radial.png`  
*Validation:* `validation/transport-coherence-basin.md`  
*Tier:* 3 — Interesting, requires additional context
