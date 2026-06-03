# Render Scheduler

Short name: **Render Scheduler — Baseline vs. Reorder**

## Purpose

Investigates the render scheduler's baseline vs. reorder-only mode. The "reorder-only" mode reorders evaluation of pending work without changing the integration logic, testing whether smarter work ordering alone improves convergence speed.

## Source / Generation Context

- Scripts: render scheduler investigation scripts
- Scenes: `test-curved-minimal.tscn`, `test-curved-minimal-backdrop.tscn`
- Subfolders: multiple timestamped runs plus named runs (`priors_diagnosis_2026-03-28`, `retuned_priors_2026-03-28`)

## What the Output Shows

`summary.json` / `summary.md` per run comparing baseline vs. reorder-only for both scenes. Named subfolders capture targeted priors-diagnosis and retuning investigation from March 2026. `curved_minimal__baseline.log`, `curved_minimal__reorder-only.log` etc. record per-mode Godot output.

## Key Files

- `*/summary.md` — Scheduler comparison summary
- `*/summary.json` — Machine-readable summary
- `priors_diagnosis_2026-03-28/` — Priors tuning investigation
- `retuned_priors_2026-03-28/` — Post-retuning result
- `*baseline.log` / `*reorder-only.log` — Mode logs

## Suggested MisterY Labs Card Summary

Interpretation pending — render scheduler baseline vs. reorder-only investigation, including a March 2026 priors-retuning session.

## Status

Test output

## Notes / Next Steps

- Connect to `doe_scheduler_resonance` for the stride-resonance context.
- Retuned priors findings should be documented and fed into the default scheduler configuration.
