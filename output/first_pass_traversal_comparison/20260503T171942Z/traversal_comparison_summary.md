# First-Pass Traversal Comparison

This compares pass1 traversal order only. Hit math, shading, resolver scoring, beauty post-processing, and scheduler decisions are unchanged.

| step | traversal | status | band_% | horizontal | vertical | changed_vs_row | corner_required_precision | corner_ownership_changes |
|---:|---|---:|---:|---:|---:|---:|---:|---:|
| 0.00625 | `checkerboard` | 0 | 5.4167 | 0.09375 | 0.188889 | 4384 | 0.003125 | 572 |
| 0.00625 | `column` | 0 | 5.4167 | 0.09375 | 0.112121 | 4896 | 0.003125 | 572 |
| 0.00625 | `row` | 0 | 18.1701 | 0.215173 | 0.173333 | 0 | 0.003125 | 572 |
| 0.00625 | `tile` | 0 | 5.4167 | 0.09375 | 0.122778 | 4896 | 0.003125 | 572 |
| 0.0125 | `checkerboard` | 0 | 9.8611 | 0.150424 | 0.193651 | 11168 | 0.003125 | 572 |
| 0.0125 | `column` | 0 | 10.2378 | 0.154858 | 0.192857 | 11360 | 0.003125 | 572 |
| 0.0125 | `row` | 0 | 5.4167 | 0.09375 | 0.230864 | 0 | 0.003125 | 572 |
| 0.0125 | `tile` | 0 | 9.1198 | 0.142745 | 0.197009 | 10752 | 0.003125 | 572 |
| 0.015 | `checkerboard` | 0 | 12.7535 | 0.186636 | 0.17735 | 10144 | 0.003125 | 572 |
| 0.015 | `column` | 0 | 8.2708 | 0.127244 | 0.175758 | 12224 | 0.003125 | 572 |
| 0.015 | `row` | 0 | 5.6493 | 0.095035 | 0.198611 | 0 | 0.003125 | 572 |
| 0.015 | `tile` | 0 | 12.1632 | 0.177998 | 0.180342 | 10336 | 0.003125 | 572 |
| 0.018 | `checkerboard` | 0 | 5.1146 | 0.102292 | 0.143519 | 12864 | 0.003125 | 572 |
| 0.018 | `column` | 0 | 4.2569 | 0.092319 | 0.111667 | 15040 | 0.003125 | 572 |
| 0.018 | `row` | 0 | 5.875 | 0.097917 | 0.172222 | 0 | 0.003125 | 572 |
| 0.018 | `tile` | 0 | 4.1892 | 0.090851 | 0.09246 | 15232 | 0.003125 | 572 |

## Interpretation Hooks

- Column traversal rotating or suppressing horizontal bands supports row traversal as an amplifier.
- Tile traversal localizing artifacts supports moving toward tile/domain scheduling.
- Shared corner instability across traversal modes points back to hit/geodesic precision.
