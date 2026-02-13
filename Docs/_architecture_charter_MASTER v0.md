# Curved-Ray Renderer Architecture Master Outline

## 1) Executive Summary

This document is the code-grounded master outline for the curved-ray renderer
in this repository.

- Current state: the runtime path is a Godot-driven renderer where curved rays
  are generated as bounded segment chains (`RayBeamRenderer.RaySeg`) and consumed
  by a two-pass film pipeline (`GrinFilmCamera.RenderStep`).
- Current state: field and geometry broadphase data are snapshotted into
  `RendererCore.SceneSnapshot.SceneSnapshot` and shared via
  `RendererCore.Common.FrameSnapshotBus`.
- Current state: pass-2 collision/narrowphase still uses Godot physics
  (`IntersectRay`, `IntersectShape`, `CastMotion`) with optional
  `GeometryTLAS` candidate pruning.
- Planned / TODO: internal triangle-level intersection (BLAS + internal
  narrowphase) is not wired yet.

What this engine is:

- A curved-ray film renderer embedded in Godot.
- A hybrid architecture where `RendererCore` owns snapshot/field/TLAS data
  structures while final collision is still delegated to Godot physics.

What this engine is not (today):

- Not yet a fully internal intersection stack.
- Not yet an end-to-end task-graph scheduler in `RendererCore/Scheduler`.
- Not yet a production "Core" backend that replaces the legacy film path.

What "curved rays are first-class" means in implementation terms:

- Rays are explicitly represented as segment sequences (`RaySeg`) with:
  `A`, `B`, `TraveledB`, and `RadiusBound`.
- `RadiusBound` is consumed by pass-2 envelope broadphase
  (`Aabb3.FromSegment(...).Expand(...)`).
- Pass-1 integration can be physically-driven (`UseIntegratedField`) with
  adaptive step sizing, not just straight-line + post bend.

Source anchors:

- `GrinFilmCamera.cs`
- `RayBeamRenderer.cs`
- `RendererCore/SceneSnapshot/SceneSnapshot.cs`
- `RendererCore/Common/FrameSnapshotBus.cs`

## 2) Current Architecture at a Glance

```text
Godot scene tree
  -> GodotAdapter.SnapshotBuilder.BuildFromGodotScene(...)
      -> SceneSnapshot { Fields, FieldParams, FieldTLAS, Geometry, GeometryTLAS }
      -> CurvatureBoundGrid (added by GrinFilmCamera.RenderFrameBackend)
      -> FrameSnapshotBus.Set(snapshot, frameId)

GrinFilmCamera backend dispatch (per _Process when UpdateEveryFrame=true)
  -> BackendMode.Legacy:
       LegacyBackend.RenderFrame(snapshot)
         -> GrinFilmCamera.RenderStep()
            Pass 1 (Parallel.For): segment integration + optional pass1 hit probes
            Pass 2 (Main thread): broadphase + Godot physics + shading + writeback
            Upload Image -> ImageTexture -> TextureRect/FilmOverlay2D

  -> BackendMode.Core:
       CoreBackend.RenderFrame(snapshot)   // snapshot summary print
       LegacyBackend.RenderFrame(snapshot) // still renders output

  -> BackendMode.Compare:
       LegacyBackend.RenderFrame(snapshot) // TODO compare mode
```

Current state:

- Main entry is `GrinFilmCamera._Process -> RenderFrameBackend`.
- Snapshot is rebuilt every frame from the live Godot scene.
- Legacy film pipeline is still the output-producing path.

Source anchors:

- `GrinFilmCamera.cs`
- `GodotAdapter/SnapshotBuilder.cs`
- `RenderBackends/LegacyBackend.cs`
- `RenderBackends/CoreBackend.cs`

## 3) Core Design Principles (As Implemented)

### Snapshot Immutability

- Current state: `SceneSnapshot` properties are `init`-set and rebuilt each
  frame (`SnapshotBuilder.BuildFromGodotScene`).
- Current state: consumers read snapshot arrays as immutable for the frame.
- Current state: immutability is by discipline/convention; arrays are not deep
  read-only wrappers.

### Data-Oriented Layout

- Current state: field/geometry snapshot data are SOA arrays and packed buffers
  (`FieldEntitySOA`, `GeometryEntitySOA`, `PackedParamBuffer`).
- Current state: TLAS node arrays are contiguous (`FieldTLAS.Nodes`,
  `GeometryTLAS.Nodes`) for stack-based traversal.

### Determinism and Threading

- Current state: snapshot extraction order is stabilized by sorting node paths
  (`string.CompareOrdinal`).
