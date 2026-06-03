# Tile Commit Traversal Repeatability

Short name: **Tile-Commit Traversal — Repeatability Check**

## Purpose

Confirms that tile-commit traversal produces deterministic output across two independent runs (run\_a / run\_b). Non-determinism in traversal order would cause pixel-level differences that accumulate visibly over passes.

## Source / Generation Context

- Script: `scripts/run_tile_commit_traversal_repeatability.sh`
- Scene: curved minimal
- Runs: `20260503T180206Z`, `20260503T181441Z`

## What the Output Shows

`repeatability_summary.json` compares run\_a vs. run\_b hash and pixel diff. `run_a.log` / `run_b.log` capture full Godot output. If hash-match is true the traversal is deterministic; any changed\_pixels indicates non-determinism introduced by threading or order sensitivity.

## Key Files

- `*/repeatability_summary.json` — Hash match and pixel diff summary
- `*/run_a.log` / `run_b.log` — Per-run logs
- `*/tile_commit_traversal_summary.md` / `.json` — Run metadata

## Suggested MisterY Labs Card Summary

Interpretation pending — determinism check for the tile-commit traversal across two independent runs.

## Status

Validation candidate

## Notes / Next Steps

- If repeatability\_summary shows hash\_match=true, tile-commit is safe for deterministic regression testing.
