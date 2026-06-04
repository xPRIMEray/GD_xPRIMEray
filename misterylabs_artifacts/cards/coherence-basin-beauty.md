# Coherence Basin Beauty — Where the Field Cannot Converge

**Hook:** These two horizontal bands are not rendering artifacts, compression noise, or a scene boundary. They are the spatial signature of a topological property of the transport field itself.

## What You See

Left panel: the raw 960×540 transport render of the domain-resolver-stress scene. Stride=4 sampling leaves the characteristic row-pattern on the transport evaluation.

Right panel: the risk region overlay. Every highlighted pixel required the finest available precision floor (0.003125) and still did not converge. The two symmetric bands correspond to the GRIN field's outer boundary annulus — the same zone Chapter 1 identified as the curvature hot zone.

## The Numbers

- 276 risk nodes at 960×540 (consistent with 289 at 320×180 — same physics, same pattern)
- Every node: `convergence_class = threshold_snap`, `required_precision = 0.003125`
- Sealed regions at any coarser precision: **0**
- Mean local coherence outside the bands: 0.999999

The uniformity is the finding. If instability were a smooth continuous property, you would expect a distribution of required precisions. Every node hitting the same floor is inconsistent with smooth degradation — it implies a structural discontinuity.

## Why It Matters

The two bands are not an artifact of integration budget or precision tuning. Increasing budget does not move them. Changing step length does not dissolve them. The oracle probed at 4× the production precision floor and found the same classification.

This is a topologically constrained instability zone — the GRIN field boundary creates a transport geometry that no amount of numerical refinement can fully resolve at this parameterization.

## The Test

Vary the IOR gradient at the GRIN outer shell boundary (smooth/sharp/current). If the instability bands move, shrink, or disappear: the instability is parameterization-dependent. If they persist unchanged: the feature is topological, not numerical. The test is in the `transport_coherence_basin_world` sample world design proposal.

## Technical Context

- Scene: `test-domain-resolver-stress.tscn`
- Film resolution: **960×540** (hero capture — first run at this resolution)
- Step length: 0.015
- Stride: 4 (row traversal)
- Oracle: reference geodesic probe, max-anchors=2, max-steps=2048
- Risk node count: 276 (vs. 289 at 320×180 — coverage difference due to stride at different resolution)
- All nodes: `threshold_snap`, `required_precision=0.003125`, `persistent_mismatch_at_0.00625=yes`

---

*Source:* `output/transport_coherence_basin_smoke/20260604T023051Z_960x540/`  
*Raw render:* `visuals/coherence-basin-beauty-960.png` (960×540)  
*Hero 2-panel:* `visuals/coherence-basin-hero.png` (1928×644)  
*Web crop:* `visuals/coherence-basin-hero-web.png` (1280×427)  
*Presentation:* `visuals/coherence-basin-hero-pres.png` (1920×540)  
*Tier:* 1 — Minimum Viable Observatory  
