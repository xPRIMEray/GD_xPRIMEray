# Curved-Ray Renderer — Architecture Overview

## Overview

This document explains the architecture of the curved-ray / GRIN rendering engine at a system level.

It is intended for:

* Contributors onboarding to the project
* Future architectural refactors
* Research and experimentation planning
* Long-term maintenance

This renderer is a self-contained rendering system embedded inside Godot. It owns its simulation, intersection, and scheduling logic.

---

## High-Level Architecture

The renderer is organized as a layered pipeline:

```
Godot Scene
     │
     ▼
┌────────────────────┐
│  Scene Snapshot    │
└────────────────────┘
     │
     ▼
┌────────────────────┐
│ Acceleration Layer │
│  (BLAS + TLAS)     │
└────────────────────┘
     │
     ▼
┌────────────────────┐
│  Field System      │
└────────────────────┘
     │
     ▼
┌────────────────────┐
│  Ray Integrator    │
└────────────────────┘
     │
     ▼
┌────────────────────┐
│ Intersection Sys   │
└────────────────────┘
     │
     ▼
┌────────────────────┐
│ Shading + Film     │
└────────────────────┘
     │
     ▼
Final Image Output
```

Each layer has strict responsibilities and communicates through explicit data contracts.

---

## Data Flow Diagram

The runtime pipeline is tile-based and parallel:

```
Frame Start
   │
   ▼
[Build SceneSnapshot]
   │
   ▼
[Update Acceleration Structures]
   │
   ▼
┌───────────────────────────────────┐
│ Parallel Tile Jobs                │
│                                   │
│  Integrate Curved Rays            │
│        │                          │
│        ▼                          │
│  Generate Path Chunks             │
│        │                          │
│        ▼                          │
│  BVH Traversal + Intersection     │
│        │                          │
│        ▼                          │
│  Shade + Write Film               │
└───────────────────────────────────┘
   │
   ▼
Frame Complete
```

---

## Subsystem Breakdown

### Scene Snapshot

**Purpose**

Create a compact, immutable representation of the scene for the renderer.

**Responsibilities**

* Extract geometry from Godot meshes
* Capture instance transforms
* Store materials and field sources
* Freeze state for deterministic rendering

**Key Properties**

* Immutable during a render step
* Struct-of-arrays memory layout
* Renderer-native representation

**Contract**

```
SceneSnapshot = {
    Instances[],
    MeshData[],
    Materials[],
    FieldSources[],
    CameraParams
}
```

---

### Acceleration Layer

This layer enables fast intersection queries.

#### BLAS (Bottom-Level Acceleration Structure)

* Built per mesh
* Triangle BVH in object space
* Static unless mesh changes

#### TLAS (Top-Level Acceleration Structure)

* Built over instances
* World-space BVH
* Refit or rebuilt per frame

**Traversal Model**

```
TLAS → Instance → BLAS → Triangle Test
```

**Design Goals**

* Cache-friendly node layout
* Iterative traversal (no recursion allocations)
* Deterministic ordering

---

### Field System

**Purpose**

Provide fast evaluation of GRIN fields and curvature bounds.

**Responsibilities**

* Acceleration sampling at position
* Optional spatial caching
* Curvature bounds for integrator step control

**Interface**

```
Vector3 AccelAt(Vector3 position)
float CurvatureBound(AABB region)
```

---

### Ray Integrator

**Purpose**

Simulate curved ray motion.

**Output**

Macro path chunks:

```
RayChunk = {
    p0,
    p1,
    radiusBound,
    t0,
    t1
}
```

Chunks represent conservative envelopes used for BVH traversal.

**Design Goals**

* Deterministic integration
* Allocation-free hot path
* Pluggable integrators (Euler, RK, etc.)

---

### Intersection System

**Purpose**

Detect geometry hits using acceleration structures.

**Pipeline**

```
For each RayChunk:
    Traverse TLAS envelope
        Traverse BLAS
            Triangle intersection
```

**Output**

```
HitRecord = {
    distance,
    normal,
    materialId,
    instanceId
}
```

---

### Shading + Film System

**Purpose**

Convert hit information into pixel color.

**Responsibilities**

* Material evaluation
* Lighting (current or future)
* Film accumulation
* Tile writeback

---

### Scheduler

**Purpose**

Coordinate parallel execution.

**Model**

* Tile-based work distribution
* Parallel integrate + intersect
* Integrated watchdog + telemetry

**Execution Diagram**

```
Scheduler
   ├─ Tile 0 job
   ├─ Tile 1 job
   ├─ Tile 2 job
   └─ …
```

Each tile runs independently.

---

## Memory Architecture

Hot paths use data-oriented layouts:

```
RayPositionsX[]
RayPositionsY[]
RayPositionsZ[]
RayDirectionsX[]
...
```

Goals:

* Cache coherence
* SIMD friendliness
* Minimal allocations

Temporary memory uses frame arenas or fixed buffers.

---

## Godot Integration Boundary

The renderer is embedded in Godot but architecturally separate.

```
Godot Nodes
    │
    ▼
Renderer Adapter Layer
    │
    ▼
Renderer Core (engine-agnostic)
```

Godot provides:

* Scene extraction
* Display output
* Debug visualization

Renderer core owns:

* Simulation
* Acceleration
* Intersection
* Scheduling

---

## Extension Points

The architecture intentionally supports:

* New integrators
* Alternative BVH builders
* Experimental field models
* GPU compute backends
* Advanced shading pipelines

Subsystems should evolve without rewriting others.

---

## Design Invariants

The following must remain true:

* Scene snapshots are immutable per frame
* Intersection is renderer-owned
* Curved rays are first-class primitives
* Subsystems communicate through explicit contracts
* Hot paths avoid dynamic allocation
* Telemetry is always available

---

## Contributor Mental Model

When adding features, think in layers:

1. Does this belong in SceneSnapshot?
2. Is it acceleration or field logic?
3. Is it integrator behavior?
4. Is it shading?
5. Is it scheduling?

Avoid cross-layer coupling.

---

## Long-Term Trajectory

This architecture is designed to scale toward:

* Millions of rays
* Large scenes
* Dynamic fields
* CPU/GPU hybrid execution
* Research-grade simulation

The goal is a stable foundation for experimentation and growth.

---

## Summary Diagram

```
┌───────────────────────────┐
│       SceneSnapshot       │
└─────────────┬─────────────┘
              ▼
┌───────────────────────────┐
│   Acceleration (BLAS/TLAS)│
└─────────────┬─────────────┘
              ▼
┌───────────────────────────┐
│       Field System        │
└─────────────┬─────────────┘
              ▼
┌───────────────────────────┐
│     Ray Integrator        │
└─────────────┬─────────────┘
              ▼
┌───────────────────────────┐
│   Intersection System     │
└─────────────┬─────────────┘
              ▼
┌───────────────────────────┐
│   Shading + Scheduler     │
└───────────────────────────┘
```

This is the renderer’s backbone.

All development should reinforce, not erode, this structure.
