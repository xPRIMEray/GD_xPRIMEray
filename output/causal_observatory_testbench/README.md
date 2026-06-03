# Causal Observatory Testbench

Short name: **Causal Ultra Turbo — Tile Scheduler Validation**

## Purpose

Validates the "causal ultra turbo" render path using the object-seeded tile scheduler on the atomic orbital visual observatory scene. Confirms high tile-metric hit rates and checks that causal ordering holds across threaded tile dispatch.

## Source / Generation Context

- Script: `scripts/run_causal_observatory_testbench.sh`
- Scene: `test-atomic-orbital-visual-observatory.tscn`
- Fixture: `causal_ultra_turbo` + `scheduler-object-seeded-tile`
- Runs: `20260531T152107Z`, `20260601T012354Z`, `20260601T012408Z`

## What the Output Shows

Each run produces a rendered PNG, a tile metrics summary JSON, and derivative step telemetry. The `causal_observatory_testbench.log` records full Godot output. Healthy runs show stalledSteps=0, noCandidates=0, and strong tile hit rates. The most recent run (20260601) confirmed Phase 1 material crossing paths are hot and stable (BLV summary: 140k events, zero stalls).

## Key Files

- `*causal_ultra_turbo*.png` — Rendered beauty capture
- `*tile_metrics_summary.json` — Tile scheduler performance per run
- `*derivative_step.json` — Per-step transport telemetry
- `causal_observatory_testbench.log` — Full run log
- `atomic_visual_telemetry.csv` — Aggregated frame telemetry

## Suggested MisterY Labs Card Summary

A recurring testbench for xPRIMEray's causal tile scheduler in "ultra turbo" mode, run against the atomic orbital observatory scene. Each run confirms that causal ordering, hermetic closure, and tile hit rates remain healthy under concurrent dispatch — the key stability gate before visual renders are promoted.

## Status

Validation candidate

## Notes / Next Steps

- Latest run (20260601T012408Z) validated Phase 1 BoundaryLayerVolume material changes.
- Add PNG beauty capture for the most recent run (proxy scene didn't produce one).
- Consider making this the nightly regression gate.
