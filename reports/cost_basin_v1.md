# Cost Basin v1

**Concept Architecture — Renderer Observatory**

**Status:** Pure design document. No code changes, no instrumentation additions, no optimization. This document only names, defines, and relates a new conceptual layer ("Cost Basin") to existing observatory primitives using data and visualizations that are already produced by the current renderer (hit_diagnostics.csv, traversal_step_heatmap, budget_exhaustion maps, latest_perf_frame_report, film_capture, render_health, curvature sweeps, Observer Storyboard / contact sheets, Cathedral Probe overlays, Query Observatory v1).

**Core data sources referenced (no new collection required for the concept):**
- Per-pixel: `final_step_count`, `step_count`, `segment_count` (from hit_diagnostics.csv).
- Per-band / per-frame aggregate: `pass2_query_ms`, `band_physics_queries`, `subdivided_ray_queries`, `SubdividedRayCalls`, `avgSub` / substep behavior (from latest_perf_frame_report, "Film perf" snapshots, adaptive_stepping summaries).
- Spatial maps already generated: traversal_step_heatmap.png, budget_exhaustion_heatmap.png, frame_coverage_map.png, ownership_seams, continuity vectors, risk overlays.
- Cross-curvature: renderer_cost_observatory.md tables, curvature_signature_ladder.png, Query Observatory v1 storyboard.

**Scope note (repeated from all observatory work):** Hermetic closure validates transport completion within a known scene contract. Cost Basin observes where the renderer expends effort; it does not claim physical correctness, does not feed any runtime decision, and does not justify any performance change.

---

## 1. Definition

**Cost Basin**: A spatial (screen-space / film-plane) map or basin structure that shows where computational cost *accumulates* during rendering.

- "Inside the basin" = regions or neighborhoods where the renderer pays a high or sustained cost (measured by the four primitive accumulators below).
- "Basin geometry" = the shape, depth, boundaries, and local maxima of that cost field.
- "Basin boundary / seam" = the transition where cost drops sharply or where high-cost regions become isolated "islands" or "risk nodes."
- Unlike a simple global average or ladder scalar, the Cost Basin is *spatial and topological*: it reveals *where* (which pixels, bands, ownership domains, curvature features) the cost is concentrated, not merely how much total work occurred.

It is the natural dual of the existing **Closure Basin** (where classification succeeds or fails) and **Coherence Basin** (where transport solutions agree). Cost is the "effort price" paid to achieve (or fail to achieve) closure and coherence.

**Primary metrics (exactly as specified for v1):**
1. **step count** — transport integration steps (final_step_count / step_count per pixel; avg/max traversal steps in perf reports). Primary proxy for path length / field deflection work.
2. **query count** — number of physics / ray queries (subdivided_ray_queries, rayQueryCount returned from SubdividedRayHit, band_physics_queries, telemetry query counts). The multiplier on top of raw steps.
3. **query ms** — wall-clock cost of the query phase (pass2_query_ms from latest_perf_frame_report and Film perf prints; the dominant term inside pass2_phys_ms in all recent curvature sweeps).
4. **substep count** — subdivision factor inside query resolution (maxSubsteps / steps in the SubdividedRayHit for-loop per segment; visible as avgSub in perf reports, ~2.5 in the 20260607T221311Z 0% cell). The per-segment query amplification.

These four are already emitted or derivable from every full-coverage curvature_fps_benchmark and similar observatory run. No new arrays or timers are required to *define* the basin, only to refine its resolution.

**Per-frame vs. aggregate views (core to the design):**
- **Per-frame (band / refresh)**: Cost for the pixels actively processed in one RenderStep / micro-frame (the "latest_perf_frame_report" snapshot, present_pixels_updated ~320 in the example band, band_* counters). Shows instantaneous cost geography during progressive coverage.
- **Aggregate (full run / full coverage)**: Accumulated cost across all visits required to reach 100% traced + beauty-written (multiple passes over the film in the 50-frame matrix runs). This is the "true" Cost Basin for the image as finally classified. The existing renderer_cost_observatory.md tables are already aggregate views.

---

## 2. Visualization Language (Observer Storyboard Compatible)

Cost Basin inherits the visual grammar of the rest of the observatory:

