# Telemetry Heatmap Quick

Short name: **Telemetry Heatmap — Quick Baseline (5 Variants)**

## Purpose

Quick heatmap run for the five standard pruning variants on the backdrop scene. First pass at the heatmap visualization pipeline to confirm that all variant heatmaps render correctly before longer sweeps.

## Source / Generation Context

- Scene: `test-curved-minimal-backdrop.tscn`
- Variants: baseline, default, tight\_env, loose\_env, stride\_off

## What the Output Shows

Five rendered PNGs plus resolve heatmaps. `heat_pass1_steps.png` and `heat_query.png` per pruning variant. Quick visual check that the heatmap pipeline is producing correct output for all five variants.

## Key Files

- `*baseline_prune_off*heat_resolve.png` — Baseline resolve
- `*prune_on_default*heat_pass1_steps.png` — Default pruning step count
- `*prune_on_tight_env*heat_query.png` — Tight env query count
- `*derivative_step.json` — Telemetry per variant

## Suggested MisterY Labs Card Summary

Interpretation pending — quick five-variant heatmap run confirming the visualization pipeline.

## Status

Archived

## Notes / Next Steps

- Superseded by `telemetry_heatmap_quick_v2` and the full adaptive sweep. Archive.
