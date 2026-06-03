# Telemetry Adaptive — Regime No Relax

Short name: **Adaptive Telemetry — No Relaxation**

## Purpose

Tests the adaptive scheduler with relaxation disabled (α=1.0 or equivalent "no decay"). Without relaxation, each pass's prior is fully replaced by current evidence, giving maximum responsiveness at the cost of potential oscillation between passes.

## Source / Generation Context

- Scene: `test-curved-minimal-backdrop.tscn`
- Variant: `basic`, neutral regime, no relaxation

## What the Output Shows

Eight heatmaps. Without relaxation, `heat_candidates.png` may show noisier selection or sharp shifts between passes compared to the α=0.85/0.90 variants.

## Key Files

- `*heat_candidates.png`, `*heat_work.png`, `*heat_curvature_mean.png`

## Suggested MisterY Labs Card Summary

Interpretation pending — no-relaxation adaptive telemetry; the "fully reactive" baseline for the regime sweep.

## Status

Test output

## Notes / Next Steps

- Compare oscillation behavior (if any) to the relaxed α=0.85/0.90 variants.
