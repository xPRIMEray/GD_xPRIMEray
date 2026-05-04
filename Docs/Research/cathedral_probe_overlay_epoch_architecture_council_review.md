# xPRIMEray Architecture Council Review

Phase: Cathedral Probe Overlay Epoch + Transport Continuity Emergence

This review is grounded in the currently produced artifacts, not in claims about real physics. Terms such as null geodesic, Gordon metric, coherence basin, and transport manifold are renderer-design metaphors unless directly tied to measured renderer behavior.

Primary evidence reviewed:

- `output/tile_commit_traversal_comparison/20260504T010110Z/tile_commit_traversal_summary.md`
- `output/tile_commit_traversal_comparison/20260504T010110Z/**/transport_continuity_summary.md`
- `output/tile_commit_traversal_comparison/20260504T010110Z/**/transport_shape_regions.csv`
- `output/tile_commit_traversal_comparison/20260504T010110Z/corner_probe_after_beauty/**/corner_threshold_report.md`
- `output/doe_scheduler_resonance/20260503T002804Z/scheduler_DOE_summary.md`
- `output/first_pass_traversal_comparison/20260503T171942Z/traversal_comparison_summary.md`
- `output/transport_coherence_basin_smoke/20260503T001944Z/*`

## 1. Executive Summary

The renderer is no longer best understood as a raster image with occasional numerical artifacts. It is revealing a transport ownership topology that the raster traversal can amplify, suppress, or reshape.

The strongest conclusion is an overlap failure model:

1. Local hit/transport ambiguity is real and persists across traversal modes.
2. Scheduler cadence is also real and can amplify local ambiguity into row-scale artifacts.
3. Lower step length is not a monotonic cure in full-frame metrics; it exposes different precision thresholds and resonance regimes.
4. The most valuable next architecture step is transport ownership graph extraction, not more full-frame broad sweeps.

Do not global-smooth the image. Do not treat the 0.003125 reference as truth. Do not move directly to adaptive stepping until the ownership graph, basin boundaries, and epsilon-stable regions are measured.

## 2. Strongest Evidence Table

