# Fixture 003: Offset Field Baseline

## Purpose

Fixture 003 is the first spatial-regime shift after Fixture 002. It preserves
the same hardened runtime, capture, verification, ledger, and reporting
architecture while introducing one primary canonical spatial change: the field
center is moved horizontally off-axis.

Its role is to keep the simpler single-row source topology from Fixture 002 but
break radial symmetry in a clean, interpretable way. This makes Fixture 003 the
first baseline for studying asymmetry driven by field placement rather than by
source-topology expansion or broader scene restructuring.

## Why It Exists Relative To Fixture 002

- Fixture 002 established the simpler single centered source row.
- Fixture 003 keeps that source row fixed so interpretation stays comparable.
- Fixture 003 changes only the field-center placement, creating a controlled
  rightward asymmetry baseline.
- This gives the repo a canonical first step from centered-field behavior into
  off-axis spatial behavior without changing the harness machinery.

## Current Harness

- Harness script: `scripts/run_fixture_003.sh`
- Reporter: `tools/fixture_003_report.py`
- Shared report core: `tools/fixture_characterization_report.py`
- Scene: `res://test-grin-basic-visual-linear-offset-minimal.tscn`
- Fixture identity expected by launch audit: `grin_basic_visual_linear_offset_minimal`
- Fixture id: `fixture_003`
- Launcher token configured by the harness: `run_fixture_003`

## What Changed

- The fixture keeps the Fixture 002 single-row five-dot source layout.
- The fixture keeps the same backdrop, camera framing, and general harness
  structure.
- The field center is moved moderately in `+X` / rightward by `2.0` world
  units.
- The default Fixture 003 harness operating point requests:
  - `stepLength = 0.040`
  - `turnThreshold = 3.2`
  - `errorTolerance = 0.010`

## What Stayed Fixed

- Single centered source row topology from Fixture 002
- Same fixture scale and backdrop layout
- Same controller and capture flow
- Same verification fields
- Same ledger append behavior
- Same preferred visual/capture modes
- Same shared artifact/report contract

## Baseline Assumptions

- Launch only through `scripts/run_fixture_003.sh`.
- Rebuild against the hardened Windows runtime mirror before each run.
- Treat Fixture 003 as non-destructive and independent of Fixture 001 and
  Fixture 002 histories.
- Use `diagnostic_flat` for primary visual interpretation.
- Use `categorical_final` when row coverage and radial-profile artifacts are
  required.

## Canonical Analysis Basis

- Primary analysis basis: `field-relative`
- Companion analysis basis: `image-center`
- Reason: Fixture 003 intentionally breaks image-center symmetry by moving the
  field center off-axis in `+X`. The field-relative basis is therefore the
  canonical way to interpret radial structure and lateral asymmetry relative to
  the transport field itself.
- Image-center artifacts remain useful companion diagnostics because they show
  the screen-space consequence of the offset field, but they should not be
  treated as the primary canonical basis for Fixture 003.

## Control-Validation Contract

Treat a Fixture 003 run as usable for control-surface interpretation only when
all of these checks are true:

- `runtime_fingerprint_present = true`
- `assembly_timestamp_present = true`
- `effective_step_matches_requested = true`
- `row_diagnostics_present = true`
- `scheduler_clean = true`

The output contract matches Fixtures 001 and 002:

- `summary.json`
- `metrics.json`
- `summary.txt`
- `capture.png`
- `analysis_capture.png`
- `debug_capture.png`
- `row_coverage.txt`
- `row_coverage.json`
- `radial_profile.txt`
- `radial_profile.json`
- `radial_sector_profile.txt`
- `radial_sector_profile.json`
- `field_radial_profile.txt`
- `field_radial_profile.json`
- `field_radial_sector_profile.txt`
- `field_radial_sector_profile.json`
- ledger append through `tools/characterization_ledger/ledger_writer.py`

## Expected Capture Modes

- Visual mode: `diagnostic_flat`
- Analysis capture mode: `categorical_final`
- Alternate analysis capture mode: `resolved_film`

## Expected Artifact Set

- `summary.json`
- `metrics.json`
- `capture.png`
- `analysis_capture.png`
- `debug_capture.png`
- `row_coverage.txt`
- `row_coverage.json`
- `radial_profile.txt`
- `radial_profile.json`
- `radial_sector_profile.txt`
- `radial_sector_profile.json`
- `field_radial_profile.txt`
- `field_radial_profile.json`
- `field_radial_sector_profile.txt`
- `field_radial_sector_profile.json`

For Fixture 003, radial-profile artifacts are still produced under
`categorical_final`, but they should be treated as a comparative diagnostic
summary rather than as evidence of preserved radial symmetry. The point of this
fixture is that symmetry is intentionally broken by the off-axis field center.

## Best Verified Operating Point

Accepted operating point:

- `stepLength = 0.040`
- `turnThreshold = 2.4`
- `errorTolerance = 0.010`

Accepted clean reference runs:

- `2026-03-22T17-03-53`
- `2026-03-26T21-29-05`

Both runs stayed scheduler-clean and verified under the offset-field fixture
topology.

## Lower-Variance Fallback

Use the same `0.040 / 2.4 / 0.010` operating point as the fallback until a
separate verified branch is deliberately characterized.

The `turnThreshold = 3.2` path is not accepted for Fixture 003. In the March
26, 2026 blocking run (`2026-03-26T21-15-57`), that operating point triggered
an early scheduler drift:

- first `guard_progress` trigger in band `startRow=8 endRow=16`
- `forcedAdvance = 1`
- `scheduler_clean = false`
- `run_verified = false`

The failing run did not show candidate starvation (`noCandidates = 0`,
`geomPixNoCand = 0`), so this was classified as fixture-specific harness
sensitivity rather than a transport or candidate regression.

## Topology Interpretation

Placeholder. Expected initial interpretation focus:

- whether hit concentration shifts laterally relative to the still-centered
  source row
- whether rendered-no-hit area expands more strongly on one side of the image
- whether unrendered area remains cleanly separated despite the asymmetric
  field placement
