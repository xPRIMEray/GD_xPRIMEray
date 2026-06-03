# Telemetry Heatmap Test

Short name: **Telemetry Heatmap — Stride 2 Test**

## Purpose

Heatmap test run using stride=2 (vs. stride=1 in the quick runs). Tests whether the coarser stride produces correct heatmap output and whether stride affects the resolve and query heatmaps visibly.

## Source / Generation Context

- Scene: `test-curved-minimal-backdrop.tscn`
- Variants: baseline, default, tight\_env, loose\_env — all at stride=2

## What the Output Shows

Heatmaps at stride=2. Compare `heat_query.png` and `heat_resolve.png` to stride=1 runs to see whether heatmap density correlates with stride.

## Key Files

- `*stride-2*heat_query.png` — Query heatmap at stride 2
- `*stride-2*heat_resolve.png` — Resolve heatmap at stride 2
- `*derivative_step.json` — Telemetry per variant

## Suggested MisterY Labs Card Summary

Interpretation pending — stride=2 heatmap test run.

## Status

Test output

## Notes / Next Steps

- Compare to stride=1 quick runs to document the stride/heatmap density relationship.