- Current state: pass-1 is parallel per pixel; pass-2 remains main-thread due
  Godot physics API calls.
- Current state: `RenderStep` has a re-entry guard (`Interlocked`).
- Current state: optional SoftGate random probing uses `_rng.Randf()`, so
  deterministic replay is not guaranteed when randomness is enabled.

Source anchors:

- `GodotAdapter/SnapshotBuilder.cs`
- `RendererCore/SceneSnapshot/*.cs`
- `RendererCore/Fields/FieldTLAS.cs`
- `RendererCore/Geometry/GeometryTLAS.cs`
- `GrinFilmCamera.cs`

## 4) Module Map (Folders and Responsibilities)

### `RendererCore/*`

- `SceneSnapshot/*`: snapshot container and SOA/pod types.
- `Fields/*`: field enums, field evaluation, field TLAS, curvature bound grid.
- `Geometry/*`: geometry TLAS over world AABBs.
- `Integrators/*`: currently only `StepPolicy` (dt helper).
- `Common/*`: snapshot bus + debug overlay/log toggles.
- `Accel/`: currently empty.
- `Scheduler/`: currently empty.

Source anchors:

- `RendererCore/SceneSnapshot/SceneSnapshot.cs`
- `RendererCore/Fields/FieldSystem.cs`
- `RendererCore/Geometry/GeometryTLAS.cs`
- `RendererCore/Integrators/StepPolicy.cs`
- `RendererCore/Common/FrameSnapshotBus.cs`

### `RenderBackends/*`

- Backend interface and mode enum.
- `LegacyBackend`: drives `GrinFilmCamera.RenderStep()`.
- `CoreBackend`: currently logs snapshot summary.
- `BackendSelector`: exists but is not the active dispatch path.

Source anchors:

- `RenderBackends/IRenderBackend.cs`
- `RenderBackends/LegacyBackend.cs`
- `RenderBackends/CoreBackend.cs`
- `RenderBackends/BackendSelector.cs`

### `GodotAdapter/*`

- Godot scene extraction into `SceneSnapshot`.
- Field collection, parameter packing, TLAS builds.
- Geometry collection as world AABBs + Godot instance IDs.

Source anchors:

- `GodotAdapter/SnapshotBuilder.cs`

### Root-level Orchestrators / Runtime Nodes

- `GrinFilmCamera`: backend dispatch, frame snapshot publish, film render loop,
  pass-1/pass-2 pipeline, budgets/watchdogs, telemetry.
- `RayBeamRenderer`: curved segment integration primitives and collision helper
  APIs used by film pass.
- `FieldSource3D`: authoring/runtime field node definition.
- `FieldGrid3D`: pass-1 acceleration cache (vector field grid).
- `FieldProbe3D`: runtime probe of snapshot field system + debug overlay output.
- `FilmOverlay2D`: 2D overlay renderer for debug rays/hit normals/bus items.
- `PerfScope` / `PerfStats`: timing/counter aggregation and log output.

Source anchors:

- `GrinFilmCamera.cs`
- `RayBeamRenderer.cs`
- `FieldSource3D.cs`
- `FieldGrid3D.cs`
- `FieldProbe3D.cs`
- `FilmOverlay2D.cs`
- `PerfScope.cs`
- `PerfStats.cs`

## 5) Data Model and Memory Layout

### `SceneSnapshot` Shape

Current state (`RendererCore.SceneSnapshot.SceneSnapshot`):

- `InstanceSOA Instances`
- `FieldEntitySOA Fields`
- `PackedParamBuffer FieldParams`
- `FieldTLAS FieldTLAS`
- `GeometryEntitySOA Geometry`
- `GeometryTLAS GeometryTLAS`
- `CurvatureBoundGrid CurvatureGrid`

### SOA Containers

Current state:

- `InstanceSOA`: mesh/material IDs, object/world transforms, world bounds.
- `GeometryEntitySOA`: world bounds + `GodotInstanceIds`.
- `FieldEntitySOA`: metric/shape/curve enums, transforms, bounds,
  param offsets/lengths, flags.

### Packed Parameters

Current state:

- `PackedParamBuffer.AppendBlock8(...)` stores field params in blocks of 8:
  `rInner, rOuter, amp, a, b, c, r0, r1`.

### TLAS Layers

Current state:

- `FieldTLAS` built from `FieldEntitySOA.WorldBounds`.
- `GeometryTLAS` built from `GeometryEntitySOA.WorldBounds`.

### Frame Rebuild vs Reuse

Current state:

