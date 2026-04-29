# Audit: Domain-Aware Rendering Changes

Date: 2026-04-28
Audit range used: `1348589..HEAD` (`HEAD=dd4ff22`). Note: the working tree was on `main` and `git diff main...HEAD` was empty, so the recent committed range was audited instead.

## Minimal Fix Implementation Update

Date: 2026-04-29

### Pass / Fail Table

| Required fix | Status | Evidence |
|---|---|---|
| Keep fixture tile-priors resolver off by default | PASS | `scripts/run_fixture_tile_priors_active.sh` now passes `--enable-domain-aware-first-hit-resolver=${XPRIMERAY_ENABLE_RESOLVER:-0}` and documents observe-only default. |
| Add resolver-change telemetry | PASS | `domain_telemetry_summary.json` now includes `resolver_change_summary` with compared/changed pixels, collider-id changes, hit-distance changes, mean/max hit-distance delta, mean score delta, and score-component means. |
| Export normal discontinuity | PASS | Telemetry export now writes `normal_discontinuity.png` and includes its stats in summary JSON. |
| Rename derived PhaseCoherence | PASS | Renderer field is now `BoundaryStability`; resolver weight is `DomainAwareBoundaryStabilityWeight`. No renderer-integrated `PhaseCoherence` field remains in source. |
| Add quick audit fixture mode | PASS | `--domain-audit-quick` honors `--render-test-frames`/`--render-test-warmup` and limits default runs to one baseline run; `scripts/run_domain_audit_quick.sh` runs OFF, telemetry ON, resolver ON under `output/domain_audit_quick/`. |
| Soften docs claims | PASS | Updated docs describe integrated domain maps as heuristic diagnostics, not proof of metric-only domain ownership; inspiration references are not validation evidence. |
| Default render behavior unchanged | PASS | Feature defaults remain false; quick OFF vs telemetry ON beauty PNG hashes were identical. |
| Resolver ON change counters when changes occur | PARTIAL | Counters are emitted. In the quick fixture run, resolver made zero changed-hit selections, so nonzero-counter behavior was not exercised by this scene. |

### Exact Files Changed

Implementation files:

| File | Classification |
|---|---|
| `GrinFilmCamera.cs` | telemetry-only and gated render-behavior telemetry; resolver behavior remains gated off by default |
| `RendererCore/Common/DomainTelemetry.cs` | telemetry-only |
| `RendererCore/Testing/RenderTestRunner.cs` | validation/tooling |
| `scripts/run_fixture_tile_priors_active.sh` | validation/tooling |
| `scripts/run_domain_audit_quick.sh` | validation/tooling |

Documentation files:

| File | Classification |
|---|---|
| `Docs/README.md` | docs/navigation |
| `Docs/README_obs.md` | docs/navigation |
| `Docs/diagnostics/domain_ownership.md` | docs/navigation |
| `Docs/glossary.md` | docs/navigation |
| `Docs/index.md` | docs/navigation |
| `Docs/validation/AUDIT_DOMAIN_AWARE_RENDERING.md` | validation/tooling docs |

Generated/runtime artifacts present after validation:

- `.godot/mono/temp/...` build outputs changed by `dotnet build`.
- `output/domain_audit_quick/20260429T005445Z/` contains the quick audit artifacts.
- Pre-existing `output/domain_audit/` remains untracked from the original audit run.

### Commands Run

