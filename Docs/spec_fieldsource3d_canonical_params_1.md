# Specification - FieldSource3D Canonical Params + Legacy Compatibility

**Status:** Implemented  
**Key source files:** `FieldSource3D.cs`, `RayBeamRenderer.cs`, `GodotAdapter/SnapshotBuilder.cs`, `FieldProbe3D.cs`

---

## 1) Purpose

Define a single source of truth for authored field parameters in `FieldSource3D`:

- Canonical model: `Shape + Curve + Amp + A/B/C + Radii + ModeFlags`
- Legacy model: retained only for scene compatibility
- Resolver contract: `ResolveEffectiveParams(out reason)` decides effective values

This removes parallel tuning surfaces and prevents silent legacy overrides.

---

## 2) Canonical Inspector Surface

Primary inspector groups:

- `Field Model (Canonical)`: `MetricModel`, `RInner`, `ROuter`, `Amp`, `ModeFlags`, `Softening`, `Sigma`
- `Shape`: `ShapeType`, `BoxExtents`
- `Curve`: `CurveType`, `CurveA`, `CurveB`, `CurveC`

Legacy controls remain serialized and visible under:

- `Legacy (Deprecated)`

with explicit comments: "Deprecated compat. Use Shape/Curve/Amp for new scenes."

---

## 3) Resolver Contract

### `IsCanonicalUnset()`

Canonical is considered unset when all are true:

- `Amp == 0`
- `CurveA == 0`, `CurveB == 0`, `CurveC == 0`
- `RInner == 0`, `ROuter == 0`
- `ModeFlags == 0`
- `ShapeType == SphereRadial`
- `CurveType == Linear`

### `ResolveEffectiveParams(out reason)`

Resolution order:

1. If canonical is not unset: use canonical (`reason = canonical`).
2. If canonical is unset and legacy has meaningful values: map legacy to canonical (`reason = legacy_migrated`).
3. Otherwise: use canonical defaults.

Behavioral guarantees:

- Legacy cannot override canonical when canonical is set.
- Existing legacy scenes still load and become usable via migration path.

---

## 4) Legacy Mapping Rules

When migration is used:

- `Strength -> amp`
- Radii:
  - Prefer `InnerRadius` / `OuterRadius` when outer > 0
  - Else fallback to `MinRadius` / `MaxRadius`
- Profile to canonical curve:
  - `Power -> CurveType.Power`, `a = Gamma` (or 1)
  - `InversePower -> CurveType.Power`, `a = -abs(Gamma)` (or -1)
  - `Gaussian -> CurveType.Exponential`, `a ~= 1/sigma`
  - `Shell -> CurveType.Polynomial` (compat fallback)
- `Attract == false` sets `ModeFlags` bit0 (`INVERT_SIGN`) in resolved output
- `Softening` and `Sigma` are forwarded to resolved params

---

## 5) Warnings, Logs, and Summary

On `_Ready()`:

- If canonical and legacy are both materially set and differ, warn once:
  - `[FieldSource3D][Warn] canonical+legacy both set; using canonical. (legacy ignored)`
- If migration occurs, log once:
  - `[FieldSource3D] migrated legacy params to canonical (reason=...)`
- If `DebugVizEnabled`, emit one-line effective summary:
  - `[FieldSource3D] <path> shape=... curve=... amp=... a=... r=[..] sigma=.. source=canonical|legacy_migrated`

`EffectiveSummary` is exposed for debug tooling and overlays.

---

## 6) Runtime Consumption Contract

All packaging paths consume resolved params:

- `GodotAdapter.SnapshotBuilder` uses `ResolveEffectiveParams(...)`
- `RayBeamRenderer.SnapshotFieldSources` uses `BuildFieldSourceSnap(fs)` from resolved params
- `FieldProbe3D` uses the same snap conversion helper
- `FieldSource3D` debug helpers (`ResolveAcademicRadii`, packed params, local/world influence bounds) use resolver output

This centralizes truth in one resolver and keeps renderer hot loops unchanged in shape.

---

## 7) Backward Compatibility

- Old scenes with legacy serialized fields remain loadable.
- New scenes should author only canonical controls.
- Legacy controls are compatibility-only and should not be used for new tuning.