- Rebuilt per frame: snapshot, field TLAS, geometry TLAS, curvature grid.
- Reused across frames: camera-owned pass buffers (`_segBuf`, hit arrays,
  quick-ray caches, perf windows, optional field grid cache with cadence).

Important present limitation:

- `SnapshotBuilder` currently sets `Instances = InstanceSOA.Empty()`; instance
  transform SOA is defined but not populated in this path.

Source anchors:

- `RendererCore/SceneSnapshot/SceneSnapshot.cs`
- `RendererCore/SceneSnapshot/InstanceSOA.cs`
- `RendererCore/SceneSnapshot/GeometryEntitySOA.cs`
- `RendererCore/SceneSnapshot/FieldEntitySOA.cs`
- `RendererCore/SceneSnapshot/PackedParamBuffer.cs`
- `GodotAdapter/SnapshotBuilder.cs`

## 6) Coordinate Spaces and Transforms

Current state:

- Field evaluation is world-query driven:
  `FieldSystem.AccelAt(Vector3 pWorld, SceneSnapshot snapshot)`.
- Field transforms are explicit per entity:
  `WorldFromLocal[]` and `LocalFromWorld[]`.
- Field radius/shape tests are evaluated in field-local space and converted back
  to world contribution.
- Geometry in snapshot is currently represented in world AABBs.
- Camera convention follows Godot: forward is `-Basis.Z`.

Current state for instance transforms:

- `InstanceSOA.WorldFromObject` and `ObjectFromWorld` are part of model but not
  currently populated by `SnapshotBuilder`.

Source anchors:

- `RendererCore/Fields/FieldSystem.cs`
- `RendererCore/SceneSnapshot/FieldEntitySOA.cs`
- `RendererCore/SceneSnapshot/InstanceSOA.cs`
- `GodotAdapter/SnapshotBuilder.cs`
- `RayBeamRenderer.cs`

## 7) Fields and Metrics System

### Field Entity Representation

Current state:

- Authored by `FieldSource3D` with exported:
  `MetricModel`, `FieldShapeType`, `FieldCurveType`, radii, amplitude, flags,
  and curve coefficients.
- Extracted by `SnapshotBuilder` into SOA arrays and `PackedParamBuffer`.

### Metric Models

Current state (`RendererCore.Fields.MetricModel`):

- `GRIN = 0`
- `GordonMetric = 1`

Current behavior:

- `FieldSystem` flips local direction sign for `GordonMetric` vs `GRIN`.

### Shapes and Curves

Current state:

- Shapes: `SphereRadial`, `BoxVolume`.
- Curves: `Linear`, `Power`, `Polynomial`, `Exponential`.
- `FieldCurves.Eval(...)` handles curve law evaluation.

Current limitations / TODO already in code:

- `FieldSystem`: `BoxVolume` currently falls back to radial distance model.
- `FieldSystem`: flags behavior includes TODO for `1/r^2` mode details.

### Curvature Bounds and Grids

Current state:

- `CurvatureBoundGrid.BuildAroundCamera(...)` computes per-cell `Kmax` using
  candidate fields via `FieldTLAS.QueryAabb`.
- `RayBeamRenderer.BuildRaySegmentsCamera_Pass1` converts local `Kmax` into
  segment envelope radius bound (`RaySeg.RadiusBound`).
- `StepPolicy.ComputeDt` exists and is used by `FieldProbe3D` for probe readouts.

### Field Cache (`FieldGrid3D`)

Current state:

- Optional pass-1 vector field cache (`UseFieldGrid`) rebuilt on cadence or
  field cache refresh; sampled before source fallback.

Source anchors:

- `FieldSource3D.cs`
- `RendererCore/Fields/FieldModels.cs`
- `RendererCore/Fields/FieldCurves.cs`
- `RendererCore/Fields/FieldSystem.cs`
- `RendererCore/Fields/CurvatureBoundGrid.cs`
- `FieldGrid3D.cs`
- `FieldProbe3D.cs`

## 8) Curved Ray Representation and Integration

### Ray State in Code

Current state:

- Segment struct: `RayBeamRenderer.RaySeg` (`A`, `B`, `TraveledB`,
  `RadiusBound`).
- Hit payload: `RayBeamRenderer.HitPayload`.
- Pass-1 hit metadata: `RayBeamRenderer.Pass1HitInfo`.

### Integration Method

Current state:

- Main pass-1 builder: `RayBeamRenderer.BuildRaySegmentsCamera_Pass1(...)`.
- Uses explicit stepping over `StepsPerRay` with two paths:
  - Integrated path (`UseIntegratedField=true`): update velocity by field
    acceleration and step adaptively.
  - Analytic bend path: parametric bend by `beta * t^gamma * bendScale`.
