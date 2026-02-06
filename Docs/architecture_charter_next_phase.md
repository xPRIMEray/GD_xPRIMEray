# Curved-Ray Renderer — Architecture Phase Charter

## Purpose

This document defines the architectural charter for the next development phase of the curved-ray / GRIN rendering engine. It serves as the guiding artifact for all major refactors and system design decisions.

This phase marks the transition from a working experimental prototype into a scalable, extensible rendering architecture capable of supporting advanced research and long-term evolution.

---

## Vision

We are building a renderer where **curved rays are a first-class primitive**.

The system must support:

* Physically meaningful curved-ray integration
* Accurate and deterministic intersection
* Scalable performance across large scenes
* Clean modular extensibility
* A clear path toward CPU/GPU hybrid execution

This renderer is not a hack layered on top of an existing engine. It is an independent rendering architecture embedded within Godot.

---

## Core Architectural Principles

### 1. Ownership of Intersection

The renderer must own its intersection pipeline.

Godot physics may be used for validation and debugging, but production intersection must be handled by an internal acceleration structure and triangle intersection system.

This enables:

* Full multithreading
* Deterministic behavior
* Performance scaling
* Architectural independence

---

### 2. Immutable Scene Snapshots

Each render step operates on an immutable scene snapshot:

* Geometry instances
* Materials
* Field sources
* Camera parameters

Snapshots are compact, cache-friendly, and renderer-native. They are decoupled from live Godot scene state.

---

### 3. Separation of Concerns

The renderer is divided into explicit subsystems:

* Scene snapshot construction
* Acceleration structures
* Field evaluation
* Curved ray integration
* Intersection
* Shading
* Scheduling

Each subsystem must evolve independently with minimal coupling.

---

### 4. Data-Oriented Design

Performance-critical systems favor:

* Struct-of-arrays layouts
* Allocation-free hot paths
* Cache-friendly memory access
* Deterministic execution

Object-oriented convenience must not compromise data locality in core loops.

---

### 5. Path-Based Ray Representation

Curved rays are represented as bounded path segments (“chunks”) rather than infinite lines.

Each chunk provides:

* Start/end points
* Conservative spatial envelope
* Parameter range

Acceleration structures operate on these envelopes.

---

## Target Architecture Overview

### Scene Layer

**SceneSnapshot**

* Immutable per frame
* Extracted from Godot
* Contains compact geometry and field data

---

### Acceleration Layer

**BLAS (Bottom-Level Acceleration Structures)**

* Static per mesh
* Triangle BVH in object space

**TLAS (Top-Level Acceleration Structure)**

* Instance BVH in world space
* Rebuilt or refit per frame

---

### Field System

* Fast evaluation of field acceleration
* Optional spatial caches
* Curvature bounds for step control

---

### Ray System

**CurvedRayIntegrator**

* Deterministic stepping
* Produces macro path chunks
* Supports adaptive strategies

---

### Intersection System

* BVH traversal
* Segment/triangle testing
* Hit payload generation

---

### Render Scheduler

* Tile-based job system
* Parallel integration and intersection
* Watchdog and telemetry integrated

---

## Development Roadmap

### Phase 1 — Scene Ownership

* Implement SceneSnapshot
* Extract geometry and transforms
* Maintain Godot physics as validation backend

### Phase 2 — CPU Intersection Core

* Implement triangle intersection
* Build BLAS BVH per mesh
* Validate against Godot results

### Phase 3 — TLAS Integration

* Instance BVH
* Replace Godot collision with internal traversal
* Enable full multithreading

### Phase 4 — Path Chunk Acceleration

* Macro segment chunking
* Envelope-based BVH traversal
* Optimize traversal batching

### Phase 5 — Scheduler Evolution

* Tile-based job system
* Unified integrate/intersect pipeline
* Expanded telemetry

---

## Non-Goals (This Phase)

* GPU implementation
* Photorealistic global illumination
* Advanced material systems
* Engine-level editor integration

These remain future directions.

---

## Success Criteria

This phase is successful when:

* The renderer no longer depends on Godot physics for production intersection
* Full-frame rendering is parallelized end-to-end
* Scenes scale predictably with geometry complexity
* Architecture supports experimentation without structural rewrites
* Telemetry provides actionable performance insight

---

## Guiding Philosophy

We design for longevity.

Every architectural decision should answer:

* Does this make the system more modular?
* Does this make performance behavior more predictable?
* Does this reduce hidden coupling?
* Does this enable future evolution?

Correctness and clarity take priority over premature optimization.

---

## Closing Statement

This phase establishes the renderer’s foundation.

The goal is not incremental improvement. The goal is architectural transformation.

We are building a system that can grow into a research-grade curved-ray engine.

All major work in this phase should align with this charter.
