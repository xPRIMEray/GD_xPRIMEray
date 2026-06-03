# First Pass Traversal Comparison

Short name: **Traversal Mode — Row vs. Column vs. Tile**

## Purpose

Compares first-pass traversal modes (row-by-row, column-by-column, tile-based) to identify which produces the fastest convergence and fewest visible banding artifacts. Part of the investigation that led to the tile-commit traversal design.

## Source / Generation Context

- Script: `scripts/run_first_pass_traversal_comparison.sh`
- Scene: curved minimal
- Runs: four timestamped under `output/first_pass_traversal_comparison/`

## What the Output Shows

`traversal_comparison_summary.md` / `.json` / `.csv` table comparing traversal modes. `row_vs_column_diff.png` shows pixel differences between modes. `traversal_mode_contact_sheet.png` grids representative frames from each mode. `corner_roi_convergence_by_traversal.png` shows convergence rate in the high-curvature corner ROI per mode.

## Key Files

- `*/traversal_comparison_summary.md` — Mode comparison table
- `*/traversal_mode_contact_sheet.png` — Mode contact sheet
- `*/row_vs_column_diff.png` — Row vs. column pixel diff
- `*/corner_roi_convergence_by_traversal.png` — Corner ROI convergence by mode
- `*/traversal_comparison.log` — Full run log

## Suggested MisterY Labs Card Summary

A head-to-head comparison of three first-pass traversal strategies — row, column, and tile — showing how traversal order affects convergence speed and banding artifacts in curved-field renders. The corner ROI convergence plot reveals which mode resolves the hardest pixels fastest.

## Status

Test output

## Notes / Next Steps

- Results motivated the tile-commit design. See `tile_commit_traversal_comparison` for the tile-commit follow-up.
