# Testbench

Short name: **Testbench — Smoke Cache and Manifests**

## Purpose

Central testbench coordination folder: holds shared smoke-test cache files and run manifests used by multiple testbench scripts. Not a direct output of a single run — it's the coordination layer for the testbench harness.

## Source / Generation Context

- Populated by testbench harness scripts across multiple runs
- Subfolders: `_manifests/` (per-run manifests), `_smoke_cache/` (cached smoke results)

## What the Output Shows

`_smoke_cache/hermetic_closure_smoke.json` — Cached closure smoke result (used to skip re-running the smoke when results are already valid). `_manifests/` — Per-run manifest JSONs recording what was run, when, and with what parameters.

## Key Files

- `_smoke_cache/hermetic_closure_smoke.json` — Smoke cache
- `_manifests/` — Run manifests

## Suggested MisterY Labs Card Summary

Interpretation pending — testbench coordination folder (smoke cache + manifests).

## Status

Test output

## Notes / Next Steps

- Smoke cache should be invalidated and re-run after any integrator changes.
