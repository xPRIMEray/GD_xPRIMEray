# Curved Field Validation Ladder

Short name: **Curved Field — Validation Ladder**

## Purpose

Systematic validation ladder confirming that the curved GRIN field integrator produces correct output across a range of field strengths and scene configurations. Each rung of the ladder isolates one parameter change to confirm the field behaves as expected.

## Source / Generation Context

- Script: `scripts/run_curved_field_validation_ladder.sh`
- Scene: `test-curved-minimal.tscn` and backdrop variant
- Runs: five timestamped (May 2026)

## What the Output Shows

`curved_fixture_inventory.md` / `.csv` lists the scenes and parameters tested. `curved_ladder_summary.md` / `.json` / `.csv` summarizes pass/fail per rung. `curved_vs_control_storyboard.png` compares curved vs. straight-field renders side by side. `comparability_report.json` records whether each rung's output is comparable to the reference.

## Key Files

- `*/curved_ladder_summary.md` — Ladder pass/fail summary
- `*/curved_vs_control_storyboard.png` — Curved vs. control contact sheet
- `*/curved_fixture_inventory.md` — Scene inventory
- `*/comparability_report.json` — Comparability metadata
- `*/run_metadata.json` — Run parameters

## Suggested MisterY Labs Card Summary

A systematic validation ladder for xPRIMEray's GRIN field integrator: five runs confirming the curved field produces correct output across a range of strengths. The storyboard pairs each curved render against its straight-field control to make the bending effect directly visible.

## Status

Validation candidate

## Notes / Next Steps

- Latest run: May 2026. Re-run after any field integration changes.
- Promote `curved_vs_control_storyboard.png` as the site's GRIN explainer image.
