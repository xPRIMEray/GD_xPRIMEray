# Phase 3 Roadmap — Tile Scheduler Research Path

**Date:** 2026-05-18  
**Status:** Active — Phase 3.0 in progress  
**Invariant:** Every phase must pass the hermetic closure gate before proceeding.

---

## Goal

Introduce spatial structure into the traversal scheduler without changing rendering
decisions, step budget, or transport semantics. Each phase is a minimal increment that
can be independently validated against the hermetic closure gate.

The long-term direction (v0.2+ Oracle Scheduler) is documented in
[ORACLE_SCHEDULER_V02_ARCHITECTURE.md](../Docs/Research/ORACLE_SCHEDULER_V02_ARCHITECTURE.md).
This roadmap covers only the Phase 3 research path leading toward that direction.

---

## Phase 3.0 — Tile Scheduler v0: Traversal Order Only

**Goal:** Prove that 2D tile-major traversal order does not break hermetic closure.
No adaptive budget. No oracle feedback. No new rendering decisions.

**What changes:**
- `RenderTestFirstPassTraversalMode = "tile"` in new test scenes (existing export, not new code)
- `[TileScheduler]` log line in `GrinBasicVisualController` (mode + traversalRowsCompleted)
- `--tile` flag in `tools/hermetic_observatory_observe.py` (selects tile-mode case list)
- Four new test scenes: tile-mode hermetic pairs (quick + full) for straight and GRIN

**What does not change:**
- Rendering decisions, shading, resolver
- Global step budget (`StepsPerRay`)
- Existing row-mode scenes and validation
- TileMetrics scaffold flags (remain false)
- ObjectSeededTileScheduler (not invoked)

**Gate:** Tile-mode hermetic observatory — both straight and GRIN must achieve
`missHits == 0` and `traversalRowsCompleted == filmHeight`.

**Status:** In progress.

---

## Phase 3.1 — 2D Tile Completion Bitmask

**Goal:** Replace the 1D `_fixtureRowsCompleted` row bitmask with a 2D tile completion
structure. Track per-tile traversal completion independently of the row cursor.

**What changes:**
- `GrinFilmCamera`: new `_fixturetilesCompleted` 2D bitmask (width_tiles × height_tiles)
- Per-tile marking in `ForEachTraversalSampleOrigin` (tile mode only)
- New diagnostics field: `traversalTilesCompleted` / `traversalTilesTotal` in `FixtureWriteDiagnosticsSnapshot`
- `[TileScheduler]` log line updated to emit `tilesCompleted/tilesTotal`
- Python validator updated to report tile completion ratio

**What does not change:**
- Row bitmask retained for row-mode compatibility
- No budget changes
- No ordering policy changes

**Gate:** Same hermetic closure gate. Additionally: `traversalTilesCompleted == traversalTilesTotal`
at capture time.

**Status:** Deferred — after Phase 3.0 passes.

---

## Phase 3.2 — Risk-Scored Tile Ordering

**Goal:** First adaptive step. Order tile visits by a risk score derived from a
coarse first-pass color gradient map. High-gradient tiles visited earlier in subsequent
passes. No budget change — still uniform step allocation.

**What changes:**
- Coarse first-pass (low step budget) over all tiles to build a gradient map
- Risk score per tile: color gradient magnitude + geometry discontinuity presence
- Tile visit queue sorted by risk score for refinement pass
- `[TileScheduler]` log extended with `riskMode`, `highRiskTileCount`, `visitOrder`

**What does not change:**
- Step budget per tile remains uniform
- Hermetic closure gate: unchanged (risk ordering must not introduce escapes)
- Resolver, shading, transport physics

**Gate:** Hermetic closure (`missHits == 0`) + render output visually comparable to
row-mode reference (no regression in background hit distribution).

**Status:** Deferred — after Phase 3.1.

---

## Phase 3.3 — Spatial Budget Allocation

**Goal:** Allocate step budget per-tile proportional to risk score. High-risk tiles
get additional step budget; low-risk tiles get reduced budget. Total budget conserved
globally.

**What changes:**
- Per-tile `StepsPerRay` derived from risk score
- Global `StepsPerRay` becomes a budget envelope, not a per-ray constant
- New `budgetExhaustedPixels` tracking per tile
- `[TileScheduler]` log extended with per-tile budget statistics

**Gate:** Hermetic closure under variable budget. High-amplitude GRIN field variants
may now pass that previously budget-exhausted (this is progress, not regression).

**Status:** Deferred — after Phase 3.2.

---

## Phase 4 (Future) — Full Oracle Scheduler v0.2+

Documented in [ORACLE_SCHEDULER_V02_ARCHITECTURE.md](../Docs/Research/ORACLE_SCHEDULER_V02_ARCHITECTURE.md).

Key additions beyond Phase 3.3:
- Oracle path replay in unresolved island neighborhoods
- Transport ownership boundary refinement
- High-curvature region detection (per-ray bend-rate logging)
- Voxel / field-cell adaptive resolution
- Coarse-to-fine 3-pass model with oracle guardrail

The oracle guardrail constraint: oracle path outputs never feed back into rendering
decisions. Oracle is an analysis instrument, not a renderer.

**Status:** Architecture documented. Implementation not scheduled.

---

## Invariant

Every phase must pass the hermetic closure gate before proceeding:

```
missHits == 0
traversalRowsCompleted == filmHeight
tracedPixels > 0
```

If any phase introduces `missHits > 0`, that is a hard blocker — not a warning,
not a known limitation. Traversal order, budget allocation, and oracle scheduling
must never break closed-box transport.

---

## References

- Hermetic closure baseline: [PHASE2_HERMETIC_CLOSURE_MILESTONE.md](PHASE2_HERMETIC_CLOSURE_MILESTONE.md)
- Oracle architecture: [ORACLE_SCHEDULER_V02_ARCHITECTURE.md](../Docs/Research/ORACLE_SCHEDULER_V02_ARCHITECTURE.md)
- Oracle direction note: [ORACLE_SCHEDULER_V02_DIRECTION.md](ORACLE_SCHEDULER_V02_DIRECTION.md)
- Validation tool: `tools/hermetic_observatory_observe.py`
- Run script: `scripts/run_hermetic_observatory_full_pixel.sh`
