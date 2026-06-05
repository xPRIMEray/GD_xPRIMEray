# Oracle Scheduler v0.2 — Adaptive Optical Transport Scheduling Architecture

**Status:** Research architecture document. Not scheduled for implementation.  
**Version target:** v0.2+ (post-hermetic closure baseline)  
**Framing:** This is an observatory engineering design note. No renderer changes are proposed here. No physics claims are made.

---

## Abstract

The current xPRIMEray transport loop allocates step budget uniformly and traverses film rows sequentially. This is adequate for hermetic calibration and baseline visual validation. It is not adequate for efficient, high-fidelity measurement of scenes with transport ownership boundaries, high-curvature regions, and unresolved island neighborhoods.

The Oracle Scheduler is the proposed architecture for spatially adaptive transport traversal allocation. The core principle is: **allocate integration effort where transport decisions are uncertain, not where they are already stable.** The analogy is FEA adaptive auto-meshing, which refines element density at gradient boundaries and structural discontinuities rather than applying uniform mesh resolution everywhere.

This document defines the architecture, scheduling primitives, risk taxonomy, budget allocation strategies, failure modes, validation requirements, and integration path with the existing hermetic closure infrastructure.

---

## 1. Why Uniform Traversal Is Inefficient

The present traversal strategy is straightforward: the film is divided into rows. `_rowCursor` advances sequentially from row 0 to `filmHeight - 1`. When a row is processed, its ray budget is consumed uniformly across all pixels in that row. The loop repeats each frame until `traversalRowsCompleted == filmHeight`.

This is efficient when transport decisions are uniform across the film. It is inefficient in the common case where transport decisions are not uniform.

**What the evidence shows:**

The transport island microscopy study (see [transport_island_microscopy.md](transport_island_microscopy.md)) found that in the domain resolver stress scene, 16.9% of sampled comparisons were unresolved — but those comparisons collapsed to a 9×7 pixel patch at one corner of the film. The other 83.1% of the frame was stable at the coarsest tested step length. A uniform row traversal expends equal budget on pixels that are already stable and pixels that need finer integration.

The Cathedral Probe DOE established that step-length refinement at frame scale does not govern horizontal band coverage (band percentage is approximately flat over step range 0.00625–0.025 at stride 1). What governs visible artifacts is traversal cadence — which rows are processed together and whether that cadence resonates with transport ownership seam locations. This is a scheduling problem, not a precision problem.

The domain ownership clustering analysis (see [curvature_domain_ownership.md](curvature_domain_ownership.md)) showed that transport phase-space partitions into k=3 regimes (near-side, bridge, far-side) with sharp multi-metric discontinuities at the bridge boundary. A scheduler that does not account for regime topology spends budget uniformly across regimes that have very different integration requirements.

**The efficiency gap.** A pixel near no transport boundary, in a stable ownership region, with low field curvature, is stable at the production step floor. It requires one traversal pass. A pixel inside a coherent seam island, near a collider ownership boundary, with high field gradient, requires multiple-pass oracle comparison at 4–8× finer step to seal. Uniform traversal gives both pixels the same budget.

The Oracle Scheduler closes this gap by measuring where budget is needed and allocating it there.

---

## 2. Transport-Risk Regions

A **transport-risk region** is a spatial region of the film where transport decisions are predicted to be uncertain, unstable, or high-cost to resolve. Risk is not error — a high-risk region may ultimately yield a correct transport decision. It is a signal to allocate more integration budget and diagnostic attention.

### 2.1 Risk classes

**Class 1: Ownership boundary proximity**  
Pixels whose null-geodesic path approaches a collider ownership seam. The path crosses the boundary at a shallow angle, and small step-length changes determine which collider is registered as the hit. These are the pixels that form coherent seam islands (see §4). Detection: oracle comparison at a range of step lengths; `EpsilonStabilityClass.Unresolved` at production step floor.

**Class 2: High-curvature path segments**  
Pixels whose ray traverses a region of the field with high geodesic curvature. The path bends significantly per unit step, and coarse integration accumulates larger trajectory error. Detection: per-segment curvature `K_max` from `OracleSegmentRecord`; pixels with `K_max` above a scene-calibrated threshold.

**Class 3: Transport domain transition**  
Pixels at the boundary between transport regimes (e.g., near-side to bridge domain in the wormhole configuration). These pixels do not share transport topology with their neighbors in the same way that interior domain pixels do. Detection: domain-boundary proximity from `curvature_domain_ownership` analysis; regime cluster membership discontinuity in `_fixtureRowsCompleted` neighborhoods.

**Class 4: Budget-exhausted pixels**  
Pixels where the integration reached the step budget cap (`MaxSteps`) without registering a hit or exit. These are explicit misses or near-misses that require either more steps, a finer step length, or both. Detection: `budgetExhaustedPixels` in the `[GrinBasicVisual][Coverage]` log; `missHits > 0` in hermetic fixtures.

