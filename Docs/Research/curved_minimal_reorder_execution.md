# Curved Minimal Reorder-Only Execution

## Scope

This note evaluates a real reorder-only execution experiment built on the existing tile-metrics scaffold.

Constraints preserved:

- feature-flagged and default-off
- subtile width fixed at `8`
- no budget reduction
- no pruning changes
- no cross-band scheduler redesign
- same subtile set and same total work, with execution order changed only inside each scan band

Reference artifacts:

- baseline-off validation log: `/tmp/curved_minimal_reorder_off_MSdh.log`
- final reorder-on validation log: `/tmp/curved_minimal_reorder_on3_Yvk5.log`
- earlier observe-only reference: `/tmp/curved_minimal_sim_tVDA.log`

## Execution Model

The reorder-only path uses the same empirical priority contract as the observe-only simulation:

- `hitYield` descending
- `totalHits` descending
- `noCandRatio` ascending

Important implementation detail:

- ordering history is keyed by the band's spatial slice (`y`, `height`), not by the transient band index
- this keeps the `y=12`, `y=18`, and `y=24` active bands from borrowing each other's history

## Validation Status

- default-off curved-minimal regression still passed unchanged: `pass=1 fail=0`
- default-off log emitted no `[TileMetrics][ExecOrder]` or `[TileMetrics][ExecSummary]` lines
- reorder-on curved-minimal regression also passed: `pass=1 fail=0`

So the reorder-only execution experiment preserved the current validated behavior envelope on this path.

## Real Execution Results

Frame 1 is a cold-start seed frame:

- no prior band history exists yet
- execution remains left-to-right
- first summary therefore reports no front-loading gain
- in current logs this appears as `framePhase=cold_start_seed_only` and `rankActive=0`

Cold-start summary:

| Metric | Value |
| --- | ---: |
| `bandsWithHits` | `3` |
| `totalHits` | `30` |
| `execTop1Share` | `0.000` |
| `execTop2Share` | `0.000` |
| `avgFirstHitOrdinal` | `5.000` |
| `avgHit50Ordinal` | `5.333` |

After that seed frame, the real execution settles into a stable pattern for the remaining `186` frame summaries:

| Metric | Legacy Front-Load | Reordered Front-Load |
| --- | ---: | ---: |
| Top-1 cumulative hit share | `0.000` | `0.533` |
| Top-2 cumulative hit share | `0.000` | `1.000` |
| Top-3 cumulative hit share | `0.000` | `1.000` |
| Avg first-hit ordinal | `5+` effective | `1.000` |
| Avg 50%-hit ordinal | `5+` effective | `1.000` |

Warm-start interpretation:

- once the band has one completed history sample, active-band logs switch to `phase=warm_start_ranked`
- frame summaries switch to `framePhase=warm_start_ranked` with `rankActive=1`
- these warm-start summaries are the ones that should be compared to observe-only simulation results

Representative steady-state active-band lines:

```text
[TileMetrics][ExecOrder] step=11 band=0 y=12 h=6 mode=experimental_reorder_only phase=warm_start_ranked rankActive=1 ... totalHits=8 ... execTop1Share=0.625 execTop2Share=1 firstHitOrdinal=1 hit50Ordinal=1 source=history
[TileMetrics][ExecOrder] step=12 band=0 y=18 h=6 mode=experimental_reorder_only phase=warm_start_ranked rankActive=1 ... totalHits=18 ... execTop1Share=0.5 execTop2Share=1 firstHitOrdinal=1 hit50Ordinal=1 source=history
[TileMetrics][ExecOrder] step=13 band=0 y=24 h=6 mode=experimental_reorder_only phase=warm_start_ranked rankActive=1 ... totalHits=4 ... execTop1Share=0.5 execTop2Share=1 firstHitOrdinal=1 hit50Ordinal=1 source=history
```

## Comparison To Observe-Only Simulation

Observe-only prediction from Phase 1.5 / simulation:

- `simTop1Share=0.533`
- `simTop2Share=1.000`

Real reorder-only steady state:

- `execTop1Share=0.533`
- `execTop2Share=1.000`

Conclusion:

- real execution behaved as predicted once per-band history was keyed correctly
- the reorder-only gain is not just a post-hoc scoring artifact; it survives actual traversal reordering
- the productive region still concentrates in the same two subtiles (`x=32` and `x=40`)

## Recommendation

Recommended next step: `1. persist reorder-only`

Why:

- it matches the observe-only prediction almost exactly in steady state
- it front-loads useful work immediately without changing total work
- it passed the curved-minimal validation path unchanged
- it gives a clean new baseline for future experiments

Why not `2. add neighbor promotion` yet:

- the current two-subtile active region is already fully captured within the first two reordered subtiles
- neighbor promotion would add policy complexity before this simpler reorder-only baseline is fully socialized and reused

Why not `3. begin cautious budget reduction` yet:

- current evidence proves ordering benefit, not a safe reduction threshold
- budget reduction should come only after reorder-only is treated as the new stable baseline and compared on more than one scene

## Assumptions

- the first frame of each run is expected to remain effectively baseline because reorder-only needs one completed pass of per-band history before it can rank subtiles
- "legacy" front-load share is the current left-to-right subtile order, used as the baseline comparison inside the same band
