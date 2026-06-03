# Telemetry Adaptive — Regime Neutral 0.85

Short name: **Adaptive Telemetry — Neutral Regime, α=0.85**

## Purpose

Tests the adaptive scheduler in "neutral" regime with a relaxation factor α=0.85. The neutral regime neither aggressively pursues high-curvature regions nor avoids them; α=0.85 controls how quickly the prior decays toward current-pass evidence.

## Source / Generation Context

- Scene: `test-curved-minimal-backdrop.tscn`
- Variant: `basic`, neutral regime, α=0.85

## What the Output Shows

Eight heatmaps. Compare to `telemetry_adaptive_regime_neutral090` and `telemetry_adaptive_regime_norelax` to evaluate the relaxation sensitivity.

## Key Files

- `*heat_candidates.png`, `*heat_work.png`, `*heat_curvature_mean.png`
- `*heat_curvature_max.png`, `*heat_query.png`, `*heat_pass1_steps.png`

## Suggested MisterY Labs Card Summary

Interpretation pending — neutral-regime adaptive telemetry at α=0.85.

## Status

Test output

## Notes / Next Steps

- Compare to α=0.90 (`telemetry_adaptive_regime_neutral090`) and no-relax (`telemetry_adaptive_regime_norelax`) variants.
