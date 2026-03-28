# xPRIMEray Multithreading Audit + Roadmap

## Purpose

This note captures the current state of CPU multithreading in the xPRIMEray Godot repo, identifies the safest expansion points, and proposes a minimal-surgery roadmap toward a more explicit job-system style renderer.

---

## Executive Summary

**Current state:**
- The renderer already uses **real CPU multithreading** in the current `Legacy` render path.
- The multithreaded region is primarily **Pass 1**, where per-pixel segment-building work is farmed out across worker threads.
- The overall render loop is **not** yet a full job-system architecture with multiple concurrent render steps.
- **Pass 2** remains intentionally **main-thread constrained** for collision/shading and engine-facing work.

**Interpretation:**
- We are **not starting from zero** on multithreading.
- We already have a strong architectural foothold: **band scheduling + worker pass + main-thread pass + snapshot-style separation**.
- The cleanest path forward is to **formalize Pass 1 as a true job queue / chunk queue**, then progressively push more snapshot-only work into worker execution.

---

## 1. Current Multithreading Audit

### 1.1 What is already multithreaded

The repo already appears to use **multicore CPU work** in `GrinFilmCamera` during **Pass 1**.

Observed architectural traits:
- `RenderStep` performs work in bounded chunks rather than attempting one giant blocking frame.
- The code distinguishes a **worker pass** from a **main-thread pass**.
- Per-pixel / per-band work is distributed using parallel worker execution.
- Shared counters are accumulated using thread-safe aggregation.
- Worker-side state is prepared in thread-local / copied form to avoid unsafe scene access.

### 1.2 What is *not* yet fully multithreaded

The renderer does **not** currently behave like a full asynchronous frame graph or broad job scheduler where multiple render steps execute concurrently.

Observed limitations:
- `RenderStep` is guarded against re-entry.
- The design implies **one active render step at a time**.
- The main-thread portion still performs collision/shading / engine-touching work.
- SceneTree-sensitive operations remain deliberately centralized.

### 1.3 Practical conclusion

The current architecture is best described as:

**Hybrid parallel renderer**
- **Parallel:** Pass 1, per-pixel / per-segment build work
- **Serial:** Pass 2, collision/shading / engine-facing completion work
- **Scheduled:** render bands, row budgets, adaptive chunking, watchdogs

That means the repo already has **multithreaded compute**, but not yet a **fully jobified renderer core**.

---

## 2. Safest Multicore Insertion Point

### 2.1 Best target: Pass 1 expansion

The safest place to add more multicore work is **Pass 1**, specifically:
- curved-ray integration
- field sampling
- segment generation
- pre-hit metadata accumulation
- snapshot-only broadphase preparation

Why this is safest:
- It is already architecturally separated from SceneTree-heavy logic.
- The existing implementation already treats it as worker-friendly.
- It is math-heavy and usually embarrassingly parallel across pixels / ray groups / tiles.
- It already appears to use snapshot-like camera and field inputs.

### 2.2 Second-best target: snapshot-based broadphase / candidate preparation

Any logic that can run purely on immutable data should be the next candidate, for example:
- curvature bound sampling
- field grid lookups
- tile candidate generation
- lightweight broadphase metadata scoring
- precomputed per-tile / per-band work summaries

This is especially attractive because the architecture already seems to be moving toward:
- `SceneSnapshot`
- `CurvatureBoundGrid`
- backend-driven frame preparation

That is fertile soil for worker-thread expansion.

### 2.3 Least safe targets right now

The riskiest areas to parallelize prematurely are:
- direct SceneTree access
- live node traversal
- camera / transform refreshes tied to engine objects
- overlay / film texture updates
- Godot-facing collision/shading logic that depends on engine state

These areas should remain on the main thread unless and until they are fully abstracted behind immutable data structures.

---

## 3. Minimal-Surgery Job-System Roadmap

## Goal

Evolve from:
- **scheduler with internal parallel loop**

to:
- **scheduler that submits explicit render jobs and consumes finished outputs**

without destabilizing current fixtures, watchdogs, or visual baselines.

---

## Phase A: Formalize Pass 1 jobs

### Objective
Turn the current worker region into explicit job units.

### Suggested model
Each job owns a chunk such as:
- one band
- one tile
- one row range
- one pixel block

Each job receives immutable inputs:
- camera snapshot
- field snapshot
- render config snapshot
- frame / band bounds
- filter / plane snapshot

