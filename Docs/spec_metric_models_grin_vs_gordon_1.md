# Specification — Metric Models (GRIN vs Gordon / Transport Tier Roadmap)

**Charter section:** §7.2 Transport Tier Roadmap, §7.3 Gordon Bridge
**Status:** Tier 0 implemented, Tier 1 partial, Tiers 2–3 planned
**Key source files:** `RendererCore/Fields/FieldModels.cs`, `RendererCore/Fields/FieldSystem.cs`

---

## 1) Purpose

The MetricModel abstraction allows multiple physical interpretations of the
same field parameter set. Both models share the FieldEntity contract (shape,
transform, radii, curve, amplitude, flags) and differ only in how they
produce acceleration from those parameters.

---

## 2) MetricModel Enum (Implemented)

```csharp
public enum MetricModel
{
    GRIN = 0,
    GordonMetric = 1
}
```

---

## 3) GRIN Model — Tier 0 (Implemented)

**Physics:** Optical gradient-index medium. Rays follow Fermat's principle
through a scalar index field n(x).

**Behaviour in code:**
```
dirLocal = normalize(pLocal)   // radial outward
magnitude = amp * f(u)
contribution = dirLocal * magnitude
```

The field pushes rays radially outward (repulsive by default). Attractive
behaviour is achieved via negative `amp` or GordonMetric mode.

---

## 4) Gordon Metric — Tier 1 (Partially Implemented)

**Physics:** The Gordon metric (Gordon 1923) shows that light in a moving
dielectric propagates along null geodesics of an effective spacetime metric.
This provides a bridge between optical-medium and GR formulations.

**Behaviour in code:**
```
dirLocal = -normalize(pLocal)  // radial INWARD (attractive)
magnitude = amp * f(u)
contribution = dirLocal * magnitude
```

The only difference from GRIN is the sign flip on direction. This is a
first-order approximation of the Gordon bridge.

**Full Gordon bridge (planned):** Proper tensor mapping from (n(x), v(x)) →
effective metric g_eff(x), enabling exact correspondence between GRIN
parameters and a spacetime metric.

---

## 5) Transport Tier Roadmap

| Tier | Model | Ray State | Stepping Law | Status |
|------|-------|-----------|-------------|--------|
| 0 | GRIN | 3-vector (pos, vel) → RaySeg | ẍ = AccelAt(x) | **Implemented** |
| 1 | Gordon Metric | 3-vector (same) | ẍ = -AccelAt(x) via sign flip | **Partially implemented** |
| 2 | Full GR Geodesic | 4-vector (xᵘ, kᵘ, λ) → RayState4 | d²xᵘ/dλ² + Γᵘ_αβ dxᵅ/dλ dxᵝ/dλ = 0 | **Planned** |
| 3 | Exotic / Wormhole | RayState4 + chart ID | Tier 2 + coordinate atlas | **Planned** |

**Key architectural invariant:** All tiers produce `RaySeg[]` chains as output.
The downstream broadphase/narrowphase/shading pipeline is transport-agnostic.

---

## 6) Gordon as Bidirectional Bridge

- **Upward (Tier 0 → 2):** GRIN parameters reinterpretable as Gordon effective
  metric → on-ramp to geodesic integration for artist-authored fields.
- **Downward (Tier 2 → 0):** Weak-field/slow-motion GR metrics approximable as
  effective GRIN → interactive preview at reduced fidelity.

---

## 7) Mode Flags (Partially Implemented)

`FieldEntitySOA.Flags` carries `uint` mode flags per entity. Planned flag bits:

| Flag | Effect | Status |
|------|--------|--------|
| INVERT_SIGN | Flip attraction/repulsion | Planned (TODO in code) |
| CLAMP_01 | Clamp f(u) to [0,1] | Implemented (always on) |
| USE_INV_R2 | Apply 1/r² scaling | Planned (TODO in code) |
| USE_INV_R | Apply 1/r scaling | Planned |
| FLAT_INNER | Zero influence for r < rInner | Implicit in current u computation |
| PLATEAU_INNER | Constant influence inside rInner | Planned |

Flags are stored but not consumed by `FieldSystem.AccelAt` in current code.

---

## 8) User-Facing Controls (Unified)

Both models expose identical inspector parameters via `FieldSource3D`:
MetricModel, ShapeType, CurveType, rInner, rOuter, amp, curveA/B/C, ModeFlags.

This ensures unified serialisation, snapshot packing, and the ability to swap
metric models without re-authoring the scene.

---

## 9) Determinism

- Field contributions sum in stable entity ID order
- Within a field, evaluation uses deterministic math paths
- No randomisation in either metric model
