# Curvature FPS Benchmark Game Plan

## Summary

This milestone proves more than FPS. It proves that a simple sealed fixture can be evaluated quickly, repeatably, and visibly while curvature ramps from 0% to 100%.

The benchmark uses the existing hermetic sealed-room fixture and the repository's established artifact language: `output/.../<timestamp>/cells/...`, `metadata.json`, `effective_status.txt`, `hit_diagnostics.csv`, `budget_exhaustion_*`, `transport_continuity_*`, diagnostic overlays, and scene-contract closure language.

Guardrail: hermetic closure validates a known scene contract, not physical truth. Diagnostic outputs must not feed rendering, scheduling, hit selection, shading, resolver decisions, traversal, or adaptive precision.

## Scope

- Main fixture: `test-hermetic-curved-room.tscn` with `Fixtures/fixture_hermetic_curved_room.tscn`.
- Acceptance contract: every evaluated pixel/ray should hit one of the sealed room receivers.
- Excluded from the acceptance gate: event horizons, abyss/no-hit scenes, complex object islands, cathedral probe edge cases, and wormhole/oracle investigations.
- Optional diagnostics may be attached when existing tools already produce them, but they are not pass/fail gates.

## Curvature Sweep

Run exactly five curvature levels:

| Curvature | Field amplitude |
|---:|---:|
| 0% | 0.0 |
| 25% | 0.2875 |
| 50% | 0.575 |
| 75% | 0.8625 |
| 100% | 1.15 |

`1.15` is retained as the 100% benchmark amplitude because it is the current validated curved-minimal canonical amplitude in `CurvedMinimalFingerprint`. The hermetic benchmark records both the requested amplitude and the resolved hermetic fixture curvature.

## Implementation

Add a one-command runner:

```bash
bash scripts/run_curvature_fps_benchmark.sh
```

The runner will:

- build the C# project;
- run five hermetic benchmark cells under `output/curvature_fps_benchmark/<timestamp>/cells/curvature_<pct>/row`;
- pass `--curvature-fps-benchmark=1` to the existing `RenderTestRunner`;
- pass `--hermetic-curvature-strength=<amplitude>` for each curvature level;
- enable render-test capture and existing diagnostic overlays;
- run existing post-process tools for wireframes, ownership graph, hit normals, graph-plus-hit-normal summaries, and hermetic closure analysis;
- aggregate results into `output/curvature_fps_benchmark/<timestamp>/summary.json`;
- generate `reports/weekend_fps_curvature_sweep.md`;
- copy report-local visual assets into `reports/weekend_fps_curvature_sweep_assets/`.

Add a narrow benchmark mode in `RenderTestRunner`:

- enabled only by `--curvature-fps-benchmark=1`;
- keeps default render-test behavior unchanged;
- enables existing `EnableProfiling` and `EnableFramePerf` only for benchmark cells;
- emits `curvature_fps_result.json` in the capture directory after each run.

## Result Schema

Each curvature result should include, where available:

- curvature percent;
- field amplitude;
- mean FPS;
- mean frame time;
- p95 frame time;
- sample count;
- screenshot path;
- total pixels/rays evaluated;
- hit count;
- miss count;
- miss rate;
- hit percent;
- average traversal steps;
- max traversal steps;
- precision/epsilon warnings from budget exhaustion, max-step use, derivative diagnostics, oracle/island diagnostics when present;
- latest `PerfFrameReport`;
- render-health diagnostics;
- fixture fingerprint or fixture contract lines;
- optional oracle/cathedral/island/closure fingerprints only when existing systems already produced them;
- paths to visual artifacts such as normal overlay, hit/miss map, traversal-step heatmap, budget/precision map, renderer-health overlay, and diagnostic overlays.

## Final Report

Generate `reports/weekend_fps_curvature_sweep.md` with two layers.

Executive summary:

- Did it run?
- Did Godot exit cleanly?
- Did all five curvature levels complete?
- Did sealed-scene hit validation pass?
- Did FPS reach 30?
- Did FPS reach 60?
- Biggest observed bottleneck.

Detailed benchmark table:

| Column | Meaning |
|---|---|
| curvature % | requested sweep level |
| amplitude | requested field amplitude |
| mean FPS | `1000 / mean frame ms` |
| p95 frame time | p95 frame time in ms |
| hit % | sealed-scene closure percent |
| miss count | evaluated pixels/rays without hit |
| average traversal steps | mean `final_step_count` or `step_count` |
| max traversal steps | max `final_step_count` or `step_count` |
| screenshot | linked beauty capture |
| visual metrics available | linked reusable diagnostic artifacts |

## Test Plan

Static checks:

```bash
dotnet build "Physical Light and Camera Units.csproj"
python3 -m py_compile tools/curvature_fps_benchmark_report.py
```

Smoke benchmark:

```bash
CURVATURE_FPS_FRAMES=10 CURVATURE_FPS_WARMUP=2 CURVATURE_FPS_FILM_SCALE=0.125 bash scripts/run_curvature_fps_benchmark.sh
```

Acceptance:

- `reports/curvature_fps_benchmark_game_plan.md` exists before benchmark implementation.
- `reports/weekend_fps_curvature_sweep.md` exists after the run.
- all five curvature levels produce cells, screenshots, metrics, and visual diagnostics.
- sealed baseline reports `missed_hits = 0`, or nonzero misses are explicitly reported.
- unrelated dirty docs/site/output changes remain untouched.

## Handoff Notes

- Claude should focus next on tightening report interpretation and acceptance wording if the smoke run exposes nonzero miss rates or budget saturation.
- Grok should focus next on performance hypotheses from `PerfFrameReport`, traversal-step trends, and render-health diagnostics.
- Neither follow-up should turn cathedral/object-island/event-horizon cases into the main gate for this milestone.
