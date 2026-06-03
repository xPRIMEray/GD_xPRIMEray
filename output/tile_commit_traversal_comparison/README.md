# Tile Commit Traversal Comparison

Short name: **Tile-Commit Traversal — Mode Comparison**

## Purpose

Compares tile-commit traversal against row and checkerboard traversal to confirm that tile-commit improves convergence at the cost of coherence locality. Nine runs across multiple traversal configurations provide statistical confidence in the comparison.

## Source / Generation Context

- Script: `scripts/run_tile_commit_traversal_comparison.sh`
- Scene: curved minimal
- Runs: nine timestamped under `output/tile_commit_traversal_comparison/`

## What the Output Shows

`tile_commit_traversal_summary.md` / `.json` / `.csv` table per traversal mode. `row_vs_tile_diff.png` and `row_vs_checkerboard_diff.png` show pixel differences between modes. `traversal_contact_sheet.png` grids representative frames. `band_support_by_mode.png` shows how different modes cover the high-curvature band in early passes.

## Key Files

- `*/tile_commit_traversal_summary.md` — Comparison table
- `*/traversal_contact_sheet.png` — Mode contact sheet
- `*/row_vs_tile_diff.png` / `row_vs_checkerboard_diff.png` — Pixel diffs
- `*/band_support_by_mode.png` — Band coverage by traversal mode
- `*/tile_commit_traversal.log` — Full run log

## Suggested MisterY Labs Card Summary

A nine-run comparison of tile-commit, row, and checkerboard traversal strategies. The band-coverage plot shows that tile-commit reaches the high-curvature regions in fewer passes — the key advantage for interactive render convergence.

## Status

Test output

## Notes / Next Steps

- See `tile_commit_traversal_repeatability` for the determinism check.
- Tile-commit is now the default for production renders.
