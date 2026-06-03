# Tile Commit Traversal Summary

The traversal flag controls render-test pass1 acquisition and pass2 beauty commit/write order. Beauty cells keep diagnostics disabled; corner ROI probes are aggregated from separate post-beauty cells when present.

| step | mode | status | hash | traversal_once | runtime_once | band_% | h_score | v_score | changed_vs_row | max_h_run | clusters | max_tile_run | corner_precision | corner_ownership |
|---:|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0.015 | `row` | 0 | `36801b346c88` | 1 | 0 | 0.0 | 0.0 | 0.014815 | 0 | 0 | 0 | 0 |  |  |
| 0.015 | `tile` | 0 | `a156e86e0b70` | 1 | 0 | 0.0556 | 0.1 | 0.008642 | 384 | 32 | 1 | 16 |  |  |
