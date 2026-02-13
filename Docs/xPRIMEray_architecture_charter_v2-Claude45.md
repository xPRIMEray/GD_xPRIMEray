# xPRIMEray Architecture Charter — Master v3

> **Engine identity:** *xPRIMEray* — the `x` denotes any curved-field modular transport;
> *PRIME* is the baseline integration spine from which every curved-ray specialisation derives.

---

## 1) Executive Summary

xPRIMEray is a **modular curved-ray renderer** embedded in Godot, designed to be
academically solid from Tier 0 interactive GRIN lensing up to Tier 3 exotic-metric
(wormhole, Kerr) gravitational optics.

**Current code state (what is real today):**

- Runtime path: curved rays are generated as bounded segment chains
  (`RayBeamRenderer.RaySeg`) and consumed by a two-pass film pipeline
  (`GrinFilmCamera.RenderStep`).
- Field and geometry broadphase data are snapshotted into
  `RendererCore.SceneSnapshot.SceneSnapshot` and shared via
  `RendererCore.Common.FrameSnapshotBus`.
- Pass-2 collision/narrowphase still uses Godot physics
  (`IntersectRay`, `IntersectShape`, `CastMotion`) with optional `GeometryTLAS`
  candidate pruning.
- Internal triangle-level intersection (BLAS + internal narrowphase) is not wired yet.

**What this engine is:**

- A curved-ray film renderer embedded in Godot.
- A hybrid architecture where `RendererCore` owns snapshot/field/TLAS data structures
  while final collision is still delegated to Godot physics.
- A platform designed to accept any curved-ray transport law — GRIN, Gordon metric,
  full GR null geodesics, or wormhole atlas metrics — through a unified `IRayTransport`
  contract.

**What this engine is not (today):**

- Not yet a fully internal intersection stack (BLAS planned).
- Not yet an end-to-end task-graph scheduler in `RendererCore/Scheduler`.
- Not yet a production "Core" backend replacing the legacy film path.
- Not yet multi-scene wormhole portalling (designed for; not implemented).

**Key principle for correctness:**
> Scene geometry remains **Euclidean**. Curvature lives in the **ray transport law**
> (GRIN effective-medium, Gordon-metric bridge, or metric/geodesic ODE).
> This matches the standard GR rendering pattern: integrate rays in a curved manifold;
> intersect rays against embedded Euclidean geometry surfaces.

---

## 2) Engine Naming and Scope

| Symbol | Meaning |
|--------|---------|
| `x` in xPRIMEray | Any curved-field modular transport — GRIN, Gordon, Schwarzschild, Kerr, wormhole, or future custom metric |
| `PRIME` | The baseline integration spine. Every curved-ray system plugs into PRIME as its ODE/stepping contract |
| Tier 0 | GRIN-field interactive (current default) |
| Tier 1 | Gordon-metric bridge (GRIN↔GR adapter) |
| Tier 2 | Full GR null geodesics (Schwarzschild / Kerr) |
| Tier 3 | Exotic / wormhole metrics with multi-scene portal seaming |

The naming convention is deliberately publication-friendly: `xPRIMEray` reads as
"any-field prime ray" and connects cleanly to standard gravitational optics terminology
(GRIN, geodesic, metric).

---

## 3) Current Architecture at a Glance

