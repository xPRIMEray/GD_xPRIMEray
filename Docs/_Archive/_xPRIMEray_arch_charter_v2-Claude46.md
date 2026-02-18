# xPRIMEray — Curved-Ray Renderer Architecture Charter

**Version:** v2-Claude4.6 · **Date:** 2026-02-13
**Lineage:** Consolidation of v0 (code-grounded baseline), v1 (gravity/academic extensions), v2 (ChatGPT merge)
**Review of:** v3-Claude4.5 (Sonnet consolidation attempt — see §A for critique)

---

## 0) Document Conventions

This charter distinguishes three epistemic states for every claim:

- **Implemented** — the code path exists and executes in the current build.
- **Stubbed** — types or folders exist but contain no substantive logic.
- **Planned** — described in this charter or subordinate specs; no code yet.

Source anchors cite the file where a reader can verify the claim. Where a section
covers planned work, it is labelled explicitly. The charter never claims planned
interfaces as implemented.

---

## 1) Executive Summary

xPRIMEray is a curved-ray film renderer embedded in Godot 4 / C#. It produces
images by integrating rays through spatially varying fields and testing the
resulting piecewise-linear segment chains against scene geometry.

### What Is Real Today

| Subsystem | Status | Key entry point |
|-----------|--------|-----------------|
| Curved-ray segment integration (GRIN + analytic bend) | Implemented | `RayBeamRenderer.BuildRaySegmentsCamera_Pass1` |
| Two-pass film pipeline (parallel Pass-1, main-thread Pass-2) | Implemented | `GrinFilmCamera.RenderStep()` |
| Scene snapshot with SOA field/geometry + packed params | Implemented | `SnapshotBuilder.BuildFromGodotScene` |
| Field TLAS + Geometry TLAS (BVH broadphase) | Implemented | `FieldTLAS`, `GeometryTLAS` |
| Curvature bound grid for envelope radius | Implemented | `CurvatureBoundGrid.BuildAroundCamera` |
| Pass-2 narrowphase via Godot physics | Implemented | `IntersectRay`, `IntersectShape`, `CastMotion` |
| Budget/watchdog/telemetry framework | Implemented | `GrinFilmCamera` + `PerfScope` + `PerfStats` |
| Backend dispatch (Legacy/Core/Compare) | Implemented (Legacy only produces output) | `RenderFrameBackend` |
| GRIN + GordonMetric field models | Implemented (direction-sign flip) | `FieldSystem.AccelAt` |
| Internal BLAS / triangle intersection | Stubbed (`RendererCore/Accel/` empty) | — |
| Task-graph scheduler | Stubbed (`RendererCore/Scheduler/` empty) | — |
| CoreBackend rendering | Stubbed (prints summary only) | `CoreBackend.RenderFrame` |

### What This Engine Is Not (Today)

- Not a fully internal intersection stack — Pass-2 narrowphase is Godot physics.
- Not a general-relativity geodesic integrator — transport is 3-vector field acceleration, not 4-vector geodesic ODE.
- Not a multi-scene wormhole renderer — no chart atlas, no portal compositing.

### Governing Physical Principle

> **Euclidean scene geometry, non-Euclidean light transport.**
>
> All meshes, colliders, and AABBs live in standard ℝ³. Curvature is encoded
> entirely in the ray transport law. The broadphase and narrowphase pipelines
> are metric-agnostic: they receive segment chains and test them against
> Euclidean geometry regardless of what stepping law produced those segments.

This matches the standard pattern in gravitational-optics rendering (James et al. 2015;
Müller & Grave 2009): integrate rays in a curved manifold, intersect against
embedded Euclidean surfaces.

---

## 2) Architecture at a Glance

```
Godot scene tree
  → SnapshotBuilder.BuildFromGodotScene(...)
      → SceneSnapshot { Fields, FieldParams, FieldTLAS, Geometry, GeometryTLAS }
      → CurvatureBoundGrid (camera-centred, added by RenderFrameBackend)
      → FrameSnapshotBus.Set(snapshot, frameId)

GrinFilmCamera._Process → RenderFrameBackend(delta)
  ├─ BackendMode.Legacy:
  │    LegacyBackend.RenderFrame(snapshot)
  │      → GrinFilmCamera.RenderStep()
  │          Pass-1 (Parallel.For per pixel): segment integration
  │          Pass-2 (main thread): broadphase + Godot narrowphase + shading
  │          → Image → ImageTexture → TextureRect / FilmOverlay2D
  │
  ├─ BackendMode.Core:
  │    CoreBackend.RenderFrame(snapshot)   // summary print only
  │    LegacyBackend.RenderFrame(snapshot) // still produces output
  │
  └─ BackendMode.Compare:
       LegacyBackend.RenderFrame(snapshot) // TODO: real compare mode
```

