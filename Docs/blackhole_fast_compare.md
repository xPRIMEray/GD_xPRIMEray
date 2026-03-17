# BlackHole Fast Compare (GRIN vs Metric)

Use this when you want a narrow transport comparison run that avoids long matrix sweeps.

Related reference: [Black Hole Optical Texture Reference](black_hole_optical_texture_reference.md) for the shadow, photon-ring, and lensing features this comparison is intended to distinguish.

## Exact commands

From `C:\godot\godot_xPRIMEray`:

1. `set NO_PAUSE=1 && run_render_test_blackhole_grin_fast_compare.bat`
2. `set NO_PAUSE=1 && run_render_test_blackhole_metric_fast_compare.bat`

Optional pair runner:

1. `run_render_test_blackhole_fast_compare_pair.bat`

## What these scripts set

- fixture: `blackhole_minimal`
- profile: `blackhole_compare_fast`
- transport: `grin` or `metric`
- smartscale: off
- lifecycle stress: off
- workspace-local app data paths for stable `user://` logging

## Quick log checks

GRIN:

`rg -n "Applied scoped render-test profile|\\[MATRIX START\\]|\\[Transport\\] active|\\[BlackHoleFixture\\]\\[ComparisonSummary\\]|BlackHoleMinimalFingerprint:" logs\\ab_blackhole_grin_fast_compare.log`

Metric:

`rg -n "Applied scoped render-test profile|\\[MATRIX START\\]|\\[Transport\\] active|\\[BlackHoleFixture\\]\\[ComparisonSummary\\]|BlackHoleMinimalFingerprint:" logs\\ab_blackhole_metric_fast_compare.log`

Expected:

- profile line includes `runs=2 framesPerRun=80 warmup=10`
- matrix line includes `runs=2 framesPerRun=80 warmup=10`
- transport line is `GRIN_Optical` for GRIN run and `Metric_NullGeodesic` for Metric run
- BlackHole output includes:
  - `transportModel`
  - `absorbCount` and `absorbRate`
  - `hitRate`
  - `sourcePatternSummary`
  - fingerprint line (`BlackHoleMinimalFingerprint:`)
