# Fixture 005

Short name: **Fixture 005 — Throat Depth Map**

## Purpose

Fifth fixture: adds throat depth measurement to the existing profile suite. `throat_depth.json` records the depth of the wormhole throat (the minimum-radius cross-section) across parameter variations.

## Source / Generation Context

- Script: `scripts/run_fixture_005.sh`
- Twenty-two run subfolders

## What the Output Shows

`throat_depth.json` / `.txt` per run records the measured throat depth. Combined with `field_radial_sector_profile.json` and `nearest_attractor_profile.txt` this gives a complete structural profile of the wormhole at each parameter setting.

## Key Files

- `*/throat_depth.json` — Throat depth measurement
- `*/field_radial_sector_profile.json` — Sector profile
- `*/nearest_attractor_profile.txt` — Attractor profile
- `*/analysis_capture.png` — Analysis render
- `*/summary.json` — Run summary

## Suggested MisterY Labs Card Summary

Interpretation pending — wormhole throat depth profile fixture.

## Status

Test output

## Notes / Next Steps

- Plot throat depth vs. field parameters to identify the parameter region where the throat collapses.
