# Telemetry Heatmap Quick v2

Short name: **Telemetry Heatmap — Quick v2 (Revised Pipeline)**

## Purpose

Second version of the quick heatmap run, incorporating fixes or additions to the heatmap pipeline identified during v1. Confirms the revised pipeline produces correct output for all five pruning variants before running the full adaptive sweep.

## Source / Generation Context

- Scene: `test-curved-minimal-backdrop.tscn`
- Variants: baseline, default, tight\_env, loose\_env, stride\_off

## What the Output Shows

Same five-variant structure as v1. Key question is whether the fixes in v2 change any heatmap values, which would indicate a real pipeline bug was corrected.

## Key Files

- Same structure as `telemetry_heatmap_quick`
- Compare v1 vs. v2 heatmap PNGs for pipeline correction evidence

## Suggested MisterY Labs Card Summary

Interpretation pending — revised quick heatmap pipeline, v2.

## Status

Archived

## Notes / Next Steps

- If v1 and v2 heatmaps are identical, the pipeline is stable. If not, document what changed.
