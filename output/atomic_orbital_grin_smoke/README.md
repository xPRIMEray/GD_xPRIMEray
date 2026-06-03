# Atomic Orbital GRIN Smoke

Short name: **GRIN Smoke — A2 Hydrogen Baseline**

## Purpose

Fast smoke test for the A2 static hydrogen GRIN scene. Confirms the integrator renders without crashing and produces visually stable output across five pruning variants before committing changes. Not a quantitative efficiency run.

## Source / Generation Context

- Script: `scripts/run_atomic_orbital_grin_ladder.sh` (smoke mode)
- Scene: `test-atomic-orbital-grin-room.tscn`
- Fixture: `A2_static_hydrogen`
- Single run subfolder: `A2_static_hydrogen/`

## What the Output Shows

Five PNG renders (one per pruning variant: baseline, default, tight\_env, loose\_env, stride\_off) plus a frame telemetry CSV. The images can be visually diff'd to confirm pruning variants produce equivalent output. `atomic_frame_telemetry.csv` shows per-frame timing.

## Key Files

- `A2_static_hydrogen/atomic_orbital_grin_room__a2_static_hydrogen__*.png` — One render per pruning variant
- `A2_static_hydrogen/atomic_frame_telemetry.csv` — Per-frame timing data
- `A2_static_hydrogen/*.derivative_step.json` — Derivative step telemetry per variant

## Suggested MisterY Labs Card Summary

Smoke confirmation that xPRIMEray's GRIN integrator produces stable output for a static hydrogen atomic orbital across five pruning strategies. Used as a quick sanity check before longer quantitative runs.

## Status

Test output

## Notes / Next Steps

- Promote the best-looking PNG to the visual observatory run.
- Add a temporal modulation case to the smoke so animation regressions are caught early.
