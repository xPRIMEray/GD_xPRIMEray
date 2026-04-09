# Wormhole Render Pipeline Validation

## 1. Overview

The current wormhole prototype in `GD_xPRIMEray` is a curved-ray rendering path that combines:

- GRIN-driven pass-1 transport around each mouth
- spherical boundary-shell crossing as the topological transition surface
- scene-space remap from Scene A to Scene B
- standard pass-2 geometry query and film write after remap

This differs from the ordinary single-scene curved-ray path because transport is not only bending through a field. Rays can also cross a paired boundary shell, change scene coordinates, and continue resolving geometry in a different scene context.

Validation was required because the wormhole path adds multiple new failure points that do not exist in a normal GRIN fixture:

- remap could fail
- post-remap candidates could fail to become queries
- post-remap queries could fail to hit geometry
- film capture could succeed while the film itself remained empty

The validation goal was to prove the full end-to-end path:

`pass-1 transport -> boundary interaction -> scene remap -> candidate generation -> query dispatch -> hit resolution -> film write`

The validated prototype scene is:

- `res://test-wormhole-prototype.tscn`

The core runtime pieces involved are:

- `BoundaryLayerVolume.cs`
- `RayBeamRenderer.cs`
- `GrinFilmCamera.cs`
- `WormholePrototypeRig.cs`

## 2. Rendering Pipeline (Ground Truth)

The exact validated wormhole rendering pipeline is:

1. **Pass-1 transport (GRIN stepping)**  
   Rays are integrated through the concentric `FieldSource3D` GRIN region around each mouth.

2. **Boundary interaction (`BoundaryShell` / remap trigger)**  
   The spherical `BoundaryLayerVolume` shell acts as the topological crossing surface.

3. **Scene transform (A -> B)**  
   On shell crossing, the ray is remapped into the paired mouth's scene-space.

4. **Candidate generation (TLAS)**  
   Post-remap segments generate geometry candidates through the existing geometry TLAS path.

5. **Query dispatch (`OverlapOnly` path)**  
   Candidate-bearing post-remap segments dispatch pass-2 overlap queries in the normal serial pass-2 path.

6. **Narrowphase hit resolution**  
   Post-remap queries resolve actual geometry hits through the normal narrowphase logic.

7. **Film write**  
   Accepted post-remap hits produce final film writes in the normal film pipeline.

All seven stages above are now **proven working** in the current prototype.

## 3. Validation Methodology

### Remap Validation

Remap correctness was proven with wormhole spatial probes added to the post-remap segment path.

Observed result:

- representative post-remap segments logged Scene B world coordinates around the expected Scene B offset
- candidate AABBs were in the same world-space neighborhood

This proved the remap was placing rays into the correct destination scene-space rather than leaving them in Scene A coordinates.

### Query Validation

Post-remap query dispatch was proven by instrumenting the active `OverlapOnly` path and counting post-remap candidate-bearing segments that actually dispatched overlap queries.

Observed result:

- post-remap query counts reached millions in the validated overlap-only runs
- query dispatch was therefore not the blocker once the accounting path was corrected

This distinguished a real transport issue from an earlier diagnostic blind spot.

### Hit Validation

Initial post-remap geometry hits were zero under the static validation framing. Spatial probe output showed that remapped rays were valid, but sampled segments were simply missing downstream geometry.

To isolate that from renderer failure, temporary validation geometry was introduced:

- `SceneB/ProbeWallB`

Observed result:

- `geom_hits` became non-zero
- remapped rays successfully intersected destination-scene geometry

This proved that post-remap narrowphase resolution was functional.

### Film Validation

Film validation used the same real film-buffer capture philosophy already used by working render-test style scenes. The wormhole validation path captured the accumulated film result rather than a generic debug viewport.

Observed result after hit validation:

- `final_write_px > 0`
- a valid film PNG was saved

This proved that post-remap hits were not only resolved, but were also reaching the 2D film result.

## 4. Failure Modes Discovered

The debugging journey found one real blocker and several false blockers.