**Class 5: Unresolved island neighborhood**  
Pixels adjacent to a known or suspected unresolved transport island (as defined in [transport_island_microscopy.md](transport_island_microscopy.md) §4). The island has a neighborhood of higher-risk pixels whose stability grade is near-threshold. Detection: island bbox expansion; `ThresholdSnap` pixels from oracle comparison.

**Class 6: Phase coherence gap**  
Pixels in a spatial region where the neighbour-normal-delta (continuity vector density) is elevated. These are visible transport discontinuities, not coherent-seam islands. Detection: Cathedral Probe continuity vector analysis; `FixtureDebugHitColoringEnabled` visual inspection.

Note: classes 1 and 6 are distinct. Class 6 is detectable by continuity-vector analysis (visible spatial gradients). Class 1 (coherent seam islands) is invisible to continuity vectors because neighboring pixels make the same systematic error and produce no local contrast. Oracle comparison is required to detect class 1.

---

## 3. Tile vs. Row vs. Voxel Scheduling

The current row-cursor scheduler is the simplest scheduling primitive. The Oracle Scheduler architecture adds three additional scheduling primitives and a policy layer that selects among them per frame and per region.

### 3.1 Row scheduling (current baseline)

Unit: one horizontal scanline.  
Sequential ordering: row 0 → row N-1.  
State: `_rowCursor` (single integer), `_fixtureRowsCompleted` (1D bitmask).  
Strengths: simple, sequential, cache-friendly read patterns.  
Weaknesses: susceptible to row-phase resonance with horizontal transport boundaries; expends equal budget on all rows regardless of per-row risk.

### 3.2 Tile scheduling

Unit: W×H rectangular tile (e.g., 16×16 or 32×32 pixels).  
Ordering: deterministic — by risk score, Hilbert curve, checkerboard interleaving, or random with fixed seed.  
State: 2D completion bitmask over tiles; risk score table per tile.  
Strengths: breaks row-phase resonance (confirmed by Cathedral Probe tile comparison: band % 20.2% → 0.0%); enables spatial budget allocation; supports risk-sorted scheduling.  
Weaknesses: requires 2D completion state; tile boundary effects require attention at seams.

This is the primary adaptive unit for the Oracle Scheduler. Most adaptive scheduling decisions operate at tile granularity.

### 3.3 Vertical band scheduling

Unit: V-pixel-wide vertical strip (e.g., one column per 8 columns).  
Ordering: interleaved or seeded from vertical transport features.  
Use case: vertically oriented transport seams; asymmetric field configurations where horizontal transport structure dominates.  
Strengths: complements row scheduling for rotated-anisotropy scenes.  
Weaknesses: orthogonal to the primary row-cursor infrastructure; requires separate pass management.

### 3.4 Voxel / field-cell scheduling

Unit: 3D spatial voxel aligned to the field discretization.  
Ordering: by field-gradient magnitude.  
Use case: scheduling traversal attention in field-space rather than screen-space. High-gradient voxels contain the pixels whose ray paths are most sensitive to step precision.  
Strengths: directly maps to field physics; identifies precision-sensitive regions in field coordinates before projection to screen.  
Weaknesses: requires field gradient computation (not yet exposed per voxel); requires mapping from field-space voxels to screen-space pixels; architectural infrastructure does not yet exist.

### 3.5 Policy layer

The policy layer selects scheduling primitives per frame based on:
- Current traversal pass number (coarse vs refinement)
- Per-tile risk scores from the previous pass
- Scene fingerprint stability (field configuration, camera position, geometry)
- Budget remaining vs budget consumed

In v0.2, the policy layer is intentionally simple: tile scheduling with risk-sorted ordering, falling back to row scheduling for the full-frame coarse pass. Voxel scheduling is deferred to v0.3+.

---

## 4. Ownership-Boundary Refinement

Ownership boundaries are the edges between transport regimes: the screen-space projections of field seams, collider geometry boundaries, and domain transition lines. They are the primary cause of transport-risk class 1 (ownership boundary proximity) and class 3 (domain transition).

### 4.1 Seam detection

Two instruments are available for seam detection:

**Continuity vector analysis** (Cathedral Probe): measures pixel-to-pixel transport decision discontinuity at a fixed step. Detects visible seams where neighboring pixels have measurably different transport outcomes. Misses coherent seam islands (§2, class 1) where all neighbors make the same error.

**Oracle comparison** (ReferenceTransportOracle): measures step-to-step stability for each pixel against a fine-step reference. Detects both visible and hidden seams. Required for ownership-boundary refinement in the coherent-island case.

The Oracle Scheduler uses both. Continuity vector analysis is fast and can run in the production loop as a per-tile risk classifier. Oracle comparison is expensive and is reserved for the refinement pass on identified risk tiles.

