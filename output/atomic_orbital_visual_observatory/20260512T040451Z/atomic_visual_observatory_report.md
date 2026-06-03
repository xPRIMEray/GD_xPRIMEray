# Atomic Orbital Visual Observatory Report

Interpretation only; not closure validation.

![contact sheet](atomic_visual_contact_sheet.png)

| panel | image | electron_count | radius | strength | modulation | tick | phase |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `V0_baseline_no_field` | atomic_orbital_visual_observatory__v0_baseline_no_field__baseline_prune_off__scheduler-baseline__targetms-1000__stride-1__runid-1.png | 0 | 8.0 | 0 | 0 | 0 | 0 |
| `V1_static_hydrogen` | atomic_orbital_visual_observatory__v1_static_hydrogen__baseline_prune_off__scheduler-baseline__targetms-1000__stride-1__runid-1.png | 1 | 8.0 | 0.05 | 0 | 0 | 0 |
| `V2_exaggerated_hydrogen` | atomic_orbital_visual_observatory__v2_exaggerated_hydrogen__baseline_prune_off__scheduler-baseline__targetms-1000__stride-1__runid-1.png | 1 | 9.0 | 0.1 | 0 | 0 | 0 |
| `V3_tick0` | atomic_orbital_visual_observatory__v3_tick0__baseline_prune_off__scheduler-baseline__targetms-1000__stride-1__runid-1.png | 1 | 8.0 | 0.065 | 0.35 | 0 | -1.570796 |
| `V4_tick1` | atomic_orbital_visual_observatory__v4_tick1__baseline_prune_off__scheduler-baseline__targetms-1000__stride-1__runid-1.png | 1 | 8.0 | 0.065 | 0.35 | 1 | 1.570796 |

## Temporal Beauty Diff

- changed_pixels: 4844
- changed_fraction: 0.021024
- mean_abs_channel_delta: 3.372179
- max_channel_delta: 255
- metrics are descriptive only; they are not pass/fail gates.

## Static Beauty Diffs

These are descriptive image differences against the no-field baseline.

### V0_vs_V1

![V0_vs_V1](V0_vs_V1_scaled_diff.png)

- changed_pixels: 3857
- changed_fraction: 0.016740
- mean_abs_channel_delta: 2.084232
- max_channel_delta: 253
- metrics are descriptive only; they are not pass/fail gates.

### V0_vs_V2

![V0_vs_V2](V0_vs_V2_scaled_diff.png)

- changed_pixels: 4576
- changed_fraction: 0.019861
- mean_abs_channel_delta: 2.533912
- max_channel_delta: 252
- metrics are descriptive only; they are not pass/fail gates.

