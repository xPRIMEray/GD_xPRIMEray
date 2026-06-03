# Experiment 1 — Derivative Step v2

Short name: **Exp1 v2 — Derivative Step (Stability Check)**

## Purpose

Second iteration of the derivative step experiment. Same two-scene structure as v1 but with updated integrator parameters or telemetry version. Documents the state of derivative step behavior after a code change between v1 and v2.

## Source / Generation Context

- Scenes: `test-curved-minimal.tscn` and `test-curved-minimal-backdrop.tscn`
- Variants: baseline + firstpass per scene
- Three run subfolders

## What the Output Shows

Same structure as v1. The key value is the delta between v1 and v2 derivative step JSONs, which isolates the effect of the code change made between iterations.

## Key Files

- `*curved_baseline*.png` / `*curved_firstpass*.png`
- `*.derivative_step.json`
- `run.log`

## Suggested MisterY Labs Card Summary

Interpretation pending — v2 iteration of the derivative step experiment.

## Status

Archived

## Notes / Next Steps

- Diff derivative step JSONs against v1 to isolate the change effect. See v4 for final state.