### 4.2 Refinement protocol

1. **Coarse pass**: traverse all tiles at production step. Classify each tile by risk score (continuity vector density, `budgetExhaustedPixels`, boundary event count).
2. **Seam identification**: tiles with risk score above threshold are flagged for ownership boundary refinement.
3. **Refinement pass**: run oracle comparison on flagged tiles at a sequence of production steps (convergence ladder). Identify which pixels inside the tile are unresolved vs stable.
4. **Precision assignment**: unresolved pixels inside the tile receive a per-pixel step-length assignment derived from their first-stable-step on the convergence ladder.
5. **Targeted re-traversal**: flagged pixels are re-traversed at their assigned precision. Non-flagged pixels in the same tile continue at production step.

This matches the island microscopy workflow (see [transport_island_microscopy.md](transport_island_microscopy.md) §7) but generalizes it from a manual DOE-time protocol to a scheduler-time operation.

### 4.3 Guardrail

Oracle outputs must not feed rendering, shading, hit selection, or resolver decisions. The oracle is a diagnostic instrument. Per-pixel step-length assignments derived from oracle comparison results may inform scheduling precision — but the final rendering decision for any pixel must be made by the production integration at its assigned step, not by the oracle integration.

```csharp
public const string DiagnosticOnlyGuardrail =
    "ReferenceTransportOracle computes best-known renderer-reference transport paths for validation only.";
```

This is an existing hard architectural constraint in `RendererCore/Validation/ReferenceTransportOracle.cs`.

---

## 5. Curvature-Gradient Refinement

High field curvature concentrates ray-bending within a small spatial volume. In these regions, the error accumulated by a coarse integration step is larger per unit path-length than in low-curvature regions. High-curvature pixels need finer steps, not more steps.

### 5.1 Curvature signal sources

The `OracleSegmentRecord` already records `K_max` (maximum curvature over the segment). For the production integration (not oracle), per-segment curvature is not currently exposed. This is a prerequisite for curvature-gradient refinement:

**Required**: expose per-segment `K_max` (or a binned curvature statistic) from the production ray integrator into a per-tile aggregate. This is a diagnostic addition — it does not change any integration decision.

### 5.2 Curvature risk score

For each tile, a curvature risk score is computed as:

```
curvature_risk = max(K_max) across all segments in tile's pixels
```

or a percentile-weighted variant to avoid single-pixel outliers distorting the tile score. Tiles whose curvature risk exceeds a scene-calibrated threshold receive finer step allocation in the refinement pass.

### 5.3 Curvature-guided step allocation

The step allocation formula for a tile with curvature risk `C` is:

```
allocated_step = production_step / (1 + alpha * C / C_threshold)
```

where `alpha` is a calibration constant (recommended range: 1.0–4.0) and `C_threshold` is derived from the scene's field configuration. At `C = 0`, the allocated step equals the production step. At `C = C_threshold`, the allocated step is halved. At `C >> C_threshold`, the step is reduced toward `production_step / (1 + alpha)`.

This formula is a heuristic calibration starting point, not a physical derivation. It must be validated against oracle comparison results before use in production.

### 5.4 Relationship to GRIN field parameters

The hermetic GRIN fixture (`Amp=0.6, ROuter=3.0, CanonicalGamma=1.5`) produces moderate curvature within the inner 3-unit radius. At these parameters, all pixels seal without budget exhaustion. At higher `Amp` values (beyond the tested range), curvature risk increases and the allocated step may need to decrease below the production floor to maintain hermetic closure. The hermetic gate (`missHits == 0`) will detect regression: any step allocation that allows escape will fail loudly.

---

## 6. Step-Budget Allocation Strategies

The current step budget is a global scalar: one `StepsPerRay` setting applies to all pixels. The Oracle Scheduler introduces spatial budget distribution.

### 6.1 Global vs. spatial budgets

| Model | Budget unit | Granularity | Current state |
|---|---|---|---|
| Global | StepsPerRay (scalar) | Frame-level | Implemented |
| Per-tile | Steps allocated per tile | Tile-level | Proposed v0.2 |
| Per-pixel | Steps allocated per pixel | Pixel-level | Proposed v0.3+ |
| Per-segment | Adaptive step length within path | Segment-level | Deferred (requires adaptive integrator) |

v0.2 implements per-tile spatial budget allocation. Per-pixel and per-segment are deferred.

### 6.2 Budget allocation algorithm

```
total_budget = StepsPerRay * total_pixels_in_frame

For each tile T:
    base_budget(T) = StepsPerRay * pixels_in_tile(T)
    risk(T) = max(ownership_risk, curvature_risk, island_risk) for T
    allocated_budget(T) = base_budget(T) * (1 + beta * risk(T))

if sum(allocated_budget) > total_budget:
    scale all allocated_budget values to preserve total_budget
```

