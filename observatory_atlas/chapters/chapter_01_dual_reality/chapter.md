# Chapter 1 — Dual Reality

**Act:** I — Seeing  
**Role in arc:** Entry. Opens the atlas with wonder and establishes the core visual claim.  
**Status:** Ready — all supporting artifacts promoted.

---

## Core Question

*What does a wormhole look like when light actually bends through it — and how do we know the bending is real and not a rendering trick?*

---

## What Is the Visitor Investigating?

The visitor is standing inside a wormhole, looking outward. The scene around them is rendered by integrating curved null geodesics through the Gordon effective metric — not post-processed, not faked with lens shaders. Every pixel represents a real ray path through a spatially varying refractive index field.

The question the visitor brings: *Is this just a visual effect, or is the bending physically principled?* The Dual Reality overlay answers this directly by showing two renders of identical geometry side by side — one curved (GRIN integration), one straight (conventional transport). If the bending were decorative, the two renders would be similar. They are not.

---

## Observation

**Six images, each adding one interpretive layer:**

**01 — Clean curved render**
The bare wormhole. No overlays. The annular ring compression, the throat distortion, the asymmetric background distribution — these are the raw output of ray integration. Notice: the distortion is concentrated at the portal *boundary*, not at the throat. Most visitors expect the throat to be the most distorted region. It isn't.

**02 — Reference Reality inset**
A straight-ray render of the same scene appears as an inset. The comparison makes the wormhole's optical effect unambiguous. Background objects that appear on the left in straight transport appear on the right in curved transport. The throat region, which appears visually small and compressed in curved transport, would be much larger under straight rays. The gap between the two is geometry, not art direction.

**03 — Curvature heat map**
Cumulative absolute ray-turn-angle per pixel, mapped to a heat scale. The annular ring at the portal boundary glows brightest — this is where rays bend most. The throat interior is cooler. This inverts the naive expectation: the visually dramatic throat region is not the optically expensive region. The portal *shell* is.

**04 — Curvature + semantic glyphs**
BLV portal markers anchor the curvature ring to the physical geometry. The glyph positions correspond to the `BoundaryLayerVolume` nodes that define the portal shell in the scene. They confirm that the curvature ring and the geometric boundary are the same thing — the heat map is measuring the shell's optical effect, not a rendering artifact.

**05 — Curvature + collision radar**
Collision-active labels added. Key observation: high-curvature zones and collision-active zones do *not* fully overlap. Some regions with high ray bending contain no collidable geometry. Some collidable geometry sits in low-curvature zones. This non-overlap is a transport insight: optical bending and geometric presence are distinct properties of the same scene.

**06 — Full stack**
Reference Reality inset, curvature heat map, semantic glyphs, and collision radar simultaneously. Information-dense. For a visitor who has followed the sequence, every layer is now interpretable on its own.

---

## Artifacts

| File | Role |
|------|------|
| `misterylabs_artifacts/visuals/wormhole-dual-reality-story.png` | **Primary thumbnail.** Six-panel contact sheet of the complete sequence. |
| `misterylabs_artifacts/visuals/wormhole-dual-reality-full-stack.png` | **Feature image.** The fully-loaded frame 06 — all layers simultaneously. |
| `misterylabs_artifacts/visuals/wormhole-dual-reality-curvature-map.png` | **Explainer image.** Frame 03 standalone — the curvature heat map without competing overlays. |
| `misterylabs_artifacts/visuals/wormhole-structure-observatory.png` | **Context image.** Six-panel observatory showing the wormhole in six diagnostic modes beyond the DR story. |
| `misterylabs_artifacts/visuals/wormhole-structure-resonance-chamber.png` | **Supplement.** The resonance chamber overlay — wave-packet physics vocabulary grounded in renderer telemetry. |
| `misterylabs_artifacts/cards/wormhole-dual-reality-story.md` | **Card text.** Ready for site. |

---

## Sample World

**`dual_reality_view_world`** — `sample_worlds/dual_reality_view_world/`

Scene: `test-overspace-wormhole-witness-fixture.tscn`

The world makes the six-image sequence interactive: the visitor starts with the clean curved render and toggles overlays on in the same order as the static sequence. The Reference Reality toggle is the core interaction — the moment the inset appears, the chapter's claim becomes directly verifiable.

**Current status:** Design proposal. Scene exists and is production-parameterized. The main missing runtime feature is a drag-able split-screen interaction for the Reference Reality comparison.

---

## Validation Question

*If the Reference Reality inset is toggled on at this camera pose, what fraction of pixels should visibly change?*

The observer disagreement experiment (Chapter 2) measures this directly: approximately 23.8% of pixels classify differently between curved and straight transport at an off-axis pose. A visitor who notices the inset looks dramatically different is observing a real measurement, not a visual impression.

*Secondary:* Is the curvature hot zone in frame 03 at the portal boundary or at the throat? Expected: boundary. If the throat appears hotter, the GRIN field parameterization has changed significantly from the promoted artifact.

---

## Key Insight

**The gap between curved and straight transport is not a visual effect — it is geometry, and it is measurable at the pixel level.**

---

## Next Chapter

**Chapter 2 — Observer Disagreement**

Chapter 1 establishes that curved and straight transport produce different images. Chapter 2 quantifies exactly how different: 30,839 pixels changed, with a 9:1 asymmetry between "hits become escapes" and "escapes become hits." The atlas moves from perception to measurement.

*Logical bridge:* "We've seen the difference. Now let's measure it."
