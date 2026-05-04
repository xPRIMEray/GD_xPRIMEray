# Tile Commit Traversal Summary

The traversal flag controls render-test pass1 acquisition and pass2 beauty commit/write order. Beauty cells keep diagnostics disabled; corner ROI probes are aggregated from separate post-beauty cells when present.

| step | mode | status | hash | traversal_once | runtime_once | band_% | h_score | v_score | changed_vs_row | max_h_run | clusters | max_tile_run | corner_precision | corner_ownership |
|---:|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0.0125 | `checkerboard` | 0 | `8d2ad1ec037b` | 1 | 0 | 19.4375 | 0.241293 | 0.169307 | 1250 | 160 | 193 | 16 | 0.003125 | 468 |
| 0.0125 | `column` | 0 | `4c16556f23eb` | 1 | 0 | 19.1823 | 0.238125 | 0.16708 | 1639 | 160 | 187 | 16 | 0.003125 | 468 |
| 0.0125 | `row` | 0 | `6a2b5257bf53` | 1 | 0 | 19.3941 | 0.240754 | 0.167435 | 0 | 160 | 186 | 16 | 0.003125 | 468 |
| 0.0125 | `tile` | 0 | `b403f75d966a` | 1 | 0 | 19.9149 | 0.24722 | 0.166154 | 1855 | 160 | 194 | 16 | 0.003125 | 468 |
| 0.015 | `checkerboard` | 0 | `367c5e46c33b` | 1 | 0 | 18.9601 | 0.226014 | 0.163294 | 2407 | 132 | 277 | 16 | 0.003125 | 468 |
| 0.015 | `column` | 0 | `975ceca852f5` | 1 | 0 | 19.7899 | 0.235906 | 0.164107 | 1739 | 160 | 245 | 16 | 0.003125 | 468 |
| 0.015 | `row` | 0 | `0b2b0617df5b` | 1 | 0 | 20.9635 | 0.249897 | 0.164722 | 0 | 160 | 221 | 16 | 0.003125 | 468 |
| 0.015 | `tile` | 0 | `9bf54f4c8ac8` | 1 | 0 | 21.2031 | 0.252752 | 0.166448 | 1667 | 160 | 245 | 16 | 0.003125 | 468 |
