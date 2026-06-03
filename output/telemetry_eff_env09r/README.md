# Telemetry Efficiency — Env 0.9 Repeat

Short name: **Telemetry Efficiency — Env 0.9 Repeatability**

## Purpose

Repeatability check for `telemetry_eff_env09_fix`. Confirms env=0.9 produces deterministic heatmaps across independent runs.

## Source / Generation Context

- Scene: `test-curved-minimal-backdrop.tscn`
- Variant: env\_factor=0.9, repeat run

## What the Output Shows

Same eight heatmaps as `telemetry_eff_env09_fix`. Hash-match confirms determinism.

## Key Files

- `*heat_resolve.png`, `*heat_d2k_max.png`, `*heat_query.png`
- `*telemetry_summary.json`

## Suggested MisterY Labs Card Summary

Interpretation pending — repeatability check for env=0.9 efficiency run.

## Status

Test output

## Notes / Next Steps

- Compare hash to `telemetry_eff_env09_fix` to confirm determinism.
