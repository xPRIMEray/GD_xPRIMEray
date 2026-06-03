# Telemetry Adaptive — Four State Warm P80

Short name: **Adaptive Telemetry — Four-State Warm, P80 Threshold**

## Purpose

Tests the four-state adaptive scheduler in "warm" initialization mode with a P80 (80th percentile) curvature threshold. Measures whether warm-starting with prior-pass statistics and a P80 cutoff produces a better curvature/work distribution than cold-start or different percentile thresholds.

## Source / Generation Context

- Scene: `test-curved-minimal-backdrop.tscn`
- Variant: `basic`, four-state warm, P80 threshold

## What the Output Shows

Eight heatmap PNGs (same suite as the baseline) capturing the warm P80 adaptive behavior. `heat_candidates.png` shows the candidate set selected by the adaptive criterion, `heat_work.png` shows total integration work, `heat_curvature_mean.png` tracks mean curvature per pixel.

## Key Files

- `*heat_candidates.png` — Candidate pixel selection under P80 threshold
- `*heat_work.png` — Total work distribution
- `*heat_curvature_mean.png` — Mean curvature heatmap
- `*heat_pass1_steps.png`, `*heat_curvature_max.png`, `*heat_query.png`

## Suggested MisterY Labs Card Summary

Interpretation pending — four-state warm adaptive telemetry with P80 curvature threshold.

## Status

Test output

## Notes / Next Steps

- Compare `heat_candidates.png` to `telemetry_adaptive_regime_*` folders to evaluate threshold sensitivity.