| Observation | Evidence | Interpretation | Confidence | Architectural Implication |
|---|---|---|---|---|
| Stride can nearly suppress broad bands. | Scheduler DOE at 0.015: stride 1 band 32.625%, stride 2 band 1.8472%, stride 4 band 0.3403%, stride 8 band 0.1944%. At 0.0125: stride 1 band 33.0347%, stride 4 band 0.2222%, stride 8 band 0.1944%. | Traversal cadence can dominate visible full-frame artifact support. | High | Scheduling must be decorrelated from scanline/stride harmonics before judging geometry quality. |
| Tile/checkerboard traversal changes beauty hashes and artifact topology. | Tile-commit comparison has different hashes for all traversal modes; changed pixels vs row range from 1250 to 2407 in latest 0.0125/0.015 beauty cells. | Pass1+pass2 visitation order is part of the artifact expression, even with hit math unchanged. | High | Traversal order is an experimental variable and should become a first-class diagnostic axis. |
| Tile/checkerboard do not universally improve scalar band metrics. | Latest tile-commit run: at 0.015, checkerboard band 18.9601% vs row 20.9635%, but tile band 21.2031%. At 0.0125, row 19.3941%, tile 19.9149%, checkerboard 19.4375%. | Decorrelation helps some cases but is not a complete fix. The residual is not only row traversal. | High | Avoid declaring tile scheduling solved. Use it to reduce resonance while measuring local topology. |
| Earlier pass1-only traversal showed stronger traversal effects. | First-pass comparison: at 0.00625 row band 18.1701%, tile/column/checkerboard 5.4167%; at 0.018 tile 4.1892% vs row 5.875%. | Pass1 acquisition order alone can strongly reshape artifacts in some regimes. | Medium-high | Acquisition and commit should both remain instrumented. Differences between pass1-only and pass1+pass2 are themselves diagnostic. |
| Corner/edge ROIs remain high risk across traversal modes. | Corner reports at 0.0125 and 0.015 show 648/648 samples requiring fine/reference precision or ownership changes; required precision 0.003125 across row/column/tile/checkerboard. | Local edge/corner ambiguity persists independent of traversal. | High | Future studies should focus on ownership boundaries and corner/edge basins, not whole-frame averages. |
| Continuity vectors align with transport shape boundaries. | Latest beauty continuity summaries show ~6475-6655 vectors per mode, high vectors nearly equal total vectors, shape overlap equals total vectors; shape-region CSV reports boundary high-vector-density alignment true. | The vector layer is exposing ownership boundary discontinuities, not random image noise. | High | Transport ownership graphs are now extractable and should become the main analysis object. |
| Transport regions vary by traversal and step. | Shape region examples: 0.015 row top region area 161, column 97, tile 129; 0.0125 row/tile top region area 65, checkerboard/column 33. | Ownership topology is traversal-sensitive, especially near thin/edge regions. | Medium-high | Region topology metrics should replace single image-wide band percent as the core diagnostic. |
| Coherence basin smoke found stable interiors but unsealed risk regions. | Basin smoke: 8 stable coherent basins with local coherence ~0.999999, entropy 0; risk node report: 289 threshold-snap nodes, persistent mismatch at 0.00625, required precision 0.003125; risk regions: 289 unsealed nonconvergent. | Some interiors are locally coherent, but surrounding risk regions are not sealed at tested precision. | Medium | The system likely contains coherence plateaus and unstable seams. Need targeted basin sealing experiments. |
| Coherence-basin DOE mode changes band output dramatically. | Scheduler DOE coherence_basin_on cells report band 1.3194%, horizontal score 0.0625 across 0.015/0.0125/0.00625 and strides 1/2/4/8. | Diagnostics/render-time mode may alter timing/budget behavior or capture surface; not safe to compare directly to OFF beauty as a fix. | Medium | Keep diagnostics passive for beauty claims; treat diagnostic-on modes as measurement layers, not rendering improvements. |
| Portal event data remains incomplete. | Continuity summaries now miss only `portal_event_count` after hit diagnostics enrichment. | Current continuity field model is mostly populated but portal/throat semantics are incomplete. | High | Do not overinterpret portal/throat continuity until per-pixel event export is real. |

## 3. Council Lens Review

### Einstein / Minkowski: Ordering And Coordinate Dependence

The renderer shows coordinate/order artifacts: row, column, tile, and checkerboard modes produce different hashes and changed-pixel sets under fixed scene and step length. This is not observer consistency in a physical sense; it is render-order dependence. The scheduler DOE shows the strongest form of this dependence: stride 1 and stride 2 can sustain row-global bands while stride 4/8 collapse broad band support to below 1% in many cells.

Architectural reading: the current raster traversal is acting like a coordinate chart with numerical side effects. Transport ownership needs a representation that is less tied to row order.

### Roger Penrose: Emergent Topology

The transport shape regions and continuity vectors make topology visible. Ownership islands, thin strips, and boundary-aligned vector fields are measurable. However, the evidence does not yet justify calling these true conformal structures. The safe claim is narrower: the renderer now exposes discretized ownership regions whose boundaries behave like unstable seams.

The strongest topology result is negative: risk regions are unsealed in the basin smoke. There are coherent interiors, but the enclosing transition regions remain nonconvergent at tested precision.

### James Clerk Maxwell: Resonance And Forced Excitation

The scheduler DOE is the clearest resonance evidence. Stride acts like a forcing cadence: at 0.015 OFF, stride 1 is 32.625% band pixels, stride 2 is 1.8472%, stride 4 is 0.3403%, and stride 8 is 0.1944%. That is too structured to dismiss as random numerical noise.

The cadence does not create all ambiguity. It appears to excite or amplify local ambiguous transport into global bands.

### William Rowan Hamilton: Path Continuity

The reference probes and continuity vectors point toward trajectory-centered analysis. Pixel-centered stepping is not enough because neighboring pixels can diverge in collider ownership, domain id, path length, boundary count, and step count at the boundary. The high-value object is now the local transport signature and its continuity under perturbation.

Architectural implication: adaptive precision should eventually be trajectory/region-centered, but not yet. First extract the ownership graph and prove stable basins and transition seams.

