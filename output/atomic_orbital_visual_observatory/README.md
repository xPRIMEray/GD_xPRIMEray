# Atomic Orbital Visual Observatory

Short name: **GRIN Atomic Orbital — Parameter Ladder**

## Purpose

Systematically explores how varying electron count, orbital radius, field strength, and temporal modulation changes the appearance of a hydrogen-style atomic orbital rendered through xPRIMEray's GRIN field integrator. Each panel isolates one parameter change so visual regressions are immediately visible.

## Source / Generation Context

- Script: `scripts/run_atomic_orbital_visual_observatory.sh`
- Scene: `test-atomic-orbital-visual-observatory.tscn`
- Fixture: `AtomicOrbitalGrinRoom` (A2 static hydrogen and modulated variants)
- Runs timestamped under `output/atomic_orbital_visual_observatory/YYYYMMDDTHHMMSSZ/`

## What the Output Shows

A contact sheet (`atomic_visual_contact_sheet.png`) of five rendered panels:

- **V0** — No GRIN field; baseline geometry render.
- **V1** — Single static hydrogen orbital (low strength).
- **V2** — Exaggerated hydrogen (higher radius + strength); visually dramatic lens-like distortion.
- **V3 / V4** — Temporally modulated orbital at tick 0 and tick 1; pixel diff between ticks shows the moving fringe pattern (~4–11% pixels changed).

The report (`atomic_visual_observatory_report.md`) captures exact parameter values and temporal diff metrics per run.

## Key Files

- `atomic_visual_contact_sheet.png` — Side-by-side panel grid
- `atomic_visual_observatory_report.md` — Parameter table + temporal diff metrics
- `V0_vs_V1_diff.png`, `V0_vs_V2_scaled_diff.png` — Per-pixel difference images
- `testbench_stdout.log` — Full Godot run log
- `testbench_command.txt` — Exact CLI invocation for reproduction

## Suggested MisterY Labs Card Summary

xPRIMEray renders hydrogen-like atomic orbitals using GRIN field integration — no rasterization, no ray-marching approximations. This observatory sequences five parameter variants side-by-side to show how electron count, field radius, and temporal phase each sculpt the light. The modulated case reveals a living fringe pattern that shifts between frames.

## Status

Visual reference

## Notes / Next Steps

- Most recent runs (May 2026) at `20260531T*` — latest is most valid.
- Higher modulation strengths and multi-electron cases (helium, lithium) not yet in the ladder.
- Connect to `atomic_orbital_grin_ladder` for quantitative efficiency results alongside the visual.
