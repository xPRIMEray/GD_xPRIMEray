# xPRIMEray Architecture Charter

**Version:** v4-FinalCoherence · **Date:** 2026-02-13  
**Lineage:** Synthesis of all provided drafts (v0–v3 lineages from ChatGPT, Claude, and coherence passes). This is the authoritative master charter, reconciling code-grounded reality with forward-looking academic modularity for gravitational optics and exotic ray transport.  
**Perspective:** As if reviewed by Roger Penrose — emphasizing modular null geodesics in curved spacetimes, twistor-friendly representations where apt, and a clean separation between observer tetrad (camera frame) and manifold embedding. The design honors the equivalence principle: Euclidean scene embedding with non-Euclidean null curves, ensuring the renderer can simulate any effective metric without deforming the underlying geometry. This positions xPRIMEray as a bridge from interactive GRIN rendering to Penrose-style conformal compactifications and wormhole traversability studies.

---

## 0. Document Status and Conventions

This charter merges all prior drafts into a single, non-duplicative document. It prioritizes:

- **Implemented** features (verifiable in code).
- **Partial** features (scaffolds exist, incomplete).
- **Planned** features (architectural intent, no code yet).

Claims are anchored to source files where possible. Detailed mechanics defer to subordinate specs (e.g., `/Docs/spec_*.md`). This is a high-level system shape, not a low-level algorithm guide.

Epistemic markers:
- **Implemented** — Exists and executes.
- **Partial** — Stubbed or incomplete.
- **Planned** — Intent only.

No dynamic mutations outside defined contracts. All tiers preserve the core invariant: **Euclidean geometry, curved transport**.

---

## 1. Executive Summary

xPRIMEray is a modular curved-ray film renderer embedded in Godot 4, designed to integrate rays through arbitrary spacetime metrics while intersecting against standard Euclidean scene geometry. It separates the observer's extrinsic perspective (camera tetrad and film sensor) from the intrinsic ray transport, enabling plug-and-play curvature models — from GRIN optics to full general-relativistic null geodesics and exotic wormholes.

### Key Properties
- **Modularity:** Ray curvature is abstracted via `IRayTransport` and `IMetricField`, allowing seamless swaps between transport laws (e.g., GRIN fields, Gordon metrics, Schwarzschild geodesics) without rewriting collision or shading pipelines.
- **Academic Fidelity:** Tiers scale from interactive previews (heuristic stepping) to PhD-grade validation (error-bounded integrators with invariant preservation), supporting reproducibility for gravitational optics research.
- **Output:** A single-observer film image, simulating "as-seen" through a camera sensor in the chosen metric, with optional relativistic effects (e.g., Doppler shifts, aberration).

### Current Reality vs. Vision
| Aspect                 | Status      | Notes                                                                   |
|------------------------|-------------|-------------------------------------------------------------------------|
| GRIN Optical Transport | Implemented | Vector fields bend rays via local acceleration.                         |
| Gordon Metric Bridge   | Partial     | Direction-sign inversion in field logic; full effective metric pending. |
| Full Null Geodesics    | Planned     | Metric + Christoffel integration for Schwarzschild/Kerr.                |
| Wormhole Atlases       | Planned     | Multi-chart mappings for traversable throats.                           |
| Internal Intersection  | Partial     | Godot physics for narrowphase; internal BLAS stubbed.                   |
| Task Scheduling        | Partial     | Parallel Pass-1; main-thread Pass-2; full graph planned.                |

The system guarantees compatibility with existing engines (e.g., Godot physics) while providing a portable core for headless validation or future backends.

Source anchors: `GrinFilmCamera.cs`, `RayBeamRenderer.cs`, `RendererCore/SceneSnapshot/SceneSnapshot.cs`.

---

## 2. High-Level Architecture

```
Godot Scene (FieldSource3D, Geometry, Camera)
  ↓ (Extraction)
SnapshotBuilder.BuildFromGodotScene()
  ↓ (Immutable Data)
SceneSnapshot (FieldSOA, GeometrySOA, TLAS, CurvatureGrid)
  ↓ (Publish)
FrameSnapshotBus.Publish
  ↓ (Render)
GrinFilmCamera.RenderStep()
  - Pass 1: Parallel ray integration (IRayTransport.Advance)
  - Pass 2: Broadphase prune → Narrowphase hit (Godot/Internal)
  - Shading & Film Output (Observer Tetrad Projection)
```

**Core Invariant:** Transport is host-agnostic; geometry queries are adapter-based. Rays are piecewise segments, enabling broadphase envelopes independent of the curvature model.

Source anchors: `GodotAdapter/SnapshotBuilder.cs`, `RenderBackends/LegacyBackend.cs`.

---

## 3. Core Design Principles

### 3.1 Euclidean Geometry, Non-Euclidean Transport
Meshes remain in ℝ³. Curvature is confined to the integrator, ensuring:
- Compatibility with physics engines.
- Portable backends.
- Separation of concerns (à la Penrose: embed surfaces in the manifold without deforming them).

### 3.2 EffectiveConfig Contract
`ResolveEffectiveConfig()` freezes runtime parameters for deterministic replay and academic overrides. Primary surface for tuning tiers, tolerances, and metrics.

### 3.3 Host-Agnostic Core
`RendererCore` has no Godot dependencies. Adapters handle extraction, queries, and display. Future: headless mode for validation suites.

### 3.4 Modular Ray Curvature
Plug-in models via `IRayTransport`:
- Input: Initial ray state (position, direction in observer tetrad).
- Output: Segment chain with bounds.
- Examples: GRIN (vector accel), Gordon (effective g_eff), GR (geodesic ODE).

This modularity allows "processor scaling" — low-tier for real-time, high-tier for accuracy.

