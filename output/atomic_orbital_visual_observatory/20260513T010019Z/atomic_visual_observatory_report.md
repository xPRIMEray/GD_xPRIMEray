# Atomic Orbital Visual Observatory Report

Interpretation only; not closure validation.

![contact sheet](atomic_visual_contact_sheet.png)

| shading | panel | image | electron_count | radius | strength | modulation | tick | phase |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `normal_rgb` | `V0_baseline_no_field` | atomic_orbital_visual_observatory__normal_rgb_v0_baseline_no_field__baseline_prune_off__scheduler-baseline__targetms-1000__stride-1__runid-1.png | 0 | 8.0 | 0 | 0 | 0 | 0 |
| `normal_rgb` | `V1_static_hydrogen` | atomic_orbital_visual_observatory__normal_rgb_v1_static_hydrogen__baseline_prune_off__scheduler-baseline__targetms-1000__stride-1__runid-1.png | 1 | 8.0 | 0.05 | 0 | 0 | 0 |
| `normal_rgb` | `V2_exaggerated_hydrogen` | atomic_orbital_visual_observatory__normal_rgb_v2_exaggerated_hydrogen__baseline_prune_off__scheduler-baseline__targetms-1000__stride-1__runid-1.png | 1 | 9.0 | 0.1 | 0 | 0 | 0 |
| `normal_rgb` | `V3_tick0` | atomic_orbital_visual_observatory__normal_rgb_v3_tick0__baseline_prune_off__scheduler-baseline__targetms-1000__stride-1__runid-1.png | 1 | 8.0 | 0.065 | 0.35 | 0 | -1.570796 |
| `normal_rgb` | `V4_tick1` | atomic_orbital_visual_observatory__normal_rgb_v4_tick1__baseline_prune_off__scheduler-baseline__targetms-1000__stride-1__runid-1.png | 1 | 8.0 | 0.065 | 0.35 | 1 | 1.570796 |
| `depth_heatmap` | `V0_baseline_no_field` | atomic_orbital_visual_observatory__depth_heatmap_v0_baseline_no_field__baseline_prune_off__scheduler-baseline__targetms-1000__stride-1__runid-1.png | 0 | 8.0 | 0 | 0 | 0 | 0 |
| `depth_heatmap` | `V1_static_hydrogen` | atomic_orbital_visual_observatory__depth_heatmap_v1_static_hydrogen__baseline_prune_off__scheduler-baseline__targetms-1000__stride-1__runid-1.png | 1 | 8.0 | 0.05 | 0 | 0 | 0 |
| `depth_heatmap` | `V2_exaggerated_hydrogen` | atomic_orbital_visual_observatory__depth_heatmap_v2_exaggerated_hydrogen__baseline_prune_off__scheduler-baseline__targetms-1000__stride-1__runid-1.png | 1 | 9.0 | 0.1 | 0 | 0 | 0 |
| `depth_heatmap` | `V3_tick0` | atomic_orbital_visual_observatory__depth_heatmap_v3_tick0__baseline_prune_off__scheduler-baseline__targetms-1000__stride-1__runid-1.png | 1 | 8.0 | 0.065 | 0.35 | 0 | -1.570796 |
| `depth_heatmap` | `V4_tick1` | atomic_orbital_visual_observatory__depth_heatmap_v4_tick1__baseline_prune_off__scheduler-baseline__targetms-1000__stride-1__runid-1.png | 1 | 8.0 | 0.065 | 0.35 | 1 | 1.570796 |

## NormalRGB Beauty Diffs

These are descriptive image differences from the primary normal_rgb beauty captures.

### V0_vs_V1

![V0_vs_V1](V0_vs_V1_scaled_diff.png)

- changed_pixels: 11512
- changed_fraction: 0.049965
- mean_abs_channel_delta: 5.680874
- max_channel_delta: 235
- metrics are descriptive only; they are not pass/fail gates.

### V0_vs_V2

![V0_vs_V2](V0_vs_V2_scaled_diff.png)

- changed_pixels: 8873
- changed_fraction: 0.038511
- mean_abs_channel_delta: 4.773806
- max_channel_delta: 255
- metrics are descriptive only; they are not pass/fail gates.

### tick0_vs_tick1

![tick0_vs_tick1](tick0_vs_tick1_scaled_diff.png)

- changed_pixels: 12247
- changed_fraction: 0.053155
- mean_abs_channel_delta: 7.242525
- max_channel_delta: 255
- metrics are descriptive only; they are not pass/fail gates.

