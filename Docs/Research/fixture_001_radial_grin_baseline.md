# Fixture 001: Radial GRIN Baseline

## Purpose

Fixture 001 is the compact radial GRIN baseline used to validate that the
current curved-ray transport path can launch, render, capture, and report a
stable result through the dedicated harness.

This note records the current harness contract and the local run history that
supports promoting A2 as the official Fixture 001 baseline.

It also records the first verified repeat-sweep result for Fixture 001 so the
project has a concrete example of how to characterize a fixture control surface
using only hardened, fully verified runs.

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

### Verified-control contract

Treat a Fixture 001 run as usable for control-surface interpretation only when
all of these verification checks are true:

- `runtime_fingerprint_present = true`
- `assembly_timestamp_present = true`
- `effective_step_matches_requested = true`
- `row_diagnostics_present = true`
- `scheduler_clean = true`

This is the current baseline procedure for validating Fixture 001 controls:

1. Launch only through `scripts/run_fixture_001.sh`.
2. Rebuild against the hardened Windows runtime mirror before each run.
3. Generate `params.json`, `metrics.json`, `summary.json`, and ledger append
   through the existing report flow.
4. Interpret only runs that remain fully verified after report generation.

## Verified characterization update (2026-03-22)

A verified top-config repeat sweep was run on March 22, 2026 through the
hardened Windows runtime mirror at `/mnt/c/godot/godot_xPRIMEray`.

The repeat set used three verified runs each for:

- `stepLength=0.040`, `errorTolerance=0.012`, `turnThreshold=2.0`
- `stepLength=0.040`, `errorTolerance=0.010`, `turnThreshold=2.4`
- `stepLength=0.040`, `errorTolerance=0.010`, `turnThreshold=3.2`

For the `turnThreshold=3.2` branch, `errorTolerance=0.010` was used explicitly
as the nearest baseline-compatible path already established in prior Fixture
001 work.

### Repeatability summary

| Config | mean hit_rate | median hit_rate | std dev hit_rate | mean traced_rate | std dev traced_rate | robust_score | verified runs |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `0.040 / 0.012 / 2.0` | `109.654/s` | `110.781/s` | `1.814/s` | `2361.923/s` | `22.902/s` | `109.874/s` | 3 |
| `0.040 / 0.010 / 2.4` | `109.540/s` | `108.830/s` | `1.159/s` | `2373.456/s` | `4.672/s` | `108.251/s` | 3 |
| `0.040 / 0.010 / 3.2` | `112.289/s` | `112.484/s` | `3.249/s` | `2404.632/s` | `50.686/s` | `110.860/s` | 3 |

### Topology call

Fixture 001 verified characterization indicates a stable local basin centered
near `stepLength=0.040`, `errorTolerance=0.010`, `turnThreshold≈3.2`.

The best verified operating point in current testing is `0.040 / 0.010 / 3.2`,
with `0.040 / 0.010 / 2.4` as a lower-variance fallback.

This is best described as a stable basin with a mild ridge toward
`turnThreshold=3.2`, not a fragile single-run spike.

### Why this matters

- The top verified point was not isolated; neighboring verified settings stayed
  in the same performance band.
- All nine repeat runs stayed fully verified, so the result is not tied to a
  build/runtime mismatch or scheduler regression.
- This gives Fixture 001 a usable state-of-the-art reference for future
  fixture-note writeups: record the winner, the fallback, and the basin shape.

### Radial profile interpretation

Fixture 001 optimization improves the same outer-mid annular hit band rather
than shifting or broadening it. The best verified config, `0.040 / 0.012 / 2.0`,
increases localized hit conversion in bins 5 and 6 without changing rendered
coverage.

## Historical run history

The current local Fixture 001 history under `output/fixture_runs/fixture_001`
contains six recorded runs from March 19-20, 2026.

