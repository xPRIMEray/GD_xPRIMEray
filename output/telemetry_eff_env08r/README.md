# Telemetry Efficiency — Env 0.8 Repeat

Short name: **Telemetry Efficiency — Env 0.8 Repeatability**

## Purpose

Repeatability check for `telemetry_eff_env08_fix`: re-runs env=0.8 (corrected) to confirm deterministic output. Ensures the efficiency numbers are stable and not subject to run-to-run variation.

## Source / Generation Context

- Scene: `test-curved-minimal-backdrop.tscn`
- Variant: env\_factor=0.8, repeat run

## What the Output Shows

Same eight heatmaps as `telemetry_eff_env08_fix`. Hash-match against the fix run confirms determinism.

## Key Files

- `*heat_resolve.png`, `*heat_d2k_max.png`, `*heat_query.png`
- `*telemetry_summary.json`

## Suggested MisterY Labs Card Summary

Interpretation pending — repeatability check for env=0.8 efficiency run.

## Status

Test output

## Notes / Next Steps

- Compare hash to `telemetry_eff_env08_fix` to confirm determinism.
