# Fixture 007 — Field

Short name: **Fixture 007 — Field-Focused Characterization**

## Purpose

Seventh fixture: field-focused characterization isolating the GRIN field's contribution to transport behavior independently of topology. Measures field-only effects by varying field parameters while holding topology fixed.

## Source / Generation Context

- Script: `scripts/run_fixture_007_field.sh`
- Four run subfolders

## What the Output Shows

Same file suite as previous fixtures. The `run.log` per subfolder is the primary artifact; the field-focused parameter choices are recorded in `params.json` (if present).

## Key Files

- `*/run.log` — Godot run log
- `*/throat_depth.json` — Throat depth (as field reference)
- `*/field_radial_sector_profile.json` — Field profile
- `*/summary.json` — Run summary

## Suggested MisterY Labs Card Summary

Interpretation pending — field-only GRIN characterization with topology held fixed.

## Status

Test output

## Notes / Next Steps

- Compare field profiles to Fixture 006 topology runs to separate field vs. topology contributions.
