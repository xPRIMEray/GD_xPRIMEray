# Specification — Scheduler & Task Graph

## Purpose

This document defines the renderer’s execution model: how work is partitioned, scheduled, executed, and monitored.

The scheduler must support:

* End-to-end parallel rendering (integration + intersection + shading)
* Deterministic output for a fixed seed/config
* Budget guards (frame time caps / watchdog)
* Scalable performance from real-time previews to offline rendering
* Future compatibility with GPU compute backends

This spec defines:

* Task graph stages
* Tile decomposition
* Work queue model
* Threading strategy
* Memory ownership rules
* Telemetry and watchdog integration
* Cancellation and progressive rendering behavior

---

## Guiding Principles

1. **Tile-based work** is the primary unit of scheduling.
2. Hot loops must be **allocation-free** and avoid shared mutable state.
3. Tasks must operate on an **immutable SceneSnapshot** for the render step.
4. Render output should be **progressive** and cancellable.
5. Budgets and watchdog must be first-class, not bolted on.

---

## Terminology

* **Render Step**: One scheduler-run execution of the pipeline producing a film update.
* **Stage**: A pipeline phase (snapshot, accel, integrate, intersect, shade, writeback).
* **Tile**: A rectangular region of the film (e.g., 16×16 pixels).
* **Task**: Executable work unit associated with a tile and stage.
* **Worker**: A CPU thread executing tasks from queues.
* **Budget**: Time, step, and/or work quota constraints.

---

## Task Graph Model

The scheduler runs a *fixed* stage graph per render step.

### Baseline Stage Graph (v1)

```
Stage 0: Build SceneSnapshot           (main thread)
Stage 1: Build/Update Accel (BLAS/TLAS)(main thread + optional background)
Stage 2: Prepare FieldSystem caches    (main thread + optional background)

Stage 3: Tile Jobs — Integrate         (parallel)
Stage 4: Tile Jobs — Intersect+Shade   (parallel)
Stage 5: Tile Jobs — Writeback         (parallel or merged with Stage 4)

Stage 6: Present + Telemetry           (main thread)
```

Notes:

* Stages 0–2 are prerequisite barriers for tile jobs.
* Stages 3–5 operate per tile and are parallelizable.
* Stage 4 and 5 may be fused for simplicity in v1.

---

## Tile Decomposition

### Tile Size

Default tile size is configurable (recommended starting point: 16×16 or 32×16).

Tile sizes must:

* balance overhead vs cache locality
* provide enough tasks to saturate CPU cores
* support progressive updates (smaller tiles show results earlier)

### Tile Enumeration

Tiles are enumerated in a stable order (e.g., scanline tile order) to ensure deterministic progression and reproducible debug behavior.

---

## Work Queue Strategy

### Required: Work-Stealing Queues (preferred)

Each worker owns a local deque:

* pushes/pops from local end (LIFO for cache locality)
* steals from other workers’ opposite end (FIFO) to reduce contention

Benefits:

* excellent load balancing under variable tile cost (curvature-heavy zones)
* reduces global lock contention
* supports scalability across many cores

### Acceptable v1 Alternative: Global Concurrent Queue

A single thread-safe queue of tile tasks.
Simpler but higher contention and worse load balancing.

---

## Worker Lifecycle

Workers run a loop:

1. Fetch next available task
2. Execute task (tile stage)
3. Report telemetry
4. Check cancellation/budget
5. Repeat

Workers must never:

* write to shared global state without atomic/locked protocols
* allocate per-pixel or per-step memory from the heap

---

## Task Types

### TileIntegrateTask

Input:

* SceneSnapshot (read-only)
* FieldSystem (read-only)
* Tile coordinates
* Integrator config
* Per-task scratch buffers

Output:

* Per-pixel ray chunk buffers OR compact per-pixel “integration output”
* SoftGate metadata (optional, retained)

### TileIntersectShadeTask

Input:

* SceneSnapshot (read-only)
* Acceleration (read-only)
* Tile integration outputs
* Shading config

Output:

* Film pixel contributions for tile

### TileWritebackTask (optional)

Input:

* Tile pixel results
* Film buffer

Output:

* Film updated in tile region

---

## Task Fusion Policy