- **Primary artifact**: `cost_heatmap.png` (or layered cost attribution map) — a film-resolution or downsampled image where intensity or hue encodes composite cost or the four separate channels (step, query count, query ms, substep). Analogous to traversal_step_heatmap.png and budget_exhaustion_heatmap.png.
- **Composite / diagnostic overlays**: Add a "cost" layer to combined_diagnostic_overlay.png, Cathedral Probe stacks, and ownership/continuity visuals. High-cost regions can be contoured or alpha-modulated.
- **Ladders**: Cost sensitivity ladder (0% → 100% curvature) showing how the basin warps (depth, location of maxima, total integral). Extends the existing renderer_cost_ladder.png and curvature_signature_ladder.png.
- **Storyboards / 9-panel**: New or extended "Cost Basin" panel in the Observer Storyboard framework (see Query Observatory v1 and observer_storyboard.py). Questions it answers: "Where did the renderer actually pay?", "Which metric dominates the local cost?", "Does the high-cost region coincide with a closure or coherence boundary?"
- **Contact sheets**: Always show (1) plausible beauty, (2) the raw cost heatmap or attribution, (3) the instrumented numbers (per-pixel or regional), (4) exact scene/step/budget so the basin is reproducible.
- **Risk overlay**: High-cost local maxima become explicit "Cost Risk Nodes" (building on existing risk_probe_markers and "persistent risk-node region" language).

All of the above are *reporting / post-process* only. They do not affect rendering, scheduling, or classification.

---

## 3. How Cost Basin Relates to Existing Concepts

### 3.1 Relation to Closure Basin

The Closure Basin is the region inside which every evaluated pixel reaches terminal classification (hit found, or valid miss/exhaust under the contract) before the declared budget.

**Cost Basin relationship (predictive and bounding):**
- High-cost sub-basins (long step counts + high query/substep volume) are the most likely locations for closure failure when budget is insufficient. The outer boundary of reliable closure frequently *coincides with or is predicted by* the outer boundary of the Cost Basin.
- Inside the Closure Basin it is still possible (and common) to have high local cost — the renderer simply paid a lot to classify those pixels successfully.
- Persistent Cost Basin "islands" that lie *outside* the Closure Basin at production budgets are diagnostic of "paying without return": the work is being done but classification is not completing. These are the regions that produce the 0% closure at low budget in the hermetic hero exhibits.
- In the xeno/zeno citation atlas language: far-field, boundary, and caustic Xenos often manifest as cost spikes near or at the Closure Basin edge.

**Observable signature in current data (20260607T221311Z family):**
- Mean final_step_count ~273, max ~299–304 across 17,920 pixels.
- query cost 92.4–93.8% of pass2_phys.
- 100% closure is achieved, but the Cost Basin is still "deep" (hundreds of steps + millions of queries per cycle). Lowering the step cap below the observed max would shrink the Cost Basin but immediately produce budget_exhausted_without_hit pixels — directly falsifying the hermetic contract.

Cost Basin therefore gives an *a priori* map of where closure is most expensive and therefore most fragile.

### 3.2 Relation to Curvature Signature (Sensitivity Signature)

Curvature Signature is the signed delta map (more steps = red, fewer = blue) of transport effort between baseline (0% curvature) and activated field amplitudes. It is the "field-induced change in work" layer.

**Cost Basin relationship (modulation and warping):**
- The Cost Basin at 0% is the *geometry baseline* cost field (straight-line traversal to the receivers plus whatever query work the scene geometry demands).
- Activating curvature warps, deepens, or migrates the Cost Basin. Caustics (concentrated focusing) create local cost hotspots even if they improve closure in some pixels. Deflection can lengthen paths for other pixels, increasing both step count and the subsequent query volume.
- The Sensitivity Signature (Panel 8 in storyboards, the curvature_signature_ladder) is literally the *difference field* between the Cost Basin at 0% and the Cost Basin at N%. Field-sensitive cost features (those that appear or intensify only when curvature > 0) are the parts of the Cost Basin that are "paid for by the field."
- In the renderer_cost_observatory.md data: average traversal steps rise only modestly (273.21 → 278.43) from 0% to 100%, yet query cost remains ~93% of physics. The Cost Basin *shape* may be stable while its *depth* (total query work) or location of maxima shifts with curvature.