### Real blocker: no downstream geometry under the static framing

Initial state:

- remaps occurred
- post-remap candidates existed
- post-remap queries occurred
- `geom_hits = 0`
- `final_write_px = 0`

Root cause:

- the static validation framing did not provide reliable downstream intercept geometry after remap

Proof:

- a representative post-remap segment had correct destination coordinates
- the same segment produced `overlapCount = 0`
- a manual narrowphase check on that exact segment also produced no hit
- adding `ProbeWallB` made `geom_hits` and `final_write_px` become non-zero

### False blockers that were ruled out

These were investigated and eliminated as primary causes:

- **Remap failure**  
  False. Spatial probes showed post-remap rays in correct Scene B coordinates.

- **Query failure**  
  False. Post-remap queries were proven to dispatch in large counts once the active overlap-only accounting path was instrumented correctly.

- **Frame budget**  
  False. Increasing the per-step budget removed budget rejection without restoring hits in the no-geometry case.

- **Stride**  
  False. Disabling pass-2 collision stride did not restore post-remap hits in the no-geometry case.

Important distinction:

- the renderer was not failing to remap or query
- it was correctly tracing remapped rays through space where the validation scene originally offered too little reliable downstream target geometry

## 5. Smart Adjust (Adaptive Path) Validation Strategy

Any future adaptive path, smart adjust logic, or optimization must preserve the wormhole pipeline invariants below.

Required invariants:

- `remap count > 0`
- `post-remap queries > 0`
- `geom_hits > 0`
- `final_write_px > 0`

Required test: **Wormhole Validation Loop**

1. Run the static wormhole validation scene.
2. Use the film-path validation capture, not a generic world/debug viewport capture.
3. Capture the wormhole funnel metrics from the runtime log.
4. Verify:
   - remaps still occur
   - post-remap queries still occur
   - post-remap geometry hits still occur
   - final film writes still occur
5. Treat any optimization as invalid if one of those pipeline invariants drops to zero.

This loop is the required safety gate for future smart adjust work.

## 6. Deterministic Harness Requirement

Wormhole validation and performance runs must execute under deterministic harness conditions.

Required conditions:

- fixed camera transform
- no live mouse drift
- no keyboard-driven camera movement
- stable scene framing across runs
- identical film-path capture methodology across before/after comparisons

Why this matters:

- remap counts, post-remap query counts, and hit/write totals are camera-sensitive
- small input drift can change portal framing, downstream geometry coverage, and candidate density near the mouth
- uncontrolled mouse capture or free-fly motion can contaminate pass-1 and pass-2 timing comparisons

The current wormhole harness therefore locks traveler input during validation/performance runs:

- `WormholePrototypeRig` can disable `FreeFlyCamera` input for validation mode
- mouse mode is forced visible during the locked run
- the validation scene keeps a fixed camera transform and does not rely on operator discipline

This deterministic harness requirement is part of the wormhole validation contract, not an optional convenience.

## 7. Optimization History

The wormhole optimization history is now grounded enough to distinguish keepers from regressions.

### Rejected: `BroadphasePolicy = Both`

Result:

- reduced useful hits and writes
- failed to preserve the validated wormhole output quality
- was reverted

Engineering conclusion:

- enabling `Both` broadphase globally for this wormhole scene is not a safe forward path

### Kept: per-frame overlap-result reuse cache in the `OverlapOnly` pass-2 path

Result:

- reduced `pass2.query`
- improved `pass2.physics`
- preserved remaps, queries, geometry hits, and final film writes
- remained repeatable under locked deterministic runs

Engineering conclusion:

- this optimization is currently worth keeping

### Kept: geometry-aware low-value sector throttle

Current kept profile:

- `layer = 0`
- `radial_bin = 3`
- `theta bins = {13,14,15,0}`
- `period = 2`

Why it is kept:

- it targets a measured waste region identified by the portal-centric usefulness map
- it does not touch the invariant annulus (`layer = 1`, `radial_bin = 3`)
- it improved the two target timing buckets while preserving wormhole output and both validation contracts

