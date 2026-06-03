# Atomic Orbital GRIN Ladder

Short name: **GRIN Ladder — Atomic Orbital Efficiency Sweep**

## Purpose

Quantitative efficiency sweep over the atomic orbital GRIN scene. Measures how pruning mode, stride, and scheduler settings affect render time and output consistency. Produces a summary JSON + Markdown report per run.

## Source / Generation Context

- Script: `scripts/run_atomic_orbital_grin_ladder.sh`
- Scene: `test-atomic-orbital-grin-room.tscn`
- Fixture: `AtomicOrbitalGrinRoom` (A2 static hydrogen)
- Runs timestamped under `output/atomic_orbital_grin_ladder/YYYYMMDDTHHMMSSZ/`
- Six ladder runs recorded (May 2026)

## What the Output Shows

Each run produces `atomic_orbital_grin_ladder_report.md` and `atomic_orbital_grin_ladder_summary.json` comparing pruning variants (baseline, default, tight\_env, loose\_env, stride\_off) across scheduler settings. The ladder tracks band pixel counts, hash-match status (identical output vs. off), and step performance.

## Key Files

- `atomic_orbital_grin_ladder_summary.json` — Machine-readable results per cell
- `atomic_orbital_grin_ladder_report.md` — Human-readable ladder summary
- `atomic_orbital_grin_ladder.log` — Full run log

## Suggested MisterY Labs Card Summary

A quantitative efficiency ladder showing how different pruning and scheduler configurations affect xPRIMEray's GRIN-field renderer on the atomic orbital scene. The ladder tracks both output correctness (hash-match) and throughput across six parameter variants, pinpointing which settings deliver clean images fastest.

## Status

Test output

## Notes / Next Steps

- Latest run: `20260512T041011Z`. Re-run after any pruning or scheduler change.
- Connect visual panel output from `atomic_orbital_visual_observatory` alongside these efficiency numbers.
