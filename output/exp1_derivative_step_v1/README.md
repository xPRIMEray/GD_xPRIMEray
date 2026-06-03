# Experiment 1 — Derivative Step v1

Short name: **Exp1 v1 — Derivative Step (Curved + Backdrop)**

## Purpose

Extends the derivative step experiment to both `curved_minimal` and `curved_minimal_backdrop` scenes. V1 captures baseline and firstpass variants for both scenes simultaneously, broadening the comparison to confirm the optimization generalizes across scene types.

## Source / Generation Context

- Scenes: `test-curved-minimal.tscn` and `test-curved-minimal-backdrop.tscn`
- Variants: baseline + firstpass per scene
- Three run subfolders

## What the Output Shows

Six renders (baseline + firstpass for curved, backdrop, and a repeat) plus derivative step JSONs. Comparing firstpass vs. baseline telemetry across both scenes shows whether the optimization produces consistent convergence gains.

## Key Files

- `*curved_baseline*.png` / `*curved_firstpass*.png` — Curved scene pair
- `*backdrop_baseline*.png` / `*backdrop_firstpass*.png` — Backdrop scene pair
- `*.derivative_step.json` — Telemetry per variant
- `run.log` — Run log

## Suggested MisterY Labs Card Summary

Interpretation pending — v1 of the derivative step experiment, extending the baseline/firstpass comparison to two scene types.

## Status

Archived

## Notes / Next Steps

- Part of the v0–v4 series. See v4 for the final hold-step variants.
