# Curvature Full-Coverage Mini Experiment

Hermetic closure validates transport completion within a known scene contract. It does not establish physical correctness.

## Runs Compared

| run | output directory | preset | resolution | frames | warmup | full_frame_requested | render mode | full-frame coverage |
|---|---|---|---:|---:|---:|---:|---|---|
| Baseline | `output/curvature_fps_benchmark/20260607T185034Z` | mini | 160x112 | 10 | 2 | false | partial-frame diagnostic snapshot | partial |
| Experiment A | `output/curvature_fps_benchmark/20260607T191143Z` | mini | 160x112 | 50 | 5 | false | full-frame render | complete |
| Experiment B | `output/curvature_fps_benchmark/20260607T191820Z` | mini | 160x112 | 50 | 5 | true | full-frame render | complete |

## Summary Table

All percentages below are minimums across the five curvature cells unless otherwise noted.

| run | traced % | beauty written % | hit % over traced pixels | miss count | rows_completed | FPS label | mean FPS avg | p95 frame ms avg | pass2_phys_ms avg | frame_coverage_map | curvature_signature_ladder |
|---|---:|---:|---:|---:|---:|---|---:|---:|---:|---|---|
| Baseline | 37.7 | 24.6 | 100.000 | 0 | 42-43 | mean diagnostic FPS | 0.599 | 1759.08 | n/a | partial, 37.7-38.2% filled | present |
| Experiment A | 100.0 | 100.0 | 100.000 | 0 | 112 | mean FPS | 0.712 | 1622.71 | 33018.66 | full, 100% filled | present |
| Experiment B | 100.0 | 100.0 | 100.000 | 0 | 112 | mean FPS | 0.728 | 1557.77 | 31346.32 | full, 100% filled | present |

## Per-Curvature Metrics

### Baseline: 10 Frames, 2 Warmup, No Full-Frame Request

| curvature % | traced % | beauty written % | hit % | miss count | rows_completed | mean diagnostic FPS | p95 frame ms | pass2_phys_ms |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 | 37.7 | 24.6 | 100.000 | 0 | 42 | 0.558 | 1827.23 | n/a |
| 25 | 38.0 | 24.6 | 100.000 | 0 | 43 | 0.605 | 1777.29 | n/a |
| 50 | 38.2 | 24.6 | 100.000 | 0 | 43 | 0.647 | 1681.06 | n/a |
| 75 | 38.0 | 24.6 | 100.000 | 0 | 43 | 0.607 | 1708.42 | n/a |
| 100 | 38.2 | 24.6 | 100.000 | 0 | 43 | 0.580 | 1801.37 | n/a |

### Experiment A: 50 Frames, 5 Warmup, No Full-Frame Request

| curvature % | traced % | beauty written % | hit % | miss count | rows_completed | mean FPS | p95 frame ms | pass2_phys_ms |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 | 100.0 | 100.0 | 100.000 | 0 | 112 | 0.647 | 1779.27 | 36226.54 |
| 25 | 100.0 | 100.0 | 100.000 | 0 | 112 | 0.744 | 1538.43 | 31318.17 |
| 50 | 100.0 | 100.0 | 100.000 | 0 | 112 | 0.710 | 1681.69 | 33707.66 |
| 75 | 100.0 | 100.0 | 100.000 | 0 | 112 | 0.741 | 1519.99 | 31523.71 |
| 100 | 100.0 | 100.0 | 100.000 | 0 | 112 | 0.720 | 1594.17 | 32317.24 |

### Experiment B: 50 Frames, 5 Warmup, Full-Frame Request

| curvature % | traced % | beauty written % | hit % | miss count | rows_completed | mean FPS | p95 frame ms | pass2_phys_ms |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 | 100.0 | 100.0 | 100.000 | 0 | 112 | 0.680 | 1630.57 | 34913.39 |
| 25 | 100.0 | 100.0 | 100.000 | 0 | 112 | 0.746 | 1631.00 | 30894.18 |
| 50 | 100.0 | 100.0 | 100.000 | 0 | 112 | 0.754 | 1453.34 | 30151.87 |
| 75 | 100.0 | 100.0 | 100.000 | 0 | 112 | 0.751 | 1499.31 | 29989.90 |
| 100 | 100.0 | 100.0 | 100.000 | 0 | 112 | 0.707 | 1574.63 | 30782.27 |