### 3.5 Observer-Centric Rendering
All output is from a single extrinsic observer frame (camera tetrad), projecting null curves onto a film sensor. Planned: relativistic effects like aberration via tetrad basis.

---

## 4. Module Map

| Module         | Status      | Role                                    |
|----------------|-------------|-----------------------------------------|
| SceneSnapshot  | Implemented | Immutable frame data (SOA layouts).     |
| Fields         | Implemented | Evaluation, TLAS, bound grids.          |
| Geometry       | Implemented | TLAS over AABBs.                        |
| Integrators    | Partial     | Step policies; planned RK45/symplectic. |
| Transport      | Planned     | Abstraction for curvature models.       |
| Acceleration   | Partial     | Broadphase; BLAS stubbed.               |
| Scheduler      | Partial     | Frame execution; task graph planned.    |
| Config         | Partial     | Research overrides.                     |
| Adapters       | Implemented | Godot extraction/collision.             |
| RenderBackends | Partial     | Legacy output; Core stubbed.            |

See: `spec_scene_snapshot_data_layout.md`, `spec_bvh_acceleration.md`.

---

## 5. SceneSnapshot Data Model

```csharp
sealed class SceneSnapshot
{
    InstanceSOA Instances;
    FieldEntitySOA Fields;
    PackedParamBuffer FieldParams;
    FieldTLAS FieldTLAS;
    GeometryEntitySOA Geometry;
    GeometryTLAS GeometryTLAS;
    CurvatureBoundGrid CurvatureGrid;
}
```

- Immutable per frame.
- Cache-friendly SOA.
- Portable across hosts.

Source: `RendererCore/SceneSnapshot/SceneSnapshot.cs`.

---

## 6. Ray Representation

Core primitive:
```csharp
struct RaySeg
{
    Vector3 A, B;
    float TraveledB;
    float RadiusBound;  // For broadphase envelopes
}
```

Planned extension: `RayState4` for 4D GR states (x^μ, k^μ, λ), with optional twistor reps for Kerr stability.

---

## 7. Transport Fidelity Tiers

### Physics Model Tiers
| Tier | Model             | Status      | Description                                     |
|------|-------------------|-------------|-------------------------------------------------|
| 0    | GRIN Optics       | Implemented | Vector curvature fields (local accel).          |
| 1    | Gordon Metric     | Partial     | Effective optical analogs (n(x), v(x) → g_eff). |
| 2    | Full GR Geodesics | Planned     | Metric + Christoffel; null constraint.          |
| 3    | Exotic Metrics    | Planned     | Wormholes via chart atlases.                    |

### Integrator Quality Tiers
| Tier | Focus | Status | Methods |
|--------------------------|-------------|-------------|-------------------------------------|
| 0 (Preview)              | Performance | Implemented | Heuristic/fixed-step (Euler).       |
| 1 (Error-Bounded)        | Accuracy    | Planned     | Adaptive RK45 with tolerances.      |
| 2 (Invariant-Preserving) | Validation  | Planned     | Symplectic + constraint projection. |

Interfaces: `IRayTransport`, `IIntegrator`, `IMetricField`.  
See: `spec_metric_models_grin_vs_gordon.md`.

---

## 8. Rendering Pipeline

- **Pass 1:** Parallel integration → segment chains.
- **Pass 2:** TLAS prune → narrowphase → shading.
- **Output:** Film accumulation; debug overlays.

Curvature-adaptive stepping; envelopes for pruning.  
Source: `GrinFilmCamera.RenderStep()`.

---

## 9. Scheduling and Concurrency

- Current: Parallel Pass-1; main-thread Pass-2.
- Guards: Budgets, watchdogs, re-entry protection.
- Planned: Task-graph scheduler for decoupled execution.

See: `spec_scheduler_task_graph.md`.

---

## 10. Telemetry and Debugging

- Timing: `PerfScope`.
- Stats: Segment counts, prune analytics.
- Helpers: `FieldProbe3D`, ray overlays.

Designed for performance tuning and physics validation.

---

## 11. Research Mode System

### Configuration
`ResearchModeConfig` + `Overrides`:
- Toggles: Enabled, Preset (Validate/PaperMatch).
- Rules: Determinism (seeded RNG, fixed dt).
- Overrides: Tolerances, logging.

### Behaviors
- Clamp steps (DtMin/Max, MaxStepsPerRay).
- Disable stochastics for replays.
- Validation: Subset truth passes, ray dumps.

Integration: Merge into `EffectiveConfig`.  
See: `spec_research_mode.md` (planned).

---

## 12. Wormholes and Multi-Chart Transport

Treated as atlas mappings, not non-Euclidean meshes.  
Abstractions:
- `IChartMap`: Coordinate remaps.
- `IRaySampler`: Region dispatch.
- Planned: `WormholeSceneGraph` for nested scenes.

See: `spec_wormhole_scene_graph.md` (planned).

---

## 13. Roadmap

| Feature | Current | Next |
|--------------------|---------|--------------------------|
| Internal BLAS      | Partial | Full triangle path.      |
| Scheduler Graph    | Partial | Extract to RendererCore. |
| Tiered Integrators | Partial | RK45 + symplectic.       |
| Research Harness   | Planned | Validation suite.        |
| Wormhole Support   | Planned | Chart layer.             |

---

## 14. Stability Declaration

This v4 charter aligns implementation with academic vision. Future revisions extend specs, not restructure.

---

## 15. Closing Statement

xPRIMEray bridges real-time rendering and gravitational optics, scaling from GRIN to Penrose-inspired twistors and wormholes. It honors physics giants by modularizing the null geodesic — the path of light itself — while delivering a coherent observer view.