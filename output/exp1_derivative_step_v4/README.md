# Experiment 1 — Derivative Step v4

Short name: **Exp1 v4 — Derivative Step Hold Variants**

## Purpose

Final iteration of the derivative step experiment. Introduces hold-step variants (hold1, hold3, hold4) that defer derivative evaluation for N steps to reduce compute. Compares hold variants to the baseline to quantify the convergence / cost tradeoff.

## Source / Generation Context

- Scene: `test-curved-minimal.tscn`
- Variants: `curved_hold1`, `curved_hold3`, `curved_hold4`
- Three run subfolders

## What the Output Shows

Three PNG renders (hold1, hold3, hold4) plus derivative step JSONs. The telemetry shows how holding the derivative for more steps shifts the convergence distribution. Hold4 is the most aggressive optimization; hold1 is closest to the no-hold baseline.

## Key Files

- `*curved_hold1*.png` / `*curved_hold3*.png` / `*curved_hold4*.png` — Hold variant renders
- `*.derivative_step.json` — Step distribution per hold level
- `run.log` — Run log

## Suggested MisterY Labs Card Summary

The final experiment in the derivative step optimization series: comparing three "hold" strategies that defer curvature evaluation for 1, 3, or 4 steps. The derivative step telemetry quantifies how much convergence quality is traded for speed at each hold level.

## Status

Test output

## Notes / Next Steps

- Results informed the production hold-step default.
- Consider publishing a hold-step sensitivity chart as a methodology note on the site.
