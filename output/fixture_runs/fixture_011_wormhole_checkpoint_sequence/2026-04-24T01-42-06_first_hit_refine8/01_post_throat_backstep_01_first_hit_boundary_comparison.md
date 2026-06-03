# First-Hit Boundary Comparison

CSV: `output/fixture_runs/fixture_011_wormhole_checkpoint_sequence/2026-04-24T01-42-06_first_hit_refine8/01_post_throat_backstep_01_hit_diagnostics.csv`
Pairs considered: `258450`; boundary sample: `256`
Verdict: **divergence begins at first-hit acquisition**

| pair | final normal Δ | first normal Δ | first seg Δ | final dist Δ | first dist Δ | final cid switch | first cid switch | first cand | overwrite normal Δ |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| (479,74)v(479,75) | 2.000000 | 2.000000 | 14 | 1.375604 | 1.375604 | 1 | 1 | 8/11 | 0.000000 |
| (478,75)h(479,75) | 2.000000 | 2.000000 | 14 | 1.381010 | 1.381010 | 1 | 1 | 8/11 | 0.000000 |
| (478,75)v(478,76) | 2.000000 | 2.000000 | 14 | 1.407570 | 1.407570 | 1 | 1 | 8/11 | 0.000000 |
| (476,76)v(476,77) | 2.000000 | 2.000000 | 14 | 1.405078 | 1.405078 | 1 | 1 | 8/11 | 0.000000 |

## Summary
- `final_normal_delta`: `{'mean': 1.75, 'median': 2.0, 'max': 2.0}`
- `first_normal_delta`: `{'mean': 1.75, 'median': 2.0, 'max': 2.0}`
- `first_segment_jump`: `{'mean': 10.98046875, 'median': 14.0, 'max': 16.0}`
- `final_distance_jump`: `{'mean': 1.1114235000000001, 'median': 1.4033909999999992, 'max': 1.5998}`
- `first_distance_jump`: `{'mean': 1.1114235000000001, 'median': 1.4033909999999992, 'max': 1.5998}`
- `final_collider_switch_rate`: `0.75`
- `first_collider_switch_rate`: `0.75`
- `first_to_final_normal_delta_ratio`: `1.0`
- `boundary_delta_residual_mean`: `0.0`
- `stored_overwrite_normal_delta_mean`: `0.25`