`GrinFilmCamera.RenderStep()` is the **primary frame render trigger**. All
future refactoring must preserve it as the clean boundary between:

- **engine-agnostic transport** (RendererCore-owned), and
- **engine-specific geometry queries** (Godot today, replaceable).

Source anchors: `GrinFilmCamera.cs`, `GodotAdapter/SnapshotBuilder.cs`,
`RenderBackends/LegacyBackend.cs`, `RenderBackends/CoreBackend.cs`.

---

## 3) Core Design Principles

### 3.1 Snapshot Immutability

`SceneSnapshot` is rebuilt every frame via `SnapshotBuilder.BuildFromGodotScene`.
Properties are `init`-set. Consumers treat all arrays as read-only for the frame
lifetime. Immutability is enforced by convention, not deep wrappers.

### 3.2 Data-Oriented Layout

Field and geometry data are stored as struct-of-arrays (`FieldEntitySOA`,
`GeometryEntitySOA`) with a contiguous `PackedParamBuffer` for field parameters.
TLAS node arrays are flat for stack-based traversal.

### 3.3 Determinism and Threading

- Snapshot extraction order is stabilised by `string.CompareOrdinal` on node paths.
- Pass-1 is `Parallel.For` per pixel (disjoint output indices, `Interlocked` counter merges).
- Pass-2 is main-thread (Godot `DirectSpaceState` is not thread-safe).
- `RenderStep` has an `Interlocked` re-entry guard.
- SoftGate random probing uses `_rng.Randf()` — full determinism requires suppressing this.

### 3.4 Euclidean-Geometry / Non-Euclidean-Light Separation

See §1 governing principle. This is not merely a design goal — it is an
architectural invariant. The geometry TLAS, the narrowphase queries, and the
film shading pipeline must never need to know which transport model produced
the segment chain they are consuming.

---

## 4) Module Map

### `RendererCore/`

| Folder | Status | Responsibility |
|--------|--------|----------------|
| `SceneSnapshot/` | Implemented | `SceneSnapshot`, SOA containers, `PackedParamBuffer`, `Aabb3` |
| `Fields/` | Implemented | `FieldSystem.AccelAt`, `FieldModels` enums, `FieldCurves.Eval`, `FieldTLAS`, `CurvatureBoundGrid` |
| `Geometry/` | Implemented | `GeometryTLAS` over world AABBs |
| `Integrators/` | Implemented (minimal) | `StepPolicy.ComputeDt` |
| `Common/` | Implemented | `FrameSnapshotBus`, `DebugOverlayBus`, `DebugLogConfig` |
| `Accel/` | Stubbed (empty) | Planned: BLAS triangle BVH |
| `Scheduler/` | Stubbed (empty) | Planned: task-graph scheduling |
| `Config/` | Planned | `ResearchModeConfig`, `ResearchModeOverrides` |
| `Transport/` | Planned | `IRayTransport`, `ITransportModel` |
| `Relativity/` | Planned | `IMetricField`, metric implementations |
| `CameraModel/` | Planned | Tetrad-based relativistic camera frame |

### `RenderBackends/`

`IRenderBackend` interface, `BackendMode` enum, `LegacyBackend` (drives
`RenderStep`), `CoreBackend` (summary-only), `BackendSelector` (exists, unused).

### `GodotAdapter/`

`SnapshotBuilder` — extracts scene tree into `SceneSnapshot`. Handles field
collection, parameter packing, TLAS builds, geometry AABBs + Godot instance IDs.

### Root-Level Runtime Nodes

