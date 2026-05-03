# First-Pass Traversal Comparison

This compares pass1 traversal order only. Hit math, shading, resolver scoring, beauty post-processing, and scheduler decisions are unchanged.

| step | traversal | status | band_% | horizontal | vertical | changed_vs_row | corner_required_precision | corner_ownership_changes |
|---:|---|---:|---:|---:|---:|---:|---:|---:|
| 0.015 | `column` | 0 | 0.1181 | 0.10625 | 0.04321 | 448 | 0.003125 | 360 |
| 0.015 | `row` | 0 | 0.059 | 0.10625 | 0.047531 | 0 | 0.003125 | 360 |

## Interpretation Hooks

- Column traversal rotating or suppressing horizontal bands supports row traversal as an amplifier.
- Tile traversal localizing artifacts supports moving toward tile/domain scheduling.
- Shared corner instability across traversal modes points back to hit/geodesic precision.
