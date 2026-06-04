# Hermetic Closure Hero — Two Budgets, One Question

**Hook:** Two renders. Same scene. Same camera. One is correct. One is 100% noise. Can you tell which?

## What You See

Left panel: integration budget = 32 steps. The image looks plausible — geometry is present, shading is reasonable, no obvious holes.

Right panel: integration budget = 700 steps. The image looks nearly identical.

The difference is not visible. It is measured:

| Panel | Budget | Closure | Meaning |
|-------|--------|---------|---------|
| Left  | 32     | **0.0%** | Every pixel is unresolved budget exhaustion — noise that resembles a render |
| Right | 700    | **100.0%** | Every pixel is a real transport result |

The red/green accent strips and closure labels are the only way to know.

## Why It Matters

This is the core failure mode of curved-ray rendering: a render can look correct while being completely wrong. The failure is not obvious, not gradual, and not recoverable without instrumentation. Below budget ≈ 300, closure collapses from near-complete to near-zero. The cliff is sharp. The image before and after the cliff looks the same.

The hermetic closure contract — every ray must reach a classification endpoint before the integrator exits — is the only reliable detector. Without it, budget=32 passes casual visual inspection. With it, the failure is immediate and unambiguous.

## What the Failure Map Shows (3-panel version)

The middle panel (budget_exhaustion_heatmap) shows where the budget was exhausted. Failures cluster in the annular high-curvature zone at the field boundary — the same zone Chapter 1's heat map identified as optically expensive. Budget failure is not random. It traces the physics.

## Technical Context

- Scene: `test-hermetic-curved-room.tscn`
- Step length: 0.015
- Traversal: row, stride=1
- Film resolution: 640×360 (new hero capture, June 2026)
- Closure contract: every pixel must reach `geometry_hit`, `escaped`, or `portal_event`

## Falsification

At budget=700, step=0.015: closure should be 100.0%. Below 95% indicates regression.  
At budget=32, same settings: closure should be 0.0%. Above 5% means the scene changed — the budget=32 silence was harder to achieve, which is a meaningful physics change worth investigating.

---

*Source:* `output/hermetic_hit_closure/20260604T023019Z/`  
*Hero image:* `visuals/hermetic-closure-hero.png` (1288×464)  
*3-panel:* `visuals/hermetic-closure-hero-3panel.png` (1936×464)  
*Web crop:* `visuals/hermetic-closure-hero-web.png` (1280×461)  
*Presentation:* `visuals/hermetic-closure-hero-pres.png` (1920×540)  
*Tier:* 1 — Minimum Viable Observatory  
