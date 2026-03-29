# Performance + Trust Test Harness

This repo now includes a repeatable run-matrix harness for RenderHealth regression testing.

## Scene Hook

- Scene: `test.tscn`
- New node: `RenderTestRunner` (`res://RendererCore/Testing/RenderTestRunner.cs`)
- Target paths:
  - `GrinFilmCameraPath = ../GrinFilmCamera`
  - `TargetCameraPath = ../Camera3D`

By default, `AutoStart` is `false`, so normal runs are unchanged.

## Run Matrix Behavior

Each run:
- applies a named `TestRunConfig` override set on `GrinFilmCamera`
- optionally drives deterministic camera motion (`Fixed` or `Orbit`)
- runs `FramesPerRun` frames, ignoring first `WarmupFrames`
- prints robust markers:
  - `[RenderTest][RUN START] ...`
  - `[RenderTest][RUN SUMMARY] ...`
  - `[RenderTest][RUN END] ...`

Matrix markers:
- `[RenderTest][MATRIX START] ...`
- `[RenderTest][MATRIX END] ...`

`RUN SUMMARY` fields include:
- `meanMsPerFrame`
- `p95MsPerFrame` (approx percentile from collected frame ms samples)
- `meanSegsPerPixel` (or `na`)
- `geomRayTestsSavedPct` only when trusted; otherwise `na`

## How To Run

## Editor/manual

1. Open `test.tscn`.
2. Select `RenderTestRunner`.
3. Set `AutoStart=true` (optional) and configure frame counts.
4. Run scene and capture output log.

## CLI/headless-friendly

Use `--render-test` cmd arg so the runner auto-starts:

```powershell
godot4 --headless --path . --scene res://test.tscn -- --render-test
```

If your binary is `godot`, use that instead of `godot4`.

Set `AutoQuitOnComplete=true` on `RenderTestRunner` when running in CI.

## Parse + Regression Checks

Use the existing parser and the updated regression harness:

```powershell
python tools/renderhealth_parse.py "logs\your_run.log"
python tools/renderhealth_regress.py "logs\your_run.log"
```

`renderhealth_regress.py` exits non-zero on invariant failures and prints clear failure reasons.

## Curved Scheduler Capture

For curved scheduler baseline vs reorder-only vs reorder-only-plus-persistent-priors checks, use:

```powershell
python tools/render_test_scheduler_compare.py --godot-exe "C:\path\to\godot_console.exe"
```

This writes one timestamped run folder under `output/render_test_scheduler_compare/` with:

- `logs/` for the raw Godot logs used by `renderhealth_regress.py`
- `images/` for the restored per-run PNG captures
- `summary.json` and `summary.md` for quick review of:
  - baseline execution with observe-only ranking telemetry
  - reorder-only execution
  - reorder-only plus persistent priors

PNG names now include the fixture, requested mode label, scheduler mode, and key run settings such as subtile width when available.

Persistent priors are enabled with:

```text
--tile-metrics-persistent-priors=1
```

The compare harness keeps baseline execution unchanged by using the observe-only tile-metrics path for the baseline case, which adds actual-order ranking summaries without changing the rendered output.

## Curved Minimal Visual Check Mode

For `curved_minimal` and `curved_minimal_backdrop`, the default render-test path remains the scheduler-fast baseline.

To opt into the higher-resolution visual-check mode, add:

```text
--render-test-profile=curved_minimal_visual_check
```

This profile is intentionally separate from the scheduler-fast default and currently raises the curved-minimal-family render-test sampling to:

- `FilmResolutionScale = 0.5`
- `PixelStride = 1`
- full-frame row budget for practical capture-oriented runs

Suggested capture commands:

```bash
./scripts/godot_local.sh --path . --scene res://test-curved-minimal.tscn -- \
  --render-test --render-test-fixture=curved_minimal --lifecycle-stress=0 --smartscale=0 \
  --render-test-profile=curved_minimal_visual_check \
  --render-test-capture=1 --render-test-capture-dir=output/render_test_visual_check \
  --render-test-capture-mode=visual-check
```

```bash
./scripts/godot_local.sh --path . --scene res://test-curved-minimal-backdrop.tscn -- \
  --render-test --render-test-fixture=curved_minimal_backdrop --lifecycle-stress=0 --smartscale=0 \
  --render-test-profile=curved_minimal_visual_check \
  --render-test-capture=1 --render-test-capture-dir=output/render_test_visual_check \
  --render-test-capture-mode=visual-check
```
