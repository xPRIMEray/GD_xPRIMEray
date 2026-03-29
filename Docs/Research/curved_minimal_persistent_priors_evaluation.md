# Curved Minimal Persistent Priors Evaluation

Date: 2026-03-28

## Scope

Implement a cautious persistent-priors extension on top of the existing validated reorder-only scheduler mode.

Constraints preserved:

- feature-flagged and default-off
- reorder-only path preserved as the comparison baseline
- no budget reduction
- no pruning changes
- no neighbor promotion
- width-`8` subtiles remain the recommended scheduler granularity
- code changes kept local to the current scheduler and tile-metrics path

## Implementation Summary

The new extension is enabled with:

```text
--experimental-subtile-scheduler=1 --tile-metrics-persistent-priors=1
```

Behavior:

- current reorder-only scheduling still uses stable spatial band history keyed by `y` and `height`
- persistent priors add a second memory layer keyed by stable spatial subtile identity: `y`, `height`, `x`, `width`
- priors decay on every update with a fixed `0.85` multiplier
- priors blend into current-band ranking with a fixed cautious max weight of `0.25`
- if no current-band history exists, priors can seed a ranking without changing the work budget

Retuned 2026-03-28 pass:

- decay raised from `0.85` to `0.92`
- prior blend cap raised from `0.25` to `0.6`
- weak-current bands can receive an extra cautious boost
- priors can now seed from nearby stable slices within a small same-column vertical neighborhood instead of only exact band matches

Logging additions:

- per-band execution logs now include:
  - `segmentsTraced`
  - `hitsPerSegmentTraced`
  - `currentEvidenceShare`
  - `priorEvidenceShare`
  - `priorContributed`
  - `coldStartReduced`
- frame summaries now include:
  - `hits`
  - `segmentsTraced`
  - `hitsPerSegmentTraced`
  - `avgFirstHitOrdinal`
  - `avgHit50Ordinal`
  - `top-1`, `top-2`, `top-3` capture share
  - counts for prior contribution and prior-only cold-start reduction

## Comparison Plan

Run:

```powershell
python tools/render_test_scheduler_compare.py --godot-exe "C:\path\to\godot_console.exe"
```

The harness now captures all three required modes:

1. `baseline`
2. `reorder-only`
3. `reorder-only-persistent-priors`

For baseline, the harness uses observe-only ranking telemetry so the actual baseline execution stays unchanged while still emitting comparable scheduler summaries.

## Current Status

Implementation status:

- complete
- `dotnet build` passes
- compare script compiles with `python3 -m py_compile`
- local runtime capture completed on 2026-03-28 with `./scripts/godot_local.sh`

Primary scheduler-fast evidence packet:

- compare root:
  - `/home/bb/code/godot_xPRIMEray/output/render_test_scheduler_compare/combined_scheduler_fast_2026-03-28`
- summary:
  - `/home/bb/code/godot_xPRIMEray/output/render_test_scheduler_compare/combined_scheduler_fast_2026-03-28/summary.json`

Important tooling note:

- scheduler compare image emission completed
- automated image comparison inside `summary.json` remained unavailable because local Python image-compare support was missing `numpy`
- the compare helper still crashes when trying to write markdown for `compare_unavailable` pairs, so the retuned pass below was summarized directly from the raw logs

Retuned scheduler-fast evidence packet:

- compare root:
  - `/home/bb/code/godot_xPRIMEray/output/render_test_scheduler_compare/retuned_priors_scheduler_fast_2026-03-28`

Diagnosis packet after the retune:

- compare root:
  - `/home/bb/code/godot_xPRIMEray/output/render_test_scheduler_compare/priors_diagnosis_scheduler_fast_2026-03-28`
- note:
  - `/home/bb/code/godot_xPRIMEray/Docs/Research/curved_minimal_persistent_priors_diagnosis.md`

## Reference Baseline From Prior Validated Notes

Existing validated steady-state reference points already in the repo:

- `curved_minimal` reorder-only warm start:
  - `execTop1Share=0.533`
  - `execTop2Share=1.000`
  - `avgFirstHitOrdinal=1.000`
  - `avgHit50Ordinal=1.000`
- `curved_minimal_backdrop` reorder-only warm start:
  - `execTop1Share=0.457`
  - `execTop2Share=0.864`
  - `execTop3Share=0.951`
  - `avgFirstHitOrdinal=1.000`
  - `avgHit50Ordinal=1.500`

Those remain the baseline experimental references that persistent priors should now be compared against.

## Before / After Table

Scheduler-fast warm-start summaries after the 2026-03-28 retune:

