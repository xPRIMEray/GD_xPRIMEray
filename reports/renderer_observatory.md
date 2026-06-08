# Renderer Observatory v1

Input run: `output/curvature_fps_benchmark/20260607T221311Z`

Question: **Where is time actually spent?**

Hermetic closure validates transport completion within a known scene contract. This report treats the renderer as a fixture: the renderer passes only when cost, coverage, beauty, and closure are all visible. No renderer behavior was modified and no optimization was applied.

## Executive Verdict

- Renderer fixture status: `OBSERVED`.
- Coverage: `PASS` — traced and beauty-written coverage are 100.0% at every curvature level.
- Closure: `PASS` — 100.000% hit rate over traced pixels, 0 misses at every curvature level.
- Beauty: `PASS` — beauty capture health is OK.
- Bottleneck verdict: `pass2_query_ms` dominates `pass2_phys_ms`; query cost is 92.4% to 93.8% of physics time.
- Optimization target: `Physics query path / band query volume`. Start by explaining query count, query batching, and segment testing before changing traversal semantics.
- Curvature sensitivity: average traversal steps rise by 5.21; total segments change by 127,040; physics queries change by -21,857 from 0% to 100%.
- Visuals: [renderer_cost_ladder.png](renderer_cost_ladder.png), [renderer_storyboard_v1.png](renderer_storyboard_v1.png).

## Renderer Fixture Table

| curvature | FPS | mean frame ms | p95 frame ms | pass1_ms | pass2_phys_ms | pass2_query_ms | query cost % | avg traversal | max traversal | total segments | physics queries | traced % | beauty % | closure |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 0% | 0.672 | 1489.11 | 1647.32 | 6888.63 | 34144.41 | 32011.35 | 93.8% | 273.21 | 299 | 8,345,920 | 178,025 | 100.0% | 100.0% | PASS |
| 25% | 0.770 | 1299.53 | 1506.93 | 8109.50 | 29277.80 | 27147.36 | 92.7% | 273.54 | 299 | 8,395,840 | 153,624 | 100.0% | 100.0% | PASS |
| 50% | 0.783 | 1276.66 | 1444.35 | 8264.81 | 28137.58 | 25996.83 | 92.4% | 274.46 | 300 | 8,451,200 | 154,133 | 100.0% | 100.0% | PASS |
| 75% | 0.789 | 1267.21 | 1428.92 | 8273.30 | 27889.62 | 25757.14 | 92.4% | 276.14 | 302 | 8,467,200 | 155,148 | 100.0% | 100.0% | PASS |
| 100% | 0.754 | 1326.99 | 1488.81 | 8187.30 | 29458.68 | 27254.37 | 92.5% | 278.43 | 304 | 8,472,960 | 156,168 | 100.0% | 100.0% | PASS |

## Nine Panels

1. **Cost stack** — pass1, query time, and non-query physics time by curvature level.
2. **Traversal ladder** — average and maximum traversal steps.
3. **Query ladder** — band physics query counts and query cost percentage.
4. **Coverage ladder** — traced-pixel coverage across the sweep.
5. **Beauty ladder** — beauty-written coverage across the sweep.
6. **Closure ladder** — hit rate and miss count.
7. **Curvature cost sensitivity** — what changes between 0% and 100%.
8. **Bottleneck verdict** — the current dominant measured cost.
9. **Optimization target** — the first place Grok should inspect, without changing behavior yet.

## Before Optimization

- Do not compare this full-coverage run with partial-frame diagnostic runs as if they measure the same workload.
- Do not call a larger preset full-frame until traced, beauty-written, and closure coverage are proven at that preset.
- Verify whether `pass2_phys_ms` is cumulative over the run or normalized per reported frame before using it as a per-frame optimization target.
- The immediate evidence says query work dominates; the first tuning discussion should explain why query time is so high and whether query count can be reduced without weakening sealed closure.