| Command | Result |
|---|---|
| `rg -n "DomainAwarePhaseWeight|PhaseCoherence|BoundaryStability|NormalDiscontinuity|resolver_change_summary|BuildDomainResolverTelemetry|render-test-frames|BuildDefaultRuns|ApplyStartupCliFlagOverrides|FramesPerRun = Math.Max" GrinFilmCamera.cs RendererCore/Common/DomainTelemetry.cs RendererCore/Testing/RenderTestRunner.cs` | PASS. Located source rename/export/harness points before patching. |
| `rg -n "Penrose|Kajiya|Bandyopadhyay|metric-only|metric structure alone|proof|validation evidence|DomainTelemetry|PhaseCoherence|phase coherence|domain maps|Domain-aware|domain-aware" Docs mkdocs.yml scripts/run_fixture_tile_priors_active.sh` | PASS. Located docs claims to soften. |
| `dotnet build 'Physical Light and Camera Units.sln' -c Debug -v minimal` | PASS. 35 warnings, 0 errors. |
| `scripts/godot_local.sh --headless --path . --quit` | PARTIAL. Process exited 0, but logged existing `NullReferenceException` in `RayBeamRenderer.BuildBoundaryLayerSnap`. |
| `DOMAIN_AUDIT_QUICK_FRAMES=8 DOMAIN_AUDIT_QUICK_WARMUP=1 timeout 300s scripts/run_domain_audit_quick.sh` | PASS with logged Godot shutdown caveat. OFF and telemetry-on completed harness success then Godot Mono aborted during shutdown; script records `effective=0`. Resolver-on exited 0. |
| `python3 -m json.tool output/domain_audit_quick/20260429T005445Z/telemetry_on/*.domain_telemetry_summary.json` | PASS. JSON valid; includes `normal_discontinuity` map and disabled resolver summary. |
| `python3 -m json.tool output/domain_audit_quick/20260429T005445Z/resolver_on/*.domain_telemetry_summary.json` | PASS. JSON valid; includes enabled resolver summary. |
| `sha256sum output/domain_audit_quick/20260429T005445Z/off/*.png output/domain_audit_quick/20260429T005445Z/telemetry_on/*.png output/domain_audit_quick/20260429T005445Z/resolver_on/*.png` | PASS. OFF, telemetry ON, and resolver ON beauty PNGs all had hash `cc8bc6db8f2d96e85e39da9750e3507435ecf0768e57d5ddd2b3802a5f4c2dde`. |
| `file output/domain_audit_quick/20260429T005445Z/telemetry_on/*.png output/domain_audit_quick/20260429T005445Z/resolver_on/*.normal_discontinuity.png` | PASS. Telemetry PNGs are 80x45 RGBA, matching summary render dimensions. |

### Quick Audit Results

Output root: `output/domain_audit_quick/20260429T005445Z/`

| Case | Harness result | Notes |
|---|---|---|
| OFF | PASS effective, exit 134 | Harness reported success; Godot Mono aborted during shutdown after writing artifacts. |
| Telemetry ON | PASS effective, exit 134 | Harness reported success; beauty render identical to OFF. |
| Resolver ON | PASS, exit 0 | Resolver telemetry emitted; no selected-hit changes occurred in this short curved-minimal run. |

Telemetry summary facts from resolver-on run:

| Metric | Value |
|---|---:|
| `resolver_change_summary.enabled` | `true` |
| `compared_pixels` | 120 |
| `changed_pixels` | 0 |
| `changed_collider_id_pixels` | 0 |
| `changed_hit_distance_pixels` | 0 |
| `mean_hit_distance_delta` | 0 |
| `max_hit_distance_delta` | 0 |
| `score_available_pixels` | 120 |
| `mean_score_delta` | 0 |

Normal discontinuity export:

| Artifact | Result |
|---|---|
| `normal_discontinuity.png` | PASS, exported at 80x45 |
| Summary stats | PASS, `min=0`, `max=0`, `mean=0` for the quick curved-minimal run |

### Domain Resolver Stress Follow-Up

Follow-up stress validation is documented in
`Docs/validation/domain_resolver_stress_fixture.md`.

Result: PASS. Three existing candidate fixtures were tried first and did not
produce nonzero resolver changes. A small deterministic stress fixture was then
added under `test-domain-resolver-stress.tscn`. Its final run wrote artifacts to
`output/domain_resolver_stress/domain_resolver_stress/20260429T013524Z/`.

Key facts from that run:

| Check | Result |
|---|---|
| OFF vs telemetry ON beauty | Identical |
| Resolver ON `changed_pixels` | 8 |
| Changed collider id pixels | 8 |
| Changed hit-distance pixels | 8 |
| Changed-pixel localization | 8 pixels in bbox `(38,10)-(41,11)` |
| Boundary at changed pixels | mean `0.698039`, max `0.698039` |
| Normal discontinuity at changed pixels | mean `0.007843`, max `0.007843` |
| Selection flip at changed pixels | mean `1.0`, max `1.0` |