Thus: Curvature Signature diagnoses *why* the Cost Basin changed; Cost Basin shows *where* the price is being paid.

### 3.3 Relation to Risk Nodes

Risk Nodes (already used in curvature benchmarks, cathedral probe risk overlays, and xeno taxonomy) are localized persistent high-instability or high-difficulty regions — corners, ownership seams, high-curvature boundaries, nonconvergent islands — that survive scheduler decorrelation, increased budgets, and other mitigations.

**Cost Basin relationship (nodes as local maxima):**
- A Risk Node is typically a *local maximum or ridge* inside the Cost Basin. It is the place where one or more of the four cost primitives (steps, queries, ms, substeps) is highest and most resistant to reduction.
- The "persistent risk-node region — the outer boundary of the basin that budget cannot eliminate" (from the xeno atlas) is exactly the outer contour of the Cost Basin that remains after budget increases.
- Cost Basin makes the risk node visible as part of a larger field rather than an isolated artifact. A lone high-step pixel may be noise; the same pixel as the peak of a Cost Basin "island" that also shows high query count and query ms is structurally significant.
- In Cathedral Probe terms: the risk nodes are the places where coherence basins fail to grow and where continuity vectors are strongest. Adding the cost dimension shows *whether* that instability is also expensive (or whether it is cheap but topologically fragile).

Cost Basin therefore supplies the "effort dimension" that turns a risk node from a binary flag into a quantified feature of a cost field.

### 3.4 Relation to Query Observatory v1

Query Observatory v1 (the 9-panel storyboard just produced for the attached run) is the fine-grained *decomposition instrument* for the dominant term inside the Cost Basin.

**Cost Basin relationship (map vs. attribution):**
- Query Observatory breaks `pass2_query_ms` into the six requested sub-metrics (setup ms, intersect ray ms, broadphase ms, narrowphase ms (lumped in v1), substep count, query count) at per-frame (band snapshot) and aggregate granularity.
- The Cost Basin is the *spatial synthesis* that places those query costs (plus raw step count) back onto the film plane and identifies the basins, seams, and nodes.
- In the current data the Query Observatory already shows that query cost is 92–94% of pass2_phys and that substep/query multiplication on top of ~273 transport steps produces the millions of subdivided_ray_queries. The Cost Basin view would render that fact as a film-wide or band-local "query cost basin" whose depth is almost entirely explained by the Query Observatory's attribution.
- Future refinement: once per-pixel or per-segment query timing and counts are carried through the resolve (as already partially done via telemetryQueryCountThisPixel and first_accepted_* fields), the Cost Basin heatmaps can be directly colored by the Query Observatory's six numbers rather than by the coarser step_count proxy.

Query Observatory tells you *why a pixel or band is expensive inside the query path*. Cost Basin tells you *where in the image that expense forms coherent, mappable structure* and how that structure relates to closure, curvature, and risk.

---

## 4. Observatory Integration Points

- **Observer Storyboard / 9-panel contact sheets**: Add or promote a dedicated "Cost Basin" panel (or expand the existing traversal / budget panels). Use the same question/caption/status/artifact discipline as Query Observatory v1 and hermetic_storyboard_v2.png. The panel answers: "Where did the renderer actually spend its effort, and does that map explain the closure or curvature behavior we see?"
- **Cathedral Probe / multi-layer overlays**: Insert a cost layer (or four sub-layers for the four metrics) alongside transport ownership, continuity vectors, risk markers, and phase coherence. High-cost regions become another "cathedral" structure to read.
- **Curvature / Sensitivity Ladders**: Every existing ladder (curvature_signature_ladder, renderer_cost_ladder) gains a Cost Basin interpretation. The ladder now shows not only "more steps" but "the basin deepened or migrated."
- **Hermetic / Closure Diagnostics**: Cost Basin is the missing "why was closure expensive here?" map that sits beside the hit/miss map and budget exhaustion heatmap.
- **Xeno / Zeno Citation Atlas**: Cost Basin supplies the missing "effort" column for many citation types (far-field Xenos are high-cost by definition; caustics create cost hotspots even when they improve local closure).
- **Renderer Cost Observatory (existing)**: Cost Basin is the formal spatial abstraction that generalizes the scalar tables and ladders already present in renderer_cost_observatory.md and renderer_observatory.md.

