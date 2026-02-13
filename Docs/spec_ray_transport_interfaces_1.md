# Specification — Ray Transport & Portability Interfaces

**Charter section:** §13 Portability Interfaces
**Status:** Planned (no code yet)
**Target location:** `RendererCore/Transport/`

---

## 1) Purpose

Defines the abstract interfaces that decouple ray transport (integration),
metric field evaluation, narrowphase intersection, and camera emission from
their concrete implementations. These interfaces enable tier upgrades (GRIN →
Gordon → GR) without restructuring the pipeline.

---

## 2) Design Principle

All interfaces are consumed by the scheduler's tile tasks. They are:
- Stateless (operate on a snapshot + configuration)
- Allocation-free in hot paths
- Decoupled from Godot types (System.Numerics only)
- Compatible with the `RaySeg[]` universal output contract

---

## 3) IRayTransport

Primary abstraction: advances a ray through the scene and emits segments.

```csharp
public interface IRayTransport
{
    int Integrate(
        Vector3 origin,
        Vector3 direction,
        in SceneSnapshot snapshot,
        in IntegratorConfig config,
        Span<RaySeg> output,
        out TransportResult result);
}
```

```csharp
public struct TransportResult
{
    public int SegmentCount;
    public float TotalPathLength;
    public float MaxCurvature;
    public bool BudgetExhausted;
    public bool EarlyHit;
}
```

**Tier 0 implementation:** Wraps current `BuildRaySegmentsCamera_Pass1` logic.
**Tier 2 implementation:** Geodesic integrator producing `RaySeg[]` via 3-space projection.

---

## 4) IMetricField

Provides metric/field data at a point. Tiers differ in what they return.

```csharp
public interface IMetricField
{
    // Tier 0: 3-vector acceleration
    Vector3 AccelAt(Vector3 pWorld, in SceneSnapshot snapshot);

    // Tier 2 (future): metric tensor and Christoffel symbols
    // void MetricAt(Span<float> gMuNu16, Vector4 xMu);
    // void ChristoffelAt(Span<float> gamma64, Vector4 xMu);
    // void GeodesicRHS(ReadOnlySpan<float> state8, Span<float> dState8);
}
```

**Tier 0 implementation:** Wraps current `FieldSystem.AccelAt`.

---

## 5) IIntegrator (Tiered)

Numerical integration strategy, separate from transport and field.

| Tier | Name | Method | Error Estimate |
|------|------|--------|---------------|
| 0 | Heuristic | Symplectic Euler with curvature-adapted dt | None (implicit) |
| 1 | RK45 | Dormand–Prince embedded pair | Local truncation error |
| 2 | Hamiltonian | Symplectic (Störmer–Verlet or Gauss–Legendre) | Constraint drift |

```csharp
public interface IIntegrator
{
    StepResult Step(
        in RayState state,
        float dt,
        IMetricField field,
        in SceneSnapshot snapshot);
}

public struct StepResult
{
    public RayState NewState;
    public float ErrorEstimate;      // Tier 1+: local truncation error
    public float ConstraintDrift;    // Tier 2: null-condition violation
    public float RecommendedDt;      // adaptive suggestion for next step
}
```

---

## 6) IGeometryQueryProvider

Host-independent narrowphase, replacing Godot physics dependency.

```csharp
public interface IGeometryQueryProvider
{
    HitResult IntersectSegment(Vector3 a, Vector3 b, float radiusBound,
                               in SceneSnapshot snapshot);
    HitResult IntersectRay(Vector3 origin, Vector3 direction, float maxDist,
                           in SceneSnapshot snapshot);
}
```

**Current implementation (implicit):** Godot `DirectSpaceState` calls in Pass-2.
**Target implementation:** BLAS triangle traversal via `RendererCore/Accel/`.

---

## 7) ICameraModelProvider (Planned — Tier 2)

```csharp
public interface ICameraModelProvider
{
    RayState EmitRay(int pixelX, int pixelY, in CameraParams camera);
}
```

- **Tier 0:** Pinhole perspective (current `GrinFilmCamera` logic)
- **Tier 2:** Tetrad-based emission from observer frame in curved spacetime

---

## 8) Adoption Strategy

Interfaces are introduced **non-disruptively:**

1. Define interfaces in `RendererCore/Transport/`
2. Wrap existing concrete implementations as Tier 0 adapters
3. Scheduler's `RenderBand` / future tile task calls through the interface
4. Higher tiers drop in as new implementations without pipeline changes
5. `ResearchModeConfig` selects tier combination at runtime

No existing code needs modification until step 3.
