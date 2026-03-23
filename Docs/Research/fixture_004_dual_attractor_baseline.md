# Fixture 004: Dual Attractor Baseline

## Purpose

Fixture 004 is the first multi-attractor characterization fixture in the repo.
It preserves the simpler single-row source topology and the hardened runtime,
capture, verification, ledger, and reporting pipeline established by Fixtures
002 and 003 while introducing two controlled field centers as the primary
canonical spatial change.

Its role is to create a minimal competing-basin baseline. The goal is not to
expand source complexity or restructure the fixture, but to keep the Fixture
003 operating style as stable as practical while moving from one attractor to
two.

## Why It Exists Relative To Fixture 003

- Fixture 003 established the first off-axis single-attractor asymmetry
  baseline.
- Fixture 004 keeps the same single-row five-dot source topology and general
  scene framing.
- Fixture 004 changes the primary field topology from one field center to two
  controlled field centers.
- This gives the repo a canonical next step for studying transport under
  competing local basins without changing the hardened characterization
  pipeline.

## Current Harness

- Harness script: `scripts/run_fixture_004.sh`
- Reporter: `tools/fixture_004_report.py`
- Shared report core: `tools/fixture_characterization_report.py`
- Scene: `res://test-grin-basic-visual-linear-dual-attractor-minimal.tscn`
- Fixture identity expected by launch audit: `grin_basic_visual_linear_dual_attractor_minimal`
- Fixture id: `fixture_004`
- Launcher token configured by the harness: `run_fixture_004`

## What Changed

- The single Fixture 003 field center is replaced by two controlled field
  centers.
- The dual-center arrangement is horizontal and symmetric:
  - left attractor at `X = -2.0`
  - right attractor at `X = +2.0`
- The controller now applies fixture-level field overrides across all
  `FieldSource3D` nodes under the same fixture root so the two attractors stay
  coupled under the same control surface.
- The default Fixture 004 harness operating point requests:
  - `stepLength = 0.040`
  - `turnThreshold = 2.4`
  - `errorTolerance = 0.010`

## What Stayed Fixed

- Single centered source row topology from Fixtures 002 and 003
- Same five-dot row layout
- Same backdrop, camera framing, and overall fixture scale as Fixture 003
- Same controller, verification, capture, ledger, and report architecture
- Same preferred visual/capture modes
- Same report artifact contract
- Same ledger append behavior

## Control-Validation Contract

Treat a Fixture 004 run as usable for control-surface interpretation only when
all of these checks are true:

- `runtime_fingerprint_present = true`
- `assembly_timestamp_present = true`
- `effective_step_matches_requested = true`
- `row_diagnostics_present = true`
- `scheduler_clean = true`

Launch Fixture 004 only through `scripts/run_fixture_004.sh`, and treat it as
non-destructive and independent of Fixtures 001–003 histories.

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
- ledger append through `tools/characterization_ledger/ledger_writer.py`

## Canonical Analysis Basis

- Initial scaffold basis:
  - image-center artifacts may still be emitted for continuity
  - single-field-relative artifacts may still be emitted for continuity
- Current continuity anchor:
  - the harness `FieldPath` resolves to the left / primary attractor so the
    shared field-relative artifact path remains intact
- Expected future direction:
  - Fixture 004 is expected to motivate a nearest-attractor-relative analysis
    basis, because a single fixed attractor-relative origin is only a
    continuity bridge once transport begins to partition around competing local
    basins
- Promotion placeholder:
  - the first promoted Fixture 004 interpretation should record how these
    continuity artifacts compare against the future nearest-attractor reference
    frame

## Best Verified Operating Point

Placeholder. To be filled once the first verified Fixture 004 run is promoted.

## Lower-Variance Fallback

Placeholder. To be filled once repeatable verified runs establish the first
stable fallback operating point.

## Topology Interpretation

Placeholder. Expected initial interpretation focus:

- whether hit structure partitions around the left and right attractor basins
- whether the central source row feeds one basin preferentially or splits
  comparably between them
- whether rendered-no-hit and unrendered regions remain scheduler-clean under
  dual-center competition
