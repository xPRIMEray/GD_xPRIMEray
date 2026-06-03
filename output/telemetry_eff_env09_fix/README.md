# Telemetry Efficiency — Env 0.9 Fix

Short name: **Telemetry Efficiency — Env 0.9 Corrected**

## Purpose

Corrected env=0.9 run (same rationale as `telemetry_eff_env08_fix`): full five-variant pruning sweep after fixing the bug identified in the initial env=0.9 run. Authoritative dataset for env=0.9.

## Source / Generation Context

- Scene: `test-curved-minimal-backdrop.tscn`
- Variant: env\_factor=0.9, all five pruning variants

## What the Output Shows

Five sets of heatmaps plus `heat_efficiency.png` per pruning variant.

## Key Files

- `*prune_on_stride_off*heat_efficiency.png` — Stride-off efficiency
- `*baseline_prune_off*heat_resolve.png` — Baseline resolve
- All `heat_*.png` per variant

## Suggested MisterY Labs Card Summary

Interpretation pending — corrected env=0.9 efficiency run with full five-variant sweep.

## Status

Test output

## Notes / Next Steps

- Canonical env=0.9 result. Compare to `telemetry_eff_env08_fix` to quantify the 0.8 vs. 0.9 tradeoff.