| Node | Role |
|------|------|
| `GrinFilmCamera` | Orchestrator: backend dispatch, snapshot publish, Pass-1/Pass-2 pipeline, budgets, telemetry |
| `RayBeamRenderer` | Segment integration, collision helpers, debug ray bundles |
| `FieldSource3D` | Inspector-authored field definition (MetricModel, shape, curve, radii, amplitude) |
| `FieldGrid3D` | Optional cached vector-field grid for Pass-1 acceleration lookup |
| `FieldProbe3D` | Runtime probe of `FieldSystem.AccelAt` with overlay diagnostics |
| `FilmOverlay2D` | Debug overlay: rays, hit normals, bus items |
| `PerfScope` / `PerfStats` | Timing, counters, rolling-window diagnostics |

---

## 5) Data Model

### 5.1 SceneSnapshot

```csharp
public sealed class SceneSnapshot
{
    public InstanceSOA Instances { get; init; }       // mesh/material IDs, transforms (currently unpopulated)
    public FieldEntitySOA Fields { get; init; }       // metric/shape/curve enums, transforms, bounds, param offsets
    public PackedParamBuffer FieldParams { get; init; } // contiguous float blocks: rInner, rOuter, amp, a, b, c, r0, r1
    public FieldTLAS FieldTLAS { get; init; }         // BVH over field AABBs
    public GeometryEntitySOA Geometry { get; init; }  // world AABBs + Godot instance IDs
    public GeometryTLAS GeometryTLAS { get; init; }   // BVH over geometry AABBs
    public CurvatureBoundGrid CurvatureGrid { get; init; } // camera-centred Kmax grid
}
```

**Known limitation:** `SnapshotBuilder` sets `Instances = InstanceSOA.Empty()` —
the instance transform SOA is defined but not populated.

### 5.2 Per-Frame Lifecycle

- **Rebuilt every frame:** snapshot, both TLASes, curvature grid.
- **Reused across frames:** camera-owned pass buffers (`_segBuf`, hit arrays,
  quick-ray caches, perf windows, optional field-grid cache with cadence control).

---

## 6) Coordinate Spaces

- Field evaluation is world-space: `FieldSystem.AccelAt(Vector3 pWorld, ...)`.
- Per-field transforms `WorldFromLocal[]` / `LocalFromWorld[]` convert the query
  point to field-local space for radius/shape evaluation, then transform the
  contribution back to world.
- Geometry is world AABBs.
- Camera forward is `-Basis.Z` (Godot convention).

---

## 7) Field and Metric System

### 7.1 Current Implementation (Tier 0)

Fields are authored via `FieldSource3D` nodes with inspector exports:
`MetricModel`, `FieldShapeType`, `FieldCurveType`, radii, amplitude, flags,
curve coefficients (a, b, c). `SnapshotBuilder` packs these into SOA arrays
and `PackedParamBuffer` (8-float blocks).

`FieldSystem.AccelAt` evaluates all contributing fields at a world point:

1. Query `FieldTLAS` for candidate field indices (or brute-force if no TLAS).
2. For each candidate: bounds check → transform to local → compute radial distance
   → evaluate curve law → apply amplitude → transform contribution to world.
3. `GordonMetric` differs from `GRIN` only by negating the local direction vector.

Shapes: `SphereRadial`, `BoxVolume` (BoxVolume currently falls back to radial).
Curves: `Linear`, `Power`, `Polynomial`, `Exponential` via `FieldCurves.Eval`.

### 7.2 Transport Tier Roadmap

The engine's field/metric system is designed to scale through four physically
distinct tiers without changing the downstream segment-consumption pipeline:

| Tier | Transport Model | Physics | Ray State | Current Status |
|------|----------------|---------|-----------|----------------|
| **0** | GRIN Field | 3-vector acceleration from scalar field n(x) → ẍ = ∇n/n | `RaySeg` (A, B, TraveledB, RadiusBound) | **Implemented** |
| **1** | Gordon Metric | Effective spacetime metric of moving dielectric g_eff(x) maps to equivalent GRIN | Same as Tier 0 (adapter) | **Partially implemented** (direction flip only) |
| **2** | Full GR Null Geodesic | d²xᵘ/dλ² + Γᵘ_αβ dxᵅ/dλ dxᵝ/dλ = 0 | `RayState4`: xᵘ, kᵘ, λ, constraint drift | **Planned** |
| **3** | Exotic / Wormhole | Tier 2 + coordinate atlas + throat crossing | `RayState4` + chart ID | **Planned** |

