# Tile Commit Traversal Summary

The traversal flag controls render-test pass1 acquisition and pass2 beauty commit/write order. Beauty cells keep diagnostics disabled; corner ROI probes are aggregated from separate post-beauty cells when present.

| step | mode | status | hash | traversal_once | runtime_once | band_% | h_score | v_score | changed_vs_row | max_h_run | clusters | max_tile_run | corner_precision | corner_ownership |
|---:|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0.0125 | `checkerboard` | 0 | `7d2cd303cc12` | 1 | 0 | 20.5799 | 0.255474 | 0.166785 | 2542 | 160 | 191 | 16 | 0.003125 | 468 |
| 0.0125 | `column` | 0 | `c84a442e497a` | 1 | 0 | 20.9149 | 0.259634 | 0.165189 | 2286 | 160 | 185 | 16 | 0.003125 | 468 |
| 0.0125 | `row` | 0 | `1f83d5970f9a` | 1 | 0 | 19.5469 | 0.242651 | 0.167829 | 0 | 160 | 205 | 16 | 0.003125 | 468 |
| 0.0125 | `tile` | 0 | `fd69e4e8b7c4` | 1 | 0 | 18.8264 | 0.233707 | 0.167474 | 3238 | 160 | 173 | 16 | 0.003125 | 468 |
| 0.015 | `checkerboard` | 0 | `f1a313819849` | 1 | 0 | 18.8906 | 0.226688 | 0.162976 | 1116 | 132 | 263 | 16 | 0.003125 | 468 |
| 0.015 | `column` | 0 | `9d5252b7ab7b` | 1 | 0 | 20.3854 | 0.243005 | 0.165734 | 1523 | 160 | 260 | 16 | 0.003125 | 468 |
| 0.015 | `row` | 0 | `50bb36698244` | 1 | 0 | 20.2292 | 0.241142 | 0.164504 | 0 | 160 | 261 | 16 | 0.003125 | 468 |
| 0.015 | `tile` | 0 | `9c8ed515387e` | 1 | 0 | 19.6042 | 0.233692 | 0.162183 | 669 | 132 | 264 | 16 | 0.003125 | 468 |
