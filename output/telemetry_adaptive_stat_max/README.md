# Telemetry Adaptive — Stat Max

Short name: **Adaptive Telemetry — Max Statistic**

## Purpose

Tests the adaptive scheduler using the per-pixel maximum (rather than mean or p90) as the summary statistic for curvature-based adaptation decisions. Max is most sensitive to outlier high-curvature samples.

## Source / Generation Context

- Scene: `test-curved-minimal-backdrop.tscn`
- Variant: baseline pruning, stat=max

## What the Output Shows

Eight heatmaps including `heat_d2k_max` and `heat_dk_max` (second and first derivative maxima). Compare `heat_resolve.png` and `heat_work_minus_curvature.png` against the mean and p90 variants to see how statistic choice affects work allocation.

## Key Files

- `*heat_resolve.png`, `*heat_d2k_max.png`, `*heat_dk_max.png`
- `*heat_query.png`, `*heat_curvature_max.png`
- `*telemetry_summary.json`

## Suggested MisterY Labs Card Summary

Interpretation pending — adaptive telemetry using max curvature as the summary statistic.

## Status

Test output

## Notes / Next Steps

- Compare to `telemetry_adaptive_stat_mean` and `telemetry_adaptive_stat_p90` for statistic sensitivity.