**Key insight for the tier system:** The segment chain (`RaySeg[]`) is the
**universal output format** of all tiers. Tiers 0–3 differ in how they
*produce* segments; the broadphase, narrowphase, and shading pipeline
consumes segments identically regardless of origin. This is the "PRIME spine"
of the architecture.

### 7.3 Gordon Metric as Adapter (Clarification)

The Gordon metric (Gordon 1923) shows that light propagation in a moving
dielectric medium is equivalent to null propagation in an effective spacetime
metric g_eff. The architecture uses this as a **bidirectional bridge**:

- **Upward (Tier 0 → Tier 2):** A GRIN field can be reinterpreted as a
  Gordon effective metric, giving an on-ramp to geodesic integration for
  scenes authored with simple GRIN parameters.
- **Downward (Tier 2 → Tier 0):** In weak-field / slow-motion limits, a
  full GR metric can be approximated as an effective GRIN for interactive
  preview at reduced fidelity.

The existing `MetricModel.GordonMetric` code (direction-sign flip in
`FieldSystem`) is a first approximation of this bridge. Full tensor
mapping is planned.

---

## 8) Curved Ray Representation and Integration

### 8.1 Segment Struct

```csharp
public struct RaySeg
{
    public Vector3 A;           // segment start (world)
    public Vector3 B;           // segment end (world)
    public float TraveledB;     // cumulative path length at B
    public float RadiusBound;   // conservative curvature envelope radius
}
```

### 8.2 Current Integration (Tier 0)

`RayBeamRenderer.BuildRaySegmentsCamera_Pass1` steps rays through
`StepsPerRay` iterations with two code paths:

- **Integrated field** (`UseIntegratedField = true`): velocity updated by
  `FieldSystem.AccelAt` each step; step size adapted by curvature.
- **Analytic bend:** parametric deflection `β · t^γ · bendScale`.

Adaptive step controls: `StepLength`, `MinStepLength`, `MaxStepLength`,
`StepAdaptGain`, low-curvature boost. `CurvatureBoundGrid` provides per-cell
Kmax for envelope radius computation.

### 8.3 Segment Cadence

Segments are emitted every `CollisionEveryNSteps` integration steps (with
optional screen-space cadence adaptation). Each segment carries `RadiusBound`
for broadphase envelope expansion.

---

## 9) Acceleration Structures and Intersection

### 9.1 TLAS (Implemented)

- `FieldTLAS`: BVH over field entity AABBs; used by `FieldSystem.AccelAt`
  for candidate pruning.
- `GeometryTLAS`: BVH over geometry AABBs; used by Pass-2 for candidate
  pruning before Godot narrowphase.

### 9.2 Narrowphase (Current: Godot Physics)

Pass-2 tests segments against geometry via `DirectSpaceState`:
`IntersectRay`, `IntersectShape`, `CastMotion`, plus helper wrappers
(`SubdividedRayHit`, `SweepSegmentHit`). TLAS-gated: narrowphase hits are
accepted only if the collider ID is in the TLAS candidate set.

### 9.3 BLAS (Planned)

`RendererCore/Accel/` is reserved for an internal triangle BVH. This is the
prerequisite for moving Pass-2 off the main thread and removing the Godot
physics dependency.

---

## 10) Scheduling and Concurrency

### Current Model

- Frame work is partitioned into row bands (`RowsPerFrame`, adaptive sizing).
- Pass-1: `Parallel.For` across pixels in the current band.
- Pass-2: sequential on main thread (Godot API constraint).
- Snapshot is read-only during `RenderStep`.

### Budget and Watchdog System

- `UpdateEveryFrameBudgetMs`, `UpdateEveryFrameMaxRowsPerStep`
- `RenderStepMaxMs`, `RenderStepMaxPixelsPerFrame`, `RenderStepMaxSegmentsPerFrame`
- Multiple guard exits for stuck/no-progress/no-hit/no-candidate bands
- SoftGate budgets with per-pixel and per-frame caps, scoring model, watchdog timeout

### Planned: Task Graph

`RendererCore/Scheduler/` is currently empty. The planned task-graph system
(see `Docs/spec_scheduler_task_graph.md`) would replace the embedded
scheduling logic in `GrinFilmCamera`.

---

## 11) Rendering Backends and Output

**Output chain (Legacy path):**
Film buffer (`Image`) → `ImageTexture` → `TextureRect` (via `FilmViewPath`)
or auto-created overlay. `FilmOverlay2D` optionally draws world ray/hit
overlays and film gradient normals. Shader files exist in the repo but no
C# postprocess stage wires them at runtime.

