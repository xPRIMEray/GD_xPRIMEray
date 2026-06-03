# Telemetry Adaptive — Threshold v2

Short name: **Adaptive Telemetry — Threshold Design v2**

## Purpose

Second iteration of the adaptive threshold design, incorporating lessons from the fixed-0.8 threshold test. V2 likely uses a dynamic or percentile-based threshold rather than the fixed 0.8 cutoff.

## Source / Generation Context

- Scene: `test-curved-minimal-backdrop.tscn`
- Variant: baseline pruning, threshold=v2

## What the Output Shows

Eight heatmaps. The key comparison to `telemetry_adaptive_threshold_fixed08` reveals whether the v2 threshold design produces a better work/curvature balance.

## Key Files

- `*heat_resolve.png`, `*heat_d2k_max.png`, `*heat_query.png`
- `*telemetry_summary.json`

## Suggested MisterY Labs Card Summary

Interpretation pending — adaptive threshold v2, the follow-up to the fixed-0.8 threshold experiment.

## Status

Test output

## Notes / Next Steps

- Extract the threshold formula from the code and document it alongside these results.
