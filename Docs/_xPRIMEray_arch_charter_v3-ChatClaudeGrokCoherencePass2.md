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
| Wormhole Portals       | Planned     | WormholeFieldSource3D + phase-locked SubViewport spheres (Tier 0).      |
| Wormhole Geodesics     | Planned     | Reduced Ellis ODE integration per hit ray (Tier 1).                     |
| Wormhole Chart Atlases | Planned     | Multi-chart mappings for full GR traversability (Tier 2).               |
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
| 3    | Exotic Metrics    | Planned     | Wormholes: SubViewport (Tier 0) → reduced Ellis ODE (Tier 1) → chart atlases (Tier 2). |

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

### 12.1 Design Philosophy

Wormholes are treated as **topological transition events** in the ray path, not as non-Euclidean deformations of the scene mesh. This preserves the core invariant (Euclidean geometry, curved transport) and keeps the Godot physics engine functional. The wormhole is a special `FieldSource3D`-derived node — `WormholeFieldSource3D` — that the normal ray marcher can ignore unless its collision sphere is hit.

### 12.2 Physical Model: Morris-Thorne / Ellis Metric

The canonical traversable wormhole uses the **Ellis/Morris-Thorne metric** in proper-distance form:

```
ds² = -dt² + dl² + (b₀² + l²)(dθ² + sin²θ dφ²)
```

- `l ∈ (-∞, +∞)`: proper radial coordinate. Negative `l` is the other universe/scene.
- `b₀`: throat radius (inspector-exposed parameter on the node, in world units).
- `r(l) = sqrt(b₀² + l²)`: the areal radius of the shell at depth `l`.
- No event horizon. Geometry is everywhere regular. No photon sphere (unstable circular orbit sits marginally at the throat).

The effective potential for null geodesics:
```
V_eff(l) = L² / r(l)² = L² / (b₀² + l²)
```

Rays with impact parameter `b = L/E < b₀` enter the throat; rays with `b > b₀` are deflected. The Ellis wormhole acts as a **diverging lens** in the rim zone — unlike mass-based lenses which always converge.

Reduced 2D null geodesic ODE (equatorial plane, using angular momentum conservation `L = r(l)² dφ/dλ`):
```
d²l/dλ²  = L² · l / (b₀² + l²)²
dφ/dλ    = L / (b₀² + l²)
```

Reference: Nakajima & Asada, Phys. Rev. D 85:107501 (2012); Thorne et al. arXiv:1502.03809 (Interstellar).

### 12.3 Scene Architecture: Phase-Locked Unit Spheres

A wormhole connects two **spaces** (Godot scenes or sub-trees). Each end is represented by a sphere — a `WormholeFieldSource3D` node — which owns a `CollisionShape3D` (sphere of radius `b₀`). The two nodes are **phase-locked**: their orientations, throat radii, and render state are kept synchronized each frame via a shared `WormholeLink` resource.

```
Scene A (e.g., main.tscn)
└── WormholeFieldSource3D  [mouth_A]
    ├── CollisionShape3D (radius = b₀)
    ├── SubViewport → virtual_cam_A  (renders Scene B from the paired perspective)
    └── MeshInstance3D (portal sphere mesh, samples SubViewport texture)

Scene B (e.g., space_b.tscn, or a sub-tree within the same scene)
└── WormholeFieldSource3D  [mouth_B]
    ├── CollisionShape3D (radius = b₀)
    ├── SubViewport → virtual_cam_B  (renders Scene A from the paired perspective)
    └── MeshInstance3D (portal sphere mesh, samples SubViewport texture)
```

**Phase lock rule (virtual camera transform):**
```
T_virtual_A = T_B · R_flip · T_A⁻¹ · T_main_cam
T_virtual_B = T_A · R_flip · T_B⁻¹ · T_main_cam
```
where `R_flip` is a 180° rotation about the mouth's local normal so the virtual camera faces outward through the paired mouth. Updated in `_process()` before the SubViewports render. Both SubViewports use `UPDATE_ALWAYS`.

**Inspector-exposed parameters** on `WormholeFieldSource3D`:
- `ThroatRadius` (b₀, meters)
- `ThroughScene` (NodePath or Resource ref to Scene B root)
- `PairedMouth` (NodePath to the other `WormholeFieldSource3D`)
- `RenderTier` (Simplified, Research — see §12.5)
- `LensingLutSize` (resolution of the precomputed deflection LUT)
- `ThroughViewFovScale` (FOV correction for the virtual camera)

### 12.4 Ray Tracer Integration: Hit Dispatch

When `xPRIMEray`'s ray marcher performs its broadphase/narrowphase pass and detects a hit against the wormhole sphere's `CollisionShape3D`, a special hit type is returned instead of the standard geometry hit:

```csharp
enum HitType { Geometry, WormholeThroat }

struct WormholeHit
{
    Vector3 HitPointWorld;
    Vector3 EntryDirectionWorld;
    float   ImpactParameter;       // b = |L/E| in world units
    WormholeFieldSource3D Mouth;   // which mouth was hit
}
```

The ray marcher does **not** shade a `WormholeThroat` hit via the normal shading path. Instead it dispatches to the **wormhole sub-renderer** (§12.5), which returns either a color sample or continues the ray in the paired scene. This dispatch is gated: if no `WormholeFieldSource3D` exists in the snapshot, the dispatch code is never reached — zero overhead.

