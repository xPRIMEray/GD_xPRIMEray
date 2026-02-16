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
