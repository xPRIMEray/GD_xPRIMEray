---
title: "Ch 1 — Dual Reality"
description: What does a wormhole look like from the inside, and how do we know the bending is real?
---

# Chapter 1 — Dual Reality

**Act I — Seeing** · Entry chapter · No prerequisites

---

## Core Question

*What does a wormhole look like when light actually bends through it — and how do we know the bending is real and not a rendering trick?*

---

<figure markdown>
  ![Wormhole Dual Reality — six-panel sequence](../../assets/observatory/wormhole-dual-reality-story.png)
  <figcaption>The six-frame sequence: bare curved render → reference reality inset → curvature heat map → portal glyph annotations → collision radar → full interpretive stack. Source: <code>output/wormhole_DR_Story/latest/</code></figcaption>
</figure>

---

## What the Visitor Sees

**Frame 01 — Clean curved render.** The bare wormhole. No overlays. The annular ring compression at the portal boundary, the throat distortion, the asymmetric background distribution — all computed by integrating null geodesics through the GRIN field. Nothing is post-processed.

Notice: the distortion is concentrated at the portal *boundary*, not at the throat. Most visitors expect the throat to be the most distorted region. The curvature heat map (frame 03) confirms the boundary ring is the optically expensive zone.

**Frame 02 — Reference Reality inset.** A straight-ray render of the same scene appears as a frozen inset. The comparison is direct: background objects on the left in straight transport appear on the right in curved transport. The throat region appears much smaller under curved rays than it would under straight ones.

The gap between the two renders is not a visual effect. It is geometry — the same scene rendered under two different transport models.

**Frame 03 — Curvature heat map.** Cumulative absolute ray-turn-angle per pixel, mapped to a heat scale. The portal boundary ring glows brightest. The throat interior is cooler. This inverts the naive expectation. The Chapter 2 experiment will measure how many pixels this difference amounts to.

**Frames 04–06 — Progressive annotation.** Portal glyph markers anchor the curvature ring to the `BoundaryLayerVolume` nodes in the scene. Collision radar labels show where collision-active geometry sits. The full-stack frame combines all layers.

Key observation: high-curvature zones and collision-active zones do not fully overlap. Optical bending and geometric presence are distinct properties of the same scene.

---

## Artifacts

| Artifact | File | Notes |
|----------|------|-------|
| Six-panel contact sheet | `misterylabs_artifacts/visuals/wormhole-dual-reality-story.png` | Primary exhibit image |
| Full-stack frame | `misterylabs_artifacts/visuals/wormhole-dual-reality-full-stack.png` | Frame 06 — all layers |
| Curvature map standalone | `misterylabs_artifacts/visuals/wormhole-dual-reality-curvature-map.png` | Frame 03 without competing overlays |
| Structure observatory | `misterylabs_artifacts/visuals/wormhole-structure-observatory.png` | Six diagnostic modes on the same scene |
| Card | `misterylabs_artifacts/cards/wormhole-dual-reality-story.md` | Ready for MisterY Labs |
| Source sequence doc | `output/wormhole_DR_Story/latest/storytelling_sequence.md` | Per-frame plain-language descriptions |

---

## Sample World

**`dual_reality_view_world`** — [design proposal](https://github.com/AetherTopologist/GD_xPRIMEray/tree/main/sample_worlds/dual_reality_view_world/world.md)

Scene: `test-overspace-wormhole-witness-fixture.tscn`

The world makes the six-image sequence interactive. The visitor starts with the clean curved render and toggles overlays on in the same order as the static sequence. The Reference Reality toggle is the core interaction.

Build priority: **1** (first world to build). Scene exists and is production-parameterized.

---

## Validation Question

*Is the curvature hot zone in frame 03 at the portal boundary or at the throat?*

Expected: **boundary**. If the throat appears hotter than the boundary annulus, the GRIN field parameterization has changed significantly from the promoted artifact run.

*Secondary:* What fraction of pixels visibly change when the Reference Reality inset is toggled on? Expected: approximately 23.8% (measured in Chapter 2).

---

## Key Insight

**The gap between curved and straight transport is not a visual effect — it is geometry, and it is measurable at the pixel level.**

---

## Next Chapter

[Chapter 2 — Observer Disagreement →](chapter_02.md) quantifies the gap: 30,839 pixels, 23.8%, 9:1 asymmetry toward geometry hits becoming escapes.

*Bridge:* "We've seen the difference. Now let's measure it."

---

## Cross-links

- [Output folder README](https://github.com/AetherTopologist/GD_xPRIMEray/tree/main/output/wormhole_DR_Story/README.md)
- [Wormhole Structure Observatory research doc](../../Research/wormhole_dual_reality_transport_workflow.md)
- [Wormhole Dual Reality Framework](../../Research/DualRealityFramework.md)
- [Observatory Atlas](../observatory_atlas.md)
