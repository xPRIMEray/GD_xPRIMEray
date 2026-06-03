# Shutdown Probe

Short name: **Shutdown Probe — Graceful Exit Under Load**

## Purpose

Tests that the renderer shuts down gracefully (clean log, correct exit code, no partial-write corruption) while integration is in progress. Verifies the shutdown signal is respected mid-frame without corrupting outputs.

## Source / Generation Context

- Scene: `test-hermetic-curved-room.tscn`
- Five subfolders covering different scene states: `atomic_a2_after_cleanup`, `hermetic_same_shape`, `hermetic_same_shape_after_cleanup`, `hermetic_same_shape_after_cleanup2`

## What the Output Shows

Each subfolder contains a rendered PNG, a `diagnostic_wireframe_primitives.json`, a `hit_diagnostics.csv`, and a `derivative_step.json`. `run.log` records the shutdown sequence. The renders confirm the frame state at the moment of shutdown — partial renders are expected and indicate clean mid-frame exit behavior.

## Key Files

- `*/hermetic_curved_room__shutdown_probe__*.png` — Frame state at shutdown
- `*/hit_diagnostics.csv` — Ray hit state at shutdown
- `*/diagnostic_wireframe_primitives.json` — Scene state primitives
- `*/run.log` — Shutdown sequence log

## Suggested MisterY Labs Card Summary

Interpretation pending — graceful shutdown probe confirming the renderer exits cleanly under load without corrupting outputs.

## Status

Test output

## Notes / Next Steps

- Confirm all subfolders show clean exit codes in run.log.
- This is a prerequisite for unattended batch runs.
