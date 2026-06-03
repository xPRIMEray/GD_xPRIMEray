# Wormhole Structure Observatory

Short name: **Wormhole Anatomy — Contact Sheet**

## Purpose

Visual observatory for the wormhole scene: a structured contact sheet showing the mouth, throat, and exit from multiple transport perspectives. Designed to confirm that each structural zone renders distinctly and that overlays (curvature, collision radar, semantic glyphs) correctly annotate the geometry.

## Source / Generation Context

- Script: `scripts/run_wormhole_structure_observatory_quick.sh`
- Scene: `test-overspace-wormhole-witness-fixture.tscn` and checkpoint variant scenes
- Runs: four timestamped under `output/wormhole_structure_observatory/`, including one `timeout_probe` run

## What the Output Shows

`wormhole_structure_contact_sheet.png` grids multiple wormhole cross-sections. `wormhole_structure_report.md` describes each panel. A timeout probe run (`timeout_probe_20260514T043656Z`) captured the behavior when the render budget is exhausted mid-frame, useful for verifying graceful degradation.

## Key Files

- `wormhole_structure_contact_sheet.png` — Multi-panel contact sheet
- `wormhole_structure_report.md` — Panel-by-panel description
- `wormhole_structure_summary.json` — Run metadata
- `testbench_preview.png` — Quick preview thumbnail
- `testbench_stdout.log` / `wormhole_structure_observatory.log` — Full run logs
- `timeout_probe_*/` — Separate run investigating timeout behavior

## Suggested MisterY Labs Card Summary

A structural observatory for xPRIMEray's wormhole transport: a contact sheet showing the mouth, throat, and exit zones with curvature and collision-radar overlays. A companion timeout probe confirms the integrator degrades gracefully when render budget runs out mid-frame.

## Status

Visual reference

## Notes / Next Steps

- Latest runs from May 2026. Re-run after wormhole geometry or transport changes.
- Promote best contact sheet to site gallery.
- Merge timeout probe findings into render health documentation.