- Adaptive step controls: `StepLength`, `MinStepLength`, `MaxStepLength`,
  `StepAdaptGain`, low-curvature boost controls.

### Segment Cadence and Envelopes

Current state:

- Segment emission cadence uses base `CollisionEveryNSteps` and optional
  screen-space cadence adaptation.
- Envelope radius is computed from curvature grid when available.

Chunking and envelope status:

- Current state: envelope-carrying segments are implemented (`RadiusBound`).
- Planned / TODO: dedicated chunk system from `Docs/spec_curved_ray_chunks.md`
  is not yet materialized as separate runtime chunk types.

Source anchors:

- `RayBeamRenderer.cs`
- `GrinFilmCamera.cs`
- `Docs/spec_curved_ray_chunks.md`

## 9) Acceleration Structures and Intersection

### TLAS in Runtime

Current state:

- `FieldTLAS`: BVH over field AABBs, used for field candidate queries.
- `GeometryTLAS`: BVH over geometry AABBs, used as pass-2 candidate pruning.

### BVH/BLAS Type Inventory

Current state:

- `RendererCore.Fields.BVHNode` in `FieldTLAS`.
- `RendererCore.Geometry.GeometryBVHNode` in `GeometryTLAS`.
- No BLAS triangle BVH implementation in `RendererCore/Accel` yet.

### Intersection API Boundary

Current state:

- Pass-1 generates segments and optional probe hits.
- Pass-2 performs broadphase + narrowphase using Godot physics:
  `IntersectRay`, `IntersectShape`, `CastMotion`, and helper wrappers
  (`SubdividedRayHit`, `SweepSegmentHit`).
- When geometry TLAS pruning is enabled, narrowphase hits are accepted only if
  collider ID is in TLAS-derived candidate instance IDs.

Validation vs production status:

- Current state: Godot physics is still production narrowphase.
- Planned / TODO: internal triangle-level intersection path to replace this.

Source anchors:

- `RendererCore/Fields/FieldTLAS.cs`
- `RendererCore/Geometry/GeometryTLAS.cs`
- `GrinFilmCamera.cs`
- `RayBeamRenderer.cs`
- `RendererCore/SceneSnapshot/GeometryEntitySOA.cs`

## 10) Scheduling and Concurrency

Current state work partition:

- Frame is processed in row bands (`RowsPerFrame` and adaptive row sizing).
- Pass-1: `Parallel.For` across pixels in current band.
- Pass-2: sequential on main thread.

Current thread-safety strategy:

- Snapshot is read-only during `RenderStep`.
- Pass-1 writes per-pixel buffers (disjoint indices) and merges counters via
  `Interlocked`.
- Re-entry guard prevents overlapping `RenderStep` invocations.

Current watchdogs and budgets:

- `UpdateEveryFrameBudgetMs`, `UpdateEveryFrameMaxRowsPerStep`.
- `RenderStepMaxMs`, `RenderStepMaxPixelsPerFrame`,
  `RenderStepMaxSegmentsPerFrame`.
- Multiple guard exits for stuck/no-progress/no-hit/no-candidate bands.
- SoftGate budgets and watchdog (`Pass2SoftGate*` config set).

Planned / TODO:

- `RendererCore/Scheduler` is currently empty; task-graph scheduler doc exists
  but is not the runtime backbone yet.

Source anchors:

- `GrinFilmCamera.cs`
- `RenderBackends/LegacyBackend.cs`
- `RendererCore/Scheduler/`
- `Docs/spec_scheduler_task_graph.md`

## 11) Rendering Backends and Output

Current backend reality:

- `LegacyBackend` is the rendering backend that produces film output.
- `CoreBackend` currently prints snapshot summary only.
- `BackendMode.Core` currently executes both core summary and legacy render.
- `BackendMode.Compare` currently falls back to legacy path.

Current output chain:

- Film buffer (`Image`) updated to `ImageTexture` each render step.
- Output to configured `TextureRect` (`FilmViewPath`) or auto-created overlay.
- Optional `FilmOverlay2D` draws world ray/hit overlays + film gradient normals.
- `DebugOverlayBus` items (for example from `FieldProbe3D`) are consumed by
  `FilmOverlay2D`.

Postprocess/shader note:

- Shader files exist in repo, but this C# pipeline does not currently show a
  dedicated postprocess stage wiring them in runtime code.

Source anchors:

