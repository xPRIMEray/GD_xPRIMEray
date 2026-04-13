# Deterministic Hermetic Fixture Validation for Throat-Like Transport in Overspace Ray-Tracing Systems

## Abstract
We present a deterministic renderer-validation framework for throat-like transport behavior in overspace ray-tracing systems. The framework promotes selected overspace scenes into hermetically enclosed fixtures and evaluates them through the `GrinFilmCamera` transport path rather than through interactive rasterized inspection alone. Within this harness, every pixel is required to return a classified transport outcome within the configured transport budget. The current taxonomy includes `geom_hit`, `portal_hit`, `throat_entry`, `throat_exit`, `throat_shell_transform`, `throat_inner_absorb`, `background_hit`, `escaped_no_hit`, and `budget_exhausted`. In the current sealed fixture we obtain full-frame classified pixel coverage with deterministic artifact generation, including category maps, legends, summary reports, and a throat-depth interaction map. The current scope is intentionally narrower than full Einstein or Morris-Thorne metric transport; the contribution is a validation-first renderer framework and throat-transport taxonomy that can support later, stronger transport models.

## 1. Introduction
Portal and wormhole-inspired rendering demos can show compelling traversal behavior without yet establishing that the renderer can account for every pixel outcome in a sealed scene. That distinction matters once the system is used as a research instrument rather than only as a visual demo. A transport system that leaves rays effectively unaccounted for may still look plausible, but it is weaker as a validation substrate.

This paper frames the current overspace work as a deterministic renderer-validation framework. The focus is not to claim a physically complete wormhole solution. Instead, the goal is to show that throat-like transport can be studied in a controlled, hermetic fixture where every pixel is classified, every run is reproducible, and failure states remain visible rather than hidden.

The resulting system is intended as a bridge layer. It is stronger than a pure portal interaction demo because it operates on the renderer truth path, enforces full-frame accounting, and emits deterministic artifacts. It is weaker than a complete relativistic throat solver because the current transport semantics remain bounded and framework-level rather than metric-complete.

## 2. Framing and Positioning
The present work sits between two familiar modes of graphics and physics practice.

First, it inherits from game-engine and portal-style rendering systems, where traversal and spatial remap are often implemented for interactivity and visual continuity. Second, it borrows motivation from lensing and wormhole transport intuition, where the transport law itself becomes central to interpretation.

The contribution here is neither a new analytic solution nor a claim of completed general-relativistic rendering. The contribution is a validation language:

- hermetic enclosed fixtures
- explicit per-pixel transport categories
- deterministic capture and reporting
- renderer-side evidence that all pixels resolve to classified outcomes

This framing is consistent with the broader shared related-work note in [shared_related_work.md](/home/bb/code/godot_xPRIMEray/Docs/papers/shared_related_work.md:1), especially the emphasis on deterministic validation and measured structure.

## 3. System Architecture
The current implementation can be summarized as four layers.

### 3.1 Throat-Aware Portal Seam
Phase A introduced an additive throat-aware seam in the portal framework. The existing stable portal transport shell remains intact, but explicit throat metadata and inspector controls are exposed so the renderer and fixture tooling can reason about throat structure without requiring a transport rewrite.

### 3.2 Hermetic Overspace Fixture
Phase A.2 promoted a sealed overspace room into a renderer-validation fixture. This fixture is rendered through `GrinFilmCamera`, not validated only through the Godot scene view. The sealed geometry removes open-sky leaks and accidental infinity visibility while preserving a stable throat-bearing portal pair.

### 3.3 Renderer-Side Classification
The renderer classifies each pixel into a transport outcome. The current categories are:

- `geom_hit`
- `portal_hit`
- `throat_entry`
- `throat_exit`
- `throat_shell_transform`
- `throat_inner_absorb`
- `background_hit`
- `escaped_no_hit`
- `budget_exhausted`

This classification is emitted on the renderer path itself, making it suitable for fixture validation rather than only interaction debugging.

### 3.4 Deterministic Artifact Pipeline
Each run produces machine-readable and human-readable artifacts, including:

