# RenderStep Gate Hierarchy Snapshot

Scope: primary `GrinFilmCamera.RenderStep()` loop through pass1 segment build (`RayBeamRenderer.BuildRaySegmentsCamera_Pass1`) and pass2 narrowphase (`RayBeamRenderer.*Hit` helpers). No behavior changes.

## 1) Gate / Decision Hierarchy (RenderStep path)

### ASCII tree

```text
GrinFilmCamera.RenderStep()
+- Re-entry guard (disable UpdateEveryFrame, return)
+- Resolve effective config + research clamps (may clamp RBR ray-step params)
+- Init budgets/watchdogs/helpers
+- Film/camera state sync
|  +- EnsureFilmImageSize (Width/Height * FilmResolutionScale)
|  +- settings/camera dirty row reset (deferred if mid-band)
|  `- wrap row cursor at film end
+- Resolve effective time budget (RenderStepMaxMs AND UpdateEveryFrameBudgetMs, tighter wins)
+- Compute rowsPerFrame (adaptive rows + update-every-frame row/pixel/segment caps)
+- Band selection
|  +- pending pass2? reuse cached band, skip pass1
|  `- else y=[rowCursor, rowCursor+rowsPerFrame)
+- Band-entry guards
|  +- incomplete-band re-entry guard
|  +- stuck-band repeat watchdog -> force advance
|  +- max pixel cap -> budgetStop/yield or abort+finalize
|  `- max segment cap -> budgetStop/yield or abort+finalize
+- PASS1 (parallel) [unless pending pass2]
|  +- per-pixel stride gate (skip non-stride pixels)
|  +- _rbr.BuildRaySegmentsCamera_Pass1(...)
|  |  `- RayBeamRenderer ray integration loop (StepsPerRay/StepLength/adaptive step clamps)
|  +- pass1-end max-ms check -> defer pass2 (pending band) + finalize advance
|  `- watchdog check -> abort/force advance
+- PASS2 (main thread)
|  +- per-pixel loop
|  |  +- BVH/TLAS envelope build + QueryAabb candidate gate
|  |  +- quick-ray / overlap / hybrid gates
|  |  +- softgate scoring/budgets/watchdog gates (optional subdivide retry)
|  |  +- pass2 stride gate (skip some segments)
|  |  `- narrowphase (SweepSegmentHit / SubdividedRayHit), hit accept/reject vs TLAS candidates
|  +- film shading + _img.SetPixel writes (per pixel / stride fill)
|  +- budgetStop? (yield / incomplete mark / finalize depending reason)
|  `- normal band commit + row advance + _tex.Update(_img)
`- Finally
   +- no-row-progress watchdog -> force row advance
   `- RecordRenderHealthSample(...) (geomPix/geomRays/p2Samp/etc.)
```

### Mermaid flowchart

```mermaid
flowchart TD
  A[RenderStep()] --> B{Re-entry?}
  B -- yes --> B1[Disable UpdateEveryFrame + return]
  B -- no --> C[ResolveEffectiveConfig + init helpers/budgets]
  C --> D[EnsureFilmImageSize / camera+settings row reset]
  D --> E[Compute effectiveMaxMs + rowsPerFrame]
  E --> F[Select band yStart..yEnd / pendingPass2 reuse]
  F --> G{Band guards ok?}
  G -- no --> G1[budgetStop / forceAdvance / abort]
  G -- yes --> H{pendingPass2?}
  H -- no --> I[PASS1 Parallel.For + BuildRaySegmentsCamera_Pass1]
  H -- yes --> J[Skip PASS1]
  I --> K{max_ms after pass1?}
  K -- yes --> K1[cache pending pass2 + finalize advance]
  K -- no --> L[PASS2 main-thread loop]
  J --> L
  L --> L1[BVH/TLAS QueryAabb + broadphase + narrowphase]
  L1 --> L2[Film shading + _img.SetPixel writes]
  L2 --> M{budgetStop or watchdog?}
  M -- yes --> M1[yield / mark incomplete / finalize]
  M -- no --> N[Commit band + row advance + _tex.Update]
  M1 --> O[finally: no-row-progress watchdog + render health sample]
  N --> O
