# Basic Visual Review 2026-03-14

## Run setup

- GRIN sweep re-run with `filmOpacity=1.0`, `minRhStep=12`, `minProcessedRows=0`.
- Metric sweep re-run once with the same settings, then one bounded retry with `filmOpacity=1.0`, `minRhStep=20`, `minProcessedRows=64`.
- No FieldMath or transport-law files were changed in this task.

## Output index

- GRIN summary: `logs/grin_basic_visual_sweep/summary.json`
- Metric summary: `logs/metric_basic_visual_sweep/summary.json`
- GRIN contact sheet: `screenshots/grin_basic_visual_sweep/2026-03-14/contact_sheet.png`
- Metric contact sheet: `screenshots/metric_basic_visual_sweep/2026-03-14/contact_sheet.png`

## Capture readout

- GRIN captures all landed at `rhStep=41`; straight reached `processedRows=140`, the curved cases reached `processedRows=160-164`.
- Metric captures also landed at `rhStep=41`, but every curved Metric case stayed at `processedRows=88` even after the bounded retry.
- Result: GRIN is now visually trustworthy enough for comparison. Metric is still not materially limited by capture gating; the retry did not move it into a different visual regime.

## Visual interpretation

### GRIN

- `straight_reference` is clearly distinct: intact horizontal blue bands and regular yellow source dots remain centered and mostly undeformed.
- `minimal_baseline` now reads as a real curved case: the side bands bow inward and the center feature shrinks to a small blue/yellow island.
- `stronger_baseline` separates further from minimal: the side structure thins into sparse orange miss arcs with a much smaller center remnant.
- Straight vs minimal vs stronger is now visually obvious in the GRIN sweep.

### Metric

- `straight_reference` still renders as the same useful control image.
- Every curved Metric frame remains a near-uniform dark panel with only the HUD/debug text readable.
- The stricter retry did not materially change the images; the curved Metric variants remain visually indistinguishable from each other.
- Metric therefore does not yet visibly separate from GRIN in the way this ladder is intended to teach.

## Clearest teaching examples

- GRIN minimal: `minimal_gamma_2p2` is the clearest readable minimal example. It keeps the side bowing obvious while preserving a legible center feature.
- GRIN minimal alternate: `minimal_router_4p5` is also strong if the goal is to emphasize edge-side ringing.
- GRIN stronger: `stronger_router_5p5` is the clearest stronger example. The sparse inner orange arc is readable without the frame collapsing into near-blank white.
- GRIN stronger alternate: `stronger_gamma_2p8` is similarly useful and slightly more symmetric.
- Metric: there is no good teaching variant yet. The curved Metric images are too visually collapsed to distinguish baseline from parameter variants.

## Missing-hit artifact readability

- GRIN: yes, readable enough for tuning now, especially in `minimal_router_4p5`, `minimal_gamma_2p2`, `stronger_router_5p5`, and `stronger_gamma_2p8`.
- Metric: no, not yet. The ringed missing-hit structure is not readable in the current curved Metric outputs.

## Recommended canonical baselines

- GRIN minimal: `minimal_gamma_2p2`
- GRIN stronger: `stronger_router_5p5`
- Metric minimal: `minimal_baseline` (provisional only; not yet a good teaching image)
- Metric stronger: `stronger_baseline` (provisional only; not yet a good teaching image)
