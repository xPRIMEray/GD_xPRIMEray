# Telemetry Efficiency — Env 0.8

Short name: **Telemetry Efficiency — Environment Factor 0.8**

## Purpose

Tests the telemetry efficiency system with an environment factor of 0.8, reducing the work budget allocated to low-curvature regions. The factor controls how aggressively the system de-prioritizes "boring" (low-information) pixels.

## Source / Generation Context

- Scene: `test-curved-minimal-backdrop.tscn`
- Variant: env\_factor=0.8

## What the Output Shows

Eight heatmaps. `heat_efficiency.png` (if present) shows per-pixel efficiency gain. Compare `heat_resolve.png` and `heat_work_minus_curvature.png` to the baseline to measure the efficiency improvement at env=0.8.

## Key Files

- `*heat_resolve.png`, `*heat_d2k_max.png`, `*heat_query.png`
- `*telemetry_summary.json`

## Suggested MisterY Labs Card Summary

Interpretation pending — telemetry efficiency at environment factor 0.8.

## Status

Test output

## Notes / Next Steps

- See `telemetry_eff_env08_fix` for the corrected version of this run.