| Fixture | Mode | Hits | Segments Traced | Hits / Segment Traced | First-Hit Ordinal | Hit50 Ordinal | Top-1 Share | Top-2 Share | Top-3 Share | Notes |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| curved_minimal | baseline | `30` | `60,426` | `0.000496` | `5.000` | `5.333` | `0.533` | `1.000` | `1.000` | observe-only telemetry on unchanged baseline execution |
| curved_minimal | reorder-only | `30` | `60,426` | `0.000496` | `1.000` | `1.000` | `0.533` | `1.000` | `1.000` | strong front-loading preserved |
| curved_minimal | reorder-only + persistent priors | `30` | `60,426` | `0.000496` | `1.000` | `1.000` | `0.533` | `1.000` | `1.000` | retuned priors now reach true cold-start use, but still no net metric lift |
| curved_minimal_backdrop | baseline | `81` | `184,524` | `0.000439` | `4.500` | `5.500` | `0.457` | `0.864` | `0.951` | observe-only telemetry on unchanged baseline execution |
| curved_minimal_backdrop | reorder-only | `81` | `184,524` | `0.000439` | `1.000` | `1.500` | `0.457` | `0.864` | `0.951` | strong front-loading preserved |
| curved_minimal_backdrop | reorder-only + persistent priors | `81` | `184,524` | `0.000439` | `1.000` | `1.500` | `0.457` | `0.864` | `0.951` | retuned priors now reach true cold-start use, but still no net metric lift |

## Observed Prior Contribution

Persistent-priors logging did confirm that the cautious memory layer was participating:

- original cautious priors packet:
  - `curved_minimal`: `priorContributed=560`, `persistent_prior=0`, `coldStartReduced=0`
  - `curved_minimal_backdrop`: `priorContributed=747`, `persistent_prior=0`, `coldStartReduced=0`
- retuned priors packet:
  - `curved_minimal`: `priorContributed=563`, `persistent_prior=3`, `coldStartReduced=3`
  - `curved_minimal_backdrop`: `priorContributed=751`, `persistent_prior=4`, `coldStartReduced=4`

So the retune did improve behavioral influence:

- priors now genuinely seed some cold-start-ranked bands
- nearby stable slices are being used
- the scheduler is no longer just logging a weak auxiliary prior

But the measured scheduler outcomes still did not move.

## Follow-Up Diagnosis

The follow-up diagnosis packet confirmed that the next limiter is not prior strength anymore.

What changed:

- the retuned priors now improve the first cold-start hit-bearing frame in both fixtures
- `curved_minimal` cold-start summary moved from `avgFirstHitOrdinal=5` / `avgHit50Ordinal=5.333` to `2.333` / `2.667`
- `curved_minimal_backdrop` cold-start summary moved from `4.5` / `5.5` to `1.75` / `2.5`

What still did not change:

- the final scheduler-fast warm summaries still matched reorder-only exactly
- all three modes still finished with matching render-health totals on both fixtures

Most likely reason:

- the fixtures expose only a tiny cold-start window
- after that, almost all hit-bearing work is already warm-ranked by fresh current-band history
- reorder-only is already effectively saturated in that warm regime, so priors have almost no remaining room to improve the aggregate packet

The full diagnosis note lives at:

- `/home/bb/code/godot_xPRIMEray/Docs/Research/curved_minimal_persistent_priors_diagnosis.md`

## Recommendation

Preferred experimental mode today:

- keep plain reorder-only as the preferred experimental scheduler mode

Reason:

- persistent priors held the reorder-only gains
- but they still did not improve `hitsPerSegmentTraced`
- they still did not improve `firstHitOrdinal` or `hit50Ordinal`
- and the new cold-start reduction behavior was too small to affect the fixture-level summary metrics

Updated conditional recommendation:

- only promote persistent priors over reorder-only after a follow-up run shows a real gain in either:
  - `hitsPerSegmentTraced`
  - cold-start reduction
  - or visual trustworthiness under a true higher-resolution comparison path

Corrected visual trust path status:

- now available
- see `/home/bb/code/godot_xPRIMEray/Docs/Research/curved_minimal_corrected_evidence_packet_2026-03-28.md`
- the corrected visual-check packet did not reveal a hidden instability or regression for priors
- but it also did not change the recommendation, because scheduler metrics still only hold

## Next Step Recommendation

Recommended next step after this evidence packet:

1. harden the fixture or add a more spatially ambiguous comparison layout before doing another large priors retune
2. keep the current diagnostic split so cold-start-only gains stay visible
3. only return to another priors refinement after a harder packet shows there is still unrealized room in the cold-start phase

Reason:

- the scheduler metrics are real and useful
- the corrected visual-check capture workflow is now functional
- the retune successfully made priors matter earlier
- but the diagnosis run showed that the fixtures equalize too quickly for that gain to move the aggregate packet

## Recommendation

Status recommendation:

- persistent priors should remain experimental

Do not replace reorder-only yet because:

- the retune improved the behavior of the priors path
- but it still failed to beat reorder-only on the scheduler-fast metrics that matter

Do not abandon priors yet because:

- the retune finally produced `persistent_prior` and `coldStartReduced=1`
- that means the design now has a real lever to refine instead of being purely inert
