# Curved Minimal Combined Scheduler + Visual Packet

Date: 2026-03-28

## Evidence Roots

- scheduler-fast compare:
  - `/home/bb/code/godot_xPRIMEray/output/render_test_scheduler_compare/combined_scheduler_fast_2026-03-28`
- visual-check captures:
  - `/home/bb/code/godot_xPRIMEray/output/render_test_visual_compare/combined_evidence_2026-03-28`

## Scheduler-Fast Metrics

Warm-start summaries from the scheduler-fast compare harness:

| Fixture | Mode | Hits | Segments Traced | Hits / Segment Traced | First-Hit Ordinal | Hit50 Ordinal | Top-1 Share | Top-2 Share | Top-3 Share |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| curved_minimal | baseline | `30` | `60,426` | `0.000496` | `5.000` | `5.333` | `0.533` | `1.000` | `1.000` |
| curved_minimal | reorder-only | `30` | `60,426` | `0.000496` | `1.000` | `1.000` | `0.533` | `1.000` | `1.000` |
| curved_minimal | reorder-only + persistent priors | `30` | `60,426` | `0.000496` | `1.000` | `1.000` | `0.533` | `1.000` | `1.000` |
| curved_minimal_backdrop | baseline | `81` | `184,524` | `0.000439` | `4.500` | `5.500` | `0.457` | `0.864` | `0.951` |
| curved_minimal_backdrop | reorder-only | `81` | `184,524` | `0.000439` | `1.000` | `1.500` | `0.457` | `0.864` | `0.951` |
| curved_minimal_backdrop | reorder-only + persistent priors | `81` | `184,524` | `0.000439` | `1.000` | `1.500` | `0.457` | `0.864` | `0.951` |

## Priors Result

Persistent priors were active, but they held rather than improved reorder-only:

- `curved_minimal`:
  - `source=history_plus_prior` logged `560` times
  - `priorContributed=1` logged `560` times
  - `coldStartReduced=1` logged `0` times
- `curved_minimal_backdrop`:
  - `source=history_plus_prior` logged `747` times
  - `priorContributed=1` logged `747` times
  - `coldStartReduced=1` logged `0` times

Interpretation:

- priors were contributing
- they did not change total hits
- they did not change segments traced
- they did not improve hits-per-segment-traced
- they did not reduce first-hit or hit50 ordinal relative to reorder-only
- they did not show cold-start reduction in this packet

## Visual-Check Capture Result

The capture sweep completed for:

- `curved_minimal` baseline / reorder-only / priors
- `curved_minimal_backdrop` baseline / reorder-only / priors

The labeling and capture plumbing worked, and the image sets are on disk.

The first packet exposed a real root cause:

- logs showed `profile=curved_minimal_visual_check`
- but `PrepareRun()` then reapplied the barebones per-run test config
- that restored `resScale=0.25` and `stride=2` before final runtime measurement and capture

Corrected verification now lives under:

- `/home/bb/code/godot_xPRIMEray/output/render_test_visual_compare/corrected_visual_check_2026-03-28`

Corrected runtime proof:

- `curved_minimal` visual-check now logs:
  - `resScale=0.5`
  - `stride=1`
  - `scaledFilm=160x90`
  - `rowsPerStep=90`
- `curved_minimal_backdrop` visual-check now logs:
  - `resScale=0.5`
  - `stride=1`
  - `scaledFilm=160x90`
  - `rowsPerStep=90`

Corrected capture examples:

- `/home/bb/code/godot_xPRIMEray/output/render_test_visual_compare/corrected_visual_check_2026-03-28/images/curved_minimal__visual-check__baseline_prune_off__scheduler-baseline-observe-only__subtile-8__targetms-1000__stride-1__runid-1.png`
- `/home/bb/code/godot_xPRIMEray/output/render_test_visual_compare/corrected_visual_check_2026-03-28/images/curved_minimal_backdrop__visual-check__baseline_prune_off__scheduler-baseline-observe-only__subtile-8__targetms-1000__stride-1__runid-1.png`

## Tooling Notes

- scheduler compare image emission completed
- automated compare entries in `summary.json` were unavailable because local Python image-compare support was missing `numpy`

## Recommendation

Preferred experimental scheduler mode:

- keep `reorder-only` as the preferred experimental mode for now

Reason:

- reorder-only still provides the clear front-loading gain over baseline
- persistent priors held those gains
- but persistent priors did not improve the scheduler metrics in this first packet

Recommended next step:

- priors retuning before neighbor promotion

Reason:

- the corrected visual-check packet is now available separately at:
  - `/home/bb/code/godot_xPRIMEray/Docs/Research/curved_minimal_corrected_evidence_packet_2026-03-28.md`
- visual trust is no longer the main blocker
- the main remaining gap is that persistent priors still do not outperform reorder-only
