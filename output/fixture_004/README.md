# Fixture 004

Short name: **Fixture 004 — Field Sector Profile (Extended)**

## Purpose

Fourth fixture: extends the sector-profile analysis with more sector divisions for finer angular resolution. Measures field behavior per angular sector at multiple radii.

## Source / Generation Context

- Script: `scripts/run_fixture_004.sh`
- Fifteen run subfolders

## What the Output Shows

Same file types as Fixture 003 but with finer sector divisions in `field_radial_sector_profile.json`. `radial_profile.json` provides the sector-agnostic baseline.

## Key Files

- `*/field_radial_sector_profile.json` — Fine-resolution sector profile
- `*/radial_profile.json` — Radial baseline
- `*/analysis_capture.png` — Analysis render
- `*/summary.json` — Run summary

## Suggested MisterY Labs Card Summary

Interpretation pending — extended sector field profile fixture.

## Status

Test output

## Notes / Next Steps

- Compare sector profiles across Fixtures 002–004 to characterize field anisotropy.
