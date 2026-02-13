# Specification — Scheduler & Task Graph

**Charter section:** §10 Scheduling and Concurrency
**Status:** Embedded in GrinFilmCamera (planned migration to RendererCore/Scheduler)
**Key source files:** `GrinFilmCamera.cs`, `RenderBackends/LegacyBackend.cs`, `RendererCore/Scheduler/` (empty)

---

## 1) Purpose

Defines how rendering work is partitioned, scheduled, and budgeted. Currently
the scheduling logic lives inside `GrinFilmCamera.RenderStep()`. The planned
migration would extract this into `RendererCore/Scheduler/` as a standalone
task graph.

---

## 2) Current Execution Model (Implemented)

### 2.1 Frame Pipeline

```
GrinFilmCamera._Process
  → RenderFrameBackend(delta)
      1. SnapshotBuilder.BuildFromGodotScene   (main thread)
      2. CurvatureBoundGrid.BuildAroundCamera  (main thread)
      3. FrameSnapshotBus.Set                  (main thread)
      4. BackendMode dispatch → LegacyBackend.RenderFrame
         → GrinFilmCamera.RenderStep()
            Pass-1: Parallel.For (row band)    (worker threads)
            Pass-2: Sequential                 (main thread)
            Film upload                        (main thread)
```

### 2.2 Row Band Decomposition

Work is partitioned into row bands, not tiles. Each `RenderStep` call
processes a band of rows starting at `_rowCursor` with height determined by:
- `RowsPerFrame` (base)
- Adaptive row sizing targeting `AdaptiveTargetMsPerStep`
- Cap: `UpdateEveryFrameMaxRowsPerStep`

The row cursor advances each call, wrapping at film height to produce
progressive full-frame updates.

### 2.3 Pass-1 (Parallel)

`Parallel.For` across pixels in the current row band. Each pixel:
1. Generate camera ray
2. `BuildRaySegmentsCamera_Pass1` → segment chain
3. Store segments in per-pixel buffer region (disjoint, no contention)
4. `Interlocked` counter merges for telemetry

### 2.4 Pass-2 (Sequential, Main Thread)

For each pixel in the band:
1. Construct segment envelope AABB
2. Query `GeometryTLAS.QueryAabb` for candidates
3. Godot `DirectSpaceState` narrowphase (IntersectRay, IntersectShape, CastMotion)
4. SoftGate subdivide on uncertain misses (optional)
5. Shade and write to film buffer

**Why main thread:** Godot `DirectSpaceState` API is not thread-safe.

---

## 3) Budget and Watchdog System (Implemented)

### 3.1 Budget Parameters

| Parameter | Scope | Effect |
|-----------|-------|--------|
| `UpdateEveryFrameBudgetMs` | Per _Process call | Clamp RenderStep duration |
| `UpdateEveryFrameMaxRowsPerStep` | Per _Process call | Hard cap on row band height |
| `RenderStepMaxMs` | Per RenderStep | Hard time budget; exceeding disables UpdateEveryFrame |
| `RenderStepMaxPixelsPerFrame` | Per RenderStep | Pixel workload cap |
| `RenderStepMaxSegmentsPerFrame` | Per RenderStep | Segment workload cap |
| `RenderStepNoRowProgressRepeatLimit` | Across steps | Stall detection threshold |

### 3.2 SoftGate Budgets

| Parameter | Purpose |
|-----------|---------|
| `Pass2SoftGateMaxAttemptsPerPixel` | Per-pixel subdivide cap |
| `Pass2SoftGateMaxAttemptsPerFrame` | Per-frame attempt cap |
| `Pass2SoftGateMaxSubdividedCallsPerFrame` | Per-frame narrowphase call cap |
| `Pass2SoftGateWatchdogMs` | Timeout per subdivide operation |

### 3.3 Guard Exits

Multiple guard conditions abort the current band early:
- Time budget exceeded
- Pixel/segment budget exceeded
- No row progress (stall detection)
- No hits / no candidates (unhealthy band)
- Re-entry guard (`Interlocked.CompareExchange`)

---

## 4) Thread Safety (Implemented)

- SceneSnapshot is read-only during RenderStep
- Pass-1: disjoint per-pixel buffer writes + Interlocked counter merges
- Pass-2: single-threaded (no concurrent writes)
- Re-entry guard prevents overlapping RenderStep invocations
- Film buffer written sequentially in Pass-2 (no race conditions)

---

## 5) Planned Migration — Task Graph (RendererCore/Scheduler)

### 5.1 Target Stage Graph

```
Stage 0: Build SceneSnapshot          (main thread)
Stage 1: Build Accel (TLAS/BLAS)      (main thread + optional background)
Stage 2: Build FieldSystem caches     (main thread)
--- barrier ---
Stage 3: Tile Integrate               (parallel, per tile)
Stage 4: Tile Intersect + Shade       (parallel, per tile — requires BLAS)
Stage 5: Tile Writeback               (parallel or fused with Stage 4)
--- barrier ---
Stage 6: Present + Telemetry          (main thread)
```

**Prerequisites for migration:**
- BLAS implementation (removes Godot physics → unlocks parallel Pass-2)
- Tile decomposition (replace row bands with rectangular tiles)
- Task abstraction (decouple from GrinFilmCamera method body)

### 5.2 Tile Decomposition (Planned)

Default tile: 16×16 or 32×16 pixels. Stable scanline enumeration for determinism.
Enough tiles to saturate CPU cores while maintaining cache locality.

### 5.3 Work Queue (Planned)

Preferred: work-stealing deques (LIFO local, FIFO steal). Acceptable v1:
global concurrent queue.

### 5.4 Memory Ownership (Planned)

- Read-only shared: SceneSnapshot, TLASes, BLASes, field caches, config
- Per-task exclusive: traversal stack, segment buffers, hit payloads, RNG state
- Film buffer: tile-exclusive writes (no contention)

---

## 6) Progressive Rendering

Current: row bands produce progressive top-to-bottom film updates.
Planned: tile-based progressive with optional centre-first or foveated priority.

---

## 7) Telemetry Integration (Implemented)

- `PerfScope` / `FramePerf`: per-stage timing and counters
- `PerfStats`: rolling-window summaries and invariant checks
- RenderHealth: stall detection, hit-rate, prune behaviour
- Per-band logging via `RenderStepPhaseLog` and `RenderStepBandLog`

---

## 8) Cancellation

Current: `UpdateEveryFrame = false` disables further RenderStep calls.
Re-entry guard auto-disables on contention. Budget overruns disable per-frame
rendering.

Planned: explicit cancellation token in task graph; in-flight tasks exit at
safe points; partial tiles discarded for determinism.
