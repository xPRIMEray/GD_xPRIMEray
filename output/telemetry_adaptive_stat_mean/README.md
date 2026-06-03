# Telemetry Adaptive — Stat Mean

Short name: **Adaptive Telemetry — Mean Statistic**

## Purpose

Tests the adaptive scheduler using per-pixel mean curvature as the summary statistic. Mean is less sensitive to outliers than max but more stable across passes.

## Source / Generation Context

- Scene: `test-curved-minimal-backdrop.tscn`
- Variant: baseline pruning, stat=mean

## What the Output Shows

Eight heatmaps. `heat_curvature_mean.png` is the primary output specific to this variant. Compare resolve and work heatmaps to the max and p90 variants.

## Key Files

- `*heat_curvature_mean.png`, `*heat_resolve.png`, `*heat_work_minus_curvature.png`
- `*telemetry_summary.json`

## Suggested MisterY Labs Card Summary

Interpretation pending — adaptive telemetry using mean curvature as the summary statistic.

## Status

Test output

## Notes / Next Steps

- Part of the stat sweep: compare to `telemetry_adaptive_stat_max` and `_stat_p90`.