### Gordon Metric / GRIN Lens Perspective

Lower-step overlays reportedly show smoother, more curved continuity surfaces at 0.0125, and the shape-region changes support that the ownership map is step-sensitive. This strengthens the renderer-design interpretation that transport behaves like a curved/refractive field. It does not prove physical null geodesics. The strongest measured statement is: decreasing step length changes the inferred transport ownership geometry, especially near edge/corner regions.

### Grant Sanderson / 3Blue1Brown: Explanatory Clarity

The dual-reality overlay is the correct educational instrument: Cartesian red shows intended projection; transport cyan/blue shows what hit acquisition actually owns; yellow/magenta risk markers show where probes disagree; continuity vectors show why neighboring pixels diverge.

The next explainer should reduce the system to a six-panel invariant diagram:

1. Cartesian object projection.
2. Transport ownership regions.
3. Continuity vectors only.
4. Ownership graph nodes/edges.
5. Step precision labels by region.
6. Traversal cadence overlay.

The missing piece is an ownership graph overlay: islands, seams, adjacency, and per-edge discontinuity weights.

### Curt Jaimungal: Question Tree And Assumption Discipline

The meaningful question has changed from "which step length gives a good image?" to "which transport regions are stable under step, phase, and traversal perturbation?"

Assumptions weakened:

- Whole-frame band percent is sufficient.
- Row smoothing or post-process smoothing is a valid primary fix.
- Smaller step length monotonically improves the full-frame artifact profile.

Assumptions strengthened:

- Cartesian intent and transport reality are distinct renderer objects.
- Local ambiguity and scheduler resonance interact.
- Precision should be local and evidence-bound.

### Action Lab / Experimental Physics Lens

The cheap, repeatable experiments are now ROI and graph experiments. Full-frame sweeps are expensive and lower value unless they are used as a control. Epsilon stability should become formal: each region should report the coarsest step length whose transport signature remains within epsilon of a reference-precision baseline.

The system is close to producing measurable transport invariants: stable basin count, seam length, boundary discontinuity density, ownership graph edit distance, and precision floor distribution.

### Elon Musk / Engineering Execution Lens

What is overcomplicated: broad DOE over full frames before topology extraction. The renderer now has enough instrumentation to stop chasing aggregate images.

What should be productized immediately: diagnostic overlay packets and transport ownership graph extraction. The demo path is not adaptive precision yet; it is deterministic tile/checkerboard traversal plus a measured ownership graph showing where precision is required.

Architecture risk: letting probes, overlays, or memory feed rendering before they have deterministic invariants. That would turn measurement into another hidden scheduler.

Object-seeded probing is now effectively mandatory for targeted studies. It should seed measurement and scheduling attention, not shading decisions.

### Anirban-Inspired Phase Prime Metrics Lens

As design inspiration only: ownership regions and continuity surfaces resemble nested shape structures. Coherent interiors, unstable seams, and threshold-snap nodes suggest recursive phase basins. The measured part is the nested region behavior; the speculative part is any broader phase interpretation.

Raster traversal is not the right primitive for reasoning. It remains the final write format, but the analysis primitive should be object/topology/shape-centric.

## 4. Convergence Assessment

Current evidence supports mixed behavior:

- Local convergence: some coherent basins exist, with local coherence ~0.999999 and entropy 0 in the basin smoke.
- Persistent nonconvergence: 289/289 risk regions in the smoke are unsealed nonconvergent and require 0.003125.
- Threshold snapping: risk nodes are classified as threshold_snap, with persistent mismatch at 0.00625.
- Traversal oscillation/resonance: band metrics vary strongly with stride and traversal. Smaller step lengths do not monotonically reduce full-frame banding.
- Possible plateau: stride 4/8 repeatedly produce low broad-band support across many step lengths, suggesting the scheduler artifact has a suppressible cadence plateau.

Answer: lower step lengths are partially converging local geometry while the full-frame artifact field remains resonance-sensitive. The renderer is neither simply converging nor simply decohering. It is exposing local precision thresholds under a traversal-dependent amplification layer.

## 5. Recommended Overnight Experiment

