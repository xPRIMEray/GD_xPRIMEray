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
|  |  +- broadphase/TLAS prune envelope + candidate gate
|  |  +- quick-ray / overlap / hybrid gates
|  |  +- softgate scoring/budgets/watchdog gates (optional subdivide retry)
|  |  +- pass2 stride gate (skip some segments)
|  |  `- narrowphase (SweepSegmentHit / SubdividedRayHit), hit accept/reject vs TLAS candidates
|  +- budgetStop? (yield / incomplete mark / finalize depending reason)
|  `- normal band commit + row advance
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
  L --> M{budgetStop or watchdog?}
  M -- yes --> M1[yield / mark incomplete / finalize]
  M -- no --> N[Commit band + advance rowCursor]
  M1 --> O[finally: no-row-progress watchdog + render health sample]
  N --> O
```

## 2) Major Control Factors (what they cap / throttle)

| Factor | Caps / throttles | Consumed at (file:line) | Notes |
|---|---|---|---|
| `Width`, `Height`, `FilmResolutionScale` | Runtime film dimensions (`_filmWidth/_filmHeight`) and total pixel workload | `GrinFilmCamera.cs:128`, `GrinFilmCamera.cs:130`, `GrinFilmCamera.cs:136`; resolved in `EnsureFilmImageSize` at `GrinFilmCamera.cs:7206` | `targetW/H = Base * ResolutionScale` |
| `PixelStride` | Pass1 sampling density (skips pixels), traced-pixel count | `GrinFilmCamera.cs:139`; stride gate `GrinFilmCamera.cs:3575`; traced estimate `GrinFilmCamera.cs:2609` | Non-stride pixels are block-filled later |
| `RowsPerFrame` | Base band height before adaptive/clamps | `GrinFilmCamera.cs:142`; rows calc `GrinFilmCamera.cs:3193` | Seed for adaptive rows |
| `TargetMsPerFrame`, `MinRowsPerFrame`, `MaxRowsPerFrameCap` | Adaptive rows target and bounds | `GrinFilmCamera.cs:221`, `GrinFilmCamera.cs:224`, `GrinFilmCamera.cs:227`; apply at `GrinFilmCamera.cs:3193-3230`; update at `GrinFilmCamera.cs:6178-6188` | Affects future band size, not immediate hard stop |
| `UpdateEveryFrameBudgetMs` | Per-call time budget clamp (when `UpdateEveryFrame`) | `GrinFilmCamera.cs:197`; clamp at `GrinFilmCamera.cs:2620-2627`; watchdog path `GrinFilmCamera.cs:2951-2962` | Tighter than `RenderStepMaxMs` wins |
| `UpdateEveryFrameMaxRowsPerStep` | Hard row cap per call under `UpdateEveryFrame` | `GrinFilmCamera.cs:200`; applied at `GrinFilmCamera.cs:3203-3207` | Can block gains from higher target ms |
| `RenderStepMaxMs` | Hard RenderStep watchdog time | `GrinFilmCamera.cs:206`; used in `effectiveMaxMs` and `CheckRenderStepWatchdog` `GrinFilmCamera.cs:2951-2972` | Non-`UpdateEveryFrame`: abort disables `UpdateEveryFrame` |
| `RenderStepMaxPixelsPerFrame` | Band pixel cap | `GrinFilmCamera.cs:209`; pre-band cap `GrinFilmCamera.cs:3297-3313`; row pre-cap estimate `GrinFilmCamera.cs:3208-3210` | Can trigger `budgetStop("max_pixels")` |
| `RenderStepMaxSegmentsPerFrame` | Band segment cap (`pixelCount * maxSeg`) | `GrinFilmCamera.cs:212`; pre-band cap `GrinFilmCamera.cs:3402-3419`; row pre-cap estimate `GrinFilmCamera.cs:3211-3213` | `maxSeg` comes from ray march settings |
| `RenderStepNoRowProgressRepeatLimit` | Finally-block forced row advance watchdog | `GrinFilmCamera.cs:215`; trigger `GrinFilmCamera.cs:6566-6580` | Prevents repeated work without row cursor movement |
| `UsePass2CollisionStride`, `Near/Far/FarStartT`, `MinSegLenForStrideSkip` | Pass2 segment test skipping cadence | exports `GrinFilmCamera.cs:460-472`; helper `ComputePass2CollisionStride` `GrinFilmCamera.cs:7066-7080`; skip gate `GrinFilmCamera.cs:5625-5653` | Skips narrowphase on some pass0 segments |
| `UseGeometryTLASPruning`, `Pass2GeomEnvelopeRadiusScale`, `Pass2GeomEnvelopeAabbExpand` | Candidate pruning before narrowphase | exports `GrinFilmCamera.cs:473-481`; envelope+TLAS query `GrinFilmCamera.cs:4651-4664`, `GrinFilmCamera.cs:4942-4996` | Reduces narrowphase tests, can reject hits if too aggressive |
| SoftGate core (`EnableQuickRayMiss`, `ScoringEnabled`) | Enables score-gated retry on quick-ray miss | exports `GrinFilmCamera.cs:528-530`, `GrinFilmCamera.cs:574-576`; gating path `GrinFilmCamera.cs:4214-4220` | Requires both flags on |
| SoftGate budgets (`MaxAttemptsPerPixel`, `MaxAttemptsPerFrame`, `MaxSubdividedCallsPerFrame`, watchdog ms) | Caps soft-gated retry count/cost | exports `GrinFilmCamera.cs:537-564`; enforced `GrinFilmCamera.cs:4001-4154`, per-subdivide watchdog `GrinFilmCamera.cs:5764-5784` | Can trigger `softgate_attempt_cap` / `softgate_subdivide_cap` |
| SoftGate scoring (`MinSegmentLength`, `ScoreThreshold`, weights, random, `ScoreBudgetPerFrame`) | Selects which quick-ray misses get subdivide retry | exports `GrinFilmCamera.cs:579-595`; scoring gate `GrinFilmCamera.cs:4237-4304` | `ScoreBudgetPerFrame` is an additional limiter |
| `StepsPerRay`, `StepLength`, `MinStepLength`, `MaxStepLength`, `StepAdaptGain` | Ray integration step count and step size (pass1 ray segment generation) | exports `RayBeamRenderer.cs:28-42`; used in loops `RayBeamRenderer.cs:2236-2336` and `RayBeamRenderer.cs:1574-1647` | Directly affects `maxSeg` / segment density |
| `LowCurvaturePerpAccel`, `LowCurvatureStepBoost` | Expands step size in low curvature -> fewer segments | exports `RayBeamRenderer.cs:43-48`; used at `RayBeamRenderer.cs:2294-2303` / `RayBeamRenderer.cs:2051-2060` | Can silently lower pass1 segment density |
| `CollisionEveryNSteps`, screen-space cadence controls | Segment emission cadence in pass1 builder | exports `RayBeamRenderer.cs:82-84`, `RayBeamRenderer.cs:109-121`; used at `RayBeamRenderer.cs:2181-2184`, `RayBeamRenderer.cs:2308-2318`, emit gate `RayBeamRenderer.cs:2347-2355` | Fewer emitted segments => fewer pass2 opportunities |
| `CollisionRaySubdivideThreshold`, `MaxCollisionSubsteps` | Narrowphase per-segment ray query count | exports `RayBeamRenderer.cs:97-102`; pass2 subdivide calc `GrinFilmCamera.cs:5691-5703` | Caps fidelity/cost of `SubdividedRayHit` |

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

## Code Map (entry -> RayBeamRenderer)

- `GrinFilmCamera.RenderStep()` entry: `GrinFilmCamera.cs:2324`
- Pass1 call into renderer: `_rbr.BuildRaySegmentsCamera_Pass1(...)` at `GrinFilmCamera.cs:3605-3633`
- Ray integration loop (pass1 builder): `RayBeamRenderer.BuildRaySegmentsCamera_Pass1` at `RayBeamRenderer.cs:2142`, loop at `RayBeamRenderer.cs:2236-2355`
- Ray integration controls (`StepsPerRay`, step lengths, adaptive step): exports `RayBeamRenderer.cs:28-48`
- Pass2 narrowphase calls from film loop:
  - `RayBeamRenderer.SweepSegmentHit(...)` e.g. `GrinFilmCamera.cs:5006-5008`
  - `RayBeamRenderer.SubdividedRayHit(...)` e.g. `GrinFilmCamera.cs:5717-5725`