The scaling step prevents the scheduler from exceeding the frame budget while allowing risk-weighted redistribution. High-risk tiles get more steps; low-risk tiles get fewer; the total is conserved.

`beta` is a tuning parameter (recommended starting value: 1.0). Higher values concentrate more budget in risk regions at the cost of coarser coverage everywhere else. At `beta = 0`, the scheduler degenerates to uniform allocation.

### 6.3 Budget floor

Each tile must receive at minimum `MIN_STEPS_PER_PIXEL * pixels_in_tile(T)` steps, where `MIN_STEPS_PER_PIXEL` is a scene-determined floor (typically 100–200 steps). This prevents starvation: a tile with very low risk score should not be rendered at zero steps because the budget was entirely reallocated to high-risk tiles.

### 6.4 Budget accounting and the hermetic gate

The hermetic validation gate (`missHits == 0`) depends on adequate step budget. Spatial budget reallocation must not reduce any tile's budget below the level needed to maintain hermetic closure in sealed-chamber scenes. The recommended test:

> Run the full-frame hermetic validation gate after any change to budget allocation parameters. Any `missHits > 0` in either the straight or GRIN fixture is a regression.

---

## 7. Coarse-to-Fine Observatory Passes

The Oracle Scheduler operates in two-pass (or multi-pass) mode. This mirrors progressive-refinement approaches in scientific imaging: take a fast low-resolution survey, identify regions of interest, then commit observation time to those regions.

### 7.1 Pass 1: Survey pass

**Resolution**: full film at reduced sampling (e.g., every 4th pixel per row, every 4th row).  
**Purpose**: establish a baseline hit map; classify tiles by risk; identify candidate island neighborhoods; detect budget-exhausted pixels.  
**Budget**: 15–25% of total frame budget.  
**Output**: per-tile risk score table; candidate refinement tile set.

The survey pass is not intended to produce a publishable render. It produces a risk map.

### 7.2 Pass 2: Refinement pass

**Resolution**: full pixel density on all tiles.  
**Scheduling**: risk-sorted tile order (highest-risk tiles first).  
**Step allocation**: risk-adjusted per §6.  
**Budget**: 60–80% of total frame budget.  
**Output**: complete film traversal; `traversalRowsCompleted == filmHeight`.

Low-risk tiles (survey-confirmed stable at production step) are traversed last. If the frame budget is exceeded before all tiles complete, low-risk tiles carry over to the next frame — they are already producing stable, correct results at survey-pass resolution.

### 7.3 Pass 3 (optional): Oracle refinement pass

**Trigger**: any tile where the refinement pass produced `EpsilonStabilityClass.Unresolved` pixels.  
**Resolution**: per-pixel oracle comparison on unresolved pixels only.  
**Step allocation**: convergence-ladder derived per §4.  
**Budget**: remainder; typically 5–10% of total budget for scenes with well-behaved transport.  
**Output**: precision closure measurements; updated island registry.

The oracle refinement pass is a validation instrument, not a rendering pass. Its outputs go to diagnostic logs and the island registry, not to the film buffer.

### 7.4 Pass integration with presentation

Presentation (the film buffer shown to the user) runs continuously against whatever state the film buffer contains. The film buffer is not locked during traversal. This is already the behavior of the existing multi-pass integrator (`_img` is retained across `ResetRowCursor` calls).

The distinction between `traversalRowsCompleted` (transport completion) and `filmRowsRendered` (presentation cursor) established in v0.0-pre is the architectural prerequisite for multi-pass scheduling. The presentation layer always shows the best available film state; the scheduler operates independently on transport completion.

---

## 8. Oracle Microscopy Modes

Oracle microscopy is the focused application of the `ReferenceTransportOracle` to a bounded screen-space region, with the intent of measuring transport topology rather than validating a specific production step. The island microscopy workflow (see [transport_island_microscopy.md](transport_island_microscopy.md) §7) is the reference implementation.

Three microscopy modes are defined for the Oracle Scheduler:

### 8.1 Survey microscopy

**Trigger**: called after the survey pass (§7.1).  
**Coverage**: sparse sampling (stride 8 or greater) across the full frame.  
**Measurement**: `EpsilonStabilityClass` at each tested production step; `DecisionRisk` scalar.  
**Output**: bounding boxes of unresolved pixel clusters; candidate island list.

Survey microscopy is the diagnostic instrument that feeds the risk classification. It is not used for every frame — only when scene geometry or field configuration changes, or when the scheduler detects unexpected `budgetExhaustedPixels` or `missHits` that were not predicted by the existing risk map.

### 8.2 Island microscopy