All integration is reporting-layer only. The same rule that applies to Query Observatory, Closure Basin, and Coherence Basin applies here: the basin is observed, never optimized into the production path without separate validation that the observation itself remains valid.

---

## 5. Falsifiability and Usage

A Cost Basin claim is falsifiable when:
- A stated high-cost region (defined by thresholds on the four metrics or by seeded basin growth) does not show elevated step/query/substep counts or ms when re-measured with independent instrumentation.
- Changing curvature (Sensitivity Signature) does not produce the predicted warping or deepening of the Cost Basin in a subsequent sweep.
- A claimed "risk node" (local cost maximum) disappears under scheduler decorrelation or increased budget while the surrounding Cost Basin geometry remains — or conversely, the node persists but the Cost Basin integral drops dramatically (cheap risk vs. expensive risk).
- Per-frame Cost Basin snapshots during a progressive coverage run fail to predict which pixels will require the most additional visits to reach 100% traced + beauty-written.

**Usage (observation only):**
- Before any tuning discussion: compute the Cost Basin on the current baseline. Identify whether the dominant cost family (query vs. raw traversal vs. substep amplification) is spatially uniform or concentrated.
- Compare Cost Basins across traversal modes (row vs. tile vs. checkerboard) — the existing 4-mode contact sheets already supply the raw data.
- Use the basin to decide *where* to apply extra precision or diagnostics (cathedral-probe style) rather than globally.
- In the xeno/zeno or fixture citation process: every anomaly now gets a fifth coordinate — "does it sit inside a Cost Basin, on the boundary, or in a low-cost region?"

---

## 6. Open Questions (Design Only)

- Weighting: How to combine the four raw metrics into a single scalar "cost" field for basin growth (linear? log? query-ms dominant because it is the measured wall time?).
- Temporal vs. spatial: In progressive / per-frame views, is the Cost Basin the cost of the pixels being *visited right now*, or the *accumulated cost to classify those pixels so far*?
- Per-segment vs. per-pixel: Query and substep counts are naturally per-segment (inside SubdividedRayHit). How do we attribute them cleanly back to the final pixel for the basin map without double-counting multi-segment paths?
- Godot physics internals: broadphase vs. narrowphase remain lumped inside intersect_ray_ms until lower-level timing is added. The design treats them as first-class citizens of the Cost Basin even while the current implementation can only show their sum.
- Cross-fixture portability: The hermetic_curved_room produces a relatively uniform, high but "shallow" Cost Basin (hundreds of steps to background). Deep recursive or wormhole fixtures will produce very different basin topologies. The concept must remain useful across both.

---

## 7. Relation to Prior Renderer Cost Work

This document is a direct conceptual successor to the existing `reports/renderer_cost_observatory.md` and `reports/renderer_observatory.md` (both of which analyze the exact same family of curvature_fps_benchmark full-coverage runs). Those documents already:
- Split pass2_phys into query vs. non-query.
- Tabulate traversal steps, segments, physics queries, and query cost % by curvature.
- Produce cost ladders and storyboards.
- Insist on 100% coverage as a prerequisite for comparing cost numbers.

Cost Basin v1 simply gives that observed cost stack a *spatial basin language* that is already native to the rest of the observatory (Closure Basin, Coherence Basin, Curvature Signature, Risk Nodes, Query Observatory). It makes the tables and ladders *mappable* in the same way the Cathedral Probe made transport coherence mappable.

No new claims about renderer behavior are made. The only new thing is the name and the relational architecture.

**End of Cost Basin v1 concept architecture.**

All content above is derived from existing artifacts, data formats, and observatory methods already present in the repository. The four specified metrics (step count, query count, query ms, substep count) are already the dominant numbers appearing in the perf reports, hit diagnostics, and cost tables of the referenced runs. The relations are logical extensions of the basin/signature/risk language already in use in cathedral_probe_architecture.md, the xeno_zeno citation atlas, the Observer Storyboard framework, and the renderer_cost_observatory reports.

Observation only. No optimization. Concept architecture complete.