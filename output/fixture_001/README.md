# Fixture 001

Short name: **Fixture 001 — Parameter Sweep (104 Runs)**

## Purpose

First numbered fixture: a large parameter sweep (104 cells) across integration and scheduler settings. Fixture 001 is the broadest parameter exploration run, establishing the performance envelope of the renderer across a wide setting range.

## Source / Generation Context

- Script: `scripts/run_fixture_001.sh`
- Scene: hermetic / domain resolver stress scene
- 104 run subfolders, each with `summary.json`, `params.json`, `run.log`, `metrics.json`

## What the Output Shows

Per-cell: `params.json` records the exact parameter combination; `summary.json` records outcome; `metrics.json` records timing and quality metrics. The 104-cell sweep covers a broad multi-dimensional parameter space.

## Key Files

- `*/params.json` — Cell parameters
- `*/summary.json` — Cell outcome
- `*/metrics.json` — Timing and quality metrics
- `*/run.log` — Godot run log per cell

## Suggested MisterY Labs Card Summary

Interpretation pending — 104-cell parameter sweep exploring the renderer's performance envelope.

## Status

Test output

## Notes / Next Steps

- Extract Pareto-optimal cells (best quality / time tradeoff) from metrics.json.
- Use as baseline for Fixture 002–005 comparisons.
