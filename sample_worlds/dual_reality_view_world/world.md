# Dual Reality View World

**Status:** Design proposal  
**Scene:** `test-overspace-wormhole-witness-fixture.tscn`  
**Recommended build priority:** 1 (first world to build)

## Purpose

Show a new visitor what a wormhole *looks like* from the inside, and then make the bending immediately measurable by comparing it to what a straight-ray renderer would show instead. This world is the interactive version of the `wormhole-dual-reality-story` artifact sequence.

The central experience: the visitor starts with the bare curved render. They toggle on the Reference Reality inset and see a straight-ray version of the same scene appear. The gap between them — the pixels that change — is the wormhole's optical effect, made visible without any equations.

## What the Visitor Can Observe

1. **The bare wormhole render** — curved transport only. The throat compression and annular distortion are visible but not yet labeled.
2. **The Reference Reality inset** — a straight-ray render of the same scene frozen as an overlay. The visitor can drag or toggle it to reveal/hide the comparison.
3. **The curvature heat map** — cumulative ray-turn-angle per pixel. The bright annular ring at the portal boundary is where light bends most — *not* at the throat, which surprises most visitors.
4. **Portal glyph overlay** — BLV markers anchoring the curvature ring to the physical geometry of the portal shell.
5. **Collision radar** — collision-active labels showing where the wormhole's geometry is optically "present." High-curvature zones and collision zones only partially overlap — a key transport insight.

The visitor can turn overlays on and off in sequence, building their own interpretation layer by layer.

## Relevant Promoted Artifacts

- `wormhole-dual-reality-story.png` — the contact sheet this world makes interactive
- `wormhole-dual-reality-full-stack.png` — the target state for the fully-loaded overlay
- `wormhole-dual-reality-curvature-map.png` — the curvature overlay this world should reproduce
- `wormhole-structure-observatory.png` — the broader structural context for this scene

## Suggested Overlays

In recommended experience order:

1. `curved_ray` — start here. No annotations.
2. `dual_reality` — reveal the straight-path inset.
3. `heatmap_normals` — show where curvature is concentrated.
4. `atlas_labels` — add portal glyph anchors.
5. `validation_hud` — show live classification stats (hits, escapes, budget).

Advanced / optional:
- `ray_traces` — render explicit curved ray paths through the portal geometry.
- `cathedral_probe` — full six-layer composite for visitors who want the complete picture.

## Suggested Toggles

| Toggle | Off | On |
|--------|-----|----|
| Reference Reality | Curved render only | Straight-ray inset appears |
| Curvature band | No overlay | Cumulative turn-angle heatmap |
| Portal glyphs | No markers | BLV semantic glyph layer |
| Collision radar | No labels | Collision-active geometry labels |
| Validation HUD | No stats | Live classification counts |
| Ray path mode | Film render | Explicit geodesic trajectory lines |

## Validation Question

*"If I stand at this camera position and toggle Reference Reality on, what fraction of pixels should change?"*

Expected answer from the observer-disagreement experiment: approximately 23% at an off-axis observer position. The visitor can count or estimate the blue-tinted disagreement pixels visible in the split view. If the proportion is dramatically different, the scene or transport settings need investigation.

## MisterY Labs Exhibit Connection

This world is the live version of the "Wormhole Dual Reality — Six Steps" site card. The card tells the story; this world lets the visitor walk it. Site copy should link directly to a launcher URL or embed for this world.

Exhibit caption suggestion:
> *"A wormhole rendered twice — once by curved light, once by straight — so you can see exactly what the bending costs and gains."*

## What Is Missing

- [ ] A side-by-side split-screen drag interaction (currently only available as a static inset)
- [ ] A camera path the visitor can ride that shows disagreement changing with viewing angle
- [ ] The full-stack overlay wired to a single toggle key sequence
- [ ] A "reset to clean curved" shortcut to return to the baseline view