**Trigger**: a candidate island identified by survey microscopy or a persistent `Unresolved` cluster from the oracle refinement pass.  
**Coverage**: dense sampling (stride 1 or 2) of the island bounding box, expanded by N pixels on each side.  
**Measurement**: convergence ladder; first-stable-step per pixel; spatial precision gradient.  
**Output**: sealed / unsealed determination; per-pixel precision assignment; island record for the island registry.

Island microscopy may recurse: if the dense pass reveals a smaller sub-island that does not seal, the sub-island bounding box becomes the next microscopy target at even finer step.

### 8.3 Boundary microscopy

**Trigger**: tiles flagged as class 3 (domain transition) or class 6 (phase coherence gap).  
**Coverage**: the boundary tile plus one tile on each side.  
**Measurement**: collider match / domain match at a sequence of steps; `OwnershipGraphAgreement`; `NormalAngleDelta`.  
**Output**: boundary stability classification (sharp / gradual / ambiguous); seam location in sub-pixel coordinates.

Boundary microscopy is the complement to island microscopy. Island microscopy targets compact patches; boundary microscopy targets extended seam lines.

### 8.4 Guardrail (repeated for emphasis)

All three microscopy modes are diagnostic instruments. Their outputs update the risk map, island registry, and per-pixel precision assignments. They do not update the film buffer, hit selection, resolver decisions, or shading inputs.

---

## 9. Presentation vs. Traversal Scheduling

This distinction was established empirically during the Phase 2 hermetic closure milestone and is a foundational architectural principle for the Oracle Scheduler.

### 9.1 The two scheduling domains

**Traversal scheduling** governs how many film rows / tiles / pixels are processed per frame, in what order, and with what step precision. It is concerned with transport completeness. The relevant metric is `traversalRowsCompleted`. The gate is `traversalRowsCompleted == filmHeight`.

**Presentation scheduling** governs what the user sees on screen. It is concerned with visual refresh rate and visual completeness. The relevant metric is `filmRowsRendered` (the presentation cursor `_rowCursor`). The gate is subjective: a visually coherent update at a comfortable refresh rate.

These two domains operate on different timescales and have different correctness requirements.

### 9.2 Why they decoupled

When traversal pass 1 completes (all rows processed), `_rowCursor` resets to 0 via `ResetRowCursor("completed")`. Presentation pass 2 begins immediately. The film buffer `_img` retains the full pass-1 data. At the moment of capture (6 settle frames later), `_rowCursor` is in the low-range of pass 2 (e.g., 28–44 rows), even though the transport instrument has fully traversed the film.

`filmRowsRendered` = 28 — presentation state.  
`traversalRowsCompleted` = 360 — transport state.

Both are correct. They measure different things.

### 9.3 Implications for the Oracle Scheduler

The Oracle Scheduler must schedule traversal independently of presentation. Specifically:

- **Do not gate traversal completion on visual smoothness.** A tile can be visually noisy and still be transport-complete.
- **Do not advance the presentation cursor past transport-incomplete regions.** If a tile has not been traversed in the current pass, its film buffer state is stale. The presentation layer should mark stale tiles visually (if a diagnostic overlay is active) rather than presenting them as current.
- **Do not conflate per-tile `traversalRowsCompleted` with per-tile visual quality.** A tile near a transport seam may require more traversal passes before stabilizing visually. Transport completeness (all pixels traced once) is a weaker condition than transport stability (all pixels stable against oracle reference).

### 9.4 Scheduling state required

The Oracle Scheduler needs the following scheduling state that the current single-pass row cursor does not maintain:

| State | Current | Proposed v0.2 |
|---|---|---|
| Traversal completion | `_fixtureRowsCompleted` (1D bitmask, row-level) | 2D tile completion bitmask |
| Risk map | None | Per-tile risk score table |
| Pass number | Implicit (one pass per cursor cycle) | Explicit pass counter per tile |
| Step assignment | Global `StepsPerRay` | Per-tile `allocatedStepsPerRay` |
| Island registry | None | Persistent island record store |

---

## 10. Potential Visualization Overlays

The following overlays are identified as useful diagnostic visualizations for the Oracle Scheduler. None are currently implemented. All would use the existing `FilmOverlay2D` / overlay bus infrastructure.

### 10.1 Transport risk heatmap

Per-tile risk score rendered as a heatmap overlay on the film. Cool colors: low risk (stable at production step). Warm colors: elevated risk. Hot: unresolved island or budget-exhausted region.

Purpose: operator awareness; regression detection; immediate visual confirmation that risk scoring is identifying the expected regions.

### 10.2 Tile pass completion map

Per-tile overlay showing which pass each tile is currently in (survey / refinement / oracle refinement / complete). Allows the operator to see traversal progress at tile granularity without reading log output.

### 10.3 Island boundary overlay

Bounding boxes of known unresolved transport islands, rendered as rectangle outlines on the film. Color encodes island class (coherent seam island vs. budget-exhaustion island vs. multi-solution island). Persists across frames until the island is sealed or the scene geometry changes.

