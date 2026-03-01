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

---

## 8) Migration Cookbook (Legacy -> Canonical)

Use this when converting old scenes by hand.

### Legacy power field -> canonical power curve

Legacy inputs:

- `Profile=Power`
- `Strength=S`
- `Gamma=G` (if `OverrideGamma=true`)
- `InnerRadius=Ri`, `OuterRadius=Ro`

Canonical equivalent:

- `CurveType=Power`
- `Amp=S`
- `CurveA=G` (or `1` if legacy gamma override was off)
- `CurveB=0`, `CurveC=0`
- `RInner=Ri`, `ROuter=Ro`

### Legacy inverse power -> canonical power with negative exponent

Legacy inputs:

- `Profile=InversePower`
- `Strength=S`
- `Gamma=G`

Canonical equivalent:

- `CurveType=Power`
- `Amp=S`
- `CurveA=-abs(G)`

### Legacy gaussian -> canonical exponential

Legacy inputs:

- `Profile=Gaussian`
- `Strength=S`
- `Sigma=s`

Canonical equivalent:

- `CurveType=Exponential`
- `Amp=S`
- `CurveA=1/s` (compat mapping used by resolver)
- `Sigma=s` (kept for integrated path mapping)

### Legacy repel field -> canonical mode flag

Legacy input:

- `Attract=false`

Canonical equivalent:

- Set `ModeFlags` bit0 (`INVERT_SIGN`)

---

## 9) Equation Reference (What the Runtime Computes)

Canonical runtime evaluation for ray integration and probe sampling is:
- `FieldMath.EvalFieldAccel(...)`
- consumed by `RayBeamRenderer.ComputeAccelerationAtPointSnap(...)`
- mirrored by `FieldSource3D` read-only previews (`EffectiveEquationCore`, `EffectiveEquationIntegrated`)

### 9.1 Canonical Evaluator (`FieldMath.EvalFieldAccel`)

Definitions:

- `r = |p - c|`
- `u = clamp((r - rInner) / max(eps, rOuter - rInner), 0..1)`
- `dir`:
  - if `r <= eps`: `dir = 0`
  - else `dir = normalize(p - c)`
  - then apply sign rule:
    - if `(ModeFlags & ModeFlagInvertSign) == 0`: `dir = -dir`
    - else: keep `dir` positive

Curve/profile `f(u)` (exact implemented `FieldCurveType` behavior):

- `Linear`: `f(u) = 1 - u`
- `Power`: `f(u) = pow(max(0, 1-u), CurveA)` (`CanonicalGamma` aliases `CurveA`)
- `Exponential` (shown as "Gaussian" in UI): `f(u) = exp(-(u / max(eps, Sigma))^2)`
- `Polynomial`: `f(u) = clamp(CurveA + CurveB*u + CurveC*u^2, 0, 1)`
- `CustomCurve`:
  - if `CustomCurve != null`: `f(u) = clamp(CustomCurve.Sample(u), 0, 1)`
  - else fallback to power form: `pow(max(0, 1-u), CurveA)`

Edge ramp (`edge_ramp`):

- `edge = clamp(EdgeSoftness, 0, 1)`
- if `edge <= eps`: `edge_ramp = 1` (disabled)
- else:
  - `rampIn = smoothstep(0, edge, u)`
  - `rampOut = 1 - smoothstep(1-edge, 1, u)`
  - `edge_ramp = clamp(rampIn * rampOut, 0, 1)`

Beta resolution (`beta_eff`):

- let `safe_global = isfinite(globalBeta) ? globalBeta : 0`
- if `overrideBetaScale == false`: `beta_eff = safe_global`
- else if `abs(safe_global) > eps`: `beta_eff = safe_global * betaScale`
- else: `beta_eff = betaScale`

Final canonical magnitude and acceleration:

- `mag = beta_eff * amp * f(u) * edge_ramp`
- `a = dir * mag`

Probe overlay (`FieldProbe3D`) displays these evaluator outputs directly:
- `r = eval.R`
- `u = eval.U`
- `f(u) = eval.ProfileWithEdge` (profile after edge ramp)
- `beta_eff = eval.BetaEff`
- `final_mag = eval.Magnitude`
- `|a| = eval.AccelerationMagnitude`

### 9.2 Post-Evaluator Scaling in Renderer (`RayBeamRenderer`)

`RayBeamRenderer.ComputeAccelerationAtPointSnap(...)` calls the canonical evaluator per source, then applies additional renderer scaling:

- per source contribution: `a_source = eval.Acceleration * BendScale * FieldStrength`
- accumulated: `a_sum = Σ a_source`

So the integrated runtime path is:
- canonical field math in `FieldMath`
- then renderer-only gain (`BendScale * FieldStrength`) in `RayBeamRenderer`

### 9.3 Compatibility Notes

- `Legacy (Deprecated)` inputs still exist for scene compatibility, but are first mapped by `ResolveEffectiveParams(...)` into canonical fields.
- Canonical resolved params are the source of truth for runtime snapshots (`BuildFieldSourceSnap`), probe sampling, and integrated ray acceleration.
- Prior "split" interpretations are superseded in this path: `FieldMath` is the single evaluator for integrated/probe runtime.

---

## 10) Power GRIN vs Geodesic-Like (Gordon) Interpretation

For the current integrated runtime path (`FieldMath` + `RayBeamRenderer` + `FieldProbe3D`):

- Direction/sign is controlled by `ModeFlagInvertSign` only.
- `MetricModel` is not part of `FieldMath.EvalFieldAccel(...)` inputs and is not carried in `FieldSourceSnap`.
- Therefore, setting `MetricModel=GordonMetric` does not by itself flip acceleration direction in this path.

`MetricModel` (`GRIN`, `GordonMetric`) still exists as an enum and may be used by other/older code paths (for example `RendererCore.Fields.FieldSystem`), but that is separate from the canonical integrated evaluator documented above.