Measured effect from the kept widened-theta run:

- `pass2.query`: `3433.15 ms -> 3289.79 ms`
- `pass2.physics`: `37280.85 ms -> 36543.19 ms`
- `geom_hits`: `21933 -> 21933`
- `final_write_px`: `21933 -> 21933`
- proto-caustic invariant: `pass=true`
- low-value sector budget: `pass=true`
- low-value query-share margin improved: `0.0722 -> 0.0818`

Engineering conclusion:

- this is the current best-known wormhole performance profile
- it is both geometry-aware and contract-safe

### Rejected: stronger throttle on the same low-value family

Rejected profile:

- `layer = 0`
- `radial_bin = 3`
- `theta bins = {13,14,15,0}`
- `period = 3`

Why it was rejected:

- `pass2.query` got worse: `3289.79 ms -> 3623.40 ms`
- `pass2.physics` got worse: `36543.19 ms -> 37130.80 ms`
- `geom_hits` drifted downward: `21933 -> 21689`
- `final_write_px` drifted downward: `21933 -> 21689`
- the proto-caustic invariant still passed, but the annulus metrics weakened:
  - hit density: `1056.3125 -> 1041.0625`
  - radial gradient: `901.9375 -> 886.6875`

Engineering conclusion:

- the safe throttle boundary is currently at `period = 2`
- increasing intensity to `period = 3` crosses the safe boundary even though the formal contracts still pass
- this is a case where contract pass alone was not sufficient; hit/write stability and target bucket performance still mattered

### Rejected: overlap-result copy elision inside `RunOverlapQuery(...)`

Result:

- safe
- did not produce a useful improvement in the target bucket
- was reverted

Engineering conclusion:

- not every local pass-2 cleanup change produces measurable benefit
- future work should continue to be validated by before/after measurement, not by intuition alone

## 8. Optimization Validation Contract

Any future optimization, smart-adjust path, or adaptive rendering change must preserve the wormhole pipeline invariants below:

- `remaps > 0`
- `queries > 0`
- `geom_hits > 0`
- `final_write_px > 0`

In addition, every optimization experiment must satisfy all of the following:

- deterministic run conditions
- fixed capture path through the real film/composited result
- before/after timing collected from comparable completed-frame runs
- explicit disposition recorded as `KEEP` or `REVERT`

If one of the wormhole pipeline invariants drops to zero, the optimization is not valid for this path even if an isolated timing bucket appears to improve.

In practice, the current wormhole path now uses a two-sided contract:

- positive invariant: preserve the destination-side proto-caustic annulus
- negative invariant: keep the low-value outer-ring query share under budget

The current active negative invariant is:

- target region: `layer = 0`, `radial_bin = 3`
- baseline query share: `0.4011`
- maximum allowed query share: `baseline * 0.9 = 0.361`

This contract is necessary but not sufficient by itself. The period-3 throttle experiment showed that a change can still be rejected if:

- `pass2.query` or `pass2.physics` regresses
- `geom_hits` / `final_write_px` drift downward
- or the annulus metrics weaken materially

## 9. Smart Adjust / Adaptive Validation Guidance

Smart-adjust and adaptive logic must be validated against wormhole optical paths, not only against ordinary single-scene GRIN fixtures.

Required wormhole-side adaptive validation steps:

1. run the static wormhole validation scene
2. use deterministic camera/input conditions
3. capture funnel metrics from the runtime log
4. compare candidate, query, hit, and write continuity before and after the adaptive change
5. confirm that adaptation does not suppress wormhole-specific remap visibility or remap-hit continuity

Explicit adaptive failure conditions include:

- remaps still occur but post-remap queries collapse
- queries still occur but geometry hits collapse
- hits still occur but final writes collapse
- adaptive heuristics preserve ordinary GRIN behavior while suppressing wormhole-specific optical paths

The wormhole path must therefore remain a standing regression gate for any future smart-adjust or adaptive scheduler work.

## 10. Performance Baseline

