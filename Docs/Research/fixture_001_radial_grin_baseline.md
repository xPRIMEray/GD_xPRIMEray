# Fixture 001: Radial GRIN Baseline

## Purpose

Fixture 001 is the compact radial GRIN baseline used to validate that the
current curved-ray transport path can launch, render, capture, and report a
stable result through the dedicated harness.

This note records the current harness contract and the local run history that
supports promoting A2 as the current clean baseline candidate.

## Current harness

- Harness script: `scripts/run_fixture_001.sh`
- Reporter: `tools/fixture_001_report.py`
- Scene: `res://test-grin-basic-visual-minimal.tscn`
- Fixture identity expected by launch audit: `grin_basic_visual_minimal`
- Fixture id: `fixture_001`
- Launcher token configured by the harness: `run_fixture_001`

### Default thresholds and capture settings

- `FIXTURE_001_SETTLE_FRAMES=12`
- `FIXTURE_001_MIN_RH_STEP=20`
- `FIXTURE_001_MIN_PROCESSED_ROWS=64`
- `FIXTURE_001_CAPTURE_FILM_OPACITY=1.0`
- `FIXTURE_001_COMPARE_GRID=1`
- `FIXTURE_001_COMPARE_CROSSHAIR=1`

### What the harness records

The report step builds `params.json`, `metrics.json`, and `summary.txt` from
the Godot log and capture artifact. The primary baseline signals are:

- launch audit status
- render transport selection
- traced pixel count
- processed row count
- capture success
- scheduler guard exits and forced advances, as seen in `run.log`

## Observed run history

The current local Fixture 001 history under `output/fixture_runs/fixture_001`
contains four recorded runs from March 19, 2026.

| Label | Timestamp | StepsPerRay | Status | guard_progress exits | forcedAdvance | processed_rows | traced_pixels | Notes |
| --- | --- | ---: | --- | ---: | ---: | ---: | ---: | --- |
| A0 | `2026-03-19T22-08-00` | 500 | ok | 18 | 18 | 164 | 39200 | First functional captured baseline, but heavily scheduler-limited |
| A1 | `2026-03-19T22-49-38` | 500 | ok | 7 | 7 | 164 | 24320 | Improved over A0, still scheduler-limited |
| A1.1 | `2026-03-19T22-55-23` | 500 | ok | 10 | 10 | 164 | 27616 | Additional 500-step run, still scheduler-limited |
| A2 | `2026-03-19T22-57-53` | 350 | ok | 0 | 0 | 164 | 56288 | First scheduler-clean capture in the current run history |

## Why A2 is the current baseline candidate

A2 is the first recorded Fixture 001 run that satisfies all of the following at
the same time:

- launch audit status is `ok`
- capture succeeds
- transport is `GRIN_Optical`
- no `guard_progress` budget exits appear in `run.log`
- no `forcedAdvance=1` yield aborts appear in `run.log`
- processed row coverage remains at `164`
- traced pixel count remains positive and materially above prior runs at `56288`

This makes A2 the current clean baseline candidate for Fixture 001.

## Current baseline candidate

- Label: `A2`
- Timestamp: `2026-03-19T22-57-53`
- Scene: `res://test-grin-basic-visual-minimal.tscn`
- Fixture: `grin_basic_visual_minimal`
- Launcher observed in run log: `manual`
- Transport: `GRIN_Optical`
- StepsPerRay: `350`
- Status: `ok`
- Capture succeeded: `true`
- guard_progress exits: `0`
- forcedAdvance events: `0`
- ready_frames: `12`
- render_health_step: `41`
- processed_rows: `164`
- traced_pixels: `56288`
- source_hits: `2717`
- miss_hits: `53571`

## Acceptance criteria for baseline reuse

Treat Fixture 001 as passing the baseline when the run preserves these checks:

- status = `ok`
- capture_succeeded = `true`
- launch audit status = `ok`
- guard_progress exits = `0`
- forcedAdvance events = `0`
- processed_rows >= `164`
- traced_pixels > `0`

## Notes

- The recorded run history shows that reducing `stepsPerRay` from `500` to `350`
  is what coincides with the first scheduler-clean capture in the current local
  evidence set.
- The current history was gathered from existing local artifacts, not by
  launching a fresh run during this doc update.
