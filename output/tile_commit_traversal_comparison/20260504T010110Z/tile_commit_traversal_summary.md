# Tile Commit Traversal Summary

The traversal flag controls render-test pass1 acquisition and pass2 beauty commit/write order. Beauty cells keep diagnostics disabled; corner ROI probes are aggregated from separate post-beauty cells when present.

| step | mode | status | hash | traversal_once | runtime_once | band_% | h_score | v_score | changed_vs_row | max_h_run | clusters | max_tile_run | corner_precision | corner_ownership |
|---:|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0.015 | `column` | 0 | `975ceca852f5` | 1 | 0 | 19.7899 | 0.235906 | 0.164107 | 1739 | 160 | 245 | 16 |  |  |
| 0.015 | `row` | 0 | `0b2b0617df5b` | 1 | 0 | 20.9635 | 0.249897 | 0.164722 | 0 | 160 | 221 | 16 |  |  |
| 0.015 | `tile` | 0 | `9bf54f4c8ac8` | 1 | 0 | 21.2031 | 0.252752 | 0.166448 | 1667 | 160 | 245 | 16 |  |  |