Each job emits:
- segment lists or compact segment buffers
- candidate metadata
- per-job counters
- optional debug summaries

### Why this phase is low risk
- It matches the current architecture closely.
- It keeps Pass 2 behavior intact.
- It preserves your existing watchdog / budget model.
- It creates a future-proof seam for backend evolution.

### Deliverables
- `RenderJob` or equivalent struct/class
- `RenderJobResult`
- worker queue or chunk submission layer
- deterministic ordering rules for result consumption

---

## Phase B: Expand worker-owned pre-pass logic

### Objective
Push more non-engine work into worker jobs.

### Good candidates
- broadphase candidate precomputation
- tile-local candidate caches
- curvature / field summaries
- candidate ranking inputs
- early “no candidate / no hit likely” heuristics

### Benefit
This phase reduces main-thread burden without forcing risky SceneTree interaction changes.

### Constraint
All new worker logic should operate on:
- copied value types
- immutable snapshots
- backend-friendly plain data

not live Godot objects.

---

## Phase C: Introduce explicit job scheduler semantics

### Objective
Upgrade from “parallel loop inside RenderStep” to “RenderStep consumes a managed job queue.”

### Proposed behavior
- RenderStep enqueues work units up to a budget.
- Worker pool processes available jobs.
- Main thread polls completed jobs.
- Pass 2 consumes only completed job outputs.
- Remaining work is preserved across future RenderStep calls.

### Why this matters
This makes the renderer more legible and scalable:
- easier profiling
- better backend separation
- smoother adaptive scheduling
- easier future compute backend experimentation
- more consistent tile/band progress reporting

---

## Phase D: Optional deeper backend split

### Objective
Move toward a true renderer core abstraction that treats Godot as scene-ingest + display shell.

### Possible direction
- front-end: Godot scene ingestion and snapshot creation
- core: renderer jobs on plain data
- back-end: film write / debug output / comparison harness

### Why defer this
This is powerful, but it is **not** the minimal-surgery step. It should come after A/B/C are stable.

---

## 4. Recommended Engineering Rules

To keep the transition clean, adopt these rules during multithreading expansion.

### Rule 1: No SceneTree access in worker jobs
Workers should never directly touch live scene nodes.

### Rule 2: Snapshot everything worker jobs need
If a worker needs it, prefer copying it first.

### Rule 3: Keep job results plain and compact
Prefer small POD-like result payloads where possible.

### Rule 4: Preserve determinism where practical
Especially for regression fixtures and image comparisons.

### Rule 5: Keep Pass 2 behavior stable initially
Avoid moving collision/shading until Pass 1 jobs are mature.

### Rule 6: Instrument every phase
Track:
- job count
- queue depth
- worker occupancy
- job completion latency
- main-thread consume time
- per-band / per-tile timings

---

## 5. Proposed Immediate Next Step

### Best next implementation step
Create a **thin explicit job wrapper** around the current Pass 1 work without changing its math.

In other words:
- keep the same segment-generation logic
- keep the same render outputs
- keep the same guards and budgets
- just package the worker workload into named job units

### Why this is ideal
It gives us:
- cleaner profiling
- clearer architecture
- easier future parallel expansion
- less risk than touching Pass 2
- a concrete base for backend modernization

---

## 6. Suggested Milestone Sequence

### Milestone 1
**Pass 1 Job Wrapper**
- explicit job input/result types
- current logic moved behind job execution
- no visual output changes intended

### Milestone 2
**Chunk Queue + Completion Queue**
- RenderStep submits and drains jobs under existing time budgets

### Milestone 3
**Snapshot-Based Broadphase Expansion**
- more candidate work moved off main thread

### Milestone 4
**Per-Tile / Per-Band Profiling Dashboard**
- expose worker utilization and queue timing

### Milestone 5
**Backend Core Consolidation**
- optional deeper architectural cleanup once behavior is stable

---

## 7. Final Verdict

### Are we already multithreaded?
**Yes.** Pass 1 already uses multicore CPU parallelism.

### Where is the safest place to expand?
**Pass 1 and snapshot-only pre-pass logic.**

### Can the scheduler become a real job system with minimal surgery?
**Yes.** The repo already contains the right structural ingredients. The clean move is to formalize the existing worker pass into explicit jobs instead of trying to parallelize the entire renderer all at once.

---

## One-line recommendation

**Do not rip up the renderer. Promote the current Pass 1 worker path into a first-class job pipeline, keep Pass 2 stable, and let the architecture unfold from there.**