Validated baseline scene configuration:

- static wormhole validation scene
- `OverlapOnly` broadphase path
- validation proof geometry retained to guarantee downstream hit visibility
- current best-known low-value throttle profile:
  - `layer = 0`
  - `radial_bin = 3`
  - `theta bins = {13,14,15,0}`
  - `period = 2`

Validated baseline metrics from the completed film pass:

- traced pixels: `78,464`
- remaps: `102,957`
- post-remap candidate segments: `4,198,381`
- post-remap queries: `4,198,381`
- post-remap geometry hits: `21,812`
- final write pixels: `21,812`

Validated timing summary:

- pass-1 transport: `9,233.11 ms`
- pass-2 physics: `37,403.40 ms`
- pass-2 envelope: `2,411.47 ms`
- pass-2 candidate eval: `2,833.70 ms`
- pass-2 query dispatch: `4,618.70 ms`
- pass-2 hit resolve: `2.66 ms`

Engineering conclusion:

- pass-2 is the dominant cost
- pass-1 GRIN transport is the second-largest cost
- inside pass-2, query dispatch is the largest identifiable sub-bucket
- hit resolution itself is cheap

Therefore the primary bottleneck is:

- **pass-2 query dispatch**

Safe throttle boundary:

- safe/current: `layer=0`, `radial_bin=3`, `theta={13,14,15,0}`, `period=2`
- unsafe/rejected: same region with `period=3`

Validation versus presentation camera poses:

- `validation_nearfield`
  - backoff: `0.0`
  - role: scientific baseline for full-film validation and invariant enforcement
  - measured result:
    - hit rate: `29.57%`
    - total hits: `21,316`
    - `geom_hits`: `21,316`
    - `final_write_px`: `21,316`
    - proto-caustic invariant: `pass=true`
    - low-value sector budget: `pass=true`

- `presentation_mid`
  - backoff: `5.0`
  - role: communication pose for cleaner scene readability
  - measured result:
    - hit rate: `2.01%`
    - total hits: `1,469`
    - `geom_hits`: `1,469`
    - `final_write_px`: `1,469`
    - proto-caustic invariant: `pass=false`
    - low-value sector budget: `pass=true`

- `presentation_far`
  - backoff: `10.0`
  - role: presentation-only wide stand-off
  - measured result:
    - hit rate: `0.79%`
    - total hits: `561`
    - `geom_hits`: `153`
    - `final_write_px`: `153`
    - proto-caustic invariant: `pass=false`
    - low-value sector budget: `pass=true`

Engineering conclusion:

- the validated wormhole regime is preserved only at the near-field pose
- backing the camera away improves overlay readability and scene-legibility for communication
- larger stand-off distances collapse the proto-caustic structure and should therefore be treated as presentation poses, not validation poses

Reference artifact:

- `output/camera_distance_sweep/preset_summary.txt`

## 11. Future Work

Validated next-step priorities are:

- optimize pass-2 query dispatch without changing remap or GRIN math
- improve downstream spatial hit density near the exit mouth so validation does not depend on sparse geometry
- reduce pass-1 GRIN stepping cost after pass-2 query work is better controlled
- reintroduce pass-2 stride only behind the wormhole validation loop and only if the pipeline invariants remain non-zero

One explicit non-keeper from the validation cycle:

- switching the wormhole scene from `OverlapOnly` to `Both` broadphase was a regression and should not be treated as the current forward path
- increasing the kept low-value throttle from `period=2` to `period=3` was also a regression and should not be treated as the current forward path

Optional characterization follow-up:

- run a fine-grained local sweep around `validation_nearfield`, for example `0.0`, `0.5`, `1.0`, `1.5`, `2.0`, to determine whether the proto-caustic regime ends at a sharp threshold or across a broader near-field plateau

## Current Status

The wormhole prototype now has validated first-light proof for the full render pipeline:

- remap works
- post-remap queries work
- geometry hits work
- film writes work

That validation result is the ground truth baseline for future wormhole optimization and smart-adjust work.
