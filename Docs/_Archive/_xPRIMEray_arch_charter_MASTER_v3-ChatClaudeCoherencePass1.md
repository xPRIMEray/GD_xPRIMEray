# xPRIMEray Architecture Charter
## v3-ChatClaudeCoherencePass1

A unified master architecture charter merging implementation reality and forward academic architecture for the xPRIMEray curved-ray renderer.

---

## 0. Document Status and Conventions

This document is the coherence merge of prior architecture drafts:

- v0–v2 Chat lineage (implementation-anchored)
- v2 Claude lineage (architecture-clean, research-forward)

Status markers:

- **Implemented** — present in current codebase
- **Partial** — scaffold exists, incomplete behavior
- **Planned** — architectural intent only

Detailed mechanics live in `/Docs/spec_*.md`. This charter defines system shape, not low-level algorithms.

---

## 1. Executive Summary

xPRIMEray is a curved-ray film renderer embedded in Godot 4 that separates:

Euclidean scene geometry  
from  
non-Euclidean ray transport

Rays are integrated as piecewise segment chains and intersected against standard scene geometry through a host-agnostic architecture.

The system is designed to scale from:

- GRIN optical curvature
- effective metric optics
- full relativistic geodesics
- exotic spacetime mappings (wormholes)

without rewriting downstream collision or shading pipelines.

---

## 2. High-Level Architecture

```
Godot Scene
(FieldSource3D, Geometry, Camera)
    ↓
SnapshotBuilder.BuildFromGodotScene()
    ↓
SceneSnapshot
(FieldSOA, GeometrySOA, TLAS, CurvatureGrid)
    ↓
FrameSnapshotBus.Publish
    ↓
GrinFilmCamera.RenderStep()

Pass 1: Curved ray integration
Pass 2: Broadphase prune → narrowphase hit
Shading & film output
```

Key property:

Transport is engine-agnostic. Geometry queries are adapter-based.

---

## 3. Core Design Principles

### 3.1 Euclidean Geometry, Curved Transport

Scene meshes remain standard R^3 geometry.

Curvature exists only in the ray integrator.

This guarantees:

- compatibility with existing physics engines
- portable geometry backends
- clean separation of concerns

---

### 3.2 EffectiveConfig Contract

`ResolveEffectiveConfig()` produces a frozen configuration snapshot consumed by `RenderStep()`.

This is the primary control surface for:

- runtime tuning
- research overrides
- deterministic replay
- academic validation

No dynamic mutation outside this contract.

---

### 3.3 Host-Agnostic Core

RendererCore is designed to run without Godot dependencies.

Godot acts as an adapter layer:

- scene extraction
- geometry queries
- final display

Future backends may replace Godot physics.

---

## 4. Module Map

### RendererCore

| Module | Status | Role |
|--------|--------|------|
| SceneSnapshot | Implemented | Immutable per-frame data |
| Fields | Implemented | Field evaluation and TLAS |
| Geometry | Implemented | Geometry TLAS |
| Integrators | Partial | Ray stepping logic |
| Config | Partial | ResearchMode system |
| Transport | Planned | Transport abstraction layer |
| Acceleration | Partial | Broadphase structures |
| Scheduler | Partial | Frame execution model |

### Adapters

| Module | Status | Role |
|--------|--------|------|
| GodotAdapter | Implemented | Scene extraction and collision |
| RenderBackends | Partial | Film output pipeline |

See also:

- `spec_scene_snapshot_data_layout.md`
- `spec_bvh_acceleration.md`

---

## 5. SceneSnapshot Data Model

```csharp
public sealed class SceneSnapshot
{
    public InstanceSOA Instances;
    public FieldEntitySOA Fields;
    public PackedParamBuffer FieldParams;
    public FieldTLAS FieldTLAS;
    public GeometryEntitySOA Geometry;
    public GeometryTLAS GeometryTLAS;
    public CurvatureBoundGrid CurvatureGrid;
}
```

