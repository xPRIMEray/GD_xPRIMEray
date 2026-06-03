# Telemetry Adaptive — Stat P90

Short name: **Adaptive Telemetry — P90 Statistic**

## Purpose

Tests the adaptive scheduler using the 90th-percentile curvature as the summary statistic. P90 is a robust high-curvature indicator that suppresses noise better than max while still responding to the upper tail.

## Source / Generation Context

- Scene: `test-curved-minimal-backdrop.tscn`
- Variant: baseline pruning, stat=p90

## What the Output Shows

Eight heatmaps. Compare `heat_resolve.png` and `heat_work_minus_curvature.png` to the mean and max variants to evaluate tail-sensitivity tradeoffs.

## Key Files

- `*heat_resolve.png`, `*heat_d2k_max.png`, `*heat_curvature_max.png`
- `*telemetry_summary.json`

## Suggested MisterY Labs Card Summary

Interpretation pending — adaptive telemetry using P90 curvature as the summary statistic.

## Status

Test output

## Notes / Next Steps

- P90 is often the best balance in adaptive sampling. Compare to max and mean.