### 10.4 Curvature gradient overlay

Per-pixel `K_max` rendered as a gradient map. Highlights high-curvature paths without requiring the user to understand the underlying field geometry. Useful for calibrating the curvature risk threshold (§5.2).

### 10.5 Traversal vs. presentation cursor overlay

A horizontal indicator bar showing `filmRowsRendered` (presentation cursor) vs `traversalRowsCompleted` (traversal cursor). Directly visualizes the decoupling discussed in §9. Useful during development for confirming that the two metrics are updating independently.

### 10.6 Step precision map

Per-pixel overlay of the currently assigned integration step length (normalized to production step floor). Pixels near seams or inside islands shown at finer-step colors. Useful for confirming that budget allocation is actually reaching the intended pixels.

---

## 11. Failure Modes

The Oracle Scheduler introduces new failure modes beyond those of the current uniform traversal.

### 11.1 Risk map stale on scene change

If the scene geometry, field configuration, or camera position changes while the risk map is cached from a previous survey pass, the risk map may misidentify regions as low-risk that are now high-risk. This could result in a high-risk tile receiving insufficient budget.

**Mitigation**: implement a `SceneTransportFingerprint` change detector (as specified in [object_seeded_null_geodesic_tiling_scheduler.md](object_seeded_null_geodesic_tiling_scheduler.md) §2). On fingerprint change, invalidate the risk map and trigger a new survey pass before the next refinement pass.

### 11.2 Budget starvation: low-risk tiles rendered at insufficient density

Aggressive risk-weighted redistribution may reduce low-risk tile budget below the hermetic floor (§6.3). This causes `missHits > 0` in hermetic scenes, which is a regression.

**Mitigation**: the hermetic gate runs after any scheduler parameter change. `missHits > 0` in either straight or GRIN fixture is a hard failure.

### 11.3 Oracle guardrail violation

If oracle comparison results are accidentally used to influence scheduling decisions in a way that feeds back into rendering (e.g., by modifying hit selection based on oracle-predicted collider), the oracle loses its status as an independent reference measurement.

**Mitigation**: the `DiagnosticOnlyGuardrail` constant is an architectural enforcement point. Code review must confirm that no oracle output path reaches any rendering decision point.

### 11.4 Infinite refinement recursion

If island microscopy recurses (§8.2) on a sub-island that itself contains a sub-island, and there is no stopping rule, the recursion depth is unbounded.

**Mitigation**: enforce a maximum recursion depth (recommended: 3). If a sub-island does not seal at oracle step after 3 recursion levels, classify it as `MultiSolution` and archive it for manual review. Do not continue refining.

### 11.5 Tile boundary artifacts

When adjacent tiles receive different step allocations, the production integration may produce slightly different transport decisions at tile boundaries for pixels near ownership seams. This can create visible tile-boundary artifacts in the film.

**Mitigation**: use a gradient blending zone of 2–4 pixels at tile boundaries, where the step allocation is linearly interpolated between the two tiles' assigned steps. This is a presentation concern, not a transport correctness concern.

### 11.6 Survey pass masking real instability

If the survey pass uses too coarse a sampling stride (e.g., stride 16), it may miss a compact transport island entirely. The island would be classified as a low-risk region and receive insufficient budget in the refinement pass.

**Mitigation**: after the first full oracle survey of a new scene configuration, record the minimum spatial scale of any detected island. Set the survey stride to be no larger than half the minimum island dimension.

---

## 12. Validation Requirements

The Oracle Scheduler must not degrade any existing validated baseline. The following validation requirements apply to any v0.2 implementation.

### 12.1 Hermetic closure gate (mandatory)

Both hermetic observatory fixtures must pass with `missHits == 0` and `traversalRowsCompleted == filmHeight` under any Oracle Scheduler configuration:

```bash
bash scripts/run_hermetic_observatory_full_pixel.sh --godot-exe ./scripts/godot_local.sh
```

Any `missHits > 0` is a regression. Any `traversalRowsCompleted < filmHeight` is a regression.

### 12.2 Quick smoke gate (pre-commit)

The quick smoke gate (`--quick`) runs at 320×180 and should complete within the existing timeout budget:

```bash
bash scripts/run_hermetic_observatory_full_pixel.sh --quick --godot-exe ./scripts/godot_local.sh
```

### 12.3 Playmode verifier (no regression)

The existing playmode verifier must continue to pass:

```bash
bash scripts/run_grin_observe_playmode_verify.sh
```

### 12.4 Build gate

```bash
dotnet build "Physical Light and Camera Units.csproj"
```

Must compile with 0 errors. The existing 35 warnings are pre-existing.

### 12.5 Island registry consistency