- `summary.json`
- `metrics.json`
- `coverage.json`
- `throat_depth.json`
- `summary.txt`
- `coverage.txt`
- `throat_depth.txt`
- classification and legend images

The validated fixture path used in this draft is:

- Scene: `res://test-overspace-hermetic-fixture.tscn`
- Launcher: `./scripts/run_fixture_005.sh`
- Run directory: `output/fixture_runs/fixture_005/2026-04-12T23-32-11`

## 4. Hermetic Fixture Rule
We adopt the following harness rule for enclosed overspace validation scenes:

> A scene promoted to an overspace validation fixture should be hermetically enclosed and should produce 100% classified pixel outcomes under `GrinFilmCamera` transport, within the configured transport budget.

This statement is a validation rule, not a universal physical claim. Its purpose is methodological: if a pixel is not classified, the system should expose that gap rather than hide it behind visually plausible output.

For a hermetic enclosed fixture, the following are the primary contracts:

- `classified_pixels == total_pixels`
- `classified_coverage_ratio == 1`
- `escaped_no_hit` should ideally be zero
- `budget_exhausted` should ideally be zero

This rule is documented separately in [hermetic_fixture_rule.md](/home/bb/code/godot_xPRIMEray/Docs/validation/hermetic_fixture_rule.md:1).

## 5. Throat Transport Taxonomy
The current taxonomy should be read as a renderer-facing causal vocabulary rather than as a full metric ontology.

- `geom_hit`
  Final classified outcome lands on explicit scene geometry.
- `portal_hit`
  Outcome is attributable to the portal seam without a richer throat-shell event.
- `throat_entry`
  Entry-side boundary-layer event at the throat.
- `throat_exit`
  Exit-side boundary-layer event at the throat.
- `throat_shell_transform`
  Boundary-layer or shell-mediated transformation in the current bounded throat model.
- `throat_inner_absorb`
  Terminal absorption in the current inner-throat model.
- `background_hit`
  Intentional background classification when present.
- `escaped_no_hit`
  Transport escaped classification before termination.
- `budget_exhausted`
  Transport budget ended before classification completed.

An aggregate `throat_event` count is preserved for summary compatibility:

`throat_event = throat_entry + throat_exit + throat_shell_transform + throat_inner_absorb`

## 6. Validation Method
The current deterministic validation method is:

1. Build the active WSL repository.
2. Launch the hermetic fixture through `fixture_005`.
3. Capture renderer-side transport classification and debug imagery.
4. Parse the raw run log into deterministic reports and image artifacts.
5. Verify the hermetic coverage invariant from the emitted metrics.

The current additional diagnostic layer is a throat-depth map. This map records per-pixel throat interaction count derived from current renderer state and emits:

- `throat_depth_map.png`
- `throat_depth_annotated.png`
- `throat_depth_legend.png`
- `throat_depth.json`
- `throat_depth.txt`

This is intentionally a narrow extension. It improves scientific legibility without changing the underlying transport law.

## 7. Current Results
The evidence base for this initial draft is the validated `fixture_005` run at:

`/home/bb/code/godot_xPRIMEray/output/fixture_runs/fixture_005/2026-04-12T23-32-11`

### 7.1 Run Status
- `status = ok`
- `run_verified = true`
- runtime `= 17.799335 s`

### 7.2 Coverage Metrics
- `total_pixels = 129600`
- `classified_pixels = 129600`
- `classified_coverage_ratio = 1`
- `escaped_no_hit = 0`
- `budget_exhausted = 0`
- `unclassified = 0`

### 7.3 Transport Distribution
- `geom_hit = 75361`
- `portal_hit = 32418`
- `throat_event = 21821`
- `throat_entry = 0`
- `throat_exit = 132`
- `throat_shell_transform = 21689`
- `throat_inner_absorb = 0`

### 7.4 Throat-Depth Metrics
- `throat_pixels = 33356`
- `max_interaction_count = 107`
- `mean_interaction_count = 2.335472`