---

## 12) Telemetry and Debugging

- `PerfScope` / `FramePerf`: per-stage timing and counters.
- `PerfStats`: rolling-window frame summaries and invariant checks.
- `FieldProbe3D`: live `AccelAt` evaluation + overlay diagnostics.
- Geometry prune audit and reject-sampling instrumentation in Pass-2.
- `RayBeamRenderer` debug overlay + `GetDebugRayBundle`.
- `RayViz`, `CurvedCamera`: auxiliary debug/visual tools.

---

## 13) Portability and Academic Upgrade Interfaces (Planned)

The goal is to keep the current Godot integration unchanged while formalising
clean seams for host-independence and academic metric modes.

### 13.1 IRayTransport

Planned interface that advances a ray state (3D or 4D) through a transport
model and emits a `RaySeg[]` chain. Responsible for step-size control,
invariant tracking (GR mode), and error estimation.

```
IRayTransport
  Advance(state, snapshot, maxSteps) → RaySeg[] + diagnostics
```

**Ray state types:**
- `RayState3` — position, direction, parameterisation (Tier 0–1).
- `RayState4` — xᵘ, kᵘ, λ, constraintDrift (Tier 2–3).

`ITransportModel` is the pluggable physics backend:
- GRIN / optical medium (scalar or tensor IOR)
- Metric / geodesic (Christoffel Γ or Hamiltonian RHS)
- Gordon adapter (bridge mode)

### 13.2 IMetricField

```
IMetricField
  Metric(x)        → g_μν(x)   (4×4 symmetric)
  Christoffel(x)   → Γᵘ_αβ(x)  (optional analytic fast path)
  GeodesicRhs(state) → (dx/dλ, dk/dλ)
```

Planned implementations: Minkowski, Schwarzschild, Kerr, Morris–Thorne.
Designed so external contributors can add metrics without touching the
renderer frontend or collision backend.

### 13.3 IIntegrator (Tiered)

| Tier | Method | Error Control | Constraint Handling |
|------|--------|---------------|---------------------|
| 0 | Heuristic adaptive (current `StepPolicy`) | Curvature-based dt heuristic | None |
| 1 | RK45 / Dormand–Prince | Embedded local truncation error estimate | None (error-bounded only) |
| 2 | Hamiltonian / symplectic | Error estimate + step-halving | Null projection: g_μν kᵘ kᵛ = 0 |

**Full integrator inventory** the engine should eventually expose:
- Fixed-step explicit: Euler (debug), Midpoint, RK4
- Adaptive embedded: RKF45, Dormand–Prince DOPRI5
- Symplectic/geometric: Störmer–Verlet, implicit midpoint
- Geodesic-specific: Hamiltonian form with canonical momenta p_μ; first
  integrals (Carter constant, E, L_z for Kerr) to bound drift

### 13.4 IGeometryQueryProvider

Host-independent wrapper for broadphase and narrowphase queries.

- `GodotGeometryQueryProvider` — wraps `DirectSpaceState` (current).
- `BVHGeometryQueryProvider` — internal triangle BLAS (planned).
- `OfflineMeshQueryProvider` — headless batch / regression tests (planned).

### 13.5 ICameraModelProvider

Returns initial ray states in the camera's local frame. Godot provides
pose + projection; RendererCore provides the math for mapping to initial
`RayState3` or `RayState4`. In GR mode, optionally provides tetrad-based
emission and frequency bookkeeping for redshift/Doppler.

### 13.6 Adoption Strategy

Non-disruptive. `GrinFilmCamera.RenderStep()` remains the orchestrator.
A thin `RendererCore.RenderBand(...)` wrapper accepts snapshot, transport,
geometry provider, camera provider, and film buffer — wrapping existing
Pass-1/Pass-2 logic. This lifts concerns into RendererCore incrementally.

---

## 14) Research Mode (Planned)

### 14.1 Purpose

A configuration and validation contract making xPRIMEray suitable for
academic gravitational-optics workflows: explicit tolerances, invariant
tracking, reproducibility controls, comparison against published results.

### 14.2 Config Surface

Portable types in `RendererCore/Config/`:

