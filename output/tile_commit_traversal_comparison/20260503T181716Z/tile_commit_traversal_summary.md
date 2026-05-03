# Tile Commit Traversal Summary

The traversal flag controls render-test pass1 acquisition and pass2 beauty commit/write order. Beauty cells keep diagnostics disabled; corner ROI probes are aggregated from separate post-beauty cells when present.

| step | mode | status | hash | traversal_once | runtime_once | band_% | h_score | v_score | changed_vs_row | max_h_run | clusters | max_tile_run | corner_precision | corner_ownership |
|---:|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0.015 | `row` | 0 | `e2030f1e4674` | 1 | 0 | 0.1111 | 0.1 | 0.009877 | 0 | 32 | 2 | 16 | 0.003125 | 360 |
| 0.015 | `tile` | 0 | `77ecb1edf42f` | 1 | 0 | 0.0556 | 0.1 | 0.008642 | 32 | 32 | 1 | 16 | 0.003125 | 360 |
