# Fixture 002: Linear Transport Baseline

## Purpose

Fixture 002 is the next sub-canonical, xZeno-canonical characterization fixture
for the repo. It keeps the hardened Fixture 001 runtime and reporting pipeline
but replaces the 3x5 source grid with a single centered source row so transport
behavior is easier to read spatially.

Fixture 002 is not intended to be a visually dramatic departure from Fixture
001. Its purpose is to preserve the same radial field family while simplifying
the source topology from a dense grid to a single centered row, improving
attribution and DOE interpretability without introducing additional
field-position or geometry confounders.

The intent is not to supplant Fixture 001. Fixture 001 remains the richer radial
GRIN baseline. Fixture 002 exists to provide a simpler control surface for
future DOE work where we want cleaner separation between source hits, rendered
no-hit area, and unrendered area.

## Why It Exists Relative To Fixture 001

- Fixture 001 is the verified baseline for the current full radial-grid harness.
- Fixture 002 keeps the same capture, verification, ledger, and reporting
  architecture, but reduces geometric complexity.
- The single-row source layout should make lateral transport changes easier to
  interpret without introducing new runtime or reporting machinery.

## Current Harness

- Harness script: `scripts/run_fixture_002.sh`
- Reporter: `tools/fixture_002_report.py`
- Shared report core: `tools/fixture_characterization_report.py`
- Scene: `res://test-grin-basic-visual-linear-minimal.tscn`
- Fixture identity expected by launch audit: `grin_basic_visual_linear_minimal`
- Fixture id: `fixture_002`
- Launcher token configured by the harness: `run_fixture_002`

## Baseline Assumptions

- Launch only through `scripts/run_fixture_002.sh`.
- Rebuild against the hardened Windows runtime mirror before each run.
- Treat Fixture 002 as non-destructive and independent of Fixture 001 history.
- Use the same verified artifact contract already established for Fixture 001.
- Prefer `diagnostic_flat` for visual interpretation and `categorical_final`
  when explicit final-hit / rendered-no-hit / unrendered separation is needed.

## Canonical Analysis Basis

- Primary analysis basis: `image-center`
- Companion analysis basis: `field-relative`
- Reason: Fixture 002 keeps the transport field centered, so image center and
  effective field center coincide for interpretation purposes. The original
  radial and left/right image-center artifacts therefore remain the primary
  canonical basis.
- Field-relative artifacts may still be emitted for consistency across the
  shared fixture reporting flow and to keep direct comparison structure aligned
  with later asymmetric fixtures.

## Control-Validation Contract

Treat a Fixture 002 run as usable for control-surface interpretation only when
all of these checks are true:

- `runtime_fingerprint_present = true`
- `assembly_timestamp_present = true`
- `effective_step_matches_requested = true`
- `row_diagnostics_present = true`
- `scheduler_clean = true`

The output contract is the same as Fixture 001:

- `summary.json`
- `metrics.json`
- `summary.txt`
- ledger append through `tools/characterization_ledger/ledger_writer.py`
- verification fields in run artifacts
- row coverage artifacts
- radial profile artifacts when `categorical_final` is used
- radial sector profile artifacts when `categorical_final` is used
- field-relative radial artifacts when `categorical_final` is used
- field-relative radial sector artifacts when `categorical_final` is used

## Planned Operating-Point Characterization Workflow

1. Start from the default Fixture 002 harness with the linear-row geometry.
2. Hold scene topology fixed and vary transport controls through harness env
   overrides such as `FIXTURE_002_STEP_LENGTH`,
   `FIXTURE_002_MIN_STEP_LENGTH`, `FIXTURE_002_TURN_THRESHOLD`, and
   `FIXTURE_002_ERROR_TOLERANCE`.
3. Keep interpretation restricted to fully verified runs.
4. Use `diagnostic_flat` for quick visual sanity checks.
5. Use `categorical_final` when capture classification and radial/structured
   summaries are needed for comparison.

## Expected Capture Modes

- Visual mode: `diagnostic_flat`
- Analysis capture mode: `categorical_final`
- Alternate analysis capture mode: `resolved_film`

## Expected Artifacts

- `capture.png` if later exported manually from a selected canonical run
- `capture.png`
- `analysis_capture.png`
- `debug_capture.png`
- `row_coverage.txt`
- `row_coverage.json`
- `radial_profile.txt` when applicable
- `radial_profile.json` when applicable
- `radial_sector_profile.txt` when applicable
- `radial_sector_profile.json` when applicable
- `field_radial_profile.txt` when applicable
- `field_radial_profile.json` when applicable
- `field_radial_sector_profile.txt` when applicable
- `field_radial_sector_profile.json` when applicable

For Fixture 002, radial analysis is still meaningful when we use
`categorical_final`, because the underlying transport field remains centered and
radially organized even though the source geometry is simpler.

The harness uses `capture.png` as the primary analysis artifact and mirrors that
same image to `analysis_capture.png` so Fixture 002 can participate in the same
comparison/reporting rhythm while still exposing the explicit artifact name
requested for fixture-note work.

## Best Verified Operating Point

The March 26, 2026 rerun is accepted as the refreshed post-field-fix
comparison baseline for Fixture 002.

- Timestamp: `2026-03-26T21-15-16`
- Output path: `output/fixture_runs/fixture_002/2026-03-26T21-15-16`
- Status: `ok`
- guard_progress exits: `0`
- forcedAdvance events: `0`
- processed_rows: `164`
- traced_pixels: `54880`
- source_hits: `2149`
- miss_hits: `52731`
- radial_overall_hit_fraction: `0.027299`
- radial_sector_left_hit_fraction: `0.030615`
- radial_sector_right_hit_fraction: `0.023984`
- turnThreshold: `4`

This run is accepted even though `run_verified = false` against the older
pre-fix comparison target, because the mismatch is attributable to the
corrected `r_outer / r_inner` field cutoff behavior rather than to scheduler
instability.

## Lower-Variance Fallback

Placeholder. To be filled once repeatable verified runs establish the first
stable fallback branch.

## Accepted Post-Fix Comparison Baseline

- Timestamp: `2026-03-26T21-15-16`
- Scene: `res://test-grin-basic-visual-linear-minimal.tscn`
- Fixture: `grin_basic_visual_linear_minimal`
- Transport: `GRIN_Optical`
- Status: `ok`
- Capture succeeded: `true`
- guard_progress exits: `0`
- forcedAdvance events: `0`
- processed_rows: `164`
- traced_pixels: `54880`
- source_hits: `2149`
- miss_hits: `52731`

This accepted comparison baseline supersedes the older pre-fix capture for
future post-`r_outer / r_inner` validation work.

## Topology Interpretation

Placeholder. Expected initial interpretation focus:

- whether hits remain concentrated near the center row or spread asymmetrically
- whether rendered no-hit area expands without corresponding hit conversion
- whether unrendered area stays clearly separated from rendered outcomes
