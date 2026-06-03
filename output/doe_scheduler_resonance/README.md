# DOE Scheduler Resonance

Short name: **Scheduler DOE — Stride × Mode Resonance**

## Purpose

Investigates whether certain scheduler stride values produce systematic banding artifacts ("resonance") by sweeping stride and scheduler mode. The band-by-row heatmap reveals whether banding patterns are periodic (resonance) or random (noise).

## Source / Generation Context

- Script: `scripts/run_doe_scheduler_resonance.sh`
- Scene: curved minimal
- Runs: four timestamped under `output/doe_scheduler_resonance/`

## What the Output Shows

`scheduler_DOE_summary.md` / `.json` / `.csv` table across stride × scheduler mode cells. `scheduler_stride_plot.png` plots band score vs. stride. `band_by_row_mod_stride_heatmap.png` and `horizontal_band_score_plot.png` reveal row-mod-stride patterns that indicate resonance between the scanline evaluator and row-skip cadence.

## Key Files

- `*/scheduler_DOE_summary.md` — Summary table
- `*/scheduler_stride_plot.png` — Band score vs. stride
- `*/band_by_row_mod_stride_heatmap.png` — Resonance heatmap
- `*/horizontal_band_score_plot.png` — Horizontal band artifact score
- `*/scheduler_doe.log` — Full run log

## Suggested MisterY Labs Card Summary

A DOE sweep probing whether certain render stride values create visible banding through a resonance effect with the scheduler's row-skip cadence. The heatmap shows the periodic structure clearly — and guides which stride values are safe to use in production renders.

## Status

Test output

## Notes / Next Steps

- Findings fed into the tile scheduler design (object-seeded tile avoids row-aligned stride).
- Archive these results as the evidence base for the stride safety recommendation.
