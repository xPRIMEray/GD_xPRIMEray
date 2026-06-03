# v0.0-pre

Short name: **Pre-Release v0 — Hermetic Straight Transport Baseline**

## Purpose

First complete integration smoke images before the v0.0 release: hermetic straight-scene renders, transport classification maps, and playmode verification logs for both the straight control and curved GRIN scenes. Establishes the baseline visual standard for v0.

## Source / Generation Context

- Scripts: `scripts/run_hermetic_observatory_full_pixel.sh` and playmode verify scripts
- Scenes: hermetic straight observatory, curved GRIN minimal
- Output: flat folder (no timestamp subdirs)

## What the Output Shows

- `hermetic_straight_tile.png` / `hermetic_straight_tile_quick.png` — Full-pixel and quick tile renders of the straight hermetic scene.
- `hermetic_straight_transport_classification.png` — Ray classification overlay (budget\_exhausted / escaped / geom\_hit).
- `straight_control_final_smoke.png` / `straight_control_verify.png` — Smoke and verify captures.
- `throat_depth_map.png` — Depth map of the wormhole throat.
- `playmode_verify_*.json` / `*.log` — Playmode verify results for both scenes.
- `classification_delta/classification_delta.png` — Pixel delta between classification runs.

## Key Files

- `hermetic_straight_tile.png` — Primary v0 reference render
- `hermetic_straight_transport_classification.png` — Transport class overlay
- `throat_depth_map.png` — Wormhole throat depth
- `straight_control_final_smoke.png` — Final smoke image
- `classification_delta/classification_delta.png` — Regression delta image
- `playmode_verify_curved_grin.json` — Curved GRIN verify result

## Suggested MisterY Labs Card Summary

The pre-release baseline for xPRIMEray v0: the first hermetic straight-scene render to pass full-pixel validation, alongside a transport classification map showing exactly which rays were traced, escaped, or budget-exhausted. These images set the visual standard against which all subsequent renders are compared.

## Status

Archived

## Notes / Next Steps

- These are the historical baseline images. Do not overwrite.
- Classification delta confirms zero pixel regression from initial commit to v0 cutoff.
- Superceded visually by later hermetic observatory and overspace milestone runs.
