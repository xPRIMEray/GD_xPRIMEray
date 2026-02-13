# Specification — Field System (GRIN Evaluation)

**Charter section:** §7 Field and Metric System
**Status:** Implemented
**Key source files:** `RendererCore/Fields/FieldSystem.cs`, `RendererCore/Fields/FieldCurves.cs`, `RendererCore/Fields/FieldModels.cs`

---

## 1) Purpose

The Field System evaluates the aggregate acceleration vector at any world-space
point, given a SceneSnapshot. This acceleration drives curved-ray integration
in Pass-1.

---

## 2) Public API (Implemented)

```csharp
public static Vector3 AccelAt(Vector3 pWorld, in SceneSnapshot snapshot)
```

Returns the summed acceleration contribution from all influencing fields at
`pWorld`. Called by `RayBeamRenderer` during Pass-1 integration and by
`FieldProbe3D` for diagnostics.

---

## 3) Evaluation Pipeline (Implemented)

For each candidate field entity:

1. **Broadphase:** Query `FieldTLAS.QueryPoint(pWorld)` for candidate indices.
   Falls back to brute-force linear scan if no TLAS.

2. **Bounds check:** `worldBounds[i].Contains(pWorld)` — skip if outside.

3. **Local transform:** `pLocal = Vector3.Transform(pWorld, localFromWorld[i])`

4. **Shape distance:**
   - `SphereRadial`: `r = pLocal.Length()`
   - `BoxVolume`: `r = pLocal.Length()` (TODO: real box distance model)

5. **Parameter unpack:** Read `rInner, rOuter, amp, a, b, c` from `PackedParamBuffer`
   at `paramOffset[i]`.

6. **Range check:** Skip if `rOuter <= 0` or `r > rOuter`.

7. **Normalised coordinate:** `u = Saturate((r - rInner) / max(ε, rOuter - rInner))`

8. **Curve evaluation:** `f = FieldCurves.Eval(curveType, u, a, b, c, clamp01: true)`

9. **Direction + metric model:**
   - `dirLocal = pLocal / r` (radial outward)
   - If `MetricModel.GordonMetric`: negate direction (inward/attractive)

10. **Contribution:** `contributionLocal = dirLocal * (amp * f)`

11. **World transform:** `contributionWorld = Vector3.TransformNormal(contributionLocal, worldFromLocal[i])`

12. **Accumulate:** `total += contributionWorld`

Guard: `r < ε` → skip (avoid division by zero at field origin).

---

## 4) Curve Laws (Implemented)

```csharp
public static float Eval(FieldCurveType type, float u, float a, float b, float c, bool clamp01)
```

Input `u` is clamped to [0,1] before evaluation.

| CurveType | Formula | Notes |
|-----------|---------|-------|
| Linear | `1 - u` | Default falloff |
| Power | `(1 - u)^a` | Adjustable rolloff |
| Polynomial | `a + b*u + c*u²` | General quadratic |
| Exponential | `exp(-a * u)` | Smooth decay |

Output optionally clamped to [0,1] when `clamp01 = true` (always true in
current `AccelAt` path).

Source: `RendererCore/Fields/FieldCurves.cs`

---

## 5) Metric Model Enum (Implemented)

```csharp
public enum MetricModel   { GRIN = 0, GordonMetric = 1 }
public enum FieldShapeType { SphereRadial = 0, BoxVolume = 1 }
public enum FieldCurveType { Linear = 0, Power = 1, Polynomial = 2, Exponential = 3 }
```

Source: `RendererCore/Fields/FieldModels.cs`

---

## 6) Known Limitations

- `BoxVolume` shape falls back to radial distance (TODO in code)
- `Flags` field is present in SOA but not consumed by `AccelAt` (TODO: 1/r² mode, invert, etc.)
- TLAS candidate buffer is `stackalloc int[256]` — max 256 candidate fields per query point
- No tensor/anisotropic IOR support (Tier 0 only)

---

## 7) Determinism

- Candidate order is determined by TLAS traversal (stable given stable build)
- Accumulation is sequential float addition (order-dependent but deterministic)
- No randomisation in field evaluation

---

## 8) Performance

- Zero heap allocation in hot path (`stackalloc` for candidates)
- Single pass over candidates with early-exit on bounds/range
- `ref readonly` node access in TLAS traversal
- `Saturate` and `Clamp01` are branchless-friendly