Properties:

- immutable per frame
- cache-friendly structure-of-arrays
- portable across engines

---

## 6. Transport Fidelity Tiers

### Tier 0 — GRIN Optical Transport (Implemented)

Vector curvature fields bend rays through local acceleration.

### Tier 1 — Effective Metric Transport (Partial)

Gordon metric style optical analogs.

### Tier 2 — Full Null Geodesics (Planned)

Metric plus Christoffel integration.

### Tier 3 — Exotic Metrics (Planned)

Wormholes and spacetime atlases.

Transport interface design:

```
IRayTransport
IIntegrator
IMetricField
```

See: `spec_metric_models_grin_vs_gordon.md`

---

## 7. Ray Representation

Core primitive:

```csharp
struct RaySeg
{
    Vector3 A;
    Vector3 B;
    float TraveledB;
    float RadiusBound;
}
```

Segment envelopes drive broadphase pruning.

Adaptive integration is curvature-aware.

---

## 8. Rendering Pipeline

### Pass 1 — Integration

- parallel segment generation
- curvature-adaptive stepping

### Pass 2 — Intersection

- TLAS pruning
- host narrowphase
- hit filtering

### Shading

- film accumulation
- debug overlays

See: `spec_curved_ray_chunks.md`

---

## 9. Scheduling and Concurrency

Current model:

- Pass 1: parallel CPU integration
- Pass 2: main-thread geometry queries

Guard rails:

- frame budgets
- watchdog timers
- re-entry protection

Future direction:

- task graph scheduler
- decoupled execution model

See: `spec_scheduler_task_graph.md`

---

## 10. Telemetry and Debugging

Instrumentation includes:

- `PerfScope` timing
- segment statistics
- field visualization
- pruning analytics

Designed for:

- tuning performance
- validating physics
- research reproducibility

---

## 11. Research Mode System

### Current Implementation

- `ResearchModeConfig`
- `ResearchModeOverrides`
- merge into EffectiveConfig
- deterministic seed enforcement
- raymarch clamp guards

### Planned Extensions

- error-bounded integrators
- invariant preservation
- validation harness
- benchmark scenes

See: `spec_research_mode.md` (planned)

---

## 12. Wormholes and Multi-Chart Transport

Wormholes are treated as spacetime atlas mapping problems, not non-Euclidean meshes.

Required abstractions:

- chart mapping
- region dispatch
- throat recursion

Interfaces:

```
IChartMap
IRaySampler
```

See: `spec_wormhole_scene_graph.md` (planned)

---

## 13. Roadmap Truth Table

| Feature | Current State | Next Step |
|---------|---------------|----------|
| Internal BLAS | Uses Godot physics | Implement core BLAS |
| Scheduler graph | Partial | Extract scheduler |
| Tiered integrators | Partial | RK45 and symplectic |
| Research harness | Planned | Add validation suite |
| Wormhole support | Planned | Chart mapping layer |

---

## 14. Spec Document Integration

This charter delegates deep mechanics to:

- `spec_scene_snapshot_data_layout.md`
- `spec_bvh_acceleration.md`
- `spec_metric_models_grin_vs_gordon.md`
- `spec_scheduler_task_graph.md`
- `spec_curved_ray_chunks.md`
- `spec_research_mode.md` (future)
- `spec_wormhole_scene_graph.md` (future)

These specs are authoritative implementation references.

---

## 15. Stability Declaration

v3-ChatClaudeCoherencePass1 represents:

- removal of duplicated architecture sections
- alignment of implementation and research goals
- stable naming and module boundaries
- forward-compatible expansion path

Future revisions should extend specs rather than restructure this charter.

---

## 16. Closing Statement

xPRIMEray is positioned as:

a bridge between real-time rendering and academic gravitational optics.

The architecture supports incremental advancement from practical GRIN rendering toward full spacetime simulation without breaking the engine core.