| Label | Timestamp | StepsPerRay | Status | guard_progress exits | forcedAdvance | processed_rows | traced_pixels | Notes |
| --- | --- | ---: | --- | ---: | ---: | ---: | ---: | --- |
| A0 | `2026-03-19T22-08-00` | 500 | ok | 18 | 18 | 164 | 39200 | First functional captured baseline, but heavily scheduler-limited |
| A1 | `2026-03-19T22-49-38` | 500 | ok | 7 | 7 | 164 | 24320 | Improved over A0, still scheduler-limited |
| A1.1 | `2026-03-19T22-55-23` | 500 | ok | 10 | 10 | 164 | 27616 | Additional 500-step run, still scheduler-limited |
| A2 | `2026-03-19T22-57-53` | 350 | ok | 0 | 0 | 164 | 56288 | First scheduler-clean capture in the current run history |
| A3 | `2026-03-20T18-26-33` | 300 | ok | 1 | 1 | 172 | 71744 | Single-parameter variant from A2; higher coverage and traced pixels, but scheduler guard regressed |
| A4 | `2026-03-20T18-29-51` | 400 | ok | 0 | 0 | 164 | 40608 | Single-parameter variant from A2; scheduler-clean, but lower traced pixel count than A2 |

## StepsPerRay Sweep Comparison

The completed single-parameter `StepsPerRay` sweep around the current baseline
uses the following runs:

| Label | StepsPerRay | guard_progress | forcedAdvance | traced_pixels | processed_rows | runtime |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| A3 | 300 | 1 | 1 | 71744 | 172 | 29.458s |
| A2 | 350 | 0 | 0 | 56288 | 164 | 28.825s |
| A4 | 400 | 0 | 0 | 40608 | 164 | 29.777s |

## Baseline Decision

A2 is now the official Fixture 001 baseline.

It remains the strongest balance of stability and output quality in the current
local run history:

- `300` produced more traced pixels, but it regressed scheduler cleanliness with
  one `guard_progress` exit and one `forcedAdvance=1` event.
- `400` stayed scheduler-clean, but it reduced `traced_pixels` from `56288` to
  `40608` and increased runtime from `28.825s` to `29.777s`.
- `350` preserves scheduler cleanliness while keeping stronger output quality
  than the stable `400` variant, making it the best current balance.

## A3 variant result

- Label: `A3`
- Timestamp: `2026-03-20T18-26-33`
- Scene: `res://test-grin-basic-visual-minimal.tscn`
- Fixture: `grin_basic_visual_minimal`
- Launcher observed in run log: `manual`
- Transport: `GRIN_Optical`
- StepsPerRay: `300`
- Status: `ok`
- Capture succeeded: `true`
- guard_progress exits: `1`
- forcedAdvance events: `1`
- ready_frames: `12`
- render_health_step: `41`
- processed_rows: `172`
- traced_pixels: `71744`
- source_hits: `3361`
- miss_hits: `68383`

Compared with A2, A3 only changes `StepsPerRay` from `350` to `300`. The run
still captures successfully, but it is no longer scheduler-clean because one
`guard_progress` exit and one `forcedAdvance=1` yield abort appear in `run.log`.

## A4 variant result

- Label: `A4`
- Timestamp: `2026-03-20T18-29-51`
- Scene: `res://test-grin-basic-visual-minimal.tscn`
- Fixture: `grin_basic_visual_minimal`
- Launcher observed in run log: `manual`
- Transport: `GRIN_Optical`
- StepsPerRay: `400`
- Status: `ok`
- Capture succeeded: `true`
- guard_progress exits: `0`
- forcedAdvance events: `0`
- ready_frames: `12`
- render_health_step: `41`
- processed_rows: `164`
- traced_pixels: `40608`
- source_hits: `1928`
- background_hits: `6411`
- miss_hits: `32269`

Compared with A2, A4 only changes `StepsPerRay` from `350` to `400`. The run
remains scheduler-clean, but traced pixel count drops from `56288` to `40608`,
so A2 remains the stronger baseline in the current local history.

## Official Baseline

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

## Next Study Direction

Hold A2 fixed as the reference configuration and run the next single-parameter
micro-sweep on `StepLength`.

Recommended rationale:

- `StepsPerRay` has now been bracketed at `300`, `350`, and `400`.
- `StepLength` is the next most direct ray-march control that can change both
  scheduler behavior and output coverage while leaving the established A2
  baseline intact.
- A focused `StepLength` sweep should make it easier to see whether A2 can gain
  more traced coverage without giving back scheduler cleanliness.

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