- `ResearchModeConfig` — master toggle, preset (Off/Validate/PaperMatch/StressTest),
  logging verbosity, determinism mode.
- `ResearchModeOverrides` — strongly typed overrides for Film, Broadphase,
  RayMarch, SoftGate, Budget, Debug groups.

Integration: `GrinFilmCamera.ResolveEffectiveConfig(out EffectiveConfig cfg)`
merges research overrides into the effective config and logs a one-line
banner with a stable hash of the active override set.

### 14.3 Tiers

| Preset | Constraints | Integrator Requirement |
|--------|-------------|----------------------|
| Tier0_Preview | Current defaults; heuristic stepping; performance-first | StepPolicy (existing) |
| Tier1_ErrorBounded | DtMin/DtMax enforced; MaxStepsPerRay capped; per-ray tolerance goal | RK45 (planned) |
| Tier2_InvariantPreserving | Null constraint drift tracked and bounded; optional projection; deterministic scheduling | Hamiltonian + symplectic (planned) |

### 14.4 Determinism Rules

When `DeterministicMode = true`:
- `SoftGate.RandomProbeChance = 0`
- Work processed in fixed band/row order
- No non-deterministic reductions

### 14.5 Validation Harness (Planned)

Reproducible benchmarks runnable in-engine and headless:

- **Flat-space:** straight-line rays match analytic; envelopes conservative.
- **GRIN:** known radial profiles reproduce expected lensing; convergence tests.
- **Schwarzschild:** weak-field deflection matches analytic; photon sphere at r = 3M.
- **Kerr:** selected rays vs. published geodesic datasets (James et al. 2015).
- **Wormhole:** Morris–Thorne throat lensing; chart-transition continuity.
- **Numerical:** step-halving convergence; bounded null-constraint drift;
  constraint projection tests.

---

## 15) Wormhole and Multi-Chart System (Planned)

### 15.1 Physics Basis

Wormhole rendering requires a **non-trivial coordinate atlas** and a **metric
definition**, not non-Euclidean geometry. The simplest academically grounded
choice is the Morris–Thorne static spherically symmetric traversable wormhole
(Morris & Thorne 1988).

Requirements:
1. Wormhole line element (metric definition)
2. Coordinate atlas: chart A (mouth A) / chart B (mouth B)
3. Geodesic integration through the metric; throat crossing maps coordinates
4. Scene content sampling from the appropriate region's snapshot

### 15.2 Planned Interfaces

**IChartMap** — coordinate atlas for throat crossings:
```
WorldToChart(worldPos) → chart-local coordinates
ChartToWorld(chartPos) → world coordinates
IsThroatCrossing(state) → bool + side
MapThroughThroat(state) → RayState4 in destination chart
```

**IRaySampler** — dispatches field/geometry queries to the correct scene
region based on which chart the ray is currently in.

### 15.3 Scene Hierarchy

Wormhole scenes form a **tree** (not a flat list):

```
MasterScene (root) — owns primary camera and film output
  ├─ WormholeMouth_W1 (spherical zone, Morris–Thorne metric)
  │    └─ ChildScene_B (independent SceneSnapshot)
  │         └─ WormholeMouth_W2 (nested)
  │              └─ ChildScene_C (grandchild)
  └─ WormholeMouth_W3
       └─ ChildScene_D
```

Rules: each mouth owned by exactly one parent; child scenes are independent
snapshots; ray traversal is depth-first; cycles prohibited (DAG by
construction, validated on load); child film contributions composited into
master buffer at projected mouth screen area.

### 15.4 Throat Rendering

When a ray reaches the throat zone:
1. `IChartMap.IsThroatCrossing` detects crossing
2. `IChartMap.MapThroughThroat` transforms ray state parent → child chart
3. `IRaySampler` switches to child `SceneSnapshot`
4. Integration continues until hit, escape, or further throat crossing
5. Result composited into parent film buffer

The mouth sphere is not a flat texture portal — it is a full recursive
rendering of the destination scene through the throat's metric transform.

---

## 16) Roadmap

### 16.1 Reality Check

