# DOE Sensitivity Smoke

Short name: **Sensitivity DOE Smoke — Quick Validation**

## Purpose

Early smoke run of the sensitivity DOE to validate harness wiring before the full overnight sweep. Four short runs confirming the DOE script, cell enumeration, and output naming work correctly.

## Source / Generation Context

- Script: `scripts/run_doe_sensitivity.sh` (smoke mode)
- Scene: curved minimal
- Runs: four timestamped, late April / early May 2026

## What the Output Shows

Same structure as `doe_sensitivity` (DOE\_summary.\*, sensitivity plot) but with fewer cells and shorter budgets. Used to catch harness errors before committing to a long run.

## Key Files

- `*/DOE_summary.md` — Quick cell summary
- `*/DOE_sensitivity_plot.png` — Quick sensitivity plot
- `*/doe_run.log` — Run log

## Suggested MisterY Labs Card Summary

Interpretation pending — quick smoke validation of the sensitivity DOE harness.

## Status

Archived

## Notes / Next Steps

- Superseded by `doe_sensitivity` (full run). Archive these.