Single next study: Transport Ownership Boundary Graph Precision Sweep.

Goal: extract ownership graphs around high-risk regions and measure graph stability across step length, traversal mode, and micro-phase perturbation.

Run scope:

- Fixture: `domain_resolver_stress`
- Resolution: `320x180`
- Frames: `90`
- Warmup: `5`
- Traversal modes: `row`, `checkerboard`
- Strides: `1`, `4`
- Step lengths: `0.02`, `0.016`, `0.015`, `0.014`, `0.013`, `0.0125`, `0.011`, `0.010`, `0.0075`, `0.00625`, `0.003125`
- Reference step: `0.003125`
- ROI sources:
  - Manual corners: `40,35;280,35;40,145;280,145`
  - Existing geometry edge midpoints for object `geometry:25836914057`
  - Top continuity-vector density regions from the latest tile/checkerboard captures
  - Transport shape region boundaries with high `boundary_aligns_with_high_vector_density`

Sampling strategy:

- Do not render broad full-frame probes except as control captures.
- For each ROI, sample dense patches: 17x17 or 33x33 if budget allows.
- Include radial rings: 2, 4, 8, 16, 32 px.
- Capture hit signature: hit/miss, collider id, domain id, path length, boundary count, step count, normal, ownership region id.
- Build graph per cell:
  - nodes: connected same-collider/domain regions
  - edges: adjacency boundaries
  - edge weights: mean continuity discontinuity, ownership flip count, path-length delta, boundary-event delta

Stopping criteria:

- Stop after current cell if 12-hour budget expires.
- Stop a ROI early if graph edit distance and precision floor are unchanged for three consecutive finer steps.
- Continue sampling any ROI with persistent mismatch at 0.00625 or 0.003125.

Metrics:

- ownership_graph_node_count
- ownership_graph_edge_count
- graph_edit_distance_vs_reference
- seam_length_px
- high_discontinuity_edge_count
- mean/max continuity score by edge
- basin_count and stable_basin_count
- unsealed_region_count
- epsilon_stable_area_percent
- precision_floor_histogram
- threshold_snap_count
- traversal_resonance_delta: row stride 1 vs checkerboard stride 4
- beauty hash control only, not primary score

## 6. Transport Ownership Architecture Recommendation

Move toward transport ownership graph extraction as the next architecture phase.

Recommended model:

- Pixel hit diagnostics remain raw observations.
- Connected same-collider/domain areas become ownership islands.
- Adjacent islands define seam edges.
- Continuity vectors become edge weights.
- Risk nodes become graph annotations.
- Precision probes become edge/basin stability labels.
- Object-seeded probes provide anchors and priors for where to measure first.
- Topology-first scheduling uses graph regions to schedule attention in future versions, but not final shading yet.

Near-term architecture:

1. Extract `TransportOwnershipGraph` from hit diagnostics.
2. Export graph JSON/CSV and graph overlay.
3. Compute graph stability across traversal and step.
4. Use object-seeded anchors to decide which graph regions receive dense probes.
5. Keep all graph data diagnostic-only until deterministic invariants are demonstrated.

Do not let graph data choose final hit, normal, color, resolver result, or step length in the next phase.

## 7. Visualization Roadmap

Add these overlays next:

- `layer6_transport_ownership_graph.png`: graph nodes, seam edges, edge weights.
- `layer7_precision_floor_regions.png`: region coloring by required step length.
- `layer8_graph_edit_distance_overlay.png`: differences versus reference step or traversal baseline.
- `continuity_vector_components.png`: separate collider/domain/path/boundary/normal components.
- `cadence_resonance_overlay.png`: row/stride/tile phase classes overlaid on high-discontinuity regions.
- `roi_basin_zoom_contact_sheet.png`: for each ROI, show beauty, ownership, vectors, graph, precision floor.

Best Grant-style diagram:

Show one corner ROI as a stack:

```text
Cartesian rectangle edge
  -> transport ownership islands
  -> continuity vectors on island boundaries
  -> ownership graph
  -> precision floor labels
  -> traversal phase coloring
```

The diagram should make one point obvious: the artifact is not just a bad pixel row; it is an unstable boundary graph that row cadence can amplify.

