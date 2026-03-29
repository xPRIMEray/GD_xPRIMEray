# Curved Minimal Persistent Priors Diagnosis

Date: 2026-03-28

## Goal

Identify the next limiting factor preventing retuned persistent priors from beating reorder-only on scheduler-fast metrics.

## Diagnostic Packet

- compare root:
  - `/home/bb/code/godot_xPRIMEray/output/render_test_scheduler_compare/priors_diagnosis_scheduler_fast_2026-03-28`
- command:
  - `python3 tools/render_test_scheduler_compare.py --godot-exe ./scripts/godot_local.sh --timestamp priors_diagnosis_scheduler_fast_2026-03-28`

Tooling caveat:

- the compare run completed the renderer work and wrote the logs and captures
- the helper still crashed late while building markdown because `compare_unavailable` pairs are missing `leftMode` / `rightMode`
- that did not block the diagnosis below

## Minimal Instrumentation Added

Added localized execution-summary counters in `/home/bb/code/godot_xPRIMEray/GrinFilmCamera.cs` so the scheduler logs now split hit-bearing work into:

- `seedHitShare`
- `rankedHitShare`
- `priorOnlyHitShare`
- `seedSegmentShare`
- `rankedSegmentShare`
- `priorOnlySegmentShare`
- `top1OrderChangedBandsWithHits`
- `top1HitImprovedBandsWithHits`

This instrumentation is diagnostic-only and stays inside the tile-metrics path.

## Key Finding

The next limiting factor is not weak priors anymore.

The strongest remaining bottleneck is that the fixtures expose only a very small cold-start window, while almost all later hit-bearing work is already warm-ranked by fresh current-band history. Once that warm-start phase begins, reorder-only and retuned priors converge to the same ranking and the same total downstream work.

## Evidence

Cold-start improvement is real:

- `curved_minimal`, first hit-bearing summary:
  - reorder-only: `framePhase=cold_start_seed_only`, `seedHitShare=1`, `avgFirstHitOrdinal=5`, `avgHit50Ordinal=5.333`
  - priors: `framePhase=cold_start_prior_ranked`, `priorOnlyHitShare=1`, `avgFirstHitOrdinal=2.333`, `avgHit50Ordinal=2.667`
- `curved_minimal_backdrop`, first hit-bearing summary:
  - reorder-only: `framePhase=cold_start_seed_only`, `seedHitShare=1`, `avgFirstHitOrdinal=4.5`, `avgHit50Ordinal=5.5`
  - priors: `framePhase=cold_start_prior_ranked`, `priorOnlyHitShare=1`, `avgFirstHitOrdinal=1.75`, `avgHit50Ordinal=2.5`

But that improvement is tiny in scope:

- `curved_minimal` priors packet:
  - `source=persistent_prior`: `3`
  - `phase=cold_start_prior_ranked`: `3`
  - `phase=warm_start_ranked`: `560`
- `curved_minimal_backdrop` priors packet:
  - `source=persistent_prior`: `4`
  - `phase=cold_start_prior_ranked`: `4`
  - `phase=warm_start_ranked`: `747`

So priors help only the first `3` or `4` cold-start hit-bearing bands, then nearly all remaining useful work comes from the same warm-ranked regime that reorder-only already uses successfully.

Warm-start saturation is the reason the fixture-level metrics hold:

- `curved_minimal`, final warm summary:
  - reorder-only: `rankedHitShare=1`, `execTop1Share=0.533`, `avgFirstHitOrdinal=1`, `avgHit50Ordinal=1`
  - priors: identical
- `curved_minimal_backdrop`, final warm summary:
  - reorder-only: `rankedHitShare=1`, `execTop1Share=0.457`, `avgFirstHitOrdinal=1`, `avgHit50Ordinal=1.5`
  - priors: identical

Downstream work is also effectively invariant across modes:

- `curved_minimal`: all three modes ended with identical `RenderHealth[GeomCoverage]` totals, including `geomSegQueriedRaw=196228` and `p2SampRaw=24528`
- `curved_minimal_backdrop`: all three modes ended with identical `RenderHealth[GeomCoverage]` totals, including `geomSegQueriedRaw=455392` and `p2SampRaw=56924`

That strongly suggests the unchanged totals are being dominated by the same pass structure and fixed-budget follow-through, not by a ranking failure in the retuned priors path.

## Interpretation

Most likely bottleneck, in order:

1. fixture simplicity / insufficient spatial ambiguity after the first cold-start bands
2. scheduler-fast aggregate metrics are dominated by later warm-ranked work that equalizes
3. `segmentsTraced` is mostly insensitive here because reorder-only does not prune or reduce budget, and pass-level work stays effectively the same

Less likely as the next problem:

- priors still being too weak
- a pass1/pass2 bug specific to priors

The pass structure matters, but the diagnosis already points to where it matters: it equalizes total work after the short cold-start phase rather than exposing a new priors-specific regression.

## Recommended Next Narrow Experiment

Recommendation: `fixture hardening / more ambiguous target layout`

Why:

- the current fixtures do show a real cold-start improvement from priors
- but they leave too little ambiguous early work for that gain to survive into the aggregated scheduler-fast metrics
- reorder-only is already at the floor on `curved_minimal` once warm history exists

Suggested experiment shape:

- keep the same scheduler logic
- add or choose a fixture variant with more than `3` to `4` hit-bearing bands and weaker immediate band-to-band self-similarity
- rerun the same baseline / reorder-only / priors scheduler-fast packet

If the team wants to stay on the current fixtures first, the fallback narrow experiment would be a cold-start-only metric packet rather than another blind priors retune.