To reduce overhead, Stage 4 and Stage 5 may fuse:

```
Integrate → Intersect → Shade → WritePixel
```

This is recommended in early versions for simplicity.

However, keeping integration outputs separate enables:

* debugging visualization of chunks
* future path-BVH experiments
* reuse of integration for multiple shading passes

The scheduler must support both modes as a config option.

---

## Memory Ownership & Data Contracts

### Read-only Shared Data

* SceneSnapshot
* TLAS / BLAS arrays
* Field caches
* Material tables
* Global configuration

These are immutable during Stage 3–5.

### Per-Task Scratch Memory

Each task receives exclusive scratch:

* traversal stack
* chunk buffers
* temporary hit payload
* local RNG state (if used)

Scratch allocation strategies:

* fixed-size stackalloc / arrays
* per-thread arena allocator reset each render step

### Film Buffer

Film updates are written tile-by-tile.

Allowed write methods:

* direct write to unique tile region (no contention)
* atomic accumulation if additive blending is needed

Preferred:

* tile region is exclusive to the executing task
* film is a simple array indexed by pixel

---

## Determinism Requirements

Given identical inputs:

* SceneSnapshot
* config
* random seed (if used)
  …the output must match exactly.

Rules:

* stable tile enumeration order
* deterministic traversal/hit selection rules
* avoid “race to write” behaviors
* avoid unordered accumulation across threads unless explicitly allowed

If nondeterminism is allowed as a mode, it must be explicit and documented.

---

## Budgets, Watchdog, Cancellation

### Budget Types

* **Time budget**: per render step wall clock cap
* **Work budget**: max rays / steps / chunks per tile
* **Frame budget**: preview mode cap per frame

### Enforcement Points

Workers check budget:

* before starting a new tile task
* periodically inside long-running loops (e.g., chunk stepping)
* on safe boundaries (per pixel / per macro chunk)

### Cancellation

Cancellation triggers:

* config change
* camera movement
* scene modification
* explicit user abort

Behavior:

* Workers stop starting new tasks
* In-flight tasks exit at safe points
* Partial tile writes are either:

  * discarded (preferred for determinism), or
  * committed only if tile completes

---

## Progressive Rendering Modes

### Real-time Preview Mode

Goals:

* respond quickly to camera/config changes
* deliver partial image updates fast

Policy:

* small time budget per step
* prioritize tiles near center / foveated region (optional)
* allow incomplete sample counts

### Offline Quality Mode

Goals:

* maximize convergence and correctness
* full coverage

Policy:

* large or unlimited budget
* deterministic traversal order
* multi-sample accumulation permitted

---

## Telemetry & Instrumentation

Telemetry must be integrated at scheduler level.

### Required Metrics

Per render step:

* total time per stage
* tile throughput (tiles/sec)
* rays/sec (estimated)
* average steps/chunks per ray
* BVH traversal counts (optional)
* hit rates and miss rates

Per tile:

* task time
* step count
* max curvature encountered (optional)

### Health Signals

* watchdog triggers
* budget early-exit counts
* cancellation exit counts
* per-stage failure flags

---

## Implementation Notes for Godot

### Threading Constraints

* Presentation / texture upload must occur on main thread
* Snapshot extraction may require main thread access depending on Godot APIs
* Rendering core should be engine-agnostic where possible

### Recommended Boundary

```
Godot Node (main thread)
    ├─ Build Snapshot
    ├─ Invoke Scheduler
    └─ Present Results
RendererCore
    ├─ Workers
    ├─ BVH traversal
    ├─ integration
    └─ shading
```

---

## Extension Points

Future upgrades:

* task graph expansion (more stages)
* packet-based ray integration
* ray sorting / coherence scheduling
* hybrid CPU/GPU task queues
* async acceleration rebuilds
* multi-resolution rendering (mips / progressive refinement)

This spec’s baseline structure must not block these.

---

## Success Criteria

The scheduler is complete when:

* Integrate + intersect + shade run parallel end-to-end
* No dependency on Godot physics for production intersection
* Work scales predictably with core count
* Budget guards remain stable under stress
* Telemetry can explain performance regression clearly
* Real-time preview mode remains responsive under camera changes

This scheduler becomes the execution backbone of the renderer.
