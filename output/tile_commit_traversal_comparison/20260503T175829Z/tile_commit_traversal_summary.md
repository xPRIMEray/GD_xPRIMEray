# Tile Commit Traversal Summary

The traversal flag controls render-test pass1 acquisition and pass2 beauty commit/write order. Beauty cells keep diagnostics disabled; corner ROI probes are aggregated from separate post-beauty cells when present.

| step | mode | status | hash | traversal_once | runtime_once | band_% | h_score | v_score | changed_vs_row | max_h_run | clusters | max_tile_run | corner_precision | corner_ownership |
|---:|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0.015 | `row` | 0 | `85a4cff79a20` | 1 | 0 | 0.0556 | 0.1 | 0.0125 | 0 | 32 | 1 | 16 | 0.003125 | 360 |
| 0.015 | `tile` | 0 | `c18c4fad04aa` | 1 | 0 | 0.0 | 0.0 | 0.009524 | 384 | 0 | 0 | 0 | 0.003125 | 360 |
