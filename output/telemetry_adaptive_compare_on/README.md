# Telemetry Adaptive — Compare On

Short name: **Adaptive Telemetry — Adaptation Enabled**

## Purpose

Counterpart to `telemetry_adaptive_compare_baseline`: same scene and heatmaps but with adaptive telemetry enabled. The diff between this folder's heatmaps and the baseline shows what the adaptive system changes about per-pixel work and curvature allocation.

## Source / Generation Context

- Scene: `test-curved-minimal-backdrop.tscn`
- Variant: adaptive telemetry on, baseline pruning
- Part of the adaptive telemetry parameter sweep series

## What the Output Shows

Same eight heatmaps as the baseline (`heat_resolve`, `heat_pass1_steps`, `heat_d2k_max`, `heat_query`, `heat_curvature_max`, `heat_work_minus_curvature`, `heat_dk_max`). Comparing to `telemetry_adaptive_compare_baseline` isolates the effect of enabling adaptation.

## Key Files

- `*heat_resolve.png`, `*heat_curvature_max.png` — Primary heatmaps
- `*telemetry_summary.json` — Aggregate stats

## Suggested MisterY Labs Card Summary

Interpretation pending — adaptive telemetry enabled vs. the reference baseline.

## Status

Test output

## Notes / Next Steps

- Diff all heat\_\*.png images against `telemetry_adaptive_compare_baseline` to quantify adaptive effect.
