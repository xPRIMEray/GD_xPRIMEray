# Hermetic Closure World

**Status:** Design proposal  
**Scene:** `test-hermetic-curved-room.tscn`  
**Recommended build priority:** 3

## Purpose

Show the visitor what a renderer looks like when it's *wrong but plausible* — and then show what it looks like when it's right. This world demonstrates the hermetic closure contract: every ray entering a defined volume must be classified before the integrator exits. When the budget is too small, the renderer silently fails. When budget is sufficient, every pixel is resolved.

The central experience: the visitor sees what appears to be a reasonable-looking render. A slider or toggle reduces the step budget from 700 to 32. The image changes very little in overall appearance but the Validation HUD shows closure dropping from 100% to 0%. The visitor realizes the entire image at budget=32 is unclassified — they're looking at unresolved noise that *looks like* a real render.

This is the "silent failure" demo — and it is the most important validation story in xPRIMEray.

## What the Visitor Can Observe

1. **Budget=700 render (100% closure)** — every ray is classified: hit, escape, or portal event. The Validation HUD shows 0 unresolved pixels. This is a correct render.
2. **Budget=32 render (0% closure)** — the image looks plausible. The Validation HUD shows 200/200 rays exhausted without classification. Every "pixel" is budget-saturated noise, not a real transport result.
3. **Failure storyboard** — the failure pixels are not randomly distributed. They cluster in the high-curvature annular region where rays need more steps to find their endpoint. The spatial structure of failure reveals the transport difficulty map.
4. **Recovery heatmap** — adaptive budget scaling recovers most failures in the curvature band; corners remain the hardest to close. The recovery pattern is interpretable: where the field is strongest, adaptive scaling helps most.
5. **Budget slider** — sliding from 32 to 700 shows the closure cliff: there is a sharp transition around budget ≈ 300 where closure jumps from near-zero to near-complete. This cliff is the operating threshold.

## Relevant Promoted Artifacts

- `hermetic-hit-closure-storyboard.png` — failure pixel distribution this world reproduces
- `hermetic-hit-closure-recovery.png` — adaptive recovery heatmap
- `validation/hermetic-hit-closure.md` — detailed summary with exact budget vs. closure numbers

## Suggested Overlays

Core experience:
1. `hermetic_closure` — pixel classification overlay (hit=green, escape=blue, budget\_exhausted=red)
2. `validation_hud` — live closure %, budget pressure, and classification counts
3. `heatmap_normals` — shows why failures cluster where they do (high-curvature zones)

Advanced:
- `cathedral_probe` — full diagnostic for visitors who want the complete failure anatomy
- `ray_traces` — show individual ray paths for failing pixels to illustrate why they exhaust budget

## Suggested Toggles

| Toggle | Off | On |
|--------|-----|----|
| Step budget | 700 (full closure) | 32 (zero closure) |
| Closure overlay | No coloring | Green/blue/red per pixel classification |
| Adaptive scaling | Disabled | Enabled — shows recovery from failures |
| Failure storyboard | Live render | Static storyboard from the promoted artifact |
| Validation HUD | Hidden | Live closure %, budget pressure, class counts |

## Validation Question

*"At budget=700 and step=0.015, what percentage of pixels should be correctly classified?"*

Expected answer: 100.0% (plateau phase — all 200 rays resolve, no budget pressure). At budget=32, the expected closure is 0.0% (budget\_saturated — all rays exhaust steps before classification).

The visitor can verify this directly from the Validation HUD. If the numbers differ significantly, the scene configuration or integrator settings need investigation.

Secondary question: *"Where do failures concentrate, and why?"*
Expected pattern: failures cluster in the annular high-curvature region, not in corners or flat areas. The storyboard and recovery heatmap together answer this.

## MisterY Labs Exhibit Connection

This world supports the "Hermetic Hit Closure" research atlas card. The card presents the 0%/100% cliff finding; this world makes the cliff interactive.

Exhibit caption suggestion:
> *"A render at budget=32 looks fine. The HUD says 0% of pixels are actually resolved. This is what silent renderer failure looks like — and how xPRIMEray catches it."*

The hermetic closure contract is xPRIMEray's core correctness guarantee. Demonstrating it failing (visibly) and succeeding (measurably) is the clearest possible statement of why the engine takes transport correctness seriously.

## What Is Missing

- [ ] A continuous budget slider (currently the experiment is two discrete cells: 32 and 700)
- [ ] A "cliff finder" mode that automatically scans the budget axis to locate the transition point for the current scene and step length
- [ ] A side-by-side render: budget=32 on the left, budget=700 on the right, with the Validation HUD for each
- [ ] A failure island map that shows spatially coherent failure clusters as labeled regions
- [ ] Confirmation that `test-hermetic-curved-room.tscn` uses the same scene parameterization as the promoted artifact (step=0.015, row traversal) — this needs verification before the world can be validated
