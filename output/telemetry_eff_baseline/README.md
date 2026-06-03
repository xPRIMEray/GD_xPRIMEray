# Telemetry Efficiency — Baseline

Short name: **Telemetry Efficiency — Baseline Reference**

## Purpose

Baseline reference for the telemetry efficiency sweep: records the full heatmap suite with no efficiency optimizations applied. All `telemetry_eff_*` variants are compared against this.

## Source / Generation Context

- Scene: `test-curved-minimal-backdrop.tscn`
- Variant: baseline\_prune\_off, no efficiency modification

## What the Output Shows

Eight heatmaps: resolve, pass1\_steps, d2k\_max, query, curvature\_max, work\_minus\_curvature, dk\_max, and efficiency. `telemetry_summary.json` records aggregate statistics. This is the "all work done redundantly" reference state.

## Key Files

- `*heat_resolve.png`, `*heat_query_minus_curvature.png`, `*heat_d2k_max.png`
- `*telemetry_summary.json`

## Suggested MisterY Labs Card Summary

Interpretation pending — telemetry efficiency baseline for the env08/env09 optimization sweep.

## Status

Test output

## Notes / Next Steps

- Compare to `telemetry_eff_env08` and `telemetry_eff_env09` for efficiency gain quantification.
