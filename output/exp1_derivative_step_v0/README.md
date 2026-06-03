# Experiment 1 — Derivative Step v0

Short name: **Exp1 v0 — Derivative Step Baseline (Backdrop)**

## Purpose

First version of the derivative-step experiment: compares baseline (no first-pass traversal) vs. firstpass mode on the `curved_minimal_backdrop` scene. Establishes the derivative step telemetry baseline before any first-pass optimization was applied.

## Source / Generation Context

- Scene: `test-curved-minimal-backdrop.tscn`
- Variants: `exp1_derivative_step_v0_baseline` and `exp1_derivative_step_v0_firstpass`
- Part of a five-version series (v0–v4) tracking derivative step optimization

## What the Output Shows

Two PNG renders (baseline, firstpass) and corresponding `.derivative_step.json` telemetry files. Comparing the derivative step JSONs between baseline and firstpass reveals the step distribution and convergence difference that the first-pass traversal achieves.

## Key Files

- `*exp1_derivative_step_v0_baseline*.png` — Baseline render
- `*exp1_derivative_step_v0_firstpass*.png` — First-pass render
- `*.derivative_step.json` — Derivative step telemetry per variant
- `run.log` — Run log

## Suggested MisterY Labs Card Summary

Interpretation pending — v0 baseline of the derivative step experiment series.

## Status

Archived

## Notes / Next Steps

- See v1–v4 for the evolution of first-pass optimization. These are historical anchor points.