This proves the resolver-change telemetry can report a real selected-hit change
on acquired hit candidates. It remains a diagnostic stress test, not evidence
that the heuristic resolver improves arbitrary scenes.

### Remaining Risks

1. The quick curved-minimal resolver run did not produce changed-hit pixels; the separate domain-resolver stress fixture now covers the nonzero-counter case.
2. Godot Mono shutdown aborts occurred after harness success in OFF and telemetry-on passes; artifacts were written, but process exit codes were not clean.
3. Headless project load still logs `NullReferenceException` in `RayBeamRenderer.BuildBoundaryLayerSnap`, unrelated to this patch but still a runtime confidence risk.
4. Domain IDs and confidence remain low-information on the quick curved-minimal sample (`domain_id` p10/p50/p90 all 5; `domain_confidence` p10/p50/p90 all 0.55).
5. Integrated domain maps remain heuristic renderer diagnostics and still use runtime/fixture signals; they should not be cited as proof of metric-only domain ownership.
6. `normal_discontinuity` was zero in the quick fixture; this validates export plumbing, not coverage of a discontinuity-rich scene.
7. Deterministic mode still logs `deterministic=off`; the quick audit verified repeatable OFF-vs-telemetry image equality, not full deterministic fixture mode.
8. Resolver telemetry compares against first accepted/nearest hit within the current acquisition path; it does not reconstruct every possible legacy branch outside that path.
9. Generated `.godot/mono/temp` outputs changed during validation and should be handled according to repo artifact policy.
10. The quick script treats Godot shutdown abort after explicit harness success as effective pass, which is pragmatic for audit artifacts but should not hide the underlying shutdown bug.

## Verdict

| Area | Result | Notes |
|---|---|---|
| Default feature gating | PASS | `EnableDomainTelemetry`, `EnableDomainAwareFirstHitResolver`, adaptive envelope scaling, and TileMetrics flags default false in source. |
| Telemetry-only mode | PARTIAL | Telemetry buffers/export are gated and observe-only by intent; fixture run with telemetry on was not completed because the harness forces a long 5-run matrix. |
| Render behavior isolation | PASS with risk | Resolver is off by default. When on, it changes pass-2 first-hit choice among real narrowphase hits. |
| First-hit integration | PASS | Resolver enters pass-2 narrowphase acquisition in `GrinFilmCamera.cs`, not final image post-processing. |
| Candidate invention | PASS | Resolver scores hits returned by `SweepSegmentHit` / `SubdividedRayHit` and local candidate windows; it does not fabricate hit positions. |
| Blur/smoothing/color blending | PASS | No beauty-image smoothing or blending was added in the audited domain-aware path. |
| Telemetry export dimensions | PASS | Existing exported domain maps are 80x45 and JSON summaries report 80x45 for matching captures. |
| Telemetry metric meaning | PARTIAL | Values are measurable, but mostly coarse heuristic classifications; no normal-discontinuity or phase/coherence map is exported. |
| Deterministic fixture mode | UNKNOWN | The harness logs `deterministic=off`; no completed deterministic fixture mode run was found in this audit. |
| Docs claims | FAIL | Docs overstate validated physics/domain claims relative to the renderer-integrated telemetry evidence. |

## Files Changed

Source/docs/tooling files in the audit range, excluding generated `output/`, `site/`, and `.godot/` artifacts:

| File | Classification |
|---|---|
| `.gitignore` | validation/tooling |
| `Docs/README.md` | docs/navigation |
| `Docs/README_obs.md` | docs/navigation |
| `Docs/Research/curvature_domain_ownership.md` | docs/navigation |
| `Docs/Research/geometric_sampling_texture.md` | docs/navigation |
| `Docs/Research/phase_coherence_field.md` | docs/navigation |
| `Docs/architecture/overview.md` | docs/navigation |
| `Docs/diagnostics/README.md` | docs/navigation |
| `Docs/diagnostics/domain_ownership.md` | docs/navigation, risky claims |
| `Docs/diagnostics/heatmaps.md` | docs/navigation |
| `Docs/diagnostics/phase_coherence.md` | docs/navigation |
| `Docs/diagnostics/tile_coherence.md` | docs/navigation |
| `Docs/glossary.md` | docs/navigation |
| `Docs/index.md` | docs/navigation, risky claims |
| `Docs/index_obs.md` | docs/navigation |
| `Docs/papers/paper_001_causal_observer_ladders/analysis/geometric_phase_memory.md` | docs/navigation |
| `Docs/papers/paper_001_causal_observer_ladders/paper.md` | docs/navigation |
| `GrinFilmCamera.cs` | telemetry-only and render-behavior-changing |
| `RendererCore/Common/DomainTelemetry.cs` | telemetry-only |
| `RendererCore/Testing/RenderTestRunner.cs` | validation/tooling |
| `Wormhole/WormholePrototypeRig.cs` | validation/tooling, telemetry export |
| `mkdocs.yml` | docs/navigation |
| `scripts/run_fixture_tile_priors_active.sh` | validation/tooling, risky because it enables resolver |
| `tools/band_detector.py` | validation/tooling |
| `tools/curvature_domain_ownership_analysis.py` | validation/tooling |
| `tools/run_band_comparison.py` | validation/tooling |

Additional risky/unrelated committed artifacts:

- 438 files under `output/`, mostly validation PNG/JSON snapshots.
- 197 files under `site/`, generated MkDocs output.
- 10 `.godot/mono/temp` build artifacts.
- Deleted Windows `Zone.Identifier` sidecar files and root `README.md` moved/removed.

## Render Loop Integration

Domain-aware first-hit selection is gated by `DomainAwareFirstHitResolverEnabledForCurrentRun()` in `GrinFilmCamera.cs`. It requires `EnableDomainAwareFirstHitResolver`, `EnableDomainTelemetry`, valid film dimensions, and allocated domain state buffers.

The resolver enters pass-2 acquisition in two places:

- Main pass-2 narrowphase loop around `GrinFilmCamera.cs:12173`, where hits from `SweepSegmentHit` and `SubdividedRayHit` are wrapped as `DomainAwareHitCandidate`.
- First-hit refinement/window path around `GrinFilmCamera.cs:15525`, where already-returned candidate hits are scored.

The resolver affects first-hit selection before shading by assigning `bestHit`, `bestHp`, `bestHn`, `bestCid`, and related hit fields through `ApplyDomainAwareHitCandidate()`. Shading later consumes those selected hit fields normally.

No new beauty blur, smoothing, or color blending was found. Candidate hits are not invented; they are selected from physics query results. However, enabling the resolver disables the normal nearest-hit early break and may choose a later candidate within `0.08` world/path distance if its heuristic score is higher.

## Data Flow

| Signal | Source | Assessment |
|---|---|---|
| domain id | `BuildPixelDomainState()`, from hit/miss, fixture hit class, remap/crossing counts, normal/ray relation | Measurable but heuristic; heavily scene/fixture-label influenced via `ClassifyFixtureHitKind`. |
| confidence | fixed base confidence adjusted by boundary confidence | Meaningful as heuristic confidence, not statistical confidence. |
| boundary confidence | max of normal discontinuity, remap/crossing/transform magnitude, ambiguous ordering, selection flip | Measurable renderer signals. |
| selection flip | first accepted hit vs final hit changed collider/distance/normal, or previous frame hit state changed | Measurable renderer signals. |
| normal discontinuity | dot-product delta between first accepted and final normals | Computed internally, not exported as its own map. |
| boundary stability | `BoundaryStability = 1 - boundaryConfidence` | Derived complement of boundary confidence; not an independent phase/coherence measurement. |

## Telemetry Export

Exported filenames use capture stem plus:

- `.domain_id.png`
- `.domain_confidence.png`
- `.boundary_confidence.png`
- `.selection_flip.png`
- `.domain_telemetry_summary.json`

Existing summaries in `output/domain_telemetry_validation/current_domain_fast` report `image_width=80`, `image_height=45`; `file` reports the PNGs are 80x45 RGBA. JSON summary values are not placeholders, but the observed curved-minimal maps are low-information: `domain_id` p10/p50/p90 all 5 for the baseline sample, `domain_confidence` p10/p50/p90 all 0.55, and `boundary_confidence` p10/p50/p90 all 0.