- `RenderBackends/LegacyBackend.cs`
- `RenderBackends/CoreBackend.cs`
- `RenderBackends/BackendMode.cs`
- `GrinFilmCamera.cs`
- `FilmOverlay2D.cs`
- `RendererCore/Common/DebugOverlayBus.cs`

## 12) Telemetry, Debugging, and Validation

Current telemetry:

- `XPrimeRay.Perf.FramePerf` + `PerfScope` stage timing/counters.
- `PerfStats` rolling-window frame summaries and invariant checks.
- Frame/band render-health logging in `GrinFilmCamera`.

Current debugging and validation helpers:

- `FieldProbe3D` evaluates `FieldSystem.AccelAt` against current snapshot bus
  state and draws overlay diagnostics.
- Geometry prune audit and reject sampling instrumentation in pass-2.
- `FieldSource3D` in-game debug shape rendering.
- `RayBeamRenderer` debug overlay + `GetDebugRayBundle` handoff.
- `RayViz` and `CurvedCamera` are auxiliary debug/visual tools.

Source anchors:

- `PerfScope.cs`
- `PerfStats.cs`
- `GrinFilmCamera.cs`
- `FieldProbe3D.cs`
- `FieldSource3D.cs`
- `RayBeamRenderer.cs`
- `RayViz.cs`
- `CurvedCamera.cs`

## 13) Roadmap (Reconciled to Current Code)

### Charter Reality Check

- Previous charter said: renderer owns all production intersection.
- Code shows: pass-2 still uses Godot physics; internal TLAS is currently
  pruning/filtering, not full internal narrowphase.
- Update needed: add internal triangle acceleration + intersection path before
  claiming full ownership.

- Previous charter said: full end-to-end multithreading.
- Code shows: pass-1 is parallel, pass-2 is main-thread.
- Update needed: move pass-2 off Godot physics dependency.

- Previous charter said: scheduler/task graph subsystem.
- Code shows: scheduler folder is empty; scheduling logic is embedded in
  `GrinFilmCamera`.
- Update needed: migrate runtime scheduling to `RendererCore/Scheduler`.

### Implemented

- Scene snapshot extraction (`SnapshotBuilder`) with field/geometry SOA.
- Field and geometry TLAS builds and queries.
- Curvature bound grid creation around camera.
- Pass-1 curved segment integration with adaptive stepping and optional probes.
- Pass-2 broadphase policies + TLAS-gated Godot narrowphase.
- Budget/watchdog/telemetry framework in the film renderer.

### In Progress

- Core backend migration (`CoreBackend` exists but is summary-only).
- Geometry TLAS pruning quality instrumentation (audit/fn/fp/reject samples).
- SoftGate policy tuning for quick-ray-miss recovery.

### Planned Next

- Internal BLAS/triangle intersection path and removal of Godot as production
  narrowphase.
- `RendererCore/Scheduler` task graph implementation.
- Real compare mode in backend dispatch.
- Explicit runtime chunk system if needed beyond `RaySeg` envelope usage.

Related specialized docs:

- `Docs/architecture_overview.md`
- `Docs/spec_scene_snapshot_data_layout.md`
- `Docs/spec_bvh_acceleration.md`
- `Docs/spec_metric_models_grin_vs_gordon.md`
- `Docs/spec_curved_ray_chunks.md`
- `Docs/spec_scheduler_task_graph.md`

## 14) Glossary

- `SceneSnapshot`: immutable-for-frame container passed into rendering stages.
- `SOA`: struct-of-arrays data layout (`FieldEntitySOA`, `GeometryEntitySOA`).
- `PackedParamBuffer`: contiguous float buffer for field parameter blocks.
- `TLAS`: top-level AABB hierarchy over entities (`FieldTLAS`, `GeometryTLAS`).
- `BLAS`: lower-level triangle hierarchy; planned, not implemented in code.
- `GRIN`: metric model (`MetricModel.GRIN`) for optical gradient-index behavior.
- `GordonMetric`: metric model variant currently implemented as direction-sign
  inversion in field contribution logic.
- `RaySeg`: bounded curved segment used for pass-2 tests and envelopes.
- `RadiusBound`: conservative segment envelope radius used for geometry pruning.
- `Pass-1`: parallel segment integration stage.
- `Pass-2`: main-thread collision + shading stage.
- `SoftGate`: gated policy for extra subdivided checks on uncertain misses.
- `FieldGrid3D`: optional cached vector field for pass-1 acceleration sampling.
- `CurvatureBoundGrid`: camera-centered grid of `Kmax` upper bounds.
- `RenderHealth`: rolling diagnostics for stalls, hit-rate, prune behavior.
