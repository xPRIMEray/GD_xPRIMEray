# Telemetry Adaptive — Threshold Fixed 0.8

Short name: **Adaptive Telemetry — Fixed Threshold 0.8**

## Purpose

Tests a fixed curvature threshold of 0.8 (rather than a percentile or adaptive threshold) for candidate selection. Provides a hard-coded comparison point to validate that percentile-based thresholds outperform naive fixed ones.

## Source / Generation Context

- Scene: `test-curved-minimal-backdrop.tscn`
- Variant: baseline pruning, threshold=0.8 (fixed)

## What the Output Shows

Eight heatmaps. The resolve and work heatmaps show what the renderer does when pixels above a fixed curvature threshold are given priority — the key question is whether this fixed level is too aggressive (wasting budget on low-need pixels) or too conservative (missing high-curvature regions).

## Key Files

- `*heat_resolve.png`, `*heat_query.png`, `*heat_d2k_max.png`
- `*telemetry_summary.json`

## Suggested MisterY Labs Card Summary

Interpretation pending — adaptive telemetry with a fixed threshold of 0.8 for candidate selection.

## Status

Test output

## Notes / Next Steps

- Compare to `telemetry_adaptive_threshold_v2` for the follow-up threshold design.
