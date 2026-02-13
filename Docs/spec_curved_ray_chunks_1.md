# Specification — Curved Ray Segment Integration

**Charter section:** §8 Curved Ray Representation and Integration
**Status:** Implemented (Tier 0 integrator)
**Key source files:** `RayBeamRenderer.cs`, `GrinFilmCamera.cs`

---

## 1) Purpose

Defines how curved rays are represented, integrated, and prepared for
intersection testing. Rays are approximated as piecewise-linear segment
chains with conservative curvature envelopes.

---

## 2) Ray Segment Struct (Implemented)

```csharp
public struct RaySeg
{
    public Vector3 A;           // segment start (world space)
    public Vector3 B;           // segment end (world space)
    public float TraveledB;     // cumulative path length at B
    public float RadiusBound;   // conservative curvature deviation bound
}
```

`RaySeg` is the **universal output format** for all transport tiers. Any
integrator (GRIN, Gordon, geodesic) must produce `RaySeg[]` chains that the
downstream broadphase/narrowphase pipeline can consume unchanged.

Source: `RayBeamRenderer.cs`

---

## 3) Integration Entry Point (Implemented)

```csharp
public int BuildRaySegmentsCamera_Pass1(
    Vector3 origin, Vector3 direction, /* camera ray */
    SceneSnapshot snapshot, CurvatureBoundGrid curvatureGrid,
    RaySeg[] outSegs, int outOffset, int outCapacity,
    out Pass1HitInfo hitInfo, /* early-hit metadata */
    ...)
```

Called per pixel during Pass-1 (`Parallel.For`). Produces a segment chain
into a caller-provided buffer (no heap allocation).

---

## 4) Integration Method (Implemented — Tier 0)

Two code paths controlled by `UseIntegratedField`:

### 4.1 Integrated Field Path (default, `UseIntegratedField = true`)

Symplectic-Euler-like stepping:
```
velocity += AccelAt(position, snapshot) * dt
position += velocity * dt
```

Acceleration from `FieldSystem.AccelAt`. Step size adapted by curvature:
- Base: `StepLength`
- Bounds: `MinStepLength` ≤ dt ≤ `MaxStepLength`
- Adaptation: `StepAdaptGain` scales dt inversely with local curvature
- Low-curvature regions get boosted step sizes

### 4.2 Analytic Bend Path (`UseIntegratedField = false`)

Parametric deflection: `bend = β · t^γ · bendScale`

No field evaluation. Used for artistic control without physics grounding.

---

## 5) Segment Emission Cadence (Implemented)

Segments are emitted every `CollisionEveryNSteps` micro-integration steps.
Optional screen-space cadence adaptation adjusts emission rate based on
projected segment length.

Each emitted segment records:
- `A`, `B`: world-space endpoints
- `TraveledB`: cumulative arc length
- `RadiusBound`: conservative envelope radius from `CurvatureBoundGrid.LookupKmax`

---

## 6) Envelope Construction (Implemented)

The segment envelope for broadphase is:

```
Aabb3.FromSegment(seg.A, seg.B).Expand(seg.RadiusBound)
```

`RadiusBound` is derived from the curvature grid's Kmax at the segment
midpoint, scaled by step size squared and a safety factor. This must
**never underestimate** the true curve deviation — conservative
overestimation is safe.

---

## 7) Termination Conditions (Implemented)

Integration stops when:
- `StepsPerRay` limit reached
- Maximum distance reached
- Early hit detected (Pass-1 probe, if `Pass1DoHitTest` enabled)
- Segment buffer capacity exhausted

---

## 8) Auxiliary Structures (Implemented)

```csharp
public struct HitPayload { /* collision result data */ }
public struct Pass1HitInfo { bool Found; float Distance; Vector3 Position; }
public struct RayMeta { /* per-ray metadata */ }
```

`Pass1HitInfo` enables optional early-exit when a probe hit is found during
integration (controlled by `Pass1ProbeEveryNSegments`, `Pass1ProbeMinTravelDelta`).

---

## 9) Adaptive Step Controls (Implemented)

| Parameter | Purpose | Typical Range |
|-----------|---------|---------------|
| StepLength | Base integration step | 0.01–1.0 |
| MinStepLength | Hard floor | 0.001–0.01 |
| MaxStepLength | Hard ceiling | 0.5–5.0 |
| StepAdaptGain | Curvature → dt scaling | 0.1–2.0 |
| StepsPerRay | Max integration steps | 64–512 |
| CollisionEveryNSteps | Segment emission cadence | 1–8 |

---

## 10) Planned Upgrades

### Tier 1 — Error-Bounded Integration (Planned)

Replace heuristic step adaptation with embedded error estimation:
- RK45 / Dormand–Prince with local truncation error
- Adaptive dt based on error vs tolerance, not curvature heuristic
- Compatible with existing `RaySeg` output format

### Tier 2 — Geodesic Integration (Planned)

4-vector state `RayState4 { xᵘ, kᵘ, λ, constraintDrift }`:
- Hamiltonian formulation with canonical momenta
- Null constraint tracking: g_μν kᵘ kᵛ = 0
- Symplectic or constraint-projecting integrators
- Output: same `RaySeg[]` chain (projected to 3-space)

### Chunk System (Deferred)

`Docs/spec_curved_ray_chunks.md` describes a dedicated chunk type distinct from
`RaySeg`. The current `RaySeg` with `RadiusBound` fulfils the same role. A
separate chunk system is deferred unless profiling shows the need.

---

## 11) Performance

- Per-pixel segment buffers are pre-allocated (camera-owned `_segBuf`)
- No heap allocation during integration
- `FieldSystem.AccelAt` uses `stackalloc` candidates
- Integration is the primary cost in Pass-1 (parallel, scales with core count)

---

## 12) Determinism

Given identical snapshot, camera, and config:
- Integration produces identical segment chains
- Segment emission order is deterministic
- No randomisation in the integration path
- SoftGate randomness (Pass-2) does not affect segment generation
