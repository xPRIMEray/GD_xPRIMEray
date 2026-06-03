# DOE Scheduler Resonance — Why Stride 4 Destroys the Image

**Hook:** Change the render stride from 2 to 4 and the high-curvature band goes from 26% pixel coverage to 0.3%. This is a resonance effect, not a sampling accident.

## Scientific Context

xPRIMEray's first-pass traversal evaluates pixels in rows. The "stride" setting controls which rows are evaluated on each pass (every row, every 2nd, every 4th, etc.). When stride equals or divides the scene's natural curvature band width, the evaluator systematically skips the band's pixels on every pass — a resonance between the sampling cadence and the scene geometry. This 68-cell DOE maps the resonance landscape.

## Observation

Selected results from phase A (telemetry off):

| Step | Stride | Band % | Horizontal Score |
|------|--------|--------|-----------------|
| 0.0125 | 1 | 33.0% | 0.46 |
| 0.0125 | 2 | 18.1% | 0.25 |
| 0.0125 | 4 | **0.22%** | **0.10** |
| 0.0125 | 8 | **0.19%** | **0.09** |
| 0.011  | 1 | 25.8% | 0.36 |
| 0.011  | 2 | 23.9% | 0.31 |
| 0.011  | 4 | **0.45%** | **0.14** |

At stride=4 and stride=8, band pixel coverage collapses below 0.5% regardless of step length. The band-by-row-mod-stride heatmap makes the resonance structure visible: the coverage map becomes periodic stripes aligned with the stride cadence.

## Why It Matters

This is a practical finding with direct production consequence: stride values ≥ 4 are unsafe for scenes with horizontal curvature bands. The collapse is deterministic, not statistical — the same stride will always produce the same resonance. The fix (used in production since this study) is the object-seeded tile scheduler, which breaks the row-alignment assumption entirely.

## Next Step

Repeat the sweep with the tile scheduler to confirm it eliminates the resonance pattern. Publish the band-by-row-mod-stride heatmap as a canonical illustration of sampling resonance in scan-line renderers.

---

*Source:* `output/doe_scheduler_resonance/20260503T002804Z/`  
*Key image:* `visuals/doe-scheduler-resonance-heatmap.png`  
*Dataset:* `datasets/doe-scheduler-resonance.csv`  
*Tier:* 2 — Research Atlas