## Commands Run

| Command | Result |
|---|---|
| `git status --short --branch` | Clean working tree on `main...origin/main`. |
| `git rev-parse HEAD origin/main main` | All resolve to `dd4ff22c5f1ad1185d3639a16189e87140723b3f`. |
| `git diff --name-status main...HEAD` | Empty; current branch is `main`. |
| `git diff --name-status 1348589..HEAD` | 679 changed paths, including source, docs, generated site, build artifacts, and output artifacts. |
| `git diff --name-status 1348589..HEAD -- . :(exclude)output/** :(exclude)site/** :(exclude).godot/**` | 32 non-generated source/docs/tooling path changes listed above. |
| `rg -n "EnableDomainTelemetry|EnableDomainAwareFirstHitResolver|DomainAware|..." ...` | Located feature flags, CLI args, resolver integration, and export points. |
| `dotnet build 'Physical Light and Camera Units.sln' -c Debug -v minimal` | PASS, 35 warnings, 0 errors. |
| `scripts/godot_local.sh --version` | PASS, Godot `4.5.1.stable.mono`. |
| `scripts/godot_local.sh --headless --path . --quit` | Exit 0 but logged `NullReferenceException` in `RayBeamRenderer.BuildBoundaryLayerSnap`. |
| `git diff --check 1348589..HEAD -- ...` | PASS, no whitespace errors. |
| Godot OFF fixture command with `--enable-domain-telemetry=0 --enable-domain-aware-first-hit-resolver=0` | PARTIAL. Produced captures for run ids 1-3, then killed with exit 143 because harness ignored requested 30 frames and forced a long 5x300 matrix. |
| `find output/domain_audit/off ...` | Confirmed OFF captures and derivative JSON for run ids 1-3. |
| `file output/domain_telemetry_validation/current_domain_fast/*.domain_id.png ...` | Confirmed existing domain telemetry PNGs are 80x45. |

The requested telemetry-on and resolver-on fixture runs were not completed in this pass because the render-test harness overrode `--render-test-frames=30` to `framesPerRun=300`, `warmup=30`, `runs=5`.

## Top Risks / Bugs

1. `scripts/run_fixture_tile_priors_active.sh` enables `--enable-domain-aware-first-hit-resolver=1`; the header frames the run as TileMetrics observe-only, but it also changes rendering.
2. Renderer-integrated boundary stability is only `1 - boundaryConfidence`, not an independent phase/coherence measurement.
3. Domain classification uses fixture hit labels (`source`, `background`, `absorbed`) despite docs claiming domain ownership is derived from metric structure alone.
4. Domain-aware resolver lacks explicit metrics for how many pixels changed selected hit, score deltas, candidate distances, or chosen reasons.
5. When resolver is enabled, nearest-hit early-out is suppressed, increasing work and allowing later-hit selection within the heuristic distance window.
6. Existing domain summaries show near-constant confidence/id distributions on curved-minimal, limiting validation value.
7. No standalone export exists for normal discontinuity, despite it being one of the more measurable domain-boundary signals.
8. Godot headless project load logs a `NullReferenceException` in `BuildBoundaryLayerSnap`; unrelated to domain code, but it weakens runtime confidence.
9. Render-test CLI frame overrides are ineffective for the audited fixture path, making quick OFF/ON/resolver comparisons hard to reproduce.
10. Docs and generated site were committed alongside source and output artifacts, obscuring the trusted implementation delta.

## Minimal Patch Recommendations

1. Change `scripts/run_fixture_tile_priors_active.sh` to leave `--enable-domain-aware-first-hit-resolver=0` unless a separate explicit env var enables it.
2. Add resolver counters to telemetry summary: pixels where domain-aware choice differed from nearest/legacy, mean distance delta, max distance delta, and score component averages.
3. Export `normal_discontinuity.png` or include its stats in `domain_telemetry_summary.json`.
4. Keep the renderer-integrated derived complement named as boundary stability unless an independent measurable phase/coherence signal is added.
5. Add a short/one-run render-test mode that honors CLI `--render-test-frames` for audit fixtures.
6. Soften docs: describe integrated domain maps as heuristic renderer diagnostics, not proof of metric-only domain ownership.
