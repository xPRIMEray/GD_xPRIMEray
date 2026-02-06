# Specification — SceneSnapshot Data Layout

## Purpose

This document defines the renderer-native scene representation used during a render step.

SceneSnapshot must provide:

* Immutable, deterministic inputs for rendering tasks
* Cache-friendly, data-oriented layout
* Fast geometry access (instances, meshes, materials)
* Fast field access (GRIN sources / volumes / nodes)
* Efficient updates when objects move
* Minimal coupling to Godot runtime objects

**Critical requirement:** Field entities (GrinField nodes, GRIN volumes, point/volume sources, etc.) are first-class scene objects and must be snapshotted with the same rigor as geometry.

---

## Conceptual Model

A SceneSnapshot represents a frozen “frame state”:

```
SceneSnapshot =
    Geometry:  Instances + MeshData + Materials
    Fields:    FieldEntities + FieldAccelCaches
    Camera:    CameraParams
    Config:    EffectiveConfig / Quality settings
```

During a render step:

* The snapshot is read-only.
* Workers do not call Godot APIs.
* Field sampling uses snapshot data, not live nodes.

---

## Snapshot Boundaries & Update Philosophy

The snapshot is rebuilt per render step, but not all data is rebuilt equally.

### Static / Semi-Static Data

* Mesh triangle arrays
* Mesh BLAS BVHs
* Material tables (typically)
* Field entity static parameters (often)

These should be cached across frames and referenced by ID.

### Dynamic Data

* Instance transforms
* Instance world bounds
* Field entity transforms
* Field entity dynamic parameters
* TLAS BVH
* Optional field cache regions near camera

These update per step.

---

## Data-Oriented Layout Requirements

The SceneSnapshot must be primarily **struct-of-arrays (SoA)** for hot access.

Goal: avoid pointer chasing, avoid per-object heap allocation, keep arrays contiguous.

---

## Core Identifiers

All references must use stable integer IDs.

```
MeshId
InstanceId
MaterialId
FieldEntityId
FieldTypeId
```

IDs are stable within a snapshot and may remain stable across snapshots if cached.

---

## High-Level Structures

### SceneSnapshot (top-level)

```
struct SceneSnapshot {
    // Geometry
    InstanceSOA instances;
    MeshTable meshes;
    MaterialTable materials;

    // Fields
    FieldEntitySOA fieldEntities;
    FieldSystemCaches fieldCaches;

    // Camera + Config
    CameraParams camera;
    RenderConfig config;

    // Acceleration
    TLAS tlas; // world-space BVH over instances
}
```

---

## Geometry Layout

### InstanceSOA

Instances describe world-space placement of meshes and their materials.

```
struct InstanceSOA {
    int count;

    int[] meshId;
    int[] materialId;

    // Transform (world)
    Matrix4x4[] worldFromObject;
    Matrix4x4[] objectFromWorld;

    // Bounds
    AABB[] worldBounds;

    // Optional flags
    uint[] flags; // e.g. static, moving, shadow, etc.
}
```

Notes:

* Both transform directions are stored to avoid per-hit inverse recomputation.
* worldBounds are required for TLAS construction.

### MeshTable

```
struct MeshTable {
    int count;
    MeshData[] meshData;
}
```

### MeshData

```
struct MeshData {
    // Geometry
    Vector3[] vertices;     // object space
    int[] indices;          // triangle indices
    TriangleSOA triangles;  // optional pre-expanded triangles

    // Acceleration
    BLAS blas;              // object-space BVH over triangles

    // Metadata
    AABB objectBounds;
}
```

Triangle storage choice:

* v1: indexed triangles (vertices+indices)
* later: pre-expanded triangle arrays for faster traversal

---

## Material Layout

Materials are renderer-native IDs.

```
struct MaterialTable {
    int count;
    MaterialParams[] materials;
}
```

Shading is out-of-scope here; store what is needed for current shading and future extension.

---

## Field Layout (First-Class Requirement)

Field entities must be snapshotted analogously to instances.

They are “objects in the scene” that may move, rotate, scale, and change parameters.

### FieldEntitySOA

