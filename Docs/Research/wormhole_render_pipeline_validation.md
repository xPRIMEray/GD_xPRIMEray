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

## 6. Performance Baseline

Validated baseline scene configuration:

- static wormhole validation scene
- `OverlapOnly` broadphase path
- validation proof geometry retained to guarantee downstream hit visibility

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

## 7. Future Work

Validated next-step priorities are:

- optimize pass-2 query dispatch without changing remap or GRIN math
- improve downstream spatial hit density near the exit mouth so validation does not depend on sparse geometry
- reduce pass-1 GRIN stepping cost after pass-2 query work is better controlled
- reintroduce pass-2 stride only behind the wormhole validation loop and only if the pipeline invariants remain non-zero

One explicit non-keeper from the validation cycle:

- switching the wormhole scene from `OverlapOnly` to `Both` broadphase was a regression and should not be treated as the current forward path

## Current Status

The wormhole prototype now has validated first-light proof for the full render pipeline:

- remap works
- post-remap queries work
- geometry hits work
- film writes work

That validation result is the ground truth baseline for future wormhole optimization and smart-adjust work.
