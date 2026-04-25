# First-Hit Boundary Comparison

CSV: `output/fixture_runs/fixture_011_wormhole_checkpoint_sequence/2026-04-23T20-03-13_first_hit_boundary/00_mouth_hit_diagnostics.csv`
Pairs considered: `258229`; boundary sample: `256`
Verdict: **divergence begins at first-hit acquisition**

| pair | final normal Δ | first normal Δ | first seg Δ | final dist Δ | first dist Δ | final cid switch | first cid switch | first cand | overwrite normal Δ |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| (221,72)v(221,73) | 2.000000 | 2.000000 | 50 | 4.995294 | 4.995294 | 1 | 1 | 11/11 | 0.000000 |
| (222,72)v(222,73) | 2.000000 | 2.000000 | 50 | 4.999451 | 4.999451 | 1 | 1 | 11/11 | 0.000000 |
| (223,72)v(223,73) | 2.000000 | 2.000000 | 50 | 5.003665 | 5.003665 | 1 | 1 | 11/11 | 0.000000 |
| (224,72)v(224,73) | 2.000000 | 2.000000 | 50 | 5.007911 | 5.007911 | 1 | 1 | 11/11 | 0.000000 |
| (225,72)v(225,73) | 2.000000 | 2.000000 | 50 | 5.012219 | 5.012219 | 1 | 1 | 11/11 | 0.000000 |
| (226,72)v(226,73) | 2.000000 | 2.000000 | 50 | 5.016596 | 5.016596 | 1 | 1 | 11/11 | 0.000000 |
| (227,72)v(227,73) | 2.000000 | 2.000000 | 50 | 5.021023 | 5.021023 | 1 | 1 | 11/11 | 0.000000 |
| (228,72)v(228,73) | 2.000000 | 2.000000 | 51 | 5.025515 | 5.025515 | 1 | 1 | 11/11 | 0.000000 |
| (229,72)v(229,73) | 2.000000 | 2.000000 | 51 | 5.030075 | 5.030075 | 1 | 1 | 11/11 | 0.000000 |
| (240,72)v(240,73) | 2.000000 | 2.000000 | 51 | 5.086479 | 5.086479 | 1 | 1 | 11/11 | 0.000000 |
| (242,72)v(242,73) | 2.000000 | 2.000000 | 51 | 5.130424 | 5.130424 | 1 | 1 | 11/11 | 0.000000 |
| (242,73)h(243,73) | 2.000000 | 2.000000 | 51 | 5.145874 | 5.145874 | 1 | 1 | 11/11 | 0.000000 |

## Summary
- `final_normal_delta`: `{'mean': 2.0, 'median': 2.0, 'max': 2.0}`
- `first_normal_delta`: `{'mean': 2.0, 'median': 2.0, 'max': 2.0}`
- `first_segment_jump`: `{'mean': 26.79296875, 'median': 15.0, 'max': 71.0}`
- `final_distance_jump`: `{'mean': 2.6852758359375, 'median': 1.5553999999999988, 'max': 7.065346}`
- `first_distance_jump`: `{'mean': 2.6852758359375, 'median': 1.5553999999999988, 'max': 7.065346}`
- `final_collider_switch_rate`: `1.0`
- `first_collider_switch_rate`: `1.0`
- `first_to_final_normal_delta_ratio`: `1.0`
- `boundary_delta_residual_mean`: `0.0`
- `stored_overwrite_normal_delta_mean`: `0.0`