### 7.5 Current Artifact Set
- Classification image: [capture.png](/home/bb/code/godot_xPRIMEray/output/fixture_runs/fixture_005/2026-04-12T23-32-11/capture.png)
- Classification legend: [coverage_legend.png](/home/bb/code/godot_xPRIMEray/output/fixture_runs/fixture_005/2026-04-12T23-32-11/coverage_legend.png)
- Annotated classification composite: [coverage_annotated.png](/home/bb/code/godot_xPRIMEray/output/fixture_runs/fixture_005/2026-04-12T23-32-11/coverage_annotated.png)
- Throat-depth map: [throat_depth_map.png](/home/bb/code/godot_xPRIMEray/output/fixture_runs/fixture_005/2026-04-12T23-32-11/throat_depth_map.png)
- Throat-depth legend: [throat_depth_legend.png](/home/bb/code/godot_xPRIMEray/output/fixture_runs/fixture_005/2026-04-12T23-32-11/throat_depth_legend.png)
- Annotated throat-depth composite: [throat_depth_annotated.png](/home/bb/code/godot_xPRIMEray/output/fixture_runs/fixture_005/2026-04-12T23-32-11/throat_depth_annotated.png)

## 8. Limitations
The current system has clear and important limits.

- It is not full Einstein or Morris-Thorne metric transport.
- It does not solve the Einstein field equations.
- It does not claim a physically complete relativistic wormhole renderer.
- The current throat semantics are bounded framework semantics layered over a stable portal seam.
- The current taxonomy is stronger than a visual demo taxonomy because it is emitted on the renderer path under deterministic fixture conditions.
- The current taxonomy is weaker than a full relativistic description because shell and boundary-layer semantics are not yet derived from a complete metric model.

## 9. Future Work
The next technically natural steps are:

1. richer `BoundaryLayerVolume` attribution so shell behavior can be decomposed beyond the current subtype buckets
2. entry and exit symmetry refinement where it can be achieved without destabilizing the fixture
3. additional hermetic fixtures that vary throat geometry and camera placement while preserving full classified coverage
4. later integration with stronger curvature-aware or metric-aware transport models once the validation regime is mature

## 10. Reproducibility Appendix
Commands used for the current milestone:

```bash
cd /home/bb/code/godot_xPRIMEray
dotnet build 'Physical Light and Camera Units.csproj' -c Debug -v minimal
python3 -m py_compile tools/fixture_characterization_report.py tools/fixture_005_report.py
./scripts/run_fixture_005.sh
```

The report can also be regenerated directly:

```bash
.venv/bin/python tools/fixture_005_report.py \
  --fixture-id fixture_005 \
  --timestamp 2026-04-12T23-32-11 \
  --scene res://test-overspace-hermetic-fixture.tscn \
  --launcher run_fixture_005 \
  --run-dir output/fixture_runs/fixture_005/2026-04-12T23-32-11 \
  --log-path output/fixture_runs/fixture_005/2026-04-12T23-32-11/run.log \
  --capture-path output/fixture_runs/fixture_005/2026-04-12T23-32-11/capture.png \
  --runtime-seconds 17.799335 \
  --godot-exit-code 0 \
  --settle-frames 12 \
  --min-rh-step 1 \
  --min-processed-rows 270 \
  --capture-film-opacity 1.0 \
  --compare-grid 0 \
  --compare-crosshair 0 \
  --requested-step-length 0.05 \
  --requested-min-step-length 0.02 \
  --requested-steps-per-ray 640 \
  --requested-turn-threshold 2.4 \
  --requested-error-tolerance 0.010
```

Primary machine-readable artifacts:

- `summary.json`
- `metrics.json`
- `coverage.json`
- `throat_depth.json`

Primary human-readable artifacts:

- `summary.txt`
- `coverage.txt`
- `throat_depth.txt`

Primary figure candidates:

- `capture.png`
- `coverage_annotated.png`
- `coverage_legend.png`
- `throat_depth_map.png`
- `throat_depth_annotated.png`
- `throat_depth_legend.png`
