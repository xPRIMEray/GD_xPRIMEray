# Specification — SceneSnapshot Data Layout

**Charter section:** §5 Data Model
**Status:** Implemented (with noted gaps)
**Key source files:** `RendererCore/SceneSnapshot/*.cs`, `GodotAdapter/SnapshotBuilder.cs`, `RendererCore/Common/FrameSnapshotBus.cs`

---

## 1) Purpose

SceneSnapshot is the renderer-native, immutable-for-frame scene representation
consumed by all rendering stages. It holds field entities, geometry entities,
acceleration structures, and curvature metadata.

Requirements:
- Immutable during `RenderStep` — no Godot API calls during rendering
- Data-oriented SOA layout for cache efficiency
- Deterministic content given identical scene state
- Rebuilt per frame from live Godot scene tree

---

## 2) Top-Level Shape (Implemented)

```csharp
public sealed class SceneSnapshot
{
    public InstanceSOA Instances { get; init; }
    public FieldEntitySOA Fields { get; init; }
    public PackedParamBuffer FieldParams { get; init; }
    public FieldTLAS FieldTLAS { get; init; }
    public GeometryEntitySOA Geometry { get; init; }
    public GeometryTLAS GeometryTLAS { get; init; }
    public CurvatureBoundGrid CurvatureGrid { get; init; }
}
```

Source: `RendererCore/SceneSnapshot/SceneSnapshot.cs`

---

## 3) SOA Containers

### 3.1 FieldEntitySOA (Implemented)

```csharp
public sealed class FieldEntitySOA
{
    public int Count { get; init; }
    public int[] MetricModel { get; init; }      // MetricModel enum as int
    public int[] ShapeType { get; init; }         // FieldShapeType enum as int
    public int[] CurveType { get; init; }         // FieldCurveType enum as int
    public Matrix4x4[] WorldFromLocal { get; init; }
    public Matrix4x4[] LocalFromWorld { get; init; }
    public Aabb3[] WorldBounds { get; init; }
    public int[] ParamOffset { get; init; }       // index into PackedParamBuffer.Data
    public int[] ParamLength { get; init; }       // always 8 in current build
    public uint[] Flags { get; init; }            // ModeFlags from FieldSource3D
}
```

All arrays default to `Array.Empty<T>()`. Source: `RendererCore/SceneSnapshot/FieldEntitySOA.cs`

### 3.2 GeometryEntitySOA (Implemented)

```csharp
public sealed class GeometryEntitySOA
{
    public int Count { get; }
    public Aabb3[] WorldBounds { get; }
    public long[] GodotInstanceIds { get; }
}
```

Constructed with `count`; arrays allocated in constructor.
Source: `RendererCore/SceneSnapshot/GeometryEntitySOA.cs`

### 3.3 InstanceSOA (Implemented — Unpopulated)

```csharp
public sealed class InstanceSOA
{
    public int Count { get; init; }
    public int[] MeshId { get; init; }
    public int[] MaterialId { get; init; }
    public Matrix4x4[] WorldFromObject { get; init; }
    public Matrix4x4[] ObjectFromWorld { get; init; }
    public Aabb3[] WorldBounds { get; init; }
}
```

**Known gap:** `SnapshotBuilder` sets `Instances = InstanceSOA.Empty()`. The type
exists but is never populated. Required for BLAS-based intersection.

Source: `RendererCore/SceneSnapshot/InstanceSOA.cs`

---

## 4) Packed Parameter Buffer (Implemented)

Block layout (8 floats per field entity):

| Offset | Name | Consumed by |
|--------|------|-------------|
| +0 | rInner | FieldSystem.AccelAt |
| +1 | rOuter | FieldSystem.AccelAt |
| +2 | amp | FieldSystem.AccelAt, CurvatureBoundGrid |
| +3 | a (curve coeff) | FieldSystem.AccelAt, CurvatureBoundGrid |
| +4 | b (curve coeff) | FieldSystem.AccelAt, CurvatureBoundGrid |
| +5 | c (curve coeff) | FieldSystem.AccelAt, CurvatureBoundGrid |
| +6 | r0 (reserved) | Not consumed |
| +7 | r1 (reserved) | Not consumed |

Source: `RendererCore/SceneSnapshot/PackedParamBuffer.cs`

---

## 5) Aabb3 Primitive (Implemented)

Immutable readonly struct with `Min`, `Max`, `Center`, `Extents`.
Operations: `FromSegment`, `Encapsulate`, `Expand`, `Contains`, `Overlaps`, `Union`.
Used by TLAS nodes, geometry bounds, segment envelopes, curvature grid cells.

Source: `RendererCore/SceneSnapshot/Aabb3.cs`

---

## 6) CurvatureBoundGrid (Implemented)

Camera-centred 3D grid of per-cell Kmax (sum of `|amp| * Fmax(curve)` over
contributing fields). Flat array indexed `[x + DimX*(y + DimY*z)]`.
`LookupKmax(pWorld)` returns 0 outside grid. Built by `GrinFilmCamera.RenderFrameBackend`.

Source: `RendererCore/Fields/CurvatureBoundGrid.cs`

---

## 7) FrameSnapshotBus (Implemented)

Global static publish point: `Set(snapshot, frameId)` / `Clear()`.
Published each frame by `RenderFrameBackend`. Read by `FieldProbe3D` and diagnostics.

Source: `RendererCore/Common/FrameSnapshotBus.cs`

---

## 8) Frame Rebuild vs Reuse

| Data | Lifecycle |
|------|-----------|
| SceneSnapshot, TLASes, CurvatureBoundGrid | Rebuilt every frame |
| Camera pass buffers, quick-ray caches, perf windows | Reused across frames |
| FieldGrid3D cache | Reused with cadence-based refresh |

---

## 9) Determinism

`SnapshotBuilder` sorts nodes by `NodePath` via `string.CompareOrdinal`.
Entity indices are stable per snapshot. No randomisation in construction.

---

## 10) Planned Extensions

- `WormholeSOA` (Phase 3): mouth centres, throat radii, chart types, child scene IDs
- `MeshTable` / `MaterialTable` (Phase 1): required for BLAS intersection
- `SceneId` field for multi-scene wormhole dispatch
