# Curved Minimal Simulated Reorder

## Scope

This note documents an observe-only reorder simulation built on the existing tile-metrics scaffold.

Constraints for this phase:

- no scheduler execution-order changes
- no budget reduction or pruning changes
- subtile width fixed at `8`, following Phase 1.5
- scoring uses empirical subtile metrics only:
  - `hitYield` descending
  - `totalHits` descending
  - `noCandRatio` ascending

Reference artifacts:

- simulation log: `/tmp/curved_minimal_sim_tVDA.log`
- default-off validation log: `/tmp/curved_minimal_sim_off_GtZT.log`

## Command Shape

Observed run:

- `--render-test`
- `--render-test-fixture=curved_minimal`
- `--lifecycle-stress=0`
- `--smartscale=0`
- `--tile-metrics=1`
- `--tile-metrics-subtile-width=8`
- `--tile-metrics-max-logs=4096`
- `--tile-metrics-simulate-reorder=1`

Baseline compatibility check:

- same curved-minimal path with tile metrics and reorder simulation disabled

## Results

Validation status:

- reorder-simulation run passed `renderhealth_regress.py`: `windows=50`, `trusted=10`, `partial=40`, `fail=0`
- default-off baseline also passed unchanged
- default-off log emitted no `[TileMetrics][SimOrder]` or `[TileMetrics][SimSummary]` lines

Observed ordering behavior:

- `1500` per-band `[TileMetrics][SimOrder]` lines were emitted
- `187` frame-level `[TileMetrics][SimSummary]` lines were emitted
- all `563` active bands produced a simulated order different from the actual left-to-right order
- the active region stayed concentrated in the same two adjacent subtiles identified in Phase 1.5: `x=32` and `x=40`

Representative active-band line:

```text
[TileMetrics][SimOrder] step=3 band=0 y=12 h=6 actual=0@0:0/0/0,1@8:0/0/0,2@16:0/0/0,3@24:0/0/0,4@32:0.25/3/0,5@40:0.417/5/0,... simulated=5@40:0.417/5/0,4@32:0.25/3/0,0@0:0/0/0,... totalHits=8 actualTop1Share=0 actualTop2Share=0 simTop1Share=0.625 simTop2Share=1
```

Interpretation:

- actual order spends its first slots on empty subtiles because execution is still left-to-right
- simulated order consistently pulls the productive `x=32` / `x=40` pair to the front
- the tie-break on `noCandRatio` only matters after `hitYield` and `totalHits`; it did not destabilize the top region on this scene

## Cumulative Hit Capture

Every frame-level summary had the same values:

| Metric | Actual Order | Simulated Order |
| --- | ---: | ---: |
| Bands with hits per frame | `3` | `3` |
| Total hits per frame | `30` | `30` |
| Top-1 cumulative hit capture | `0.000` | `0.533` |
| Top-2 cumulative hit capture | `0.000` | `1.000` |
| Top-3 cumulative hit capture | `0.000` | `1.000` |

Why actual capture is `0` here:

- the current execution order is the raw left-to-right subtile order
- for curved-minimal, the first four subtiles in that order are empty on active bands
- the productive subtiles sit later in the band, so a front-of-queue capture metric correctly reports zero early capture for the actual order

## Stability Assessment

The simulated ordering looks stable enough to support a real reorder-only experiment.

Reasons:

- the same two-subtile active region stayed on top across all active bands
- frame summaries were identical rather than noisy or oscillatory
- simulated top-1 capture was consistently strong at `53.3%`
- simulated top-2 capture reached `100%`, meaning the productive region was fully front-loaded within the first two subtiles

This is a good sign for a first execution-order experiment because the simulated gain comes from obvious spatial concentration, not from one-off spikes scattered across many subtiles.

## Recommendation

Proceed to a real `reorder-only` execution experiment.

Why:

- the observe-only simulation shows a large front-loading gain without changing total work
- the top region is spatially compact and stable
- the result is easy to validate because any behavior change in the next phase can be attributed to order alone

Still defer budget reduction:

- the current evidence supports better ordering, not less work
- budget reduction should wait until reorder-only confirms that front-loading preserves output stability under real execution

## Assumptions

- "actual order" means the current scheduler's fixed left-to-right subtile execution order within each band
- cumulative hit-capture metrics intentionally measure front-of-order productivity, not total frame productivity
- curved-minimal remains a narrow-scene characterization target; broader scenes may require a wider sample before locking in a general scheduler policy
