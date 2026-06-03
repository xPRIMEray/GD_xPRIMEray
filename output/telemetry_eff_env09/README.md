# Telemetry Efficiency — Env 0.9

Short name: **Telemetry Efficiency — Environment Factor 0.9**

## Purpose

Tests the telemetry efficiency system with an environment factor of 0.9 — less aggressive de-prioritization of low-curvature regions than env=0.8. Measures the efficiency gain/loss tradeoff at this higher factor.

## Source / Generation Context

- Scene: `test-curved-minimal-backdrop.tscn`
- Variant: env\_factor=0.9

## What the Output Shows

Eight heatmaps. Compare `heat_resolve.png` and `heat_work_minus_curvature.png` between env=0.8\_fix and env=0.9 to see the sensitivity of efficiency to this factor.

## Key Files

- `*heat_resolve.png`, `*heat_d2k_max.png`, `*heat_query.png`
- `*telemetry_summary.json`

## Suggested MisterY Labs Card Summary

Interpretation pending — telemetry efficiency at environment factor 0.9.

## Status

Test output

## Notes / Next Steps

- See `telemetry_eff_env09_fix` for the corrected version.
