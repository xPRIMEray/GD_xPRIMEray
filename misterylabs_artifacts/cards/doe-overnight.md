# DOE Overnight — The Resolution Cost of Seeing the Curvature Band

**Hook:** Fine integration steps capture 26% of the high-curvature band. Coarse steps capture 0.6%. An 18-cell overnight experiment maps the exact tradeoff.

## Scientific Context

Integration step length controls how finely the GRIN field is sampled along each ray. Coarser steps are faster but may miss the narrow high-curvature band entirely. This DOE sweeps 9 step lengths × 2 telemetry modes (off vs. on) to establish the step-length operating range and quantify telemetry overhead.

## Observation

Selected results from the 18-cell sweep:

| Step Length | Band % | Telemetry Mode | Changed Pixels |
|-------------|--------|----------------|---------------|
| 0.00625 | **26.1%** | off | — (reference) |
| 0.00625 | 26.9% | on | 1,728 |
| 0.010 | 21.5% | off | — |
| 0.010 | 21.7% | on | 1,856 |
| 0.015 | 3.3% | off | — |
| 0.015 | 1.9% | on | 960 |
| 0.020 | 0.7% | off | — |
| 0.025 | **0.6%** | off | — |

The band pixel fraction drops sharply between step=0.013 and step=0.015, indicating a transition at the band's natural curvature scale. Telemetry-on produces 960–2560 changed pixels at most step lengths — confirming telemetry alters ray outcomes and should be treated as a separate rendering mode, not a passive observer.

## Why It Matters

This establishes the practical step-length floor for production renders (step ≤ 0.013 to capture the full band) and documents the telemetry contamination effect. The 0% hash-match at most telemetry-on cells means telemetry cannot be used for regression testing against non-telemetry references — a key methodological constraint.

## Next Step

Re-run at step=0.008 and step=0.006 to characterize the band saturation ceiling. Add a third mode: adaptive step length (shorter in high-curvature regions, longer elsewhere) to test whether adaptive sampling can achieve fine-step quality at coarse-step cost.

---

*Source:* `output/doe_overnight/20260502T060652Z/`  
*Key image:* `visuals/doe-overnight-band-plot.png`  
*Dataset:* `datasets/doe-overnight.csv`  
*Tier:* 3 — Interesting, requires additional context
