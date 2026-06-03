# First-Hit Boundary Comparison

CSV: `output/fixture_runs/fixture_011_wormhole_checkpoint_sequence/2026-04-24T01-42-06_first_hit_refine8/00_mouth_hit_diagnostics.csv`
Pairs considered: `258221`; boundary sample: `256`
Verdict: **divergence begins at first-hit acquisition**

| pair | final normal Δ | first normal Δ | first seg Δ | final dist Δ | first dist Δ | final cid switch | first cid switch | first cand | overwrite normal Δ |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| (221,72)v(221,73) | 2.000000 | 2.000000 | 50 | 4.995294 | 4.995294 | 1 | 1 | 11/11 | 0.000000 |
| (222,72)v(222,73) | 2.000000 | 2.000000 | 50 | 4.999450 | 4.999450 | 1 | 1 | 11/11 | 0.000000 |
| (223,72)v(223,73) | 2.000000 | 2.000000 | 50 | 5.003665 | 5.003665 | 1 | 1 | 11/11 | 0.000000 |
| (224,72)v(224,73) | 2.000000 | 2.000000 | 50 | 5.007911 | 5.007911 | 1 | 1 | 11/11 | 0.000000 |

## Summary
- `final_normal_delta`: `{'mean': 2.0, 'median': 2.0, 'max': 2.0}`
- `first_normal_delta`: `{'mean': 2.0, 'median': 2.0, 'max': 2.0}`
- `first_segment_jump`: `{'mean': 26.6015625, 'median': 15.0, 'max': 71.0}`
- `final_distance_jump`: `{'mean': 2.6660281328125, 'median': 1.5545000000000009, 'max': 7.065336}`
- `first_distance_jump`: `{'mean': 2.6660281328125, 'median': 1.5545000000000009, 'max': 7.065336}`
- `final_collider_switch_rate`: `1.0`
- `first_collider_switch_rate`: `1.0`
- `first_to_final_normal_delta_ratio`: `1.0`
- `boundary_delta_residual_mean`: `0.0`
- `stored_overwrite_normal_delta_mean`: `0.0`
