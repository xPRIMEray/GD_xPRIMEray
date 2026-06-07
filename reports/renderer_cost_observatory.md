# Renderer Cost Observatory

Input run: `output/curvature_fps_benchmark/20260607T191820Z`

Hermetic closure validates transport completion within a known scene contract. This report observes renderer cost; it does not optimize or change renderer behavior.

## Summary

- Curvature levels observed: `0%, 25%, 50%, 75%, 100%`.
- Coverage: traced and beauty-written coverage are 100.0% at every curvature level.
- Hit closure: 100.000% hit rate over traced pixels, 0 misses at every curvature level.
- Mean FPS across cells: 0.728.
- Query cost dominates `pass2_phys_ms`: 92.7% to 93.9% of physics time.
- Total segments rise by 59,840 from 0% to 100%; average traversal steps rise by 5.21.
- Visual ladder: [renderer_cost_ladder.png](renderer_cost_ladder.png).

## Renderer Cost Table

| curvature | diagnostic FPS | mean frame ms | p95 frame ms | pass1_ms | pass2_phys_ms | pass2_query_ms | query cost % | avg traversal steps | max traversal steps | total segments | physics queries | traced coverage | beauty written coverage |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0% | 0.680 | 1469.89 | 1630.57 | 6976.49 | 34913.39 | 32795.71 | 93.9% | 273.21 | 299 | 8,390,720 | 178,025 | 100.0% | 100.0% |
| 25% | 0.746 | 1341.34 | 1631.00 | 8140.10 | 30894.18 | 28763.92 | 93.1% | 273.54 | 299 | 8,418,240 | 153,624 | 100.0% | 100.0% |
| 50% | 0.754 | 1325.62 | 1453.34 | 8208.59 | 30151.87 | 27947.82 | 92.7% | 274.46 | 300 | 8,473,600 | 154,133 | 100.0% | 100.0% |
| 75% | 0.751 | 1331.96 | 1499.31 | 8334.01 | 29989.90 | 27810.55 | 92.7% | 276.14 | 302 | 8,444,800 | 155,148 | 100.0% | 100.0% |
| 100% | 0.707 | 1414.94 | 1574.63 | 8377.43 | 30782.27 | 28537.87 | 92.7% | 278.43 | 304 | 8,450,560 | 156,168 | 100.0% | 100.0% |

## Reading Notes

- `diagnostic FPS` is the benchmark-reported FPS field. In this run full-frame coverage passed, so the sweep report may label the same value as mean FPS; it is still reported here as the renderer observatory throughput metric requested for comparison.
- `pass2_query_ms` is treated as a subset of `pass2_phys_ms`. The cost stack therefore splits physics into query time and non-query physics time.
- `physics queries` uses `band_physics_queries`; direct `intersect_ray_calls` and `intersect_shape_calls` are zero in this benchmark path.
- Segment count uses `latest_perf_frame_report.segments`; `segments_tested` is available in the source summary but not shown in the headline table.
- Coverage is included so future performance tuning cannot accidentally compare partial-frame and full-frame runs as if they were equivalent.

## Observations Before Tuning

- The largest cost family is physics/query work. Query time accounts for 92.7% to 93.9% of `pass2_phys_ms`.
- Traversal work increases with curvature: average steps move from 273.21 at 0% to 278.43 at 100%, while max steps move from 299 to 304.
- Total segments are roughly flat but trend upward: 8,390,720 at 0% to 8,450,560 at 100%.
- Coverage is not a confounder for this run: every curvature level is 100.0% traced and 100.0% beauty-written.
- No optimization has been applied here; this is the baseline Observatory of the Renderer itself.
