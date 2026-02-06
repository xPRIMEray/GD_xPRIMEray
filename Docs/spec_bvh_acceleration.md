# Specification — BVH Acceleration System

## Purpose

This document defines the implementation specification for the renderer’s acceleration structures.

The BVH system must support:

* Fast triangle intersection
* Deterministic traversal
* Cache-efficient memory layout
* Parallel-safe read-only traversal
* Compatibility with curved ray chunk envelopes

This spec covers:

* BLAS (mesh BVH)
* TLAS (instance BVH)
* Node layout
* Build algorithms
* Traversal algorithms

---

## Architectural Overview

The acceleration system is a two-level hierarchy:

```
TLAS (instance BVH)
    ↓
BLAS (triangle BVH per mesh)
    ↓
Triangle intersection
```

BLAS structures are static per mesh.

TLAS structures are rebuilt or refit per frame.

---

## Node Representation

BVH uses a binary tree (BVH2).

Nodes are stored in flat arrays.

### Node Layout

```
struct BVHNode {
    AABB bounds;
    int leftChild;    // index or negative leaf marker
    int rightChild;
}
```

Leaf nodes encode primitive ranges:

```
struct BVHLeaf {
    int firstPrimitive;
    int primitiveCount;
}
```

Leaf detection rule:

```
leftChild < 0  → leaf
leafIndex = -leftChild - 1
```

This avoids virtual dispatch and pointer chasing.

---

## Memory Layout Goals

Nodes are stored in depth-first order.

Goals:

* Sequential memory traversal
* Minimal cache misses
* Branch-predictable traversal
* SIMD-friendly AABB tests

Triangles are stored in contiguous arrays:

```
Triangle {
    Vector3 v0, v1, v2;
}
```

No per-triangle heap allocation.

---

## BLAS Build Algorithm

### Builder Type

Initial implementation:

**Binned SAH (Surface Area Heuristic)**

Fallback option:

Median split (for debugging simplicity).

### Build Steps

1. Compute triangle centroids
2. Select split axis by largest centroid variance
3. Bin primitives along axis
4. Evaluate SAH cost
5. Partition primitives
6. Recurse

Termination conditions:

* Primitive count ≤ leaf threshold
* Maximum depth reached

Leaf threshold default: 4–8 triangles.

---

## TLAS Build Algorithm

TLAS builds over instance AABBs.

Two modes:

### Rebuild Mode

Full rebuild each frame.

Used when instance motion is large.

### Refit Mode

Update node bounds without topology rebuild.

Used for small motion.

TLAS builder may use simplified SAH or LBVH.

---

## Traversal Algorithm

Traversal is iterative using an explicit stack.

```
stack.push(root)

while stack not empty:
    node = stack.pop()

    if rayEnvelope intersects node.bounds:
        if node is leaf:
            test primitives
        else:
            push children
```

Child push order is front-to-back when possible.

Stack is fixed-size per thread.

No heap allocation during traversal.

---

## Envelope Intersection

Traversal uses envelope-AABB tests.

Envelope types supported:

* Segment AABB
* Capsule approximation
* Conservative chunk bounds

Envelope intersection must be branch-efficient.

---

## Triangle Intersection

Triangle tests use Möller–Trumbore or equivalent.

Requirements:

* Robust epsilon handling
* Deterministic ordering
* Early exit on nearest hit

---

## Parallelism Model

BLAS is read-only after build.

TLAS is rebuilt/refit before parallel jobs.

Traversal is fully thread-safe.

Each thread owns:

* Local traversal stack
* Local hit record

No shared mutable state.

---

## Performance Invariants

The system must:

* Avoid dynamic allocation in hot paths
* Use contiguous memory
* Minimize recursion
* Favor predictable branches
* Support vectorization later

---

## Debug & Validation

Optional validation modes:

* Compare against Godot physics
* BVH visualization
* Traversal statistics
* Hit consistency checks

These must be compile-time or runtime toggles.

---

## Extension Points

Future upgrades may include:

* BVH4 / wide BVH nodes
* SIMD traversal
* GPU BVH representation
* Motion blur support
* Packet traversal

The base layout must not prevent these.

---

## Success Criteria

The BVH system is complete when:

* All geometry intersection uses BLAS/TLAS
* Traversal scales with scene complexity
* Multithreaded rendering is stable
* Performance is predictable
* Debug validation passes consistently

This system becomes the permanent intersection backbone.
