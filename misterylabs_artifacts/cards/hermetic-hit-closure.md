# Hermetic Hit Closure — The Budget Required for Complete Integration

**Hook:** Halve the step budget and get 0% closure. Give the integrator enough room and it reaches 100%. This is the cliff edge between a reliable renderer and an unreliable one.

## Scientific Context

A hermetic scene has a known contract: every ray that enters the scene volume must be classified before the integrator exits. "Closure" is the fraction of rays that achieve a definitive classification (hit, escape, portal event) rather than exhausting their step budget without resolution. This experiment sweeps step budget values at a fixed step length to find the closure threshold.

## Observation

Two cells at step=0.015, both using row traversal:

| Budget | Closure % | Phase | Outcome |
|--------|-----------|-------|---------|
| 32 steps | **0.0%** | budget_saturated | All 200 rays ran out of steps — zero resolved |
| 700 steps | **100.0%** | plateau | All rays resolved — flat budget pressure |

At budget=32, the integrator exhausts all available steps before rays can find their endpoint. At budget=700, every ray closes completely and additional budget adds no cost (plateau). The failure storyboard shows the spatial distribution of failure pixels at budget=32 — not random noise, but coherent clusters around high-curvature regions where the integrator needs more steps.

The closure recovery heatmap shows where adaptive budget scaling successfully recovers failures: recovery is high in the annular curvature band and low at corners, consistent with curvature field structure.

## Why It Matters

Transport correctness is not gradual — it falls off a cliff. A renderer that looks plausible at budget=32 is actually classifying 0% of its pixels correctly. This experiment establishes the operating floor for the hermetic scene contract and validates that the closure oracle correctly identifies failure modes before they propagate into visual output.

## Next Step

Map the closure cliff across step sizes (not just budget). Find the surface in (step\_size, budget) space that separates complete from incomplete integration. Use adaptive budget scaling to extend the operating region beyond the static cliff edge.

---

*Source:* `output/hermetic_hit_closure/20260514T040157Z/`  
*Key images:* `visuals/hermetic-hit-closure-storyboard.png`, `visuals/hermetic-hit-closure-recovery.png`  
*Tier:* 2 — Research Atlas