```
struct FieldEntitySOA {
    int count;

    int[] fieldType;        // enum / type id
    Matrix4x4[] worldFromLocal;
    Matrix4x4[] localFromWorld;

    AABB[] worldBounds;     // required for field broadphase

    int[] paramOffset;      // index into param buffer
    int[] paramLength;      // param block size

    uint[] flags;           // enabled, debug, etc.
}
```

Field entities MUST support motion:

* worldFromLocal updates per step if the node moves.
* worldBounds updates per step.

### Field Types

FieldType examples:

* Point GRIN source
* Directed source / dipole
* Box volume GRIN
* Sphere volume GRIN
* Signed-distance field volume (future)
* Composite / stack field (future)

Field types must have a stable enum ID.

---

## Field Parameter Storage

Field params must be stored contiguously for cache efficiency.

### Option A (Recommended): Packed Float Buffer

```
struct FieldParamBuffer {
    float[] data;
}
```

Each FieldEntity has:

* paramOffset
* paramLength

FieldType determines how to interpret the slice.

Example parameter packing:

* iorInner, iorOuter, rInner, rOuter, beta, gamma, strength, falloff, etc.

### Option B: Typed Param Struct per Type

Useful for readability but less flexible.
May be adopted later after profiling.

---

## Field Broadphase Acceleration

To make field sampling fast, especially in scenes with many moving fields:

### Field TLAS (FTLAS)

A world-space BVH or uniform grid over fieldEntities’ worldBounds.

```
struct FieldTLAS {
    BVHNode[] nodes;
    int[] leafFieldIds;
}
```

Workers querying the field at position `p` do:

1. Query FTLAS for field entities whose bounds contain or influence `p`
2. Evaluate only those fields

This is essential for scaling.

---

## Field System Caches (Fast Sampling)

Field sampling must be fast and stable.

SceneSnapshot holds optional caches built per render step.

### FieldSystemCaches

```
struct FieldSystemCaches {
    FieldTLAS ftlas;

    // Optional: near-camera grid cache
    FieldGridCache nearGrid;

    // Optional: curvature bounds cache
    CurvatureBoundGrid boundGrid;
}
```

### Near-Grid Cache (Optional)

A small clipmap/grid around camera to accelerate `AccelAt(p)`.

* Used for preview modes
* Can be rebuilt each step, or updated incrementally

---

## Field Sampling API Contract

The renderer core must expose a stable interface:

```
Vector3 AccelAt(Vector3 pWorld, in SceneSnapshot snapshot)
```

Implementation rules:

* No Godot calls
* Use FTLAS to select candidate fields
* Evaluate each field in local space using localFromWorld
* Sum accelerations deterministically in fieldId order (or stable order)

---

## Determinism Rules

SceneSnapshot extraction must be deterministic given the same scene state:

* Stable ordering of instances and field entities
* Stable ID assignment within snapshot
* Stable field sum order
* No unordered parallel writes during snapshot build

If stable ordering is not guaranteed by Godot node enumeration, the snapshot builder must impose one.

---

## Snapshot Builder Responsibilities

A dedicated adapter layer converts Godot nodes to snapshot entries.

### Required Extraction

For Geometry:

* Mesh triangles / indices
* Instance transforms
* Material mapping

For Fields:

* GrinField node transform
* Bounds (world)
* FieldType
* Parameter block

Field bounds must be conservative and cover the field’s influence region.

---

## Scene Changes & Incremental Caching

Snapshot builder may cache:

* BLAS per mesh (keyed by mesh resource ID)
* pre-expanded triangle arrays
* material parameter blocks

Per step update:

* instance transforms
* instance bounds
* field transforms
* field bounds
* TLAS and FTLAS rebuild/refit

---

## Debug & Validation Hooks

Optional debug features:

* visualize TLAS/FTLAS bounds
* field influence overlay
* “field query counts” metrics per tile
* compare AccelAt results with direct full evaluation (slow mode)

---

## Success Criteria

SceneSnapshot is complete when:

* Rendering tasks never require Godot APIs
* Geometry access is fast and deterministic
* Field entities are first-class and movable
* Field sampling uses broadphase selection (FTLAS/grid)
* Snapshot rebuild/update costs are predictable
* Architecture supports scaling to many field sources

This snapshot becomes the canonical state for all renderer work.
