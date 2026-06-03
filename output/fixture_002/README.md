# Fixture 002

Short name: **Fixture 002 — Radial Profile + Analysis Capture**

## Purpose

Second fixture: focuses on radial field profiling. Measures how the GRIN field strength varies radially from the scene center and captures analysis images for visual inspection.

## Source / Generation Context

- Script: `scripts/run_fixture_002.sh`
- Four run subfolders

## What the Output Shows

`radial_profile.txt` / `.json` records field strength vs. radius. `analysis_capture.png` is the rendered frame with analysis overlays. `debug_capture.png` shows debug wireframes. `row_coverage.txt` records row-by-row render coverage.

## Key Files

- `*/radial_profile.json` — Machine-readable radial profile
- `*/analysis_capture.png` — Analysis render
- `*/debug_capture.png` — Debug wireframe render
- `*/summary.json` — Run summary

## Suggested MisterY Labs Card Summary

Interpretation pending — radial field profile analysis fixture.

## Status

Test output

## Notes / Next Steps

- Plot `radial_profile.json` to visualize field falloff curve.
