# Telemetry Adaptive — Regime Neutral 0.90

Short name: **Adaptive Telemetry — Neutral Regime, α=0.90**

## Purpose

Same as the α=0.85 neutral variant but with a higher relaxation factor. α=0.90 means the prior decays more slowly, giving prior-pass statistics more weight in the adaptation decision.

## Source / Generation Context

- Scene: `test-curved-minimal-backdrop.tscn`
- Variant: `basic`, neutral regime, α=0.90
- Part of the regime relaxation sweep

## What the Output Shows

Eight heatmaps. The key comparison is `heat_candidates.png` vs. the 0.85 and no-relax variants — higher α should produce smoother candidate selection.

## Key Files

- `*heat_candidates.png`, `*heat_work.png`, `*heat_curvature_mean.png`

## Suggested MisterY Labs Card Summary

Interpretation pending — neutral-regime adaptive telemetry at α=0.90.

## Status

Test output

## Notes / Next Steps

- Part of the α sweep: compare to 0.85, 0.90, and no-relax variants side by side.