```
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

`GrinFilmCamera.RenderStep()` is the primary **frame render trigger point** and must
remain a clean boundary between engine-agnostic transport (RendererCore-owned) and
engine-specific collision/query backends (Godot today, extensible to others).

---

## 4) Core Design Principles

### 4.1 Snapshot Immutability

`SceneSnapshot` properties are `init`-set and rebuilt each frame
(`SnapshotBuilder.BuildFromGodotScene`). Consumers treat arrays as immutable for the
frame lifetime. Immutability is by convention; arrays are not yet deep read-only wrappers.

### 4.2 Data-Oriented Layout

Field/geometry snapshot data are SOA arrays and packed buffers
(`FieldEntitySOA`, `GeometryEntitySOA`, `PackedParamBuffer`). TLAS node arrays are
contiguous (`FieldTLAS.Nodes`, `GeometryTLAS.Nodes`) for stack-based traversal.

### 4.3 Determinism and Threading

Snapshot extraction order is stabilised by sorting node paths (`string.CompareOrdinal`).
Pass-1 is parallel per pixel; Pass-2 remains main-thread due to Godot physics API
requirements. `RenderStep` has a re-entry guard (`Interlocked`). Optional SoftGate
random probing uses `_rng.Randf()`, so deterministic replay requires
`ResearchModeConfig.DeterministicMode = true` to suppress randomness.

### 4.4 Euclidean-Scene / Non-Euclidean-Light Separation

All geometry — meshes, colliders, AABBs — lives in standard Euclidean world space.
Ray bending is entirely the responsibility of the transport model; geometry never needs
to know what metric is active. This ensures GRIN, Gordon, and full GR modes are all
downstream from the same broadphase/narrowphase pipeline.

---

## 5) Module Map

### `RendererCore/*`

- `SceneSnapshot/*` — snapshot container and SOA/pod types.
- `Fields/*` — field enums, field evaluation, field TLAS, curvature bound grid.
- `Geometry/*` — geometry TLAS over world AABBs.
- `Integrators/*` — currently `StepPolicy` (dt helper); planned tiered integrators.
- `Transport/*` (planned) — engine-agnostic ray transport layer (`IRayTransport`).
- `Relativity/*` (planned) — spacetime metric definitions and adapters (`IMetricField`).
- `CameraModel/*` (planned) — relativistic camera / tetrad frame support.
- `Wormhole/*` (planned) — multi-scene portal system (`IChartMap`, `WormholeSceneGraph`).
- `Common/*` — snapshot bus + debug overlay/log toggles.
- `Config/*` — `ResearchModeConfig`, `ResearchModeOverrides`.
- `Accel/` — currently empty (planned BLAS).
- `Scheduler/` — currently empty (planned task-graph).

### `RenderBackends/*`

Backend interface and mode enum. `LegacyBackend` drives `GrinFilmCamera.RenderStep()`.
`CoreBackend` currently logs snapshot summary. `BackendSelector` exists but is not the
active dispatch path.

### `GodotAdapter/*`

Godot scene extraction into `SceneSnapshot`. Field collection, parameter packing,
TLAS builds. Geometry collection as world AABBs + Godot instance IDs.

### Root-level Orchestrators / Runtime Nodes

`GrinFilmCamera` — backend dispatch, frame snapshot publish, film render loop,
pass-1/pass-2 pipeline, budgets/watchdogs, telemetry.
`RayBeamRenderer` — curved segment integration primitives and collision helper APIs.
`FieldSource3D` — authoring/runtime field node definition.
`FieldGrid3D` — pass-1 acceleration cache (vector field grid).
`FieldProbe3D` — runtime probe of snapshot field system + debug overlay output.
`FilmOverlay2D` — 2D overlay renderer for debug rays/hit normals/bus items.
`PerfScope` / `PerfStats` — timing/counter aggregation and log output.

---

## 6) Portability and Academic Upgrade Interfaces

Goal: keep the current Godot integration working unchanged, while formalising clear
seams so the same core can run inside Godot (interactive), in headless CLI regression
tests, and in future host environments.

### 6.1 IRayTransport (planned)

Advances a ray state through a curvature model using a chosen integrator tier.
Responsible for: step size control, invariant tracking (GR mode), and producing a
polyline/segment chain compatible with existing `RaySeg` consumption.

**Ray state types (planned):**

- `RayState3` — position + direction + parameterisation (GRIN / 3D optical form).
- `RayState4` — spacetime 4-position x^μ + wavevector k^μ + affine parameter λ (GR form).

**`ITransportModel` (planned)** is the physics backend plugged into `IRayTransport`:

- GRIN / optical medium backend (scalar or tensor IOR).
- Metric / geodesic backend (Christoffel / Hamiltonian RHS).
- Gordon / effective-medium adapter backend (bridge between GRIN and GR).

### 6.2 IMetricField (planned — Research-grade)

```
IMetricField:
  Metric(x)       -> g_{μν}(x)       (4×4 symmetric)
  Christoffel(x)  -> Γ^μ_{αβ}(x)    (optional fast path)
  GeodesicRhs(state) -> ODE derivatives
```

Implementation targets: Minkowski (flat), Schwarzschild, Kerr, Morris–Thorne wormhole.
This interface is explicitly designed so academics can contribute metric modules without
touching renderer frontends or collision backends.

### 6.3 IIntegrator (planned — Tiered)

```
IIntegrator:
  Step(state, rhs, dt)  -> new state + optional error estimate
  ErrorEstimate(dt)     -> scalar error bound (Tier 1+)
  ConstraintProject()   -> null-constraint enforcement (Tier 2)
```

Tier map: Tier 0 heuristic adaptive (current `StepPolicy`) → Tier 1 RK45/Dormand–Prince
→ Tier 2 symplectic/Hamiltonian + null-constraint projection.

### 6.4 IGeometryQueryProvider (planned)

Wraps narrowphase and broadphase so geometry queries are host-independent.
`GodotGeometryQueryProvider` wraps `DirectSpaceState` calls; future implementations:
`BVHGeometryQueryProvider` (internal BLAS), `OfflineMeshQueryProvider` (headless batch).

### 6.5 ICameraModelProvider (planned)

Returns camera rays in local frame. GR mode optionally provides tetrad-based emission
and frequency (redshift) bookkeeping. Godot provides camera pose; RendererCore provides
math for mapping local camera frame → initial ray state (3D or 4D).

### 6.6 Minimal Adoption Strategy

Keep `GrinFilmCamera.RenderStep()` as orchestrator. Carve out a
`RendererCore.RenderBand(snapshot, IRayTransport, IGeometryQueryProvider, cameraRays, filmBuffer)`
call as a thin wrapper around existing Pass-1/Pass-2 routines. This lifts logic into
RendererCore incrementally without breaking the Godot front-end.

---

## 7) Fields and Metrics System

### 7.1 Field Entity Representation

Authored by `FieldSource3D` with exported `MetricModel`, `FieldShapeType`,
`FieldCurveType`, radii, amplitude, flags, and curve coefficients. Extracted by
`SnapshotBuilder` into SOA arrays and `PackedParamBuffer`.

### 7.2 Metric Model Tiers

| Tier | Model | Academic basis |
|------|-------|----------------|
| Tier 0 | GRIN | Scalar/tensor IOR optical medium (Fermat principle, stationary) |
| Tier 1 | Gordon Metric | Effective spacetime metric from moving optical medium (Gordon 1923); bridge between GRIN and GR |
| Tier 2 | Full GR — Schwarzschild, Kerr | Null geodesics, Christoffel symbols, Hamiltonian formulation |
| Tier 3 | Exotic / Wormhole | Morris–Thorne atlas, throat coordinate mapping, multi-chart portalling |

**Gordon / Effective-Medium Bridge — clarification:**
The Gordon metric is used as an adapter path. In some cases spacetime curvature can be
expressed as an effective optical medium (moving dielectric). The architecture supports
both direct metric/geodesic stepping (exact within numeric tolerance) and optional
mapping to an equivalent GRIN-style transport model (for performance or authoring
convenience). This makes Gordon Metric the natural on-ramp from Tier 0 to Tier 2.

Current code state: `MetricModel.GRIN = 0`, `MetricModel.GordonMetric = 1`.
`FieldSystem` flips local direction sign for GordonMetric vs GRIN. Full tensor
metric is planned.

### 7.3 Shapes and Curves

Current shapes: `SphereRadial`, `BoxVolume` (BoxVolume falls back to radial distance;
TODO). Current curves: `Linear`, `Power`, `Polynomial`, `Exponential`
(`FieldCurves.Eval`).

### 7.4 Curvature Bounds and Grids

`CurvatureBoundGrid.BuildAroundCamera(...)` computes per-cell `Kmax` using candidate
fields via `FieldTLAS.QueryAabb`. `RayBeamRenderer.BuildRaySegmentsCamera_Pass1`
converts local `Kmax` into segment envelope radius bound (`RaySeg.RadiusBound`).

---

## 8) Integrator Tier System

| Tier | Name | Method | When to use |
|------|------|--------|-------------|
| 0 | Preview | Heuristic adaptive stepping (current `StepPolicy`) | Interactive, art, gameplay |
| 1 | Error-Bounded | RK45 / Dormand–Prince embedded error + adaptive dt | Research validation, paper-match |
| 2 | Invariant-Preserving | Hamiltonian null geodesic + symplectic/Verlet + null-constraint projection | Full GR academic claims |

**Null constraint enforcement (Tier 2):**
`g_{μν} k^μ k^ν = 0` must be maintained within bounded tolerance. Strategy options:
renormalisation after each step, explicit projection onto constraint manifold, or
constrained integrator step. The `IConstraintProjector` interface (planned) handles this.

**PhD-grade integrator inventory the engine must expose:**

- Fixed-step explicit: Euler (debug), Midpoint, RK2, RK4.
- Adaptive embedded: RKF45 / Dormand–Prince (error estimate + dt control).
- Symplectic/geometric: Verlet / Störmer–Verlet, implicit midpoint.
- Geodesic-specific: Hamiltonian form with canonical momenta p_μ; first integrals for
  Schwarzschild/Kerr (Carter constant, energy, angular momentum) to reduce drift.

---

## 9) Wormhole System and Multi-Scene Portal Hierarchy

### 9.1 Physics Basis

Wormhole rendering does **not** require non-Euclidean scene meshes. It requires:

1. A wormhole line element (metric definition). The simplest academically grounded
   choice is the **Morris–Thorne static spherically symmetric wormhole**
   (Morris & Thorne 1988, *Am. J. Phys.* 56(5)); this is the standard in GR rendering
   literature including Müller & Grave (2009) and James et al. (2015 — *Interstellar*).
2. A coordinate atlas (chart A / mouth A, chart B / mouth B).
3. Geodesic integration through the metric; when the ray crosses the throat, map its
   coordinates to the other region's chart.
4. Sampling scene geometry/fields from the appropriate region's snapshot.

### 9.2 IChartMap (planned)

```
IChartMap:
  WorldToChart(worldPos)    -> chart-local coordinates
  ChartToWorld(chartPos)    -> world coordinates
  IsThroatCrossing(state)   -> bool + side (A or B)
  MapThroughThroat(state)   -> mapped RayState4 in destination chart
```

A wormhole mouth is a **spherical zone**. When the ray's affine parameter carries it
through the event horizon sphere, `IChartMap.MapThroughThroat` performs the coordinate
transform from chart A into chart B, injecting the ray into the destination world's
snapshot. This is a "higher-order" transform: the intersection sphere itself is rendered
by evaluating the destination scene as seen through the throat.

### 9.3 IRaySampler (planned)

Samples scene content along a ray, including "which region" dispatch rules. When a ray
is in chart A, it queries `SceneSnapshot_A`; after throat crossing it queries
`SceneSnapshot_B`. This is transparent to the broadphase/narrowphase pipeline.

### 9.4 WormholeSceneGraph — Scene Hierarchy

Wormhole scenes form a **tree**, not a flat list. This is required because a wormhole
mouth connects one scene to another, and that destination scene may itself contain
further wormhole mouths connecting to yet more scenes.

```
MasterScene (root)
  ├── SceneSnapshot_A  [owns camera, fields, geometry]
  ├── WormholeMouth_W1 (spherical zone, metric = Morris-Thorne)
  │     └── ChildScene_B  [separate SceneSnapshot]
  │           ├── fields, geometry
  │           └── WormholeMouth_W2 (optional nested mouth)
  │                 └── ChildScene_C  [grandchild SceneSnapshot]
  └── WormholeMouth_W3
        └── ChildScene_D
```

**Rules:**

- The **master scene** is the root; it owns the primary camera and film output.
- Each wormhole mouth is owned by exactly one parent scene.
- Child scenes are independent `SceneSnapshot` instances (own fields, geometry,
  FieldTLAS, GeometryTLAS).
- Ray traversal is depth-first into the tree: a ray that crosses mouth W1 is handed
  to ChildScene_B's snapshot; if it then crosses W2, it is handed to ChildScene_C's
  snapshot, and so on.
- Cycles (a scene being its own ancestor) are prohibited. The tree is a DAG by
  construction, validated on scene load.
- The master scene always renders last (compositing); child scenes produce film
  contributions that are composited into the master film buffer at the mouth's
  projected screen area.

**Data structures (planned):**

```
WormholeSceneGraph:
  SceneNode root          // master scene
  List<WormholeEdge> edges  // (parentScene, childScene, mouthConfig, IChartMap)

SceneNode:
  SceneSnapshot snapshot
  List<WormholeEdge> mouthsOwnedByThisScene

WormholeEdge:
  SceneNode parent
  SceneNode child
  WormholeMouthConfig mouth   // world position, radius, metric params
  IChartMap chartMap
```

**Godot node representation:**

- `WormholeMouth3D` node placed in a parent scene; its `ChildScenePath` property
  names the child scene to load.
- `SnapshotBuilder` walks the tree depth-first, building `WormholeSceneGraph`
  alongside the primary `SceneSnapshot`.
- A mouth with no `ChildScenePath` is an open / vacuum throat (renders background
  or sky of parent scene through distortion only).

### 9.5 Throat Rendering — "Higher-Order Transform"

When a ray reaches the throat zone (spherical event horizon seam), the following
sequence occurs:

1. `IChartMap.IsThroatCrossing` detects the crossing.
2. `IChartMap.MapThroughThroat` transforms ray state from parent chart → child chart.
3. `IRaySampler` switches to the child `SceneSnapshot` for all subsequent field
   evaluations and geometry queries.
4. Integration continues in the child chart until the ray hits geometry, escapes,
   or crosses another throat.
5. The final hit/colour result is composited into the parent film buffer at the
   pixel's projected mouth location.

The word "higher-order" in the design notes refers to this: **the mouth sphere is not
a flat texture portal but a full recursive rendering of the destination scene through
the throat's metric transform**. This matches the treatment in James et al. (2015) and
Müller (2004).

---

## 10) Data Model and Memory Layout

### 10.1 SceneSnapshot Shape

```
SceneSnapshot:
  InstanceSOA Instances
  FieldEntitySOA Fields
  PackedParamBuffer FieldParams
  FieldTLAS FieldTLAS
  GeometryEntitySOA Geometry
  GeometryTLAS GeometryTLAS
  CurvatureBoundGrid CurvatureGrid
  // Planned additions:
  WormholeSOA WormholeMouths   // mouth positions, radii, chart refs
  int SceneId                  // unique ID within WormholeSceneGraph
```

### 10.2 SOA Containers

- `InstanceSOA` — mesh/material IDs, object/world transforms, world bounds.
- `GeometryEntitySOA` — world bounds + `GodotInstanceIds`.
- `FieldEntitySOA` — metric/shape/curve enums, transforms, bounds, param offsets.
- `WormholeSOA` (planned) — mouth world centre, throat radius, chart type enum,
  child scene reference.

### 10.3 Packed Parameters

`PackedParamBuffer.AppendBlock8(...)` stores field params in blocks of 8:
`rInner, rOuter, amp, a, b, c, r0, r1`. Wormhole metric parameters
(throat radius `b0`, shape function coefficients) will use an extended block.

---

## 11) Curved Ray Representation and Integration

### 11.1 Ray State in Code

Current: segment struct `RayBeamRenderer.RaySeg` (`A`, `B`, `TraveledB`,
`RadiusBound`). Hit payload: `RayBeamRenderer.HitPayload`. Pass-1 hit metadata:
`RayBeamRenderer.Pass1HitInfo`.

Planned: `RayState3` (GRIN path), `RayState4` (GR/geodesic path).
`RayState4` carries: `x^μ` (4-position), `k^μ` (wavevector/momentum),
`λ` (affine parameter), `constraintDrift` (null invariant tracker).

### 11.2 Integration Method — Current

Main pass-1 builder: `RayBeamRenderer.BuildRaySegmentsCamera_Pass1(...)`.
Two paths: integrated field (`UseIntegratedField=true`, adaptive step) and analytic
bend (parametric `beta * t^gamma * bendScale`).

### 11.3 Integration Method — Planned Tiered System

Current `StepPolicy` becomes Tier 0. The unified `IIntegrator` contract allows
`GrinFilmCamera.RenderStep()` to accept any tier without code changes to Pass-1/Pass-2.

### 11.4 Segment Cadence and Envelopes

Segment emission cadence uses base `CollisionEveryNSteps` and optional screen-space
cadence adaptation. Envelope radius is computed from curvature grid when available.
`RadiusBound` carries a conservative segment envelope radius for geometry pruning.

---

## 12) Acceleration Structures and Intersection

### 12.1 TLAS in Runtime

`FieldTLAS` — BVH over field AABBs, used for field candidate queries.
`GeometryTLAS` — BVH over geometry AABBs, used as pass-2 candidate pruning.

### 12.2 Intersection API Boundary

Pass-1 generates segments and optional probe hits. Pass-2 performs broadphase +
narrowphase using Godot physics (`IntersectRay`, `IntersectShape`, `CastMotion`).
When geometry TLAS pruning is enabled, narrowphase hits are accepted only if the
collider ID is in TLAS-derived candidate instance IDs.

Internal triangle-level intersection (BLAS) is planned to replace Godot physics as
production narrowphase.

---

## 13) Scheduling and Concurrency

Frame is processed in row bands (`RowsPerFrame` and adaptive row sizing).
Pass-1: `Parallel.For` across pixels in current band.
Pass-2: sequential on main thread (Godot physics API constraint).

Snapshot is read-only during `RenderStep`. Pass-1 writes per-pixel buffers (disjoint)
and merges counters via `Interlocked`. Re-entry guard prevents overlapping invocations.

Watchdogs and budgets: `UpdateEveryFrameBudgetMs`, `UpdateEveryFrameMaxRowsPerStep`,
`RenderStepMaxMs`, `RenderStepMaxPixelsPerFrame`, `RenderStepMaxSegmentsPerFrame`.
SoftGate budgets and watchdog (`Pass2SoftGate*` config set).

`RendererCore/Scheduler` task-graph is planned; currently empty.

---

## 14) Research Mode

Research Mode is a **configuration + validation contract** that makes xPRIMEray
suitable for academic / PhD-level gravitational optics workflows.

### 14.1 Config Types

```
RendererCore/Config/ResearchModeConfig.cs
RendererCore/Config/ResearchModeOverrides.cs
```

In Godot, the camera exposes inspector-facing overrides under **Research Mode**, merged
into `EffectiveConfig.Research` inside `GrinFilmCamera.ResolveEffectiveConfig(...)`.

### 14.2 Research Mode Tiers

| Preset | Goal |
|--------|------|
| Tier0_Preview | Current behavior; heuristic stepping; performance first |
| Tier1_ErrorBounded | Declares per-ray tolerance; enforces dt bounds; RK45 upgrade path |
| Tier2_InvariantPreserving | Invariant tracking + constraint projection; academically comparable |

### 14.3 Determinism Rules

When `DeterministicMode = true`:

- Disable probabilistic probes (`SoftGate.RandomProbeChance = 0`).
- Process work in deterministic order (band/row order).
- Avoid non-deterministic reductions for metrics and validation output.

### 14.4 Validation Harness (planned)

Reproducible benchmark suite runnable in-engine and headless. Required scenarios:

**Flat-space baselines:** straight-line rays match analytic; segment envelopes stable.

**GRIN baselines:** known radial GRIN profiles reproduce expected qualitative lensing;
convergence tests vs tighter tolerances / higher tier integrators.

**Schwarzschild:** weak-field deflection matches analytic approximation; photon sphere
at correct radius (qualitative + numeric checks).

**Kerr (Interstellar tier):** selected rays compared against published Kerr geodesic
references / datasets; regression snapshots for key camera poses and spin parameters.

**Wormhole metrics:** Morris–Thorne throat lensing; chart transition / portal remap
correctness tests (no coordinate singularity artefacts); multi-scene tree traversal
producing correct visual continuity at mouth boundaries.

**Numerical verification:** step-halving convergence; bounded null-constraint drift;
optional constraint projection tests.

---

## 15) Rendering Backends and Output

`LegacyBackend` is the rendering backend that produces film output. `CoreBackend`
currently prints snapshot summary only. `BackendMode.Compare` falls back to legacy path.

Film buffer (`Image`) updated to `ImageTexture` each render step. Output to configured
`TextureRect` (`FilmViewPath`) or auto-created overlay. Optional `FilmOverlay2D` draws
world ray/hit overlays + film gradient normals. `DebugOverlayBus` items (from
`FieldProbe3D`) are consumed by `FilmOverlay2D`. Shader files exist in repo but
postprocess stage is not wired in current C# runtime.

---

## 16) Telemetry, Debugging, and Validation

Current telemetry: `XPrimeRay.Perf.FramePerf` + `PerfScope` stage timing/counters.
`PerfStats` rolling-window frame summaries and invariant checks.

Debugging helpers: `FieldProbe3D` evaluates `FieldSystem.AccelAt`; geometry prune
audit and reject sampling in pass-2; `FieldSource3D` in-game debug shapes;
`RayBeamRenderer` debug overlay + `GetDebugRayBundle`; `RayViz` and `CurvedCamera`.

---

## 17) Roadmap

### 17.1 Charter Reality Check

| Claim | Code reality | Action needed |
|-------|-------------|---------------|
| Renderer owns all production intersection | Pass-2 still uses Godot physics; TLAS is pruning/filtering only | Add internal BLAS triangle path |
| Full end-to-end multithreading | Pass-1 parallel; Pass-2 main-thread | Move Pass-2 off Godot physics |
| Scheduler/task graph subsystem | Scheduler folder empty; logic embedded in GrinFilmCamera | Migrate to RendererCore/Scheduler |
| Multi-scene wormhole portalling | Not implemented; architecture designed | Implement WormholeSceneGraph + IChartMap |

### 17.2 Implemented

- Scene snapshot extraction with field/geometry SOA.
- Field and geometry TLAS builds and queries.
- Curvature bound grid creation around camera.
- Pass-1 curved segment integration with adaptive stepping and optional probes.
- Pass-2 broadphase policies + TLAS-gated Godot narrowphase.
- Budget/watchdog/telemetry framework.
- GRIN and GordonMetric transport modes.
- Research Mode config structure and minimal guardrails (DtMin/DtMax, MaxStepsPerRay,
  deterministic mode flag).

### 17.3 In Progress

- Core backend migration.
- Geometry TLAS pruning quality instrumentation.
- SoftGate policy tuning.

### 17.4 Planned Next (Phase 1 — Foundation)

- Internal BLAS/triangle intersection path; remove Godot as production narrowphase.
- `IRayTransport` / `ITransportModel` / `IMetricField` interface stubs in RendererCore.
- `RendererCore/Scheduler` task graph implementation.
- Real compare mode in backend dispatch.

### 17.5 Planned (Phase 2 — Academic Metrics)

- RK45/Dormand–Prince integrator as Tier 1.
- Schwarzschild and Kerr `IMetricField` implementations.
- Null-constraint projection (`IConstraintProjector`).
- `ResearchModeConfig` full hookup with validation harness.
- Tetrad-based camera model for GR emission.

### 17.6 Planned (Phase 3 — Wormhole and Multi-Scene)

- Morris–Thorne metric + `IChartMap` implementation.
- `WormholeSceneGraph` tree structure and `SnapshotBuilder` integration.
- `WormholeMouth3D` Godot node with `ChildScenePath` export.
- Ray throat-crossing and chart-remap in Pass-1.
- Child-scene film compositing into master film buffer.
- Nested wormhole (grandchild scene) support with cycle-detection guard.

---

## 18) Glossary

| Term | Definition |
|------|-----------|
| `xPRIMEray` | Engine name: `x` = any curved-field transport; `PRIME` = baseline integration spine |
| `SceneSnapshot` | Immutable-for-frame container passed into rendering stages |
| `SOA` | Struct-of-arrays data layout (`FieldEntitySOA`, `GeometryEntitySOA`) |
| `PackedParamBuffer` | Contiguous float buffer for field parameter blocks |
| `TLAS` | Top-level AABB hierarchy over entities (`FieldTLAS`, `GeometryTLAS`) |
| `BLAS` | Lower-level triangle hierarchy; planned, not yet implemented |
| `GRIN` | Gradient-index optical medium metric (Tier 0) |
| `GordonMetric` | Effective spacetime metric of a moving dielectric; bridge between GRIN and full GR (Tier 1) |
| `IMetricField` | Interface supplying g_{μν}(x) and geodesic ODE RHS for any metric |
| `IRayTransport` | Unified interface for advancing a ray state under any transport model |
| `RayState3` | 3D ray state for GRIN/optical transport |
| `RayState4` | 4D spacetime ray state (position x^μ, momentum k^μ, affine λ) for GR transport |
| `IChartMap` | Coordinate atlas interface for wormhole throat crossings |
| `IRaySampler` | Interface for sampling scene content along a ray, including multi-region dispatch |
| `WormholeSceneGraph` | Tree of `SceneNode` objects linked by `WormholeEdge`; root is master scene |
| `WormholeMouth3D` | Godot node representing a wormhole throat zone in world space |
| `RaySeg` | Bounded curved segment used for pass-2 tests and envelopes |
| `RadiusBound` | Conservative segment envelope radius for geometry pruning |
| `Pass-1` | Parallel segment integration stage |
| `Pass-2` | Main-thread collision + shading stage |
| `SoftGate` | Gated policy for extra subdivided checks on uncertain misses |
| `FieldGrid3D` | Optional cached vector field for pass-1 acceleration sampling |
| `CurvatureBoundGrid` | Camera-centered grid of `Kmax` upper bounds |
| `RenderHealth` | Rolling diagnostics for stalls, hit-rate, prune behaviour |
| `ResearchModeConfig` | Portable config for academic/validation constraints |
| `Morris–Thorne` | Standard static spherically symmetric traversable wormhole metric (1988) |
| `Null geodesic` | Path of a photon in GR: g_{μν} k^μ k^ν = 0 |
| `Christoffel symbols` | Γ^μ_{αβ} — connection coefficients encoding metric curvature |
| `Hamiltonian geodesic` | ODE formulation using canonical momenta p_μ; preferred for invariant stability |

---

## 19) Reference Specs

- `Docs/architecture_overview.md`
- `Docs/spec_scene_snapshot_data_layout.md`
- `Docs/spec_bvh_acceleration.md`
- `Docs/spec_metric_models_grin_vs_gordon.md`
- `Docs/spec_curved_ray_chunks.md`
- `Docs/spec_scheduler_task_graph.md`
- `Docs/spec_wormhole_scene_graph.md` *(new — to be authored)*
- `Docs/spec_research_mode.md` *(new — to be authored)*

---

## 20) Academic Bibliography (Key References)

- Morris & Thorne (1988) — "Wormholes in spacetime and their use for interstellar travel"
  *Am. J. Phys.* 56(5). **Primary wormhole metric source.**
- Müller & Grave (2009) — "Catalogue of Spacetime Visualisations."
  Canonical reference for GR renderer taxonomy.
- James, von Tunzelmann, Franklin, Thorne (2015) — "Gravitational lensing by spinning
  black holes in astrophysics, and in the movie Interstellar." *Class. Quantum Grav.* 32.
  **Primary reference for Kerr geodesic visual validation.**
- Doroshenko & Mukerjee (review) — Dormand–Prince RK45 step-control reference.
- Misner, Thorne, Wheeler — *Gravitation* (1973). Christoffel symbols, geodesic equation,
  Kerr metric standard form.
- Gordon (1923) — "Zur Lichtfortpflanzung nach der Relativitätstheorie."
  *Ann. Phys.* 377(22). **Gordon metric original paper.**