If an island registry is maintained across frames, its contents must be validated against fresh oracle microscopy results after each scene configuration change. Stale island records that no longer correspond to actual transport instability must be invalidated.

### 12.6 Oracle replay gate

All oracle comparison runs must produce zero replay failures (`OracleIntegrationSettings.ReplayCount = 2`). A non-zero replay failure count indicates oracle non-determinism and invalidates any stability classifications produced in that run.

---

## 13. Hermetic Chamber Integration

The hermetic observatory pair (`fixture_hermetic_observatory_straight.tscn`, `fixture_hermetic_observatory_grin.tscn`) provides the correctness baseline for the Oracle Scheduler throughout development.

### 13.1 Role of the hermetic fixtures

The hermetic chamber is a sealed 12-unit box with no openings. Every ray must hit a classified wall surface. `missHits == 0` is not a goal — it is a hard floor. The chamber does not test visual quality; it tests transport closure.

For the Oracle Scheduler, the hermetic fixtures serve three additional roles:

**Role 1: Budget allocation floor calibration.** The minimum per-tile budget must be sufficient to close the hermetic chamber. Calibrate `MIN_STEPS_PER_PIXEL` by finding the lowest step budget at which both hermetic fixtures pass, then add a 25% margin.

**Role 2: Risk map sanity check.** In the hermetic chamber, every wall is an equivalent transport target. There are no seam instabilities, no coherent islands, no curvature gradients (in the straight fixture) and only moderate gradients (GRIN fixture). The risk map for hermetic scenes should be flat or near-flat. A risk map that identifies large high-risk regions in a hermetic scene has a calibration defect.

**Role 3: Regression detection.** Any Oracle Scheduler change that increases `missHits` in either hermetic fixture is a regression, regardless of any visual improvement in presentation scenes. The hermetic gate runs after every non-trivial scheduler parameter change.

### 13.2 Future hermetic variants for scheduler stress-testing

The current hermetic fixtures use conservative GRIN parameters (`Amp=0.6`, `ROuter=3.0`). To stress-test the Oracle Scheduler's curvature-gradient refinement path, higher-amplitude fixtures should be created at the appropriate point:

| Fixture | Amp | ROuter | Expected behavior |
|---|---|---|---|
| `fixture_hermetic_observatory_grin.tscn` (current) | 0.6 | 3.0 | Passes easily; flat risk map |
| `fixture_hermetic_observatory_grin_high_amp.tscn` (future) | 1.5–2.0 | 3.0 | May produce `missHits > 0` without curvature-aware step allocation |
| `fixture_hermetic_observatory_grin_offset.tscn` (future) | 0.6 | 2.0 (offset) | Asymmetric curvature; tests anisotropic tile scheduling |

These variants are proposed here as validation targets, not as implementation tasks. They should be created only when the Oracle Scheduler's curvature refinement path is ready to test.

---

## 14. Relationship to FEA Adaptive Meshing

The FEA adaptive meshing analogy is the primary architectural inspiration for the Oracle Scheduler and is worth making explicit.

### 14.1 The analogy

In finite element analysis (FEA), automatic adaptive meshing refines element density in regions where the solution gradient, stress concentration, or geometric discontinuity is locally high. The rest of the mesh remains coarse. The result is accurate solutions where accuracy is needed and efficient computation where it is not.

| FEA concept | Oracle Scheduler equivalent |
|---|---|
| Mesh element | Film tile (or pixel) |
| Element size | Integration step length |
| Solution gradient | Transport decision risk (`DecisionRisk` scalar) |
| Stress concentration | Transport island (compact unresolved region) |
| Geometric discontinuity | Ownership seam / domain boundary |
| Mesh refinement criterion | `EpsilonStabilityClass.Unresolved` at production step |
| Convergence test | Precision closure (`sealed at step S`) |
| Refinement cascade | Island microscopy recursion (§8.2) |
| Global error norm | Mean `DecisionRisk` across frame |

### 14.2 Where the analogy holds

The analogy holds well for:
- **Spatial budget concentration**: both FEA and the Oracle Scheduler allocate resources to high-gradient regions.
- **Convergence testing**: both use a convergence criterion (stress convergence / precision closure) rather than a fixed resolution.
- **Adaptivity without global refinement**: both avoid the trap of globally reducing element size / step length when the instability is local.

### 14.3 Where the analogy breaks

The analogy does not hold for:
- **Interpolation between elements**: FEA interpolates solution values across element boundaries; the Oracle Scheduler does not interpolate transport decisions. Each pixel is independent. Tile boundary step-blending (§11.5) is a presentation concern, not a transport interpolation.
- **A-posteriori error estimation**: FEA uses a-posteriori error estimators (e.g., Zienkiewicz-Zhu) to predict refinement need from the current solution. The Oracle Scheduler uses oracle comparison as its error estimator — this is a measurement, not an estimate from the production solution itself.
- **Mesh topology change**: FEA can add or remove elements and rebuild connectivity. The Oracle Scheduler works on a fixed film resolution. "Refinement" means finer step length and more passes, not finer pixel resolution.
- **Physical ground truth**: FEA meshing converges toward a known physical ground truth (the PDE solution). The Oracle Scheduler converges toward the best available numerical reference (oracle step 0.0015625 within the eikonal limit). This is not a physical ground truth.

