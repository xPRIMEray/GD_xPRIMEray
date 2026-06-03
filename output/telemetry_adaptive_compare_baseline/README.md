# Telemetry Adaptive — Compare Baseline

Short name: **Adaptive Telemetry — Baseline Reference**

## Purpose

Reference baseline for the adaptive telemetry system: records the full heatmap suite (resolve, pass1\_steps, d2k\_max, query, curvature\_max, work\_minus\_curvature) with no adaptive behavior enabled. All subsequent adaptive variants are compared against this.

## Source / Generation Context

- Scene: `test-curved-minimal-backdrop.tscn`
- Variant: baseline\_prune\_off, scheduler-baseline
- Part of the adaptive telemetry parameter sweep series

## What the Output Shows

Eight heatmap PNGs per run:
- `heat_resolve.png` — Resolution work per pixel
- `heat_pass1_steps.png` — First-pass step count
- `heat_d2k_max.png` — Max second-derivative curvature
- `heat_query.png` — Query count per pixel
- `heat_curvature_max.png` — Max curvature field value
- `heat_work_minus_curvature.png` — Work not accounted for by curvature
- `heat_dk_max.png` — Max first-derivative curvature

`telemetry_summary.json` records aggregate statistics.

## Key Files

- `*heat_resolve.png`, `*heat_curvature_max.png` — Primary diagnostic heatmaps
- `*telemetry_summary.json` — Aggregate stats
- All other `heat_*.png` files

## Suggested MisterY Labs Card Summary

The reference baseline for xPRIMEray's adaptive telemetry system: eight per-pixel work and curvature heatmaps with no adaptation active. These establish what the renderer sees before any adaptive sampling or budget allocation is applied.

## Status

Test output

## Notes / Next Steps

- All other `telemetry_adaptive_*` folders are compared against this. Do not overwrite.
