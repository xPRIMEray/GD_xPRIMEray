# Fixture Runs

Short name: **Wormhole Fixture Sequence — Checkpoint Debug Captures**

## Purpose

Debug and analysis captures for the wormhole checkpoint fixture sequence (mouth → throat → exit). Each sub-run captures the rendered state at a specific wormhole transition point for visual inspection and checkpoint validation.

## Source / Generation Context

- Scripts: `scripts/run_fixture_011_wormhole_checkpoint_sequence.sh` and related checkpoint scripts
- Scenes: wormhole checkpoint sequence fixture scenes
- Nine sub-runs

## What the Output Shows

Named PNG captures per checkpoint:
- `00_mouth_debug.png` — Mouth entry debug view
- `01_mouth_interp_01_capture.png` — Mouth interpolation state
- `02_mouth_to_throat_approach_capture.png` / `_debug.png` — Approach render + debug
- `03_throat_interp_01_debug.png` — Throat interpolation debug
- `04_throat_debug.png` — Throat entry debug

`checkpoint_sequence_summary.json` records pass/fail per checkpoint.

## Key Files

- `00_mouth_debug.png`, `02_mouth_to_throat_approach_capture.png`, `04_throat_debug.png` — Key checkpoint captures
- `checkpoint_sequence_summary.json` — Checkpoint sequence summary
- `run.log` — Full run log

## Suggested MisterY Labs Card Summary

A visual walk-through of the wormhole checkpoint sequence: debug captures at each structural transition from mouth to throat to exit. The renders show the wormhole's transport geometry at each stage, useful for both debugging and storytelling.

## Status

Test output

## Notes / Next Steps

- Promote clean captures (non-debug) to the wormhole storytelling sequence.
- Extend to exit checkpoint captures for a complete mouth → throat → exit narrative.