## 8. Most Important Negative Result

Tile/checkerboard traversal did not eliminate the underlying local instability.

In the latest tile-commit run, corner probes report required precision 0.003125 and 468 ownership changes for every traversal mode at 0.0125 and 0.015. In the corner reports, 648/648 samples require fine/reference precision or show ownership change across row, column, tile, and checkerboard.

This matters scientifically because it rejects the easy fix: traversal decorrelation alone is not the whole solution.

## 9. Top 3 Architecture Risks

1. Mistaking scheduler suppression for transport correctness.
   Stride 4/8 can collapse band metrics, but corners and risk regions remain unstable. A prettier traversal can hide the unresolved local topology.

2. Moving adaptive precision into rendering before graph stability is measured.
   Without ownership graph invariants, adaptive stepping could chase noisy seams and create new order-dependent behavior.

3. Continuing broad full-frame DOE as the main tool.
   Full-frame band percent is now secondary. It mixes local precision failure, traversal cadence, and region topology into one number.

## 10. What We Understand Now

- Scheduler cadence can amplify artifacts into row-global bands.
- Local edge/corner transport ambiguity persists across traversal modes.
- The renderer separates Cartesian projection intent from actual transport ownership.
- Continuity vectors align with ownership boundaries and are not merely decorative.
- Some coherent basins exist, but many risk regions remain unsealed at tested precision.
- Whole-frame band percent is useful for detecting resonance, not for diagnosing geometry by itself.
- Object/ROI/topology-centric measurement is higher value than raster-first sweeps.
- Passive instrumentation discipline is essential; diagnostic-on changes must not be confused with beauty fixes.

## 11. What Still Might Be Illusion

- Apparent curvature-like continuity surfaces may be true transport convergence, but they may also be shaped by current fixture geometry, diagnostics, or sampling thresholds.
- The 0.003125 reference baseline is not absolute truth.
- Coherence basin smoke used a limited probe budget and found only 8 stable basins; this does not prove global basin structure.
- The continuity score currently saturates around ownership boundaries; it needs component separation before ranking subtle normal/path changes.
- Portal-event continuity is incomplete because `portal_event_count` is not yet populated.
- Tile/checkerboard changed-pixel patterns may include deterministic scheduler effects rather than pure topology improvements.

## 12. Direct Answers

1. Does evidence support moving away from raster-first transport reasoning?
   Yes. Raster remains the output format, but the analysis and future scheduler should be ownership/topology-first.

2. Are transport ownership regions becoming more fundamental than rows/pixels?
   For diagnosis, yes. Rows explain amplification; ownership regions explain where transport decisions live.

3. Does decreasing step size converge toward stable null-geodesic equivalents or denser harmonic instability?
   Both effects are present. Local surfaces show signs of precision improvement, but full-frame metrics still show resonance and threshold snapping. Do not claim stable null-geodesic equivalents yet.

4. Is there evidence of coherence plateaus, epsilon-stable regions, or precision saturation?
   Yes for local coherent basins; not yet for unsealed risk regions. Precision saturation appears at 0.003125 in current probes, but that is a reference floor, not proof of final convergence.

5. Should future overnight DOE focus only on high-risk ROIs, corners, boundaries, and discontinuities?
   Yes, with small full-frame controls. Whole-frame sweeps should no longer dominate.

6. Is transport ownership graph extraction now highest value?
   Yes. It is the missing bridge between overlays and architecture.

7. What metrics should become core renderer truths?
   ownership graph edit distance, seam length, stable basin count, unsealed region count, epsilon-stable area, precision floor histogram, continuity edge weights, threshold-snap count, deterministic beauty hash controls.

8. Which metrics are probably noise or secondary?
   Raw changed pixels, global band percent without region context, aggregate runtime under diagnostics-on modes, and total vector count without component decomposition.

9. What experiment should run next 12-hour overnight?
   The Transport Ownership Boundary Graph Precision Sweep described above.

10. What architectural mistake would most likely stall progress?
   Treating traversal decorrelation or post-process smoothing as the fix before extracting and stabilizing the transport ownership topology.

