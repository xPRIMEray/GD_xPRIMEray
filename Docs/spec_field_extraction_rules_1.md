# Specification — Field Entity Extraction Rules (Godot → SceneSnapshot)

**Charter section:** §4 Module Map (GodotAdapter), §7.1 Field Entity Representation
**Status:** Implemented
**Key source files:** `GodotAdapter/SnapshotBuilder.cs`, `FieldSource3D.cs`

---

## 1) Purpose

Defines how `FieldSource3D` nodes in the Godot scene tree are extracted into
renderer-native `FieldEntitySOA` entries within `SceneSnapshot`.

---

## 2) Extraction Pipeline (Implemented)

`SnapshotBuilder.BuildFromGodotScene(root)` performs:

1. **Collect:** Recursively find all `FieldSource3D` nodes in scene tree.
2. **Sort:** Sort by `NodePath` using `string.CompareOrdinal` (deterministic ordering).
3. **Filter:** Retain only nodes with `Enabled = true`.
4. **Extract per field:**
   - Read `MetricModel`, `ShapeType`, `CurveType` enums
   - Get world transform (`GlobalTransform`) and compute `AffineInverse`
   - Call `field.GetPackedParams8(...)` → rInner, rOuter, amp, a, b, c, r0, r1
   - Read `ModeFlags`
   - Append 8-float block to `PackedParamBuffer`
   - Compute conservative world AABB via `field.GetWorldInfluenceAabbConservative()`
   - Emit into `FieldEntitySOA` arrays
5. **Build TLAS:** `FieldTLAS.Build(fields)` over the extracted entities.

---

## 3) Ordering Rules (Implemented)

Primary sort key: `NodePath.ToString()` via `string.CompareOrdinal`.

This ensures:
- Stable entity index assignment across frames (given stable scene structure)
- Deterministic accumulation order in `FieldSystem.AccelAt`
- Reproducible snapshot content for validation

No secondary sort key is needed — `CompareOrdinal` on full path is sufficient
for uniqueness.

---

## 4) Parameter Packing (Implemented)

All fields pack into uniform 8-float blocks via `PackedParamBuffer.AppendBlock8`:

```
[+0] rInner    [+1] rOuter    [+2] amp
[+3] a         [+4] b         [+5] c
[+6] r0 (reserved)   [+7] r1 (reserved)
```

Field entity stores `paramOffset` (index into buffer) and `paramLength` (always 8).

---

## 5) Bounds Extraction (Implemented)

World bounds come from `FieldSource3D.GetWorldInfluenceAabbConservative()`.
This must return a world-space AABB that **fully contains** the field's
influence region (rOuter sphere transformed to world).

**Conservatism requirement:** Bounds must never underestimate. Underestimation
causes missed field contributions → incorrect ray paths. Overestimation is
safe (only costs extra broadphase candidates).

---

## 6) Geometry Extraction (Implemented)

Parallel to field extraction, `SnapshotBuilder` also extracts geometry:

1. Collect `CollisionObject3D` nodes (sorted by NodePath)
2. Collect `VisualInstance3D` nodes (sorted by NodePath)
3. Prefer collision objects; skip visuals that have collision ancestors or descendants
4. Filter collision objects by `CollisionLayer != 0` (raycast targets only)
5. Compute world AABB per node (from collision shapes or visual AABB)
6. Inflate bounds by `GeometryInflate` constant (0.02)
7. Store as `GeometryEntitySOA` with Godot instance IDs

Shape AABB extraction handles: `BoxShape3D`, `SphereShape3D`, `CapsuleShape3D`,
`CylinderShape3D`. Unknown shapes → fallback AABB from node position ± 0.05.

---

## 7) Transform Handling (Implemented)

Transforms stored as `System.Numerics.Matrix4x4` (not Godot types).
Conversion: `SnapshotBuilder.ToMatrix4x4(Transform3D t)` extracts basis columns
and origin into a 4×4 matrix compatible with `Vector3.Transform` and
`Vector3.TransformNormal`.

Both `WorldFromLocal` and `LocalFromWorld` are stored to avoid per-query inverse.

---

## 8) Default Values

If a `FieldSource3D` property is missing or unset, `GetPackedParams8` must
provide deterministic defaults. Expected defaults:

- rInner = 0, rOuter = 1, amp = 1
- curveType = Power, a = 1, b = 0, c = 0
- shapeType = SphereRadial, metricModel = GRIN
- modeFlags = 0

---

## 9) Validation

`SnapshotBuilder` emits periodic `[SNAPSHOT]` log lines reporting:
- Total geometry count (collision vs visual breakdown)
- Collision objects skipped (non-raycast)
- Visual objects skipped (collision preference)
- Bounds samples for first 3 geometry entities

Log controlled by `DebugLogConfig.EnableSnapshotLog` and `SnapshotLogIntervalSec`.
