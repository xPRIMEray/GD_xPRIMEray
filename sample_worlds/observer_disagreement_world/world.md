# Observer Disagreement World

**Status:** Design proposal  
**Scene:** `test-grin-basic-visual-minimal-offaxis-observe.tscn` + `test-grin-basic-visual-straight-offaxis-observe.tscn`  
**Recommended build priority:** 2

## Purpose

Put the visitor in two places at once: a curved-transport observer and a straight-transport observer looking at the same scene from the same position. Make the difference between them measurable, not just visible. This world is the interactive version of the `observer-disagreement` artifact.

The central experience: the visitor sees one render. A slider or toggle switches between curved and straight transport. A third mode — the disagreement view — paints every pixel that changed between the two. The visitor sees that 23.8% of pixels are different, and can explore *why*.

## What the Visitor Can Observe

1. **Curved GRIN render** — 46,841 geometry hits, 60,295 escaped, 22,464 budget exhausted. The GRIN field bends rays away from surfaces that a straight renderer would have hit.
2. **Straight reference render** — 70,300 geometry hits, 20,964 escaped, 38,336 budget exhausted. Geometry is "closer" in straight transport because rays don't deflect past it.
3. **Disagreement overlay** — 30,839 pixels (~23.8%) where the two transports classify differently. Colored by transition type: bright blue = was geom\_hit, now escaped (the dominant transition, 27k pixels); faint cyan = was escaped, now geom\_hit (only 3k pixels).
4. **Asymmetry observation** — GRIN predominantly deflects rays *away from* surfaces. The effect is not symmetric. This asymmetry is directly observable in the disagreement map: far more blue (deflected-away) than cyan (deflected-toward).
5. **Observer pose sensitivity** — the visitor can (optionally) try different camera angles. Off-axis observers disagree more; on-axis observers disagree less. This is not yet implemented but is the intended next step.

## Relevant Promoted Artifacts

- `observer-disagreement-contact-sheet.png` — the 1600×1300 composite this world reproduces
- `observer-disagreement-observability.png` — the observability cutsheet showing detection geometry
- `datasets/observer-disagreement.json` — exact pixel counts this world should reproduce

## Suggested Overlays

Core experience:
1. `curved_ray` — start here
2. `straight_ray` — toggle to straight reference (same scene, straight paths)
3. `observer_disagreement` — show the pixel-level delta between the two
4. `validation_hud` — confirm classification counts match the promoted artifact

Supporting:
- `heatmap_normals` — helps explain why disagreement clusters near field boundaries
- `atlas_labels` — labels the GRIN field source and geometry to help visitors orient

## Suggested Toggles

| Toggle | Modes |
|--------|-------|
| Transport mode | Curved GRIN / Straight reference / Disagreement delta |
| Disagreement color scheme | By transition type (geom→escaped, escaped→geom) / By magnitude |
| Observer pose | Fixed off-axis (matching artifact) / Free look |
| Validation HUD | Off / Classification counts / Full telemetry |

## Validation Question

*"How many pixels should differ between curved and straight transport for this camera position?"*

Expected answer: approximately 30,839 (23.8% of 129,600 total pixels). The dominant transition should be geometry-hit → escaped-no-hit (≈27,619 pixels), not the reverse. If the disagreement is symmetric or reversed in direction, the GRIN field parameterization needs investigation.

This makes the world self-validating: a visitor who can count (or estimate) the colored pixels in the disagreement view is directly reproducing the experiment.

## MisterY Labs Exhibit Connection

This world supports the "Observer Disagreement" research atlas card. The card presents the numbers; the world makes them observable.

Exhibit caption suggestion:
> *"Same scene. Same camera. Two renderers. 23% of pixels disagree — because one traces curved light and the other assumes it's straight."*

Visitor takeaway: curved transport is not just a visual effect. It changes which surfaces are visible, which escape the scene, and which exhaust the budget. The difference is measurable at the pixel level.

## What Is Missing

- [ ] A continuous slider between curved and straight transport (currently only a hard toggle)
- [ ] Disagreement heatmap over observer pose space (the "angular dependence" next step from the artifact card)
- [ ] A mode that shows *individual ray paths* for disagreeing pixels — tracing both the curved and straight path for the same pixel side by side
- [ ] A count readout in the HUD showing live disagreement percentage as the visitor moves the camera