```

## 2) Major Control Factors (what they cap / throttle)

| Knob(s) | Default(s) | What it gates | Counters impacted (typical) | Typical failure signature | Consumed at (file:line) |
|---|---|---|---|---|---|
| `Width`, `Height`, `FilmResolutionScale` | `160`, `90`, `1.0` | Runtime film dimensions (`_filmWidth/_filmHeight`), total pixels per band | `pixelCount`, `bandTracedPixels`, `processedPixelsThisBand`, `_geomPixelProcessedThisFrame`, `_geomRayTests*`, film write count | `low_geom_pix`, low hits globally, budget stops before pass2 | exports `GrinFilmCamera.cs:128-136`; resolved `GrinFilmCamera.cs:7206-7223` |
| `PixelStride` | `1` | Pass1 sampling density (skip non-stride pixels) and pass2 opportunity density | `bandTracedPixels`, `_perfFrame.TracedPixels`, `_geomPixelProcessedThisFrame`, `_geomRayTests*`, `bandHits` | `low_geom_pix`, blocky coverage, low hits with normal ms | export `GrinFilmCamera.cs:139`; traced estimate `GrinFilmCamera.cs:2609-2612`; stride gate `GrinFilmCamera.cs:3575-3587` |
| `RowsPerFrame` | `8` | Base band height before adaptive/hard caps | `bandH`, `pixelCount`, `segTotal`, all band counters | Slow convergence with no cap logs | export `GrinFilmCamera.cs:142`; base rows `GrinFilmCamera.cs:3193` |
| `TargetMsPerFrame`, `MinRowsPerFrame`, `MaxRowsPerFrameCap` | `16`, `4`, `256` | Adaptive rows target and bounds (future calls) | `rowsPerFrame`, `bandH`, indirect effect on all per-band counters | `TargetMsPerFrame` raised but rows stay bounded/clamped | exports `GrinFilmCamera.cs:221-227`; apply `GrinFilmCamera.cs:3193-3230`; feedback `GrinFilmCamera.cs:6178-6188` |
| `UpdateEveryFrameBudgetMs` | `16f` | Per-call time budget clamp when `UpdateEveryFrame=true` | `budgetStop`, incomplete-band reuse, `rowsDone`, pass1/pass2 completion | `update_every_frame_budget`, `max_ms_after_pass1`, partial bands | export `GrinFilmCamera.cs:197`; clamp `GrinFilmCamera.cs:2620-2627`; watchdog `GrinFilmCamera.cs:2951-2962` |
| `UpdateEveryFrameMaxRowsPerStep` | `2` | Hard row cap per call under `UpdateEveryFrame` | `rowsPerFrame`, `bandH`, `pixelCount`, all band counters | Higher target ms has little/no effect on hits | export `GrinFilmCamera.cs:200`; row clamp `GrinFilmCamera.cs:3203-3207` |
| `RenderStepMaxMs` | `50` | Hard RenderStep watchdog time | `budgetStop` / abort, `UpdateEveryFrame` disable path, incomplete bands | `renderstep_max_ms`, watchdog abort, forced advance | export `GrinFilmCamera.cs:206`; effective max + watchdog `GrinFilmCamera.cs:2620-2627`, `GrinFilmCamera.cs:2951-2972` |
| `RenderStepMaxPixelsPerFrame` | `2000000` | Hard band pixel cap (also row pre-cap in `UpdateEveryFrame`) | `pixelCount`, `rowsPerFrame`, downstream all counters | `max_pixels`, low `geomPix` despite low stride | export `GrinFilmCamera.cs:209`; row pre-cap `GrinFilmCamera.cs:3208-3210`; pre-band cap `GrinFilmCamera.cs:3297-3313` |
| `RenderStepMaxSegmentsPerFrame` | `20000000` | Hard band segment cap (`pixelCount * maxSeg`) (also row pre-cap) | `segTotal`, `bandSegsIntegrated/Tested`, `bandPhysicsQueries`, `_geomRayTests*` | `max_segments`, pass2 under-traces even with pixels available | export `GrinFilmCamera.cs:212`; row pre-cap `GrinFilmCamera.cs:3211-3213`; pre-band cap `GrinFilmCamera.cs:3402-3419` |
| `RenderStepNoRowProgressRepeatLimit` | `6` | Finally-block no-row-progress watchdog threshold | `_noRowProgressRepeats`, forced row advance, band continuity | repeated `guard_no_row_progress` forced advances | export `GrinFilmCamera.cs:215`; watchdog `GrinFilmCamera.cs:6558-6580` |
| `UsePass2CollisionStride`, `Pass2CollisionStrideNear/Far/FarStartT`, `MinSegLenForStrideSkip` | `false`, `1/4/0.35`, `0` | Distance-based pass2 segment skip (narrowphase throttle) | `subRaysSkippedByPass2Stride`, `bandSegsTested`, `_geomRayTestsTotalThisFrame`, `bandHits` | `geomPixProcessed` normal but `geomRays` low | exports `GrinFilmCamera.cs:460-472`; helper `GrinFilmCamera.cs:7066-7080`; skip gate `GrinFilmCamera.cs:5625-5653` |
| `UseGeometryTLASPruning`, `Pass2GeomEnvelopeRadiusScale`, `Pass2GeomEnvelopeAabbExpand` | `true`, `1.10`, `0.0` | BVH/TLAS candidate pruning before narrowphase | `_geomSegmentsQueriedThisFrame`, `_geomSegWithCandidates*`, `_geomCandidates*`, `geomPixHadAnyCandidates/NoCand`, `_geomRayTestsAccepted/Rejected`, `pass2SampledSegments` hist | high `geomPixNoCand`, high rejects, lower `geomRays` | exports `GrinFilmCamera.cs:473-481`; envelope `GrinFilmCamera.cs:4651-4664`; TLAS query/counters `GrinFilmCamera.cs:4942-4996` |
| SoftGate core: `Pass2SoftGateEnableQuickRayMiss`, `Pass2SoftGateScoringEnabled` | `false`, `true` | Enables score-gated subdivide retries on quick-ray misses | `_softGate*`, `p2SoftGateAttempts/Hits`, `bandHits`, `_geomRayTestsTotalThisFrame` | quick-ray misses never recovered | exports `GrinFilmCamera.cs:528-530`, `GrinFilmCamera.cs:574-576`; gate `GrinFilmCamera.cs:4214-4220` |
| SoftGate budgets: `MaxAttemptsPerPixel`, `MaxAttemptsPerFrame`, `MaxSubdividedCallsPerFrame`, `WatchdogMs` | `2`, `5000`, `10000`, `50f` | Caps softgate retry count/cost and per-subdivide runtime | `_softGateAttemptsUsedThisFrame`, `_softGateSubdividedCallsUsedThisFrame`, `softGateBudgetExceeded`, `budgetStopReason`, `bandHits` | `softgate_attempt_cap`, `softgate_subdivide_cap`, `guard_softgate_watchdog` | exports `GrinFilmCamera.cs:537-564`; enforce `GrinFilmCamera.cs:4001-4154`; watchdog `GrinFilmCamera.cs:5764-5784` |
| SoftGate scoring: `MinSegmentLength`, `ScoreThreshold`, turn/prevLost/random, `ScoreBudgetPerFrame` | `0.2`, `1.0`, `1.0/0.75/0.01`, `32` | Selects which quick-ray misses are eligible for retry | `_softGateFrame.*Skip*`, `_softGateFrame.SoftGateAttempts/Hits`, `bandHits`, `_geomRayTestsTotalThisFrame` | softgate enabled but no attempts (`scoreTooLow`, `segLenTooShort`) | exports `GrinFilmCamera.cs:579-595`; scoring gate `GrinFilmCamera.cs:4237-4304` |
| `StepsPerRay`, `StepLength`, `MinStepLength`, `MaxStepLength`, `StepAdaptGain` | `64`, `0.25`, `0.05`, `0.5`, `0.05` | Ray integration step count + adaptive step size in pass1 segment build | `maxSeg` (derived), `_segCountPerPixel`, `bandSegsIntegrated`, `segTotal`, `pass1StepsIntegrated` | `max_segments` cap or coarse paths / missed hits | exports `RayBeamRenderer.cs:28-42`; loops `RayBeamRenderer.cs:2236-2336`, `RayBeamRenderer.cs:1574-1647` |
| `LowCurvaturePerpAccel`, `LowCurvatureStepBoost` | `0.05`, `2.0` | Enlarges steps in low curvature (fewer emitted segments) | `_segCountPerPixel`, `bandSegsIntegrated`, `segTotal`, `pass1StepsIntegrated` | low hits in gentle bends despite enough budget | exports `RayBeamRenderer.cs:43-48`; used `RayBeamRenderer.cs:2294-2303`, `RayBeamRenderer.cs:2051-2060` |
| `CollisionEveryNSteps`, `UseScreenSpaceCollisionCadence`, `CollisionMaxErrorPixels`, `MinDepthForError`, `MinCollisionEveryNSteps` | `1`, `true`, `0.75`, `0.10`, `1` | Segment emission cadence in pass1 builder (`ce`) | `_segCountPerPixel`, `bandSegsIntegrated`, `segTotal`, pass2 opportunity count | low `bandSegsIntegrated` + low hits with normal pixel coverage | exports `RayBeamRenderer.cs:82-84`, `RayBeamRenderer.cs:109-121`; cadence use `RayBeamRenderer.cs:2181-2184`, `RayBeamRenderer.cs:2308-2318`, emit `RayBeamRenderer.cs:2347-2355` |
| `CollisionRaySubdivideThreshold`, `MaxCollisionSubsteps` | `0.25`, `16` | Per-segment narrowphase fidelity/cost for subdivided ray tests | `_geomRayTestsTotalThisFrame`, `bandPhysicsQueries`, `_perfFrame.SubdividedRayQueries`, hit recovery on long segments | missed long-segment hits or expensive subdivides | exports `RayBeamRenderer.cs:97-102`; pass2 subdiv calc `GrinFilmCamera.cs:5691-5703` |

## 3) Where Coverage Metrics Are Affected (`geomPix`, `geomRays`, etc.)

### Primary mutation sites (pass2)

- `geomPixProcessed` (`_geomPixelProcessedThisFrame`)
  - Incremented by `MarkGeomPixelProcessedForWork()` at `GrinFilmCamera.cs:4530-4538`
  - Called before actual geometry work (TLAS query, overlap, quick-ray, sweep, subdivided ray), e.g. `GrinFilmCamera.cs:4945`, `GrinFilmCamera.cs:5050`, `GrinFilmCamera.cs:5562`, `GrinFilmCamera.cs:5716`
- `geomSegQueried`, `geomSegWithCandidates`, `geomSegZero`
  - TLAS query block `GrinFilmCamera.cs:4942-4996`
  - Exact increments: `GrinFilmCamera.cs:4949`, `GrinFilmCamera.cs:4953`, `GrinFilmCamera.cs:4957`
- `geomCandidatesTotal`, `geomCandidatesSegments`
  - TLAS candidate tally at `GrinFilmCamera.cs:4981-4982`
- `geomPixHadAnyCandidates`, `geomPixNoCand`
  - Per-pixel commit after pass2 pixel loop at `GrinFilmCamera.cs:5954-5961`
- `geomRayTestsTotal`
  - Quick-ray path increment at `GrinFilmCamera.cs:5564`
  - Subdivided narrowphase increments by returned `rayQueries` at `GrinFilmCamera.cs:5726-5728`
- `geomRayTestsAccepted`, `geomRayTestsRejected`
  - TLAS accept/reject filter on actual hit at `GrinFilmCamera.cs:5835-5880`

### RenderHealth coverage / sampling (`p2Samp`, candidate histogram)

- `pass2SampledSegments`, radius/env stats, candidate histogram counters are updated in `RecordRenderHealthPass2Sample(...)` at `GrinFilmCamera.cs:3847-3869`
- Called in prune-ON TLAS path with real candidate count at `GrinFilmCamera.cs:4985-4989`
- Called in prune-OFF path with candidate count `-1` (hist intentionally NA) at `GrinFilmCamera.cs:5416-5422`
- All coverage counters are committed to RenderHealth window samples via `RecordRenderHealthSample(...)` call at `GrinFilmCamera.cs:6589-6634`

## 4) Exact Gate Locations (early exits / watchdogs / budget stops)

| Gate | Location(s) | Stable search token |
|---|---|---|
| Re-entry guard | `GrinFilmCamera.cs:2326-2338` | `"[RenderStep][Guard] re-entry blocked"` |
| Effective time-budget clamp | `GrinFilmCamera.cs:2620-2627` | `choose the tighter of RenderStepMaxMs and UpdateEveryFrameBudgetMs` |
| Budget stop helper (`budgetStop`) | `GrinFilmCamera.cs:2765-2783` | `void TriggerBudgetStop(string reason)` |
| Budget yield log | `GrinFilmCamera.cs:2785-2809` | `"[RenderStep][Yield] reason="` |
| Force row advance on stop | `GrinFilmCamera.cs:2724-2744` | `void ForceAdvanceRowCursorOnStop` |
| Watchdog (ms) decision | `GrinFilmCamera.cs:2951-2972` | `bool CheckRenderStepWatchdog()` |
| Abort path (disables `UpdateEveryFrame`) | `GrinFilmCamera.cs:2974-3019` | `void AbortRenderStep(string reason)` |
| Missing renderer/camera guards | `GrinFilmCamera.cs:3102-3129` | `AbortRenderStep("No RayBeamRenderer assigned")` |
| Adaptive rows + row/pixel/segment row caps | `GrinFilmCamera.cs:3193-3230` | `apply pixel/segment caps to row budget` |
| Pending pass2 band reuse | `GrinFilmCamera.cs:3232-3245` | `if (pendingPass2)` |
| Incomplete-band same-frame guard | `GrinFilmCamera.cs:3256-3266` | `guard_incomplete_band` |
| Stuck-band repeat watchdog | `GrinFilmCamera.cs:3268-3294` | `"[RenderStep][WATCHDOG] stuckBand"` |
| Max pixel cap pre-band | `GrinFilmCamera.cs:3297-3313` | `reason=max_pixels` |
| Max segment cap pre-band | `GrinFilmCamera.cs:3402-3419` | `reason=max_segments` |
| Pass1 timeout defers pass2 | `GrinFilmCamera.cs:3698-3708` | `reason=max_ms_after_pass1` |
| Pass1 watchdog abort/force advance | `GrinFilmCamera.cs:3711-3727` | `AbortRenderStep("watchdog")` |
| SoftGate attempt/subdivide cap gates | `GrinFilmCamera.cs:4001-4154` | `softgate_attempt_cap` / `softgate_subdivide_cap` |
| SoftGate scoring/eligibility gates | `GrinFilmCamera.cs:4214-4304` | `ShouldSoftGate(` |
| TLAS prune no-candidate skip (pass2 segment) | `GrinFilmCamera.cs:5469-5480` | `candidateCount == 0 && !softGateAllowedNoCandidate` |
| Pass2 stride skip gate | `GrinFilmCamera.cs:5625-5653` | `subRaysSkippedByPass2Stride` |
| SoftGate per-subdivide watchdog | `GrinFilmCamera.cs:5764-5784` | `guard_softgate_watchdog` |
| Budget-stop finalization / incomplete-band mark | `GrinFilmCamera.cs:6340-6365` | `if (budgetStop)` (post-upload block) |
| Normal commit / no-hit advance fallback | `GrinFilmCamera.cs:6367-6389` | `ForceAdvanceOnNoHit("guard_no_progress"` |
| No-candidate band stall watchdog | `GrinFilmCamera.cs:6502-6526` | `guard_no_candidates_band` |
| No-hit band stall watchdog | `GrinFilmCamera.cs:6528-6552` | `guard_no_hit_band` |
| No-row-progress repeat watchdog (finally) | `GrinFilmCamera.cs:6558-6580` | `guard_no_row_progress` |
| Pass2 stride computation helper | `GrinFilmCamera.cs:7066-7080` | `ComputePass2CollisionStride(` |

## Common Failure Signatures (likely gate causes)

| Signature | Likely causes |
|---|---|
| `low_geom_pix` / `geomPixProcessed` much lower than expected | `PixelStride` high; small `rowsPerFrame`; `UpdateEveryFrameMaxRowsPerStep`; `RenderStepMaxPixelsPerFrame`; early `budgetStop` / watchdog before pass2; pending-pass2 churn on same band |
| `geomRays` (`geomRayTestsTotal`) low while `geomPixProcessed` is normal | Pass2 stride skipping (`UsePass2CollisionStride`); TLAS prune zero-candidate skips; quick-ray early misses with softgate disabled/strict; `NearestHitOnly` + early-outs |
| `geomPixNoCand` high / `geomPixHadAnyCandidates` low | TLAS pruning enabled with tight envelopes (`Pass2GeomEnvelopeRadiusScale` / `AabbExpand` too low); actual sparse scene; band in empty space |
| `geomRayTestsRejected` high | TLAS candidate mismatch / aggressive prune / ID-space mismatch symptoms; hits found by narrowphase but rejected by candidate set |
| `p2Samp` low despite higher `TargetMsPerFrame` | `UpdateEveryFrameBudgetMs` or `RenderStepMaxMs` still tighter; `UpdateEveryFrameMaxRowsPerStep`; pixel/segment frame caps; softgate caps; pass1 timeout causing `max_ms_after_pass1` defer |

## Max Hits Strategy (symptom -> first fix)

Use this in order. The point is to clear hard gates before tuning softer heuristics.

### If `low_geom_pix` / low `geomPixProcessed`

1. Check hard caps first (these beat `TargetMsPerFrame`):
   - `UpdateEveryFrameMaxRowsPerStep`
   - `RenderStepMaxPixelsPerFrame`
   - `RenderStepMaxSegmentsPerFrame`
2. Increase sampling density:
   - lower `PixelStride` (highest leverage if >1)
   - raise `RowsPerFrame` and/or `MaxRowsPerFrameCap`
3. Then relax time clamps:
   - `UpdateEveryFrameBudgetMs`
   - `RenderStepMaxMs`
4. If you see `max_ms_after_pass1`, pass1 is consuming the budget before pass2.

### If `geomPixProcessed` is normal but `geomRays` is low

1. Disable or reduce pass2 stride skipping:
   - `UsePass2CollisionStride=false` (A/B first)
   - reduce `Pass2CollisionStrideFar`
2. Check prune gating:
   - `UseGeometryTLASPruning=false` (A/B)
   - widen `Pass2GeomEnvelopeRadiusScale` / `Pass2GeomEnvelopeAabbExpand`
3. Check softgate suppression of retry paths:
   - enable softgate core (if off)
   - lower `Pass2SoftGateScoreThreshold`
   - raise softgate budgets (`ScoreBudgetPerFrame`, attempts/subdivides)

### If `bandSegsIntegrated` is low (few pass2 opportunities)

1. Increase ray/segment fidelity in pass1:
   - `StepsPerRay` up
   - `StepLength` down
2. Reduce cadence skipping:
   - lower `CollisionEveryNSteps`
   - tighten screen-space cadence tolerance (`CollisionMaxErrorPixels`)
3. Reduce low-curvature coarse stepping:
   - lower `LowCurvatureStepBoost` or `LowCurvaturePerpAccel`

### If `geomRayTestsRejected` is high

1. Increase TLAS envelope conservativeness:
   - `Pass2GeomEnvelopeRadiusScale`
   - `Pass2GeomEnvelopeAabbExpand`
2. A/B with `UseGeometryTLASPruning=false`
3. Review prune-audit counters before widening further

### If increasing `TargetMsPerFrame` does nothing

Most likely blockers (in order):
1. `UpdateEveryFrameMaxRowsPerStep`
2. `RenderStepMaxPixelsPerFrame`
3. `RenderStepMaxSegmentsPerFrame`
4. `UpdateEveryFrameBudgetMs`
5. `RenderStepMaxMs`

## Code Map (entry -> RayBeamRenderer)

- `GrinFilmCamera.RenderStep()` entry: `GrinFilmCamera.cs:2324`
- Pass1 call into renderer: `_rbr.BuildRaySegmentsCamera_Pass1(...)` at `GrinFilmCamera.cs:3605-3633`
- Ray integration loop (pass1 builder): `RayBeamRenderer.BuildRaySegmentsCamera_Pass1` at `RayBeamRenderer.cs:2142`, loop at `RayBeamRenderer.cs:2236-2355`
- Ray integration controls (`StepsPerRay`, step lengths, adaptive step): exports `RayBeamRenderer.cs:28-48`
- Pass2 narrowphase calls from film loop:
  - `RayBeamRenderer.SweepSegmentHit(...)` e.g. `GrinFilmCamera.cs:5006-5008`
  - `RayBeamRenderer.SubdividedRayHit(...)` e.g. `GrinFilmCamera.cs:5717-5725`
- Film writes / upload (downstream of pass2 hit decisions):
  - `_img.SetPixel(...)` helper writes at `GrinFilmCamera.cs:7173`, `GrinFilmCamera.cs:7186`
  - `_tex.Update(_img)` upload at `GrinFilmCamera.cs:6336`
