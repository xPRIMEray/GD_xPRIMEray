# Specification — BVH Acceleration System

**Charter section:** §9 Acceleration Structures and Intersection
**Status:** TLAS implemented (Field + Geometry); BLAS planned
**Key source files:** `RendererCore/Fields/FieldTLAS.cs`, `RendererCore/Geometry/GeometryTLAS.cs`, `RendererCore/Accel/` (empty)

---

## 1) Purpose

The acceleration system provides spatial queries for field evaluation (FieldTLAS)
and geometry candidate pruning (GeometryTLAS). A future BLAS layer will provide
internal triangle intersection, removing the Godot physics dependency.

---

## 2) Architecture Overview

```
Current (Implemented):
  FieldTLAS (BVH over field entity AABBs) → field candidate queries
  GeometryTLAS (BVH over geometry AABBs) → Pass-2 candidate pruning → Godot narrowphase

Planned:
  GeometryTLAS → BLAS (per-mesh triangle BVH) → internal narrowphase
```

---

## 3) Node Representation (Implemented)

### FieldTLAS Node

```csharp
public readonly struct BVHNode
{
    public readonly Aabb3 Bounds;
    public readonly int Left;   // child index, or -(leafStart+1) for leaf
    public readonly int Right;  // child index, or leafCount for leaf
}
```

### GeometryTLAS Node

```csharp
public readonly struct GeometryBVHNode
{
    public readonly Aabb3 Bounds;
    public readonly int Left;
    public readonly int Right;
}
```

Leaf detection: `Left < 0` → leaf node. `leafStart = -(Left) - 1`, `leafCount = Right`.

Both stored as flat arrays (`BVHNode[]` / `GeometryBVHNode[]`) with separate
`LeafFieldIds[]` / `LeafGeometryIds[]` arrays for leaf entity references.

---

## 4) Build Algorithm (Implemented)

Both TLASes use identical build logic:

1. Compute centroids of all entity world bounds
2. Select split axis by largest centroid extent
3. Sort entities along axis (median split via `Array.Sort` with `CentroidComparer`)
4. Split at midpoint (`length / 2`)
5. Recurse until leaf threshold (4 entities)

**Not SAH** — current build uses median split with axis selection by centroid
extent. This is simple and deterministic. SAH is a potential upgrade.

Sort comparator breaks ties by entity index for determinism.

Source: `FieldTLAS.Build(in FieldEntitySOA)`, `GeometryTLAS.Build(in GeometryEntitySOA)`

---

## 5) Traversal Algorithm (Implemented)

Both TLASes use iterative stack-based traversal:

```
stack = stackalloc int[128/256]
push(rootIndex)
while stack not empty:
    node = pop()
    if query overlaps/contains node.Bounds:
        if leaf: emit entity IDs to results span
        else: push Left, push Right
```

**FieldTLAS queries:**
- `QueryPoint(Vector3 pWorld, Span<int> results)` — point containment (stack size 256)
- `QueryAabb(Aabb3 region, Span<int> results)` — AABB overlap (stack size 128)

**GeometryTLAS queries:**
- `QueryAabb(Aabb3 region, Span<int> results)` — AABB overlap (stack size 128, overflow-safe)

No heap allocation. Results written to caller-provided `Span<int>`.
GeometryTLAS includes stack overflow guard (`sp + 2 > stack.Length → early return`).

---

## 6) Rebuild Policy

Both TLASes are fully rebuilt every frame by `SnapshotBuilder`. No refit mode
exists. This is acceptable given current entity counts but may need refit
support for large scenes.

---

## 7) BLAS — Planned (Phase 1)

Target location: `RendererCore/Accel/`

### 7.1 Purpose

Per-mesh triangle BVH enabling internal narrowphase intersection, replacing
Godot `DirectSpaceState` calls in Pass-2.

### 7.2 Node Layout (Planned)

```
struct TriBVHNode {
    Aabb3 bounds;
    int left;     // child index or -(leafStart+1)
    int right;    // child index or primitiveCount
}
```

Triangle storage: contiguous `Vector3[]` triples (v0, v1, v2) or indexed.

### 7.3 Build Algorithm (Planned)

Binned SAH (Surface Area Heuristic) preferred over current median split.
Leaf threshold: 4–8 triangles. Depth limit as safety.

### 7.4 Traversal (Planned)

Ray-AABB slab test + Möller–Trumbore triangle intersection.
Iterative stack-based, thread-local stack, no allocation.
Front-to-back child ordering by ray direction for early termination.

### 7.5 Integration

BLAS keyed by mesh resource ID → cached across frames (static geometry).
TLAS provides world-space instance → BLAS mapping.
Ray transformed to object space via `InstanceSOA.ObjectFromWorld` before BLAS traversal.

---

## 8) Envelope Intersection (Current + Planned)

Current: Pass-2 constructs segment AABB via `Aabb3.FromSegment(A, B).Expand(RadiusBound)`
and queries GeometryTLAS.

Planned: BLAS traversal will accept the same envelope AABB or a tighter
capsule approximation for curved-segment intersection.

---

## 9) Performance Notes

- No heap allocation in traversal hot paths
- `stackalloc` for traversal stacks and result spans
- Contiguous node arrays for cache locality
- `readonly struct` nodes avoid copy overhead
- Deterministic traversal order (depth-first, left-before-right)

---

## 10) Validation

- Compare TLAS candidate sets against brute-force for correctness
- BVH bounds visualisation (debug overlay)
- Traversal statistics: nodes visited, leaves hit, candidates returned
- Geometry prune audit in Pass-2 (existing instrumentation)
