# Wormhole Dual Reality Analysis

Short name: **Wormhole DualReality — Transport Analysis**

## Purpose

Quantitative analysis run for the wormhole `DualRealityTransport` stack. Captures multiple view layers (clean curved, reference reality, curvature-only, full stack) and records summary metrics per run for transport comparison.

## Source / Generation Context

- Script: `scripts/run_wormhole_dual_reality_analysis.sh`
- Scene: wormhole dual-reality fixture scenes
- Runs: four timestamped under `2026-04-10T*`

## What the Output Shows

Each run produces several view captures: `wormhole_clean_curved.png`, `wormhole_reference_only.png`, `wormhole_reference_plus_collision.png`, `wormhole_full_stack_curvature.png`. `summary.json` and `summary.txt` record per-run metadata. Together these establish the before/after visual record for transport changes.

## Key Files

- `wormhole_clean_curved.png` — Baseline curved render
- `wormhole_reference_only.png` — Straight reference reality view
- `wormhole_reference_plus_collision.png` — Reference + collision radar
- `wormhole_full_stack_curvature.png` — All overlays
- `summary.json` / `summary.txt` — Run metadata

## Suggested MisterY Labs Card Summary

The quantitative complement to the wormhole storytelling sequence: four view layers captured per run to track how transport changes affect the curved image versus the straight-path reference. Provides the raw before/after evidence for any wormhole transport modification.

## Status

Test output

## Notes / Next Steps

- Last run April 2026. Re-run after any wormhole transport or DualRealityTransport changes.
- Cross-reference with `observer_disagreement` for ray classification deltas.
