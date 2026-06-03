# Fixture 003

Short name: **Fixture 003 — Nearest Attractor Profile**

## Purpose

Third fixture: adds a "nearest attractor" profile alongside the radial field profile. Measures which field attractor each ray converges toward, useful for understanding multi-attractor field topology.

## Source / Generation Context

- Script: `scripts/run_fixture_003.sh`
- Ten run subfolders

## What the Output Shows

`nearest_attractor_profile.txt` records attractor assignment per ray sample. `field_radial_sector_profile.json` extends the radial profile with sector breakdown. `radial_profile.json` / `.txt` and `analysis_capture.png` as in Fixture 002.

## Key Files

- `*/nearest_attractor_profile.txt` — Attractor assignment profile
- `*/field_radial_sector_profile.json` — Sector-aware field profile
- `*/analysis_capture.png` — Analysis render
- `*/summary.json` — Run summary

## Suggested MisterY Labs Card Summary

Interpretation pending — nearest attractor profile fixture for multi-attractor field topology.

## Status

Test output

## Notes / Next Steps

- Visualize attractor assignment map overlaid on the scene render.