## Key Findings

More frames alone did produce full mini coverage. Experiment A moved from the baseline's 37.7-38.2% traced coverage and 24.6% beauty-written coverage to 100.0% traced and 100.0% beauty-written coverage at every curvature level.

The full-frame request did not materially change final coverage behavior relative to Experiment A. Both 50-frame runs completed 112 rows, reached 100.0% traced coverage, reached 100.0% beauty-written coverage, kept 100.000% hit rate over traced pixels, and reported zero misses. The full-frame request is still useful because it records the stricter intent and keeps the full-frame visual gate explicit.

The frame coverage maps visually fill in with 50 frames. Baseline maps are partially filled at 37.7-38.2%; Experiment A and Experiment B maps are 100.0% non-background for every curvature level.

Sealed closure remained intact. All three runs report zero misses across all five curvature cells. In the 50-frame runs, traversal budget stress also dropped from about 30.9% in the baseline to 0.0%, and max traversal steps dropped from 701 to 299-304.

FPS did not drop in these measurements. Average FPS rose from 0.599 in the baseline to 0.712 in Experiment A and 0.728 in Experiment B. That is about +18.9% for A and +21.4% for B versus baseline. This likely reflects the benchmark reaching a different completed-frame regime, so it should not be interpreted as an optimization result.

`pass2_phys_ms` was unavailable in the partial baseline because the latest perf frame report was not known. It was available in the full-coverage runs: Experiment A averaged 33018.66 ms and Experiment B averaged 31346.32 ms across curvature cells.

## Comparisons

### Baseline vs Experiment A

Experiment A confirms Grok's row-cursor/band-stepping hypothesis. Raising the measurement window from 10/2 to 50/5, without requesting full-frame mode, was enough to complete the 160x112 film: traced coverage rose from 37.7% minimum to 100.0%, beauty-written coverage rose from 24.6% to 100.0%, and rows_completed rose from 42-43 to 112.

### Baseline vs Experiment B

Experiment B also reaches complete mini coverage, with `full_frame_requested=true` recorded in metadata and summary. It preserves sealed closure, generates complete frame coverage maps, and allows the run to be called full-frame at mini resolution because full-frame coverage actually passed.

### Experiment A vs Experiment B

A and B are functionally equivalent on coverage and closure: both reach 100.0% traced, 100.0% beauty written, 100.000% hit rate, 0 misses, and 112 rows completed. The full-frame flag did not visibly change row completion, budget stress, coverage maps, or curvature-signature generation in these artifacts. Its main value is semantic: it makes the run declare and enforce full-frame intent.

## Recommended Next Command

Use Experiment B as the official mini full-coverage validation command:

```bash
CURVATURE_FPS_FULL_FRAME=1 \
CURVATURE_FPS_PRESET=mini \
CURVATURE_FPS_FRAMES=50 \
CURVATURE_FPS_WARMUP=5 \
bash scripts/run_curvature_fps_benchmark.sh
```

Experiment A proves full coverage is achievable by configuration alone. Experiment B is the better official validation command because it records full-frame intent and preserves the stricter visual gate semantics.

## Grok Handoff

Grok can begin performance analysis for the mini full-coverage configuration. The recommended target is Experiment B, especially `pass2_phys_ms`, p95 frame time, traversal-step distribution, and why full coverage eliminates the baseline budget-stress signature.

Remaining blockers before calling anything full-frame FPS:

- The run may be called mini full-frame FPS because traced, beauty-written, and classified coverage are all 100.0%.
- Do not generalize this to larger presets yet; mini 160x112 is the only validated full-frame scale in this experiment.
- Treat the FPS increase versus baseline as a regime change, not an optimization result.
- `pass2_phys_ms` appears only once full coverage/perf reporting becomes known; Grok should verify whether it is cumulative per run or normalized per reported frame before comparing it as a per-frame stage cost.
