# Curved Minimal Corrected Evidence Packet

Date: 2026-03-28

## Output Roots

- scheduler-fast metrics:
  - `/home/bb/code/godot_xPRIMEray/output/render_test_scheduler_compare/combined_scheduler_fast_2026-03-28`
- corrected visual-check captures:
  - `/home/bb/code/godot_xPRIMEray/output/render_test_visual_compare/corrected_full_packet_2026-03-28`

## Scheduler-Fast Metric Summary

These metrics come from the unchanged scheduler-fast compare packet. No scheduler behavior changed between that packet and the corrected visual-check reruns.

| Fixture | Mode | Hits | Segments Traced | Hits / Segment Traced | First-Hit Ordinal | Hit50 Ordinal | Top-1 Share | Top-2 Share | Top-3 Share |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| curved_minimal | baseline | `30` | `60,426` | `0.000496` | `5.000` | `5.333` | `0.533` | `1.000` | `1.000` |
| curved_minimal | reorder-only | `30` | `60,426` | `0.000496` | `1.000` | `1.000` | `0.533` | `1.000` | `1.000` |
| curved_minimal | reorder-only + persistent priors | `30` | `60,426` | `0.000496` | `1.000` | `1.000` | `0.533` | `1.000` | `1.000` |
| curved_minimal_backdrop | baseline | `81` | `184,524` | `0.000439` | `4.500` | `5.500` | `0.457` | `0.864` | `0.951` |
| curved_minimal_backdrop | reorder-only | `81` | `184,524` | `0.000439` | `1.000` | `1.500` | `0.457` | `0.864` | `0.951` |
| curved_minimal_backdrop | reorder-only + persistent priors | `81` | `184,524` | `0.000439` | `1.000` | `1.500` | `0.457` | `0.864` | `0.951` |

## Priors Result

Persistent priors still hold rather than improve reorder-only:

- `curved_minimal`:
  - `priorContributed=1` seen `560` times
  - `coldStartReduced=1` seen `0` times
- `curved_minimal_backdrop`:
  - `priorContributed=1` seen `747` times
  - `coldStartReduced=1` seen `0` times

Interpretation:

- priors are active
- priors do not regress the validated reorder-only gains
- priors do not improve hits, segments traced, hits-per-segment-traced, first-hit ordinal, or hit50 ordinal in this packet

## Corrected Visual-Check Verification

Corrected visual-check mode is now genuinely active at runtime:

- `curved_minimal` logs:
  - `profile=curved_minimal_visual_check`
  - `resScale=0.5`
  - `stride=1`
  - `scaledFilm=160x90`
  - `rowsPerStep=90`
- `curved_minimal_backdrop` logs:
  - `profile=curved_minimal_visual_check`
  - `resScale=0.5`
  - `stride=1`
  - `scaledFilm=160x90`
  - `rowsPerStep=90`

Representative corrected captures:

- `/home/bb/code/godot_xPRIMEray/output/render_test_visual_compare/corrected_full_packet_2026-03-28/images/curved_minimal__visual-check-baseline__baseline_prune_off__scheduler-baseline-observe-only__subtile-8__targetms-1000__stride-1__runid-1.png`
- `/home/bb/code/godot_xPRIMEray/output/render_test_visual_compare/corrected_full_packet_2026-03-28/images/curved_minimal__visual-check-reorder-only__baseline_prune_off__scheduler-reorder-only__subtile-8__targetms-1000__stride-1__runid-1.png`
- `/home/bb/code/godot_xPRIMEray/output/render_test_visual_compare/corrected_full_packet_2026-03-28/images/curved_minimal__visual-check-priors__baseline_prune_off__scheduler-reorder-only-persistent-priors__subtile-8__targetms-1000__stride-1__runid-1.png`
- `/home/bb/code/godot_xPRIMEray/output/render_test_visual_compare/corrected_full_packet_2026-03-28/images/curved_minimal_backdrop__visual-check-baseline__baseline_prune_off__scheduler-baseline-observe-only__subtile-8__targetms-1000__stride-1__runid-1.png`
- `/home/bb/code/godot_xPRIMEray/output/render_test_visual_compare/corrected_full_packet_2026-03-28/images/curved_minimal_backdrop__visual-check-reorder-only__baseline_prune_off__scheduler-reorder-only__subtile-8__targetms-1000__stride-1__runid-1.png`
- `/home/bb/code/godot_xPRIMEray/output/render_test_visual_compare/corrected_full_packet_2026-03-28/images/curved_minimal_backdrop__visual-check-priors__baseline_prune_off__scheduler-reorder-only-persistent-priors__subtile-8__targetms-1000__stride-1__runid-1.png`

Visual readout:

- the corrected high-resolution captures do not reveal an obvious instability or regression hidden by scheduler-fast mode
- reorder-only and reorder-only-plus-priors remain visually trustworthy relative to baseline in this packet
- the corrected visual-check path is now suitable as the inspection companion for future scheduler evidence packets

## Tooling Note

- scheduler compare image emission is working
- automated image comparison in `summary.json` is still unavailable locally because Python image comparison depends on missing `numpy`

## Recommendation

Preferred experimental scheduler mode:

- keep `reorder-only` as the preferred experimental mode

Reason:

- it preserves the front-loading gains over baseline
- persistent priors hold those gains
- but persistent priors do not yet show a measurable improvement

Recommended next step:

- priors retuning before neighbor promotion

Reason:

- the corrected visual-check path is now trustworthy enough
- image-compare environment cleanup is useful but non-blocking
- neighbor promotion would add another scheduler variable before persistent priors have shown any clear win
