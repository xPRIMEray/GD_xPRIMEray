# DOE Sensitivity

Short name: **Sensitivity DOE — Pruning × Stride Full Factorial**

## Purpose

Full-factorial DOE measuring sensitivity of visual output to pruning strategy and stride value. Identifies which parameter combinations produce visually stable output (hash-match) vs. changed pixels, and quantifies the sensitivity gradient.

## Source / Generation Context

- Script: `scripts/run_doe_sensitivity.sh`
- Scene: curved minimal
- Runs: three timestamped under `output/doe_sensitivity/`

## What the Output Shows

`DOE_summary.md` / `.json` / `.csv` table per (pruning, stride) cell. `DOE_sensitivity_plot.png` visualizes changed-pixel count across the parameter space. Tight-env pruning produces the most stable output; loose-env and stride-off variants show higher sensitivity.

## Key Files

- `*/DOE_summary.md` — Human-readable sensitivity table
- `*/DOE_summary.json` — Machine-readable results
- `*/DOE_sensitivity_plot.png` — Sensitivity visualization
- `*/doe_run.log` — Full run log

## Suggested MisterY Labs Card Summary

A systematic sensitivity analysis showing how pruning aggressiveness and render stride jointly affect output stability. The plot identifies the "stability island" — the parameter region where the renderer produces identical output across runs — and maps the sensitivity gradient around it.

## Status

Test output

## Notes / Next Steps

- Cross-reference with `doe_sensitivity_smoke` (early quick run).
- Results informed the default pruning settings in production renders.