| Previous Claim | Code Reality | Required Action |
|----------------|-------------|-----------------|
| Renderer owns all production intersection | Pass-2 uses Godot physics; TLAS is pruning only | Implement BLAS in `Accel/` |
| Full end-to-end multithreading | Pass-1 parallel; Pass-2 main-thread | Requires BLAS (removes Godot physics dependency) |
| Scheduler/task-graph subsystem | `Scheduler/` is empty | Migrate scheduling from `GrinFilmCamera` |
| Research Mode fully operational | Config types planned, not implemented | Implement `RendererCore/Config/` |
| Metric/geodesic ray transport | Only 3-vector field acceleration exists | Implement `IRayTransport`, `IMetricField` |

### 16.2 Phase 1 — Foundation

- [ ] Internal BLAS triangle intersection → remove Godot as production narrowphase
- [ ] `IRayTransport` / `ITransportModel` interface stubs in RendererCore
- [ ] `RendererCore/Scheduler` task-graph implementation
- [ ] `ResearchModeConfig` / `ResearchModeOverrides` with effective-config merge
- [ ] Real compare mode in backend dispatch

### 16.3 Phase 2 — Academic Metrics

- [ ] RK45 / Dormand–Prince integrator (Tier 1)
- [ ] `IMetricField` interface + Schwarzschild and Kerr implementations
- [ ] Null-constraint projection (`IConstraintProjector`)
- [ ] Tetrad-based camera model for GR emission
- [ ] Validation harness with flat-space + GRIN + Schwarzschild benchmarks

### 16.4 Phase 3 — Exotic Transport

- [ ] Morris–Thorne metric + `IChartMap` implementation
- [ ] `WormholeSceneGraph` tree and `SnapshotBuilder` integration
- [ ] `WormholeMouth3D` Godot node with child-scene loading
- [ ] Ray throat-crossing and chart remap in Pass-1
- [ ] Child-scene film compositing
- [ ] Kerr validation against published datasets (James et al. 2015)

---

## 17) Glossary

| Term | Definition |
|------|-----------|
| **xPRIMEray** | Engine name. `x` = any curved-field transport; PRIME = baseline integration spine producing `RaySeg[]` chains |
| **SceneSnapshot** | Immutable-for-frame container of all scene data passed into rendering stages |
| **SOA** | Struct-of-arrays layout (`FieldEntitySOA`, `GeometryEntitySOA`) |
| **PackedParamBuffer** | Contiguous float buffer storing field parameters in 8-float blocks |
| **TLAS** | Top-level AABB BVH over entities (`FieldTLAS`, `GeometryTLAS`) |
| **BLAS** | Bottom-level triangle BVH (planned, `RendererCore/Accel/`) |
| **RaySeg** | Bounded curved-ray segment: A, B, TraveledB, RadiusBound |
| **RadiusBound** | Conservative envelope radius for a segment, used in broadphase expansion |
| **Pass-1** | Parallel per-pixel segment integration stage |
| **Pass-2** | Main-thread collision + shading stage |
| **SoftGate** | Gated policy for extra subdivided narrowphase checks on uncertain misses |
| **GRIN** | Gradient-index optical medium model (Tier 0). Rays bend through scalar n(x) |
| **GordonMetric** | Effective spacetime metric of a moving dielectric (Gordon 1923). Bridge between GRIN and full GR |
| **IMetricField** | Planned interface: g_μν(x), Γᵘ_αβ(x), geodesic ODE RHS |
| **IRayTransport** | Planned interface: advances ray state under any transport model, emits RaySeg[] |
| **RayState3** | 3D ray state: position + direction + parameter (Tiers 0–1) |
| **RayState4** | 4D ray state: xᵘ, kᵘ, λ, constraint drift (Tiers 2–3) |
| **IChartMap** | Planned interface: coordinate atlas for wormhole throat crossings |
| **Null geodesic** | Photon path in GR satisfying g_μν kᵘ kᵛ = 0 |
| **Christoffel symbols** | Γᵘ_αβ — connection coefficients encoding how the metric varies in space |
| **CurvatureBoundGrid** | Camera-centred 3D grid of Kmax upper bounds for envelope computation |
| **FieldGrid3D** | Optional cached vector-field grid for Pass-1 acceleration lookup |
| **ResearchModeConfig** | Planned portable config for academic/validation constraints |

---

## 18) Reference Specs

- `Docs/architecture_overview.md`
- `Docs/spec_scene_snapshot_data_layout.md`
- `Docs/spec_bvh_acceleration.md`
- `Docs/spec_metric_models_grin_vs_gordon.md`
- `Docs/spec_curved_ray_chunks.md`
- `Docs/spec_scheduler_task_graph.md`
- `Docs/spec_wormhole_scene_graph.md` *(to be authored)*
- `Docs/spec_research_mode.md` *(to be authored)*

