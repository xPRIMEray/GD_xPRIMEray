# Tile Insertion Points

## Current Scheduling Flow

The current render scheduler is still scanline-band based and lives in [`GrinFilmCamera.cs`](/home/bb/code/godot_xPRIMEray/GrinFilmCamera.cs).

Key flow inside `RenderStep()`:

1. Resolve the next band from `_rowCursor`, `rowsPerFrame`, pending-pass2 state, and watchdog/budget gates.
2. Run pass 1 integration for the selected rows and cache per-pixel segment data into `_segBuf`, `_segCountPerPixel`, `_pass1StoppedEarly`, and `_pass1HitSegIndex`.
3. Run pass 2 on the main thread for the same band:
   - broadphase policy selection
   - optional TLAS candidate pruning
   - quick-ray / overlap / subdivided narrowphase
   - shading / pixel fill / overlay collection
4. Advance `_rowCursor` or mark the band incomplete when a budget/watchdog gate fires.
5. In the `finally` block, record one `RenderHealthSample` for the step and update live/test-facing health state.

Important existing anchors:

- Band selection / row advancement: `RenderStep()` around the `yStart`, `yEnd`, `_rowCursor`, and `FinalizeBandAndAdvance(...)` flow.
- Pass 1 ownership: the pass-1 block immediately before the `// ---- PASS 2` section.
- Pass 2 ownership: the band-local loops over `y`, `x`, and segment index `si`.
- Metrics/logging sink: `RecordRenderHealthSample(...)` and `LogRenderHealth(...)`.

## Recommended Insertion Points

### 1. Screen-Space Tile Descriptors

Best insertion point: immediately after the current band is chosen, before pass 1 begins.

Why:

- The scheduler already has a stable unit of work there: `yStart..yEnd` plus full film width.
- A tile descriptor can be introduced without changing render order.
- The descriptor can later grow from "full-width band tile" to sub-band screen tiles without rewriting pass logic first.

Recommended first descriptor shape:

- `stepIndex`
- `bandIndex`
- `x`, `y`, `width`, `height`
- `fullWidthBand`

That is enough to preserve current scanline semantics while creating a future seam for adaptive tile queues.

### 2. Per-Tile Metrics

Best insertion points:

- Initialize per-tile counters at pass-2 band start.
- Increment them inside the existing pass-2 loops where candidate gathering and ray tests already happen.
- Flush them beside `RecordRenderHealthSample(...)` in the `finally` block.

Why:

- Pass 2 already contains the useful work-to-hit signals:
  - traced pixels
  - hits
  - candidate references
  - candidate-bearing vs no-candidate pixels
  - geometry ray tests
- The `finally` block is the safest place to emit the sample because it runs for normal completion, budget stop, and watchdog paths.

Phase 0 used the entire band as one tile.

Phase 1 refines that into fixed-width horizontal subtiles within the same band and records:

- `rays`
- `hits`
- `candRefs`
- `candSegs`
- `candPx`
- `noCandPx`
- `geomPx`
- `geomRayTests`

Phase 1 differences from Phase 0:

- one band now emits `N` subtile samples instead of one aggregate sample
- scheduler order is unchanged; only the accounting granularity changed
- each subtile sample carries a stable id plus explicit `x/y/w/h` bounds
- per-subtile yield can now be compared across the width of a single band, which Phase 0 could not show

This is enough to derive:

- rays per hit
- candidate checks per hit
- no-candidate ratio
- per-tile hit yield

### 3. Optional Field-Aware Priority Scoring

Best insertion point: the scheduler decision point before `RenderStep()` commits to `yStart..yEnd`.

Why:

- Priority scoring should choose which tile to run next, not mutate pass-1/pass-2 behavior mid-tile.
- This keeps baseline transport and collision behavior unchanged.
- A future score can combine:
  - recent tile hit yield
  - no-candidate ratio
  - field curvature / field magnitude heuristics
  - previous-frame temporal stability

Recommended first phase:

- compute score only
- log score only
- do not reorder work until validation confirms stability

### 4. Debug / CSV Export

Best insertion points:

- Emit a structured one-line debug log when a tile sample is finalized.
- Add CSV export later in the same post-sample path, or extend `tools/renderhealth_parse.py` with a sibling tile parser.

Why:

- The project already prefers structured log lines plus offline parsing.
- Keeping export after tile finalization avoids partial rows and duplicate writes on budget exits.
- This also keeps validation runs compatible with current log-capture workflows.

Current scaffold follows this pattern with `[TileMetrics] ...` log lines, now one line per subtile rather than one line per whole band.

## Minimal Safe Implementation Plan

1. Introduce a small tile descriptor and tile metric sample type only.
2. Treat each existing full-width band as the scheduler-owned container for a fixed set of horizontal subtiles.
3. Add a feature flag so the scaffold is completely off by default.
4. Count tile-local metrics inside pass 2 only; do not alter pass 1 or shading decisions.
5. Emit structured tile logs from the same end-of-step path that already records `RenderHealth`.
6. After logs are stable, add a parser/export path before attempting any scheduler changes.
7. Only after baseline data exists, add field-aware score calculation in observe-only mode.
8. Reordering or adaptive subdivision should be a later track, gated behind a separate feature flag.

## What Must Remain Unchanged For Baseline Compatibility

- `_rowCursor` advancement semantics
- current band sizing from `rowsPerFrame` / pending-pass2 state
- pass-1 segment generation
- pass-2 broadphase, narrowphase, soft-gate, and shading decisions
- `RenderHealth` fields and trust-gating behavior
- existing render-test / fixture commands and default logging

In practice, the safe rule is:

- instrumentation may observe existing band work
- instrumentation must not choose different work yet

## Assumptions

- The existing band scheduler is the correct temporary tile unit for phase 1 instrumentation.
- "Candidate checks" are best represented initially by accumulated candidate references (`candRefs`) with segment count (`candSegs`) logged alongside them.
- CSV export is better staged as a parser follow-up than as direct file I/O in the render loop.
