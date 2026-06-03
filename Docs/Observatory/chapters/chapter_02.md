---
title: "Ch 2 — Observer Disagreement"
description: How much does curved transport change what you see — measured at the pixel level?
---

# Chapter 2 — Observer Disagreement

**Act I — Seeing** · Requires Ch 1 concept · Measurement chapter

---

## Core Question

*How much does the choice of transport model change what the observer sees — and in which direction does the error go?*

---

<figure markdown>
  ![Observer Disagreement — three-view contact sheet](../../assets/observatory/observer-disagreement-contact-sheet.png)
  <figcaption>1600×1300 contact sheet: curved GRIN render (left), straight reference render (center), disagreement delta (right). Blue pixels were geometry hits in straight transport that became escapes in curved. Source: <code>output/observer_disagreement/offaxis_observe_delta/</code></figcaption>
</figure>

---

## What the Visitor Sees

**Curved GRIN render (left):** 46,841 geometry hits, 60,295 escaped, 22,464 budget exhausted. Fewer surface contacts than the straight renderer because curved rays are deflected around geometry by the GRIN field.

**Straight reference render (center):** 70,300 geometry hits, 20,964 escaped, 38,336 budget exhausted. Straight rays find geometry that curved rays miss. The geometry hit count is 50% higher.

**Disagreement delta (right):** 30,839 pixels (23.8% of 129,600 total) classify differently. Color encoding:

| Color | Transition | Count | Meaning |
|-------|-----------|-------|---------|
| **Bright blue** | `geom_hit → escaped` | 27,619 | GRIN deflected the ray *away* from a surface it would have hit |
| **Faint cyan** | `escaped → geom_hit` | 3,220 | GRIN deflected the ray *toward* a surface it would have missed |
| **Gray** | unresolved | — | Budget exhausted in at least one transport |
| **Transparent** | unchanged | — | Same classification in both |

The **9:1 ratio** (blue to cyan) is the key finding. The GRIN field in this configuration is a defocusing lens: it predominantly redirects rays away from geometry, increasing escape rate rather than increasing surface contact rate.

---

## The Numbers

From the promoted dataset (`misterylabs_artifacts/datasets/observer-disagreement.json`):

```
changed_pixels:   30,839
changed_ratio:    0.2380  (23.80%)
dominant_transition: geom_hit → escaped_no_hit
dominant_count:   27,619  (89.6% of all changed pixels)
reverse_count:     3,220  (10.4% of all changed pixels)
asymmetry_ratio:    8.6:1
```

A straight renderer would report 70,300 geometry hits for this scene. xPRIMEray's curved transport reports 46,841 — a 33% shortfall. The difference is not noise or aliasing. It is the physics of curved rays deflecting past surfaces.

---

## Artifacts

| Artifact | File | Notes |
|----------|------|-------|
| Contact sheet (1600×1300) | `misterylabs_artifacts/visuals/observer-disagreement-contact-sheet.png` | Primary exhibit image |
| Observability cutsheet | `misterylabs_artifacts/visuals/observer-disagreement-observability.png` | Surface visibility by transport |
| Pixel counts (JSON) | `misterylabs_artifacts/datasets/observer-disagreement.json` | Machine-readable reference values |
| Card | `misterylabs_artifacts/cards/observer-disagreement.md` | Ready for MisterY Labs |
| Source report | `output/observer_disagreement/offaxis_observe_delta/classification_delta_summary.json` | Full per-class classification data |

---

## Sample World

**`observer_disagreement_world`** — [design proposal](https://github.com/AetherTopologist/GD_xPRIMEray/tree/main/sample_worlds/observer_disagreement_world/world.md)

Scenes: `test-grin-basic-visual-minimal-offaxis-observe.tscn` + `test-grin-basic-visual-straight-offaxis-observe.tscn`

The world provides a three-mode toggle: curved / straight / disagreement delta. The Validation HUD shows live classification counts that the visitor can compare to the reference values above. The world is self-validating: approximately 30,839 blue-tinted pixels in disagreement mode reproduces the experiment.

Build priority: **2**.

---

## Validation Question

*At this camera pose and transport configuration, what should the dominant disagreement transition be — and in which direction?*

Expected: `geom_hit → escaped_no_hit` (27,619 pixels, ~90% of changed pixels).

*Falsification condition:* If `escaped → geom_hit` dominates, the GRIN field is acting as a focusing lens in this configuration — unexpected, and worth investigating.

*Quantitative gate:* Total disagreement should be between 20% and 28% of pixels. Below 5% or above 40% indicates scene or transport configuration mismatch.

---

## Key Insight

**Curved transport does not just distort images — it selectively removes surface contacts, with a strong directional asymmetry. The GRIN field in this configuration is a defocusing lens: 9 pixels lost geometry contact for every 1 that gained it.**

---

## Next Chapter

[Chapter 3 — Hermetic Closure →](chapter_03.md): Chapters 1 and 2 showed that curved and straight transport produce measurably different results. Chapter 3 asks: how do we know the curved result is *correct*, rather than just differently wrong?

*Bridge:* "We've measured the disagreement. Now: how do we verify the curved version is right?"

---

## Related Research

- [Observer Disagreement Observatory V01](../../Research/OBSERVER_DISAGREEMENT_OBSERVATORY_V01.md)
- [Classification Delta Pipeline V01](../../Research/CLASSIFICATION_DELTA_PIPELINE_V01.md)
- [Observability Visualization Layer](../../Research/OBSERVABILITY_VISUALIZATION_LAYER_V01.md)
- [Observatory Atlas](../observatory_atlas.md)
