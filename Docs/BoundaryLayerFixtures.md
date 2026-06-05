# BoundaryLayerVolume Fixture Specification

Minimal deterministic fixtures for validating boundary crossing behavior, crossing policies, and nested shell ordering in the `BoundaryLayerVolume` interaction domain.

---

## Validation Entry Point

Start with:

- [`fixture_boundary_shell_entry_basic.tscn`](#fixture-1-fixture_boundary_shell_entry_basic) — simplest possible CrossingEvent baseline; confirms one entry event per ray, no exit event, clear visual displacement.

Then progress to:

- [`fixture_boundary_shell_policy_compare.tscn`](#fixture-2-fixture_boundary_shell_policy_compare) — side-by-side EntryOnly / ExitOnly / EntryAndExit; confirms policy routing and relative displacement magnitude.
- [`fixture_boundary_nested_shell_modes.tscn`](#fixture-3-fixture_boundary_nested_shell_modes) — two concentric shells with different policies and bias directions; confirms bitmask tracking, snap index ordering, and correct suppression of inner exit events.

Run them in this order. If the entry-basic fixture does not produce the expected single-event log, do not advance to the others — the crossing dispatch path has a regression that will produce misleading results in the later fixtures.

---

## Background

`BoundaryLayerVolume` is a third interaction domain distinct from `FieldSource3D` (continuous curvature) and geometry hits (discrete surfaces). It supports two execution modes:

- **Continuous** — behavior applied every integration step the ray is inside the volume.
- **CrossingEvent** — behavior applied once at the moment the ray crosses the volume boundary.

Crossing direction is controlled by `BoundaryCrossingPolicy`: `EntryOnly` (default), `ExitOnly`, or `EntryAndExit`. Detection uses a per-ray `uint` bitmask (`blvInsideMask`) initialized from the ray origin so that start-inside rays do not synthesize false entry events.

All three fixtures in this family use no `FieldSource3D` and `BendScale = 0`. Rays travel in straight lines; all visual effects are produced exclusively by boundary layer crossing events.

---

## Common Setup

| Parameter | Value | Rationale |
|---|---|---|
| `BendScale` | `0.0` | No field sources; isolates boundary layer effect |
| `UseIntegratedField` | `true` (default) | Required for boundary layer processing path |
| `BiasDirection` | `Vector3.Up` (unless noted) | Produces vertical shift, easy to measure in film |
| `DebugLogCrossings` | `true` | Confirms correct dispatch count per ray |
| Camera | Outside all shells | Prevents start-inside false entries |
| Background screen | `Z = -15` | 28 × 18 flat receiver; shows displacement pattern |

---

## Fixture 1 — `fixture_boundary_shell_entry_basic`

**Path:** `Fixtures/fixture_boundary_shell_entry_basic.tscn`

**Purpose:** Minimal CrossingEvent + EntryOnly baseline. Single sphere shell. Verifies that exactly one entry event fires per crossing ray and no exit event fires.

### Scene composition

| Node | Type | Position | Notes |
|---|---|---|---|
| `Camera3D` | Camera3D | `(0, 0, 7)` | fov=55, current=true |
| `background_screen` | StaticBody3D | `(0, 0, -15)` | 28×18 backdrop, raytrace_geometry |
| `dot_c0` / `dot_c1` / `dot_c2` | StaticBody3D | `(±3, 0, -14)` | Reference dots; show displacement magnitude |
| `BoundaryShell` | Node3D + BoundaryLayerVolume | origin | CrossingEvent, EntryOnly, Sphere |
| `RayBeamRenderer` | Node3D + RayBeamRenderer | — | BendScale=0, StepsPerRay=250 |

### BoundaryLayerVolume parameters

| Property | Value |
|---|---|
| `ExecutionMode` | `CrossingEvent` (1) |
| `CrossingPolicy` | `EntryOnly` (0, default — omitted in tscn) |
| `Radius` | `3.5` |
| `BiasDirection` | `Vector3.Up` (default — omitted) |
| `BiasStrength` | `0.15` |
| `DebugLogCrossings` | `true` |

### Expected behavior

A single entry nudge of `normalize((0,0,-1) + Up×0.15)` produces approximately **8.5° upward deflection**. Rays aimed at the shell interior hit the backdrop approximately **2.7 units above** the undeflected position (at backdrop depth of 15 + 7 = 22 units total from camera).

- Dots behind the shell appear **shifted downward** in the film (the ray that "should" have reached a dot was deflected upward and now hits above it).
- Rays outside the projected shell disc are undistorted; a sharp discontinuity marks the rim.
- `DebugLogCrossings` produces exactly **one `[BLV] entry event`** per crossing ray, zero exit events.

### Edge cases

- Camera at `Z=7` is outside the `Radius=3.5` shell. No start-inside condition. ✓
- Rays that graze the shell edge may produce noisy crossing behavior at the rim; this is expected.

---

## Fixture 2 — `fixture_boundary_shell_policy_compare`

**Path:** `Fixtures/fixture_boundary_shell_policy_compare.tscn`

**Purpose:** Side-by-side visual comparison of all three `BoundaryCrossingPolicy` values using identical shells. Policy is the only variable.

### Scene composition

| Node | Position | `CrossingPolicy` | Snap index |
|---|---|---|---|
| `BoundaryShell_EntryOnly` | `(-4, 0, 0)` | `EntryOnly` (0) | 0 |
| `BoundaryShell_EntryAndExit` | `(0, 0, 0)` | `EntryAndExit` (2) | 1 |
| `BoundaryShell_ExitOnly` | `(4, 0, 0)` | `ExitOnly` (1) | 2 |
| Reference dots | `(±4, 0, -14)` and `(0, 0, -14)` | — | One per column |
| `Camera3D` | `(0, 0, 8)` | — | fov=68; all three shells in frame |

All three shells: `Radius=2.5`, `BiasDirection=Up`, `BiasStrength=0.15`, `DebugLogCrossings=true`.

Camera at `Z=8`, shells at `X=±4`: maximum off-axis angle ≈26.6° < 34° (= fov/2). All shells visible. ✓

### Expected visual and log behavior

| Column | Events fired | Displacement at backdrop | Debug log per ray |
|---|---|---|---|
| Left (`EntryOnly`) | Entry only | ≈ 2.7 units down in film | 1× `entry event`, layer=0 |
| Centre (`EntryAndExit`) | Entry + exit | ≈ 5.4 units down in film (double) | 1× `entry event` + 1× `exit event`, layer=1 |
| Right (`ExitOnly`) | Exit only | ≈ 2.7 units down in film | 1× `exit event`, layer=2 |

The left and right columns produce equal-magnitude displacement but the kink position differs: left-column rays kink at the **near** side of the sphere (toward the camera); right-column rays kink at the **far** side. This produces a subtle difference in rim sharpness.

### Edge cases

- Camera at `Z=8` is outside all three shells (`Radius=2.5` at `Z=0`). No start-inside condition on any column. ✓
- Snap index ordering follows scene tree order: left=0, centre=1, right=2. This is deterministic as long as node order in the tscn is preserved.

---

## Fixture 3 — `fixture_boundary_nested_shell_modes`

**Path:** `Fixtures/fixture_boundary_nested_shell_modes.tscn`

**Purpose:** Two concentric shells with different policies and bias directions. Validates per-layer bitmask tracking, snap index ordering, and that inner/outer layers accumulate independently.

### Scene composition

| Node | `Radius` | `CrossingPolicy` | `BiasDirection` | Snap index |
|---|---|---|---|---|
| `BoundaryShell_Outer` | `4.0` | `EntryAndExit` (2) | `Up` (default) | **0** — listed first |
| `BoundaryShell_Inner` | `2.0` | `EntryOnly` (0, default) | `Right` `(1,0,0)` | **1** — listed second |

Both shells: `BiasStrength=0.12`, `DebugLogCrossings=true`. One central reference dot at `(0, 0, -14)`. Camera at `(0, 0, 8)`, fov=55.

The outer shell must appear **before** the inner shell in the `.tscn` node list to guarantee deterministic snap indices. This ordering is documented in the scene file.

### Crossing sequence (on-axis ray, straight through both shells)

| Step | Event | Bit | Policy | Dispatch |
|---|---|---|---|---|
| A | Enter outer (r crosses 4.0) | bit 0 set | EntryAndExit | ✓ Up bias |
| B | Enter inner (r crosses 2.0) | bit 1 set | EntryOnly | ✓ Right bias |
| C | Exit inner (r crosses 2.0 outward) | bit 1 clear | EntryOnly | ✗ (entry-only, suppressed) |
| D | Exit outer (r crosses 4.0 outward) | bit 0 clear | EntryAndExit | ✓ Up bias |

### Expected debug log (one on-axis ray)

```
[BLV] entry event: layer=0 name='BoundaryShell_Outer'  behavior=DirectionBias  pos=(0.00, 0.00,  3.99)
[BLV] entry event: layer=1 name='BoundaryShell_Inner'  behavior=DirectionBias  pos=(0.00, 0.00,  1.99)
[BLV] exit  event: layer=0 name='BoundaryShell_Outer'  behavior=DirectionBias  pos=(~0,   ~0,   -4.00)
```

Exactly **three** log lines per on-axis ray. Four would indicate incorrect inner-exit dispatch. Two would indicate a missing outer-exit dispatch.

> **Note:** All expected log sequences above assume a single on-axis ray travelling straight through both shells. Off-axis rays may produce different crossing orders or miss the inner shell entirely, depending on closest-approach geometry. Do not treat off-axis log output as a regression without first confirming the ray's actual path.

The on-axis net deflection is Up+Right+Up ≈ compound diagonal. The central dot appears shifted down-left in the film.

### Edge cases

- Camera at `Z=8` is outside the outer shell (`Radius=4.0`). No start-inside condition. ✓
- Rays clipping only the annular region between shells (`2 < r < 4` at closest approach) trigger outer crossing events only. Correct and expected.
- Bits 0 and 1 are simultaneously set while the ray is between the two shells. `ComputeInsideMask` computes both atomically per step. No race condition in single-threaded dispatch.
- `BiasStrength=0.12` (reduced from `0.15`) limits compound angle accumulation to ≈ 3 × 6.9° ≈ 20.7° total; keeps rays from missing the backdrop at scene scale.

---

## Verification Checklist

| Check | Fixture | Pass condition |
|---|---|---|
| Continuous regression | Any existing fixture | No behavior change when BLV nodes exist alongside Continuous-mode volumes |
| EntryOnly log count | `entry_basic` | Exactly 1 `entry event` per crossing ray, 0 `exit event` |
| EntryOnly visual shift | `entry_basic` | Dots behind shell shift ≈ 2.7 units downward in film |
| ExitOnly log only | `policy_compare` | Right column: 1 `exit event`, 0 `entry event` |
| EntryAndExit double shift | `policy_compare` | Centre column displacement ≈ 2× left or right column |
| Start-inside — no event | All fixtures | No log line on frame start; camera is outside all shells |
| Nested log count | `nested_shell_modes` | Exactly 3 log lines per on-axis ray (entry outer, entry inner, exit outer) |
| Snap index order | `nested_shell_modes` | Layer=0 is `BoundaryShell_Outer`, layer=1 is `BoundaryShell_Inner` in every log line |
| No FieldSource3D required | `entry_basic` | Scene renders without error when no `FieldSource3D` is present |
| >32-layer guard | Manual | 33+ CrossingEvent BLVs → single `GD.PushWarning` in Output; Continuous BLVs unaffected |
