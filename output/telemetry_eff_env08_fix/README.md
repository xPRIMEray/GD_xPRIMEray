# Telemetry Efficiency — Env 0.8 Fix

Short name: **Telemetry Efficiency — Env 0.8 Corrected**

## Purpose

Corrected version of `telemetry_eff_env08`: reruns env=0.8 after fixing a bug identified in the initial run. Includes all five pruning variants (baseline, default, tight\_env, loose\_env, stride\_off) for a full comparison, whereas the initial run may have been partial or incorrect.

## Source / Generation Context

- Scene: `test-curved-minimal-backdrop.tscn`
- Variant: env\_factor=0.8, all five pruning variants

## What the Output Shows

Five sets of heatmaps (one per pruning variant) plus `heat_efficiency.png` per variant. This is the authoritative env=0.8 dataset.

## Key Files

- `*prune_on_default*heat_efficiency.png` — Default pruning efficiency
- `*prune_on_stride_off*heat_efficiency.png` — Stride-off efficiency
- `*baseline_prune_off*heat_resolve.png` — Baseline resolve
- All other `heat_*.png` per variant

## Suggested MisterY Labs Card Summary

Interpretation pending — corrected env=0.8 efficiency run with full five-variant pruning sweep.

## Status

Test output

## Notes / Next Steps

- This is the canonical env=0.8 result. Supersedes `telemetry_eff_env08`.
