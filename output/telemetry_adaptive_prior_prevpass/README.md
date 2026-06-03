# Telemetry Adaptive — Prior Prevpass

Short name: **Adaptive Telemetry — Previous-Pass Prior**

## Purpose

Tests the adaptive scheduler initialized from the previous pass's statistics as a prior (prevpass prior mode). Measures whether using the prior pass's curvature distribution as a warm start improves adaptation speed vs. a cold start.

## Source / Generation Context

- Scene: `test-curved-minimal-backdrop.tscn`
- Variant: `basic`, prior from prevpass

## What the Output Shows

Same eight-heatmap suite. The `heat_candidates.png` and `heat_curvature_mean.png` are the primary comparison targets against cold-start and P80-threshold variants.

## Key Files

- `*heat_candidates.png`, `*heat_work.png`, `*heat_curvature_mean.png`
- `*heat_pass1_steps.png`, `*heat_curvature_max.png`, `*heat_query.png`

## Suggested MisterY Labs Card Summary

Interpretation pending — previous-pass prior adaptive telemetry variant.

## Status

Test output

## Notes / Next Steps

- Compare candidate heatmap to `telemetry_adaptive_four_state_warm_p80` to evaluate prevpass-prior vs. percentile-threshold initialization.