`SceneSnapshot` gains an optional `WormholeEntitySOA` alongside `FieldEntitySOA`. The `SnapshotBuilder` populates it by collecting `WormholeFieldSource3D` nodes, analogous to how it collects `FieldSource3D` nodes.

### 12.5 Render Tiers

#### Tier 0 — Simplified (Real-Time Preview)

- Portal interior: sample the SubViewport render texture with mild UV distortion (cosmetic, not physically accurate).
- Rim lensing: apply a precomputed **1D LUT** (`LensingLut[b/b₀] → deflection_angle`) to compute the angular deflection for rays that miss the throat but pass close. The LUT is built once at scene load from the exact deflection integral (see §12.6).
- No ODE integration per ray at render time.
- Performance cost: SubViewport render (~1× additional scene render). Comparable to a standard Godot mirror/portal.

#### Tier 1 — Reduced Geodesic (Balanced)

- Equatorial-plane reduction: each hit ray's impact geometry is projected onto the plane of symmetry through `b₀`.
- Integrate the 2D reduced ODE `(l, φ)` using adaptive Euler or RK2 inside the existing `StepPolicy` framework (`IRayTransport`).
- Early-exit when `|l|` exceeds a cutoff (`b₀ * ThroughDepthScale`) — ray has escaped to the far field on one side.
- Sign of terminal `l` selects which scene/texture to sample.
- Cost: O(~30–200 steps per wormhole-hit ray). Only rays that hit the wormhole sphere pay this cost.

#### Tier 2 — Full Null Geodesic (Research Grade)

- Full 3+1D Christoffel integration: `(l, θ, φ, dl/dλ, dθ/dλ, dφ/dλ)`.
- Uses the same `IIntegrator` interface (planned RK45 / symplectic) as the main GR integrator tier.
- Null constraint `g_μν k^μ k^ν = 0` enforced at each step (constraint projection).
- Ray state converted to/from `RayState4` (the planned 4D extension of `RaySeg`).
- Deflection angle cross-checked against Nakajima-Asada exact formula for validation in `ResearchMode`.
- Cost: high — intended for offline / validation renders only.

### 12.6 Deflection LUT Construction

The 1D LUT for Tier 0 is built by integrating the exact deflection angle for the Ellis wormhole as a function of normalized impact parameter `u = b₀ / b`:

For `u → 0` (weak field): `α ≈ (π/4) u²`
For `u → 1` (near throat): `α` diverges (captured by table endpoint clamping)

The LUT covers `u ∈ [0, 1)` with configurable resolution (default 256 entries). Values outside the table range (i.e. `b < b₀`, ray enters throat) bypass the LUT entirely — those rays are dispatched through the throat to the paired scene.

```csharp
static float[] BuildDeflectionLut(float b0, int resolution)
{
    // Numerically integrate the deflection integral for each u = b0/b
    // Result: LUT[i] = deflection angle in radians for b = b0 / (i / resolution)
}
```

The LUT is stored in `WormholeEntitySOA` and rebuilt when `ThroatRadius` changes.

### 12.7 Relationship to Existing Field System

`WormholeFieldSource3D` does **not** register in `FieldEntitySOA`. It does not contribute to the `FieldSystem.AccelAt()` accumulation. This is intentional: the wormhole's influence is not a smooth vector field — it is a boundary condition at the throat sphere. The normal ray marcher ignores it entirely. Only the hit-dispatch path (§12.4) activates wormhole logic.

This preserves backward compatibility: scenes without wormhole nodes are completely unaffected.

### 12.8 Multi-Chart Atlas (Forward Architecture)

For future full GR support, the wormhole is modeled as an **atlas**: two charts `(U_A, φ_A)` and `(U_B, φ_B)` with transition map `T_AB` at the throat. The throat sphere is the chart boundary. When the ray's `l` coordinate crosses zero, the chart transition is applied — coordinates transform, the metric tensor is evaluated in the new chart, and integration continues.

Abstractions (planned):
- `IChartMap`: Encodes `T_AB`. For Ellis wormhole: identity in `(θ, φ)`, sign flip on `l`.
- `IRaySampler`: Dispatches integration to the correct chart based on current `l`.
- `WormholeSceneGraph`: Manages the association between charts and Godot scene sub-trees, including portal visibility culling and SubViewport lifecycle.

See: `spec_wormhole_scene_graph.md` (planned).

---

## 13. Roadmap

| Feature | Current | Next |
|--------------------|---------|------------------------------------------------------|
| Internal BLAS      | Partial | Full triangle path.                                  |
| Scheduler Graph    | Partial | Extract to RendererCore.                             |
| Tiered Integrators | Partial | RK45 + symplectic.                                   |
| Research Harness   | Planned | Validation suite.                                    |
| Wormhole Support   | Planned | WormholeFieldSource3D + SubViewport portals (Tier 0) |
| Wormhole Geodesics | Planned | Reduced ODE integration (Tier 1)                     |
| Wormhole Full GR   | Planned | RayState4 + chart atlas (Tier 2)                     |
| WormholeEntitySOA  | Planned | SnapshotBuilder integration                          |

---

## 14. Stability Declaration

This v4 charter aligns implementation with academic vision. Future revisions extend specs, not restructure.

---

## 15. Closing Statement

xPRIMEray bridges real-time rendering and gravitational optics, scaling from GRIN to Penrose-inspired twistors and wormholes. It honors physics giants by modularizing the null geodesic — the path of light itself — while delivering a coherent observer view.