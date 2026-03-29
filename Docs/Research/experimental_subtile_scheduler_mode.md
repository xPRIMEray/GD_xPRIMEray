# Experimental Subtile Scheduler Mode

## Purpose

This note promotes the existing reorder-only subtile execution path into the official experimental scheduler mode for xPRIMEray.

The mode remains:

- feature-flagged
- default-off
- local to subtile ordering within the current scan band

The persistent-priors extension is also:

- feature-flagged
- default-off
- additive on top of reorder-only, not a replacement for it

## What The Mode Does

When enabled, the film pass still processes the same scan bands and the same width-`8` horizontal subtiles, but it changes the order in which those subtiles are visited inside each band.

Priority scoring is empirical and unchanged:

1. `hitYield` descending
2. `totalHits` descending
3. `noCandRatio` ascending

Current recommended subtile width: `8`

Why `8`:

- it is the first width that exposes meaningful horizontal structure on `curved_minimal`
- it avoids the over-fragmentation seen at width `4`
- it already validated well for reorder-only execution

## Cold-Start Expectation

The first frame of a run is expected to behave like baseline.

Reason:

- the scheduler mode uses one completed pass of per-band subtile history before it can rank the next pass for that same band
- after that seed frame, curved-minimal has been stable in validation

How this appears in logs:

- seed-only frames report `framePhase=cold_start_seed_only`
- ranked frames report `framePhase=warm_start_ranked`
- individual active bands report `phase=cold_start_seed` or `phase=warm_start_ranked`

Interpretation:

- the first summary in a validation run should usually be the seed-only summary
- the next summaries are the meaningful scheduler measurements
- if a run stays seed-only, ranking never became active and the run should not be treated as a scheduler-quality result

## What Remains Unchanged

This official experimental mode does **not** currently do any of the following:

- no budget reduction
- no pruning changes
- no neighbor promotion
- no cross-band scheduler redesign

It is reorder-only execution inside the current band structure.

## Persistent Priors Extension

The cautious persistent-priors extension keeps the same reorder-only ranking contract but adds a lightweight decaying memory keyed by stable spatial slice identity:

- key: `y`, `height`, `x`, `width`
- not keyed by transient band index
- stored as a decayed per-subtile prior over recent rays, hits, and no-candidate coverage

Current fixed safeguards:

- decay factor per update: `0.85`
- max prior blend weight into a current-history rank: `0.25`
- current-band evidence remains dominant when it exists
- if priors are disabled, the reorder-only path is unchanged

Effect on ranking:

1. current-band history still ranks first when available
2. persistent priors can contribute a small blended tie-break/boost
3. if current-band history is absent but a prior exists, priors can seed a cautious cold-start ranking

How this appears in logs:

- `mode=experimental_reorder_only_persistent_priors`
- `source=history_plus_prior` when current-band evidence ranked first and priors contributed
- `source=persistent_prior` when a band had no current-band history and priors reduced cold-start dependence
- `priorContributed=1` and `coldStartReduced=1` make those cases explicit in per-band logs
- frame summaries add `priorBandsWithHits`, `priorOnlyBandsWithHits`, and `priorContribBandsWithHits`

The extension still does **not** do:

- no budget reduction
- no pruning changes
- no neighbor promotion
- no broader cross-band scheduler redesign

## Current Validation Status

Curved-minimal validation has already shown:

- default-off baseline remains unchanged
- reorder-only steady state matches the earlier observe-only prediction
- steady-state front-load capture:
  - `execTop1Share=0.533`
  - `execTop2Share=1.000`
  - `execTop3Share=1.000`
  - `avgFirstHitOrdinal=1`
  - `avgHit50Ordinal=1`

Typical summary wording:

```text
[TileMetrics][ExecSummary] mode=experimental_reorder_only framePhase=cold_start_seed_only rankActive=0 ...
[TileMetrics][ExecSummary] mode=experimental_reorder_only framePhase=warm_start_ranked rankActive=1 ...
```

## Future Work Should Build On This Mode

Recommended sequence:

1. treat this reorder-only mode as the official experimental scheduler baseline
2. expand validation to additional scenes using the same mode
3. only then consider neighbor promotion or cautious budget reduction

The key point is to build on this mode, not bypass it: future scheduler work should compare against this reorder-only baseline first.
