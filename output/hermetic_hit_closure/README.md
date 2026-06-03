# Hermetic Hit Closure

Short name: **Hermetic Hit Closure — Integration Escape Analysis**

## Purpose

Tests that the hermetic scene integration loop achieves "closure" — every ray that enters a hermetic volume exits it, with no integration escapes or missed boundary crossings. Failure islands in the closure map indicate integration step size or boundary detection problems.

## Source / Generation Context

- Script: `scripts/run_hermetic_hit_closure_ladder.sh`
- Scene: hermetic curved room / overspace hermetic fixture
- Runs: two timestamped (`20260514T030505Z`, `20260514T040157Z`)

## What the Output Shows

`hermetic_closure_ladder.png` grids closure rate across step sizes. `hermetic_failure_storyboard.png` shows the spatial distribution of failure pixels. `adaptive_closure_recovery_heatmap.png` shows where adaptive step reduction recovers failures. `integration_escape_vectors.csv` lists escaped ray vectors. `hermetic_oracle_recommendations.md` gives oracle-generated recommendations for step size selection. `hermetic_failure_islands.csv` identifies spatially coherent failure clusters.

## Key Files

- `*/hermetic_closure_ladder.png` — Closure rate vs. step size
- `*/hermetic_failure_storyboard.png` — Failure pixel distribution
- `*/adaptive_closure_recovery_heatmap.png` — Recovery heatmap
- `*/integration_escape_vectors.csv` — Escaped ray data
- `*/hermetic_failure_islands.csv` — Failure cluster locations
- `*/hermetic_hit_closure_summary.md` — Summary
- `*/closure_recovery_efficiency.json` — Recovery efficiency stats

## Suggested MisterY Labs Card Summary

A closure audit for xPRIMEray's hermetic integration: tracking every ray that enters a boundary volume and confirming it exits. The failure storyboard and island map reveal where the integrator needs adaptive step reduction — and the recovery heatmap shows where the adaptation succeeds.

## Status

Validation candidate

## Notes / Next Steps

- Oracle recommendations (`hermetic_oracle_recommendations.md`) should be reviewed and applied.
- Re-run after any BoundaryLayerVolume or integration step changes.
- Connects directly to Phase 1 material work — mirror/refraction crossings must also satisfy closure.