---

## 19) Bibliography

- Gordon, W. (1923). "Zur Lichtfortpflanzung nach der Relativitätstheorie."
  *Ann. Phys.* 377(22), 421–456. — Original Gordon effective-medium metric.
- James, O., von Tunzelmann, E., Franklin, P., Thorne, K.S. (2015).
  "Gravitational lensing by spinning black holes in astrophysics, and in the
  movie Interstellar." *Class. Quantum Grav.* 32, 065001. — Primary Kerr
  geodesic visual validation reference.
- Misner, C.W., Thorne, K.S., Wheeler, J.A. (1973). *Gravitation.* —
  Christoffel symbols, geodesic equation, Kerr metric standard form.
- Morris, M.S. & Thorne, K.S. (1988). "Wormholes in spacetime and their use
  for interstellar travel." *Am. J. Phys.* 56(5), 395–412. — Primary
  wormhole metric source.
- Müller, T. & Grave, F. (2009). "Catalogue of spacetimes." — GR renderer
  taxonomy and metric catalogue.
- Dormand, J.R. & Prince, P.J. (1980). "A family of embedded Runge-Kutta
  formulae." *J. Comp. Appl. Math.* 6, 19–26. — DOPRI5 adaptive integrator.

---

## Appendix A) Critique of Prior Versions

### v2 (ChatGPT Merge)

The v2 document is the direct merge of v0 and v1 and it shows. It contains
**duplicated sections** — §4 Module Map appears twice (lines 136–310 and
815–882), §7 Fields appears twice (lines 397–488 and 887–947), §8 Integration
appears twice (lines 490–563 and 952–993), §10 Scheduling appears twice
(lines 606–649 and 998–1031), §12 Telemetry appears twice (lines 682–698
and 1036–1062). These are verbatim repeats separated by `<!-- Merged from v0 -->`
HTML comments. The document also has two separate §15 "Research Mode" sections
(lines 1130 and 1156) with overlapping but non-identical content. This makes
v2 unreliable as a single source of truth.

v2 also appends §14 "Relativistic Optics" and §15 "Research Mode" as
entirely new top-level sections rather than integrating their content into
the existing §7 (Fields/Metrics) and §12 (Validation) where it belongs
conceptually. The result is a document where related information is scattered
across non-adjacent sections.

### v3 (Claude 4.5 / Sonnet)

The Sonnet consolidation (v3_1-CLAUDE) successfully eliminates the duplication
and produces a coherent single document with clean section numbering. Its
structural choices are generally sound. Points of concern:

1. **Wormhole overemphasis relative to implementation distance.** §9 (Wormhole
   System) is the longest section in the document despite being entirely
   planned work with zero code. This skews the document's centre of gravity
   away from the working system. A charter should give proportional weight
   to what exists.

2. **Engine naming section (§2) is premature.** A half-page table defining
   the "x" and "PRIME" semantics reads as branding rather than architecture.
   This information belongs in a sentence or two, not a dedicated section.

3. **Tier numbering inconsistency.** §7.2 assigns Gordon Metric to Tier 1
   and Full GR to Tier 2. §14.2 assigns Tier0_Preview, Tier1_ErrorBounded,
   Tier2_InvariantPreserving. These are two different tier axes (physics
   model vs integrator quality) using the same numbering, which will confuse
   implementers. This document (v2-Claude4.6) disambiguates by keeping
   physics tiers in §7.2 and integrator tiers in §13.3.

4. **"Higher-order transform" language in §9.5** is non-standard terminology
   in GR optics literature. The concept (recursive rendering through a metric
   transform) is valid but the phrasing invites confusion with higher-order
   ODE methods.

5. **Insufficient marking of implemented vs planned.** While better than v2,
   the Sonnet version sometimes blurs the boundary — for example, describing
   `ResearchModeConfig` files as existing at specific paths when they are
   planned, not implemented.

This v2-Claude4.6 document addresses these issues by: giving proportional
weight to implemented systems, cleanly separating physics tiers from
integrator tiers, using the §0 conventions to mark epistemic status, and
deferring wormhole detail to a planned spec document while retaining the
architectural intent in §15.
