# Experimental Archive

The complete historical record of every sweep, smoke test, repeatability run, and exploratory harness. This is the lab notebook made public.

Use the [Observatory Trust Model](./observatory_trust_model.md) before promoting an archive result into the Gallery. **Trust stage is evidence strength, not physical truth.**

For maturity stage equivalents, see the Observatory Trust Model.

## Purpose of the Archive

- Reproducibility: Every timestamped folder contains the exact command-line args, scene snapshot summary, and outputs.
- Regression detection: Old baselines can be re-run against new code.
- Deep research: The full DOE matrices, telemetry parameter sweeps, and fixture runs live here.
- Audit trail: When a canonical result is promoted, its source run remains traceable in the archive.

## Organization Principles (Information Design)

- Group by experiment family (not by date).
- Every folder contains (or links to) a `README.md` with:
  - What question was being asked.
  - The generating script / scene / harness.
  - Key quantitative results and images.
  - Status (Archived / Test output / Validation candidate).
- Raw large files (giant reference-integration CSVs, full render sets) are referenced, not stored in the repo (use GitHub Releases or external storage).
- Small curated extracts (contact sheets, summary CSVs, hero crops) are promoted to `misterylabs_artifacts/`.

## Major Archive Families

**Design of Experiments (Full Factorials & Sweeps)**
- doe_overnight, doe_scheduler_resonance, doe_sensitivity, exp1_derivative_step variants.
- These produced the stride-vs-banding and precision-vs-risk maps that underpin the Curvature Benchmark.

**Telemetry & Adaptive Regimes**
- Dozens of telemetry_adaptive_* and telemetry_eff_* runs.
- Show the effect of different priors, thresholds, and statistics on closure and efficiency.

**Traversal & Scheduler Studies**
- first_pass_traversal_comparison, tile_commit_traversal_comparison, render_scheduler, threaded_band_eval.
- Demonstrate that the observed transport structure is scheduler-dependent.

**Probe & Reference-Integration Campaigns**
- reference_geodesic_probe_smoke, reference_transport_oracle_*, corner_transport_probe, transport_coherence_basin_*.
- The raw data behind the ladders and islands.

**Fixture Characterization Runs**
- fixture_001 through fixture_007 + fixture_runs.
- The exhaustive parameter and topology matrices.

**Specialized & One-off**
- camera_dist_sweep, shutdown_probe, render_test_visual_compare, various smoke tests.
- Early validation (v0.0-pre, wormhole_test).

## How to Navigate the Archive as a Museum

- Start in the canonical or research sections for the distilled story.
- When you want the full data behind a claim, follow the "Source" link in the card to the timestamped folder in `output/`.
- Use the per-folder READMEs as the exhibit labels.
- For public promotion, only the small curated assets move to `misterylabs_artifacts/`. The full archive stays here for researchers.

The Experimental Archive is deliberately not curated for casual visitors. It is the complete, queryable record that makes every claim in the other five sections auditable and reproducible.

**Status Legend Used in Archive READMEs**
- Visual reference — ready for homepage / MisterY Labs hero.
- Validation candidate — strong science, needs one more reproducibility run or better crop.
- Test output — useful for researchers, not yet promoted.
- Archived — historical; superseded by later runs.
