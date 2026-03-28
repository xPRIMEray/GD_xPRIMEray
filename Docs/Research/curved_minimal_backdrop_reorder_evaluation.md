# Curved Minimal Backdrop Reorder Evaluation

Date: 2026-03-28

## Purpose

Evaluate the existing experimental reorder-only subtile scheduler on `curved_minimal_backdrop` and compare it against the current left-to-right baseline order.

This fixture keeps the original curved-minimal field behavior, but adds a rear detector plane so we can tell whether front-loading benefits apply only to primary-object hits or also to downstream detector/backdrop hits.

## Configuration

- Fixture: `curved_minimal_backdrop`
- Subtile width: `8`
- No budget reduction
- No pruning change
- No neighbor promotion

Baseline capture used the existing observe-only path so the actual baseline order and the scheduler's predicted ranked order could be compared without changing execution:

```text
--tile-metrics=1 --tile-metrics-subtile-width=8 --tile-metrics-simulate-reorder=1
```

Reorder capture used the official experimental scheduler mode:

```text
--tile-metrics=1 --tile-metrics-subtile-width=8 --experimental-subtile-scheduler=1
```

## Stable Summary

Baseline actual order remained fully back-loaded on productive bands:

- `actualTop1Share=0`
- `actualTop2Share=0`
- `actualTop3Share=0`
- `actualPrimaryTop1Share=0`
- `actualBackdropTop1Share=0`
- `actualCombinedTop1Share=0`

The baseline observe-only ranking predicted the same productive front-loading seen earlier in `curved_minimal`:

- `simTop1Share=0.457`
- `simTop2Share=0.864`
- `simTop3Share=0.951`
- `simPrimaryTop1Share=0.173`
- `simBackdropTop1Share=0.284`
- `simCombinedTop1Share=0.457`

The real reorder-only execution matched that prediction after the expected one-frame seed:

- cold start: `framePhase=cold_start_seed_only`, `execTop1Share=0`, `avgFirstHitOrdinal=4.5`
- warm start: `framePhase=warm_start_ranked`, `execTop1Share=0.457`, `execTop2Share=0.864`, `execTop3Share=0.951`
- warm start class split: `execPrimaryTop1Share=0.173`, `execBackdropTop1Share=0.284`, `execCombinedTop1Share=0.457`
- warm start hit timing: `avgFirstHitOrdinal=1`, `avgHit50Ordinal=1.5`

## Interpretation

Reorder-only clearly front-loads both hit classes on this fixture.

- Primary hits are front-loaded: `0 -> 0.173` top-1 share
- Backdrop hits are front-loaded more strongly: `0 -> 0.284` top-1 share
- Combined productive work is front-loaded strongly: `0 -> 0.457` top-1 share

The mode does favor backdrop/detector hits more than primary hits in the first executed subtile, but not in a way that looks pathological. That bias matches the fixture geometry: the most productive early subtiles include a strong downstream detector component after curved deflection.

The class totals from the validated run were:

- `sourceHits=1136`
- `backgroundHits=1934`
- `unclassifiedHits=0`

That makes the fixture useful for distinguishing whether a future scheduler change improves only direct-object capture or also improves downstream intercept capture.

## Comparison To Original Curved Minimal

`curved_minimal_backdrop` preserves the same front-loading signature that justified reorder-only in the original fixture, but it is more informative for next-step scheduler work because it exposes two visible hit classes instead of one combined productive region.

That makes it a better fixture than original `curved_minimal` for answering questions like:

- does the scheduler mainly accelerate primary-object capture?
- does it disproportionately favor downstream detector hits?
- do later heuristic changes change the class balance?

## Recommendation

Reorder-only looks stable enough for broader experimental use.

- The baseline actual order is consistently unproductive at the front of the band
- The predicted ranked order is stable
- The real reorder-only mode matches the prediction on this fixture after the one-frame seed
- Validation remained clean with identical fixture hit totals and `unclassifiedHits=0`

The next scheduler step should be **persistent priors**, not neighbor promotion.

Why:

- this fixture already shows that stable per-subtile priors transfer cleanly from one completed band/frame to the next
- persistent priors build directly on the proven reorder-only mechanism without changing the subtile neighborhood definition
- neighbor promotion introduces a broader heuristic change and is harder to interpret when we now have class-separated outcomes to preserve

Neighbor promotion is still a good later experiment, but it should be compared against reorder-only plus persistent priors rather than replacing that simpler next step.

## Validation

- Baseline observe-only log: `C:\godot\godot_xPRIMEray\logs\codex_curved_minimal_backdrop_baseline_eval_2026-03-28.log`
- Reorder execution log: `C:\godot\godot_xPRIMEray\logs\codex_curved_minimal_backdrop_reorder_eval_2026-03-28.log`
- `renderhealth_regress.py` result: `summary: pass=1 fail=0` for both logs