### 14.4 A note on physics claims

This document does not claim that the Oracle Scheduler produces physically correct results. The oracle reference path is the best-known renderer-reference integration under the current metric field and scene geometry, within the eikonal approximation. Precision closure means the production integration agrees with this reference at a given step; it does not mean the integration agrees with a physical solution to the Einstein field equations, the Hamilton-Jacobi equation, or any other physical theory. These are instrumentation measurements.

---

## Implementation Prerequisites

The following are required before any Oracle Scheduler v0.2 implementation begins:

| Prerequisite | Status | Blocking what |
|---|---|---|
| `traversalRowsCompleted` metric (per-row 1D) | Complete (v0.0-pre) | Traversal vs. presentation separation |
| 2D tile completion bitmask | Not implemented | Tile scheduling state |
| Per-tile risk score table | Not implemented | Risk-weighted scheduling |
| Per-segment `K_max` in production integration | Not implemented | Curvature-gradient refinement |
| Scene transport fingerprint / cache invalidation | Partially defined (see [object_seeded_null_geodesic_tiling_scheduler.md](object_seeded_null_geodesic_tiling_scheduler.md)) | Risk map stale detection |
| Island registry persistent store | Not implemented | Cross-frame island tracking |
| `[GrinBasicVisual][Coverage]` emission confirmed | Partially confirmed | Hermetic coverage gate |

---

## Claims Register

| Claim | Type |
|---|---|
| Uniform traversal expends equal budget on stable and unstable pixels | Empirical finding (island microscopy; Cathedral Probe DOE) |
| 83.1% of ROI pixels stable at coarsest tested step; 16.9% unresolved | Empirical finding (oracle ROI sweep `20260505`) |
| Tile scheduling reduces band artifact (20.2% → 0.0%) | Empirical finding (Cathedral Probe tile comparison) |
| Oracle outputs must not feed rendering | Architectural decision (guardrail constant) |
| Coherent seam islands are blind spots for continuity vector analysis | Hypothesis (confirmed by ROI sweep vs continuity vector overlap) |
| FEA adaptive meshing analogy holds for spatial budget concentration and convergence testing | Architectural analogy; not a physical derivation |
| Oracle step 0.0015625 is physical ground truth | **Explicitly rejected.** Oracle step is best-known numerical reference within eikonal limit. |
| Oracle Scheduler will improve rendering performance | Not claimed. Adaptive allocation trades low-risk pixel budget for high-risk pixel budget. Wall-clock throughput is not predicted. |

---

## Cross-References

- [transport_island_microscopy.md](transport_island_microscopy.md) — Island microscopy workflow, oracle guardrail, convergence ladders
- [curvature_domain_ownership.md](curvature_domain_ownership.md) — Domain decomposition, bridge anomaly, multi-metric risk
- [cathedral_probe_architecture.md](cathedral_probe_architecture.md) — DOE evidence: step length vs. banding; tile vs. row scheduler comparison
- [scheduler_decorrelation_and_local_coherence.md](scheduler_decorrelation_and_local_coherence.md) — Row-phase resonance; decorrelation direction
- [object_seeded_null_geodesic_tiling_scheduler.md](object_seeded_null_geodesic_tiling_scheduler.md) — Object-seeded v1/v2/v3 architecture; TransportRiskField; SceneTransportFingerprint
- [ReleaseToProduction/ORACLE_SCHEDULER_V02_DIRECTION.md](https://github.com/AetherTopologist/GD_xPRIMEray/tree/main/ReleaseToProduction/ORACLE_SCHEDULER_V02_DIRECTION.md) — High-level direction note with scheduling primitive table
- [ReleaseToProduction/PHASE2_HERMETIC_CLOSURE_MILESTONE.md](https://github.com/AetherTopologist/GD_xPRIMEray/tree/main/ReleaseToProduction/PHASE2_HERMETIC_CLOSURE_MILESTONE.md) — Hermetic closure baseline; traversalRowsCompleted semantic correction
- `RendererCore/Validation/ReferenceTransportOracle.cs` — Oracle guardrail; EpsilonStabilityClass; OracleIntegrationSettings
- `GrinFilmCamera.cs` — `_rowCursor`, `_fixtureRowsCompleted`, `ResetRowCursor`, `_img` retention
- `tools/hermetic_observatory_observe.py` — `check_hermetic()`; traversalRowsCompleted gate
