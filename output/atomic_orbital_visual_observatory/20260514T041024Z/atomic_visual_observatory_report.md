# Atomic Orbital Visual Observatory Report

Interpretation only; not closure validation.

![contact sheet](atomic_visual_contact_sheet.png)

## Contour Overlays

Contours are analytic postprocess guides derived from fixture metadata, not sampled render telemetry.

- mode: `density`
- levels: 6
- note: postprocess overlay only

| shading | panel | image | electron_count | radius | strength | modulation | tick | phase |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `normal_rgb` | `V0_baseline_no_field` | atomic_orbital_visual_observatory__normal_rgb_v0_baseline_no_field__baseline_prune_off__scheduler-baseline__targetms-1000__stride-4__runid-1.png | 0 | 8.0 | 0 | 0 | 0 | 0 |
| `normal_rgb` | `V1_static_hydrogen` | missing | na | na | na | na | na | na |
| `normal_rgb` | `V2_exaggerated_hydrogen` | atomic_orbital_visual_observatory__normal_rgb_v2_exaggerated_hydrogen__baseline_prune_off__scheduler-baseline__targetms-1000__stride-4__runid-1.png | 1 | 9.0 | 0.1 | 0 | 0 | 0 |
| `normal_rgb` | `V3_tick0` | atomic_orbital_visual_observatory__normal_rgb_v3_tick0__baseline_prune_off__scheduler-baseline__targetms-1000__stride-4__runid-1.png | 1 | 8.0 | 0.065 | 0.35 | 0 | -1.570796 |
| `normal_rgb` | `V4_tick1` | atomic_orbital_visual_observatory__normal_rgb_v4_tick1__baseline_prune_off__scheduler-baseline__targetms-1000__stride-4__runid-1.png | 1 | 8.0 | 0.065 | 0.35 | 1 | 1.570796 |
| `depth_heatmap` | `V0_baseline_no_field` | atomic_orbital_visual_observatory__depth_heatmap_v0_baseline_no_field__baseline_prune_off__scheduler-baseline__targetms-1000__stride-4__runid-1.png | 0 | 8.0 | 0 | 0 | 0 | 0 |
| `depth_heatmap` | `V1_static_hydrogen` | missing | na | na | na | na | na | na |
| `depth_heatmap` | `V2_exaggerated_hydrogen` | atomic_orbital_visual_observatory__depth_heatmap_v2_exaggerated_hydrogen__baseline_prune_off__scheduler-baseline__targetms-1000__stride-4__runid-1.png | 1 | 9.0 | 0.1 | 0 | 0 | 0 |
| `depth_heatmap` | `V3_tick0` | atomic_orbital_visual_observatory__depth_heatmap_v3_tick0__baseline_prune_off__scheduler-baseline__targetms-1000__stride-4__runid-1.png | 1 | 8.0 | 0.065 | 0.35 | 0 | -1.570796 |
| `depth_heatmap` | `V4_tick1` | atomic_orbital_visual_observatory__depth_heatmap_v4_tick1__baseline_prune_off__scheduler-baseline__targetms-1000__stride-4__runid-1.png | 1 | 8.0 | 0.065 | 0.35 | 1 | 1.570796 |

## NormalRGB Beauty Diffs

These are descriptive image differences from the primary normal_rgb beauty captures.

### V0_vs_V2

![V0_vs_V2](V0_vs_V2_scaled_diff.png)

- changed_pixels: 39456
- changed_fraction: 0.685000
- mean_abs_channel_delta: 61.453056
- max_channel_delta: 255
- metrics are descriptive only; they are not pass/fail gates.

### tick0_vs_tick1

![tick0_vs_tick1](tick0_vs_tick1_scaled_diff.png)

- changed_pixels: 17408
- changed_fraction: 0.302222
- mean_abs_channel_delta: 17.459259
- max_channel_delta: 255
- metrics are descriptive only; they are not pass/fail gates.

