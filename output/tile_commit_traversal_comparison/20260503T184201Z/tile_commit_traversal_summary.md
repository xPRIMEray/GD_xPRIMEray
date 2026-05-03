# Tile Commit Traversal Summary

The traversal flag controls render-test pass1 acquisition and pass2 beauty commit/write order. Beauty cells keep diagnostics disabled; corner ROI probes are aggregated from separate post-beauty cells when present.

| step | mode | status | hash | traversal_once | runtime_once | band_% | h_score | v_score | changed_vs_row | max_h_run | clusters | max_tile_run | corner_precision | corner_ownership |
|---:|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0.0125 | `checkerboard` | 0 | `8bf363234fec` | 1 | 0 | 9.8021 | 0.150801 | 0.152288 | 1568 | 160 | 20 | 16 | 0.003125 | 468 |
| 0.0125 | `column` | 0 | `5b6bd8d7e80d` | 1 | 0 | 11.4306 | 0.174364 | 0.174815 | 1216 | 160 | 21 | 16 | 0.003125 | 468 |
| 0.0125 | `row` | 0 | `fbed66c644e3` | 1 | 0 | 12.7604 | 0.191406 | 0.151462 | 0 | 160 | 51 | 16 | 0.003125 | 468 |
| 0.0125 | `tile` | 0 | `3891de97bfb5` | 1 | 0 | 12.9826 | 0.19313 | 0.159259 | 1120 | 160 | 47 | 16 | 0.003125 | 468 |
| 0.015 | `checkerboard` | 0 | `5b331da646a1` | 1 | 0 | 7.8681 | 0.124232 | 0.176797 | 1216 | 128 | 61 | 16 | 0.003125 | 468 |
| 0.015 | `column` | 0 | `687d54a39bef` | 1 | 0 | 10.816 | 0.158283 | 0.176471 | 992 | 160 | 69 | 16 | 0.003125 | 468 |
| 0.015 | `row` | 0 | `850d1768ca48` | 1 | 0 | 9.8715 | 0.144461 | 0.179739 | 0 | 160 | 69 | 16 | 0.003125 | 468 |
| 0.015 | `tile` | 0 | `237efc248897` | 1 | 0 | 10.1493 | 0.147329 | 0.177778 | 896 | 160 | 70 | 16 | 0.003125 | 468 |
