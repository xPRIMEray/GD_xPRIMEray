# Chapter 2 — Observer Disagreement

**Act:** I — Seeing  
**Role in arc:** Measurement. Converts visual intuition from Chapter 1 into a falsifiable number.  
**Status:** Ready — all core artifacts promoted. Angular-dependence sweep is an enhancement.

---

## Core Question

*How much does the choice of transport model change what you see — and which direction does the error go?*

---

## What Is the Visitor Investigating?

In Chapter 1, the visitor saw that curved and straight transport produce different images. The natural follow-up: *how different, exactly?* And *in which direction* — does curved transport show more geometry than straight, or less?

This chapter answers both questions with a single measurement. At 480×270 pixels (129,600 total), the curved GRIN integrator and the straight reference renderer are run on the same scene from the same camera. Every pixel is classified in both (geometry hit, escaped, budget exhausted). The pixel-level delta is the Observer Disagreement map.

The dominant finding is asymmetric: 27,619 pixels go from "geometry hit" (straight) to "escaped" (curved). Only 3,220 go the other way. The GRIN field predominantly deflects rays *away from* surfaces — the curved observer misses geometry that the straight observer finds.

---

## Observation

**Three views of the same scene, one measurement:**

**Curved GRIN render**
At the canonical off-axis pose: 46,841 geometry hits, 60,295 escaped, 22,464 budget exhausted. The scene has relatively few surface contacts because curved rays are being deflected around geometry.

**Straight reference render**
Same scene, same camera, straight paths: 70,300 geometry hits, 20,964 escaped, 38,336 budget exhausted. Straight rays find geometry that curved rays miss. The geometry hit count is 50% higher under straight transport.

**Disagreement delta**
30,839 pixels differ between the two renders (23.8%). Color encoding in the promoted contact sheet:
- **Blue pixels** (27,619): `geom_hit → escaped_no_hit`. GRIN deflected a ray away from a surface it would have hit under straight transport.
- **Cyan pixels** (3,220): `escaped_no_hit → geom_hit`. GRIN deflected a ray *toward* a surface it would have missed. These are fewer.
- **Gray pixels**: unresolved (budget exhausted in at least one transport — cannot be compared).
- **Transparent**: classified identically by both transports.

The 9:1 ratio (blue to cyan) reveals that the GRIN field acts predominantly as a *defocusing lens* in this configuration: it spreads rays away from the geometry cluster, increasing escape rate and decreasing surface contact rate.

The **observability cutsheet** shows the detection geometry — which surfaces are "visible" to each transport and which are occluded differently. It is the geometric interpretation of the disagreement map.

---

## Artifacts

| File | Role |
|------|------|
| `misterylabs_artifacts/visuals/observer-disagreement-contact-sheet.png` | **Primary.** 1600×1300 composite showing the three-view comparison (curved, straight, delta). |
| `misterylabs_artifacts/visuals/observer-disagreement-observability.png` | **Supplement.** The observability cutsheet — surface visibility by transport model. |
| `misterylabs_artifacts/datasets/observer-disagreement.json` | **Data.** Exact pixel counts per class and per transition type. |
| `misterylabs_artifacts/cards/observer-disagreement.md` | **Card text.** Ready for site. |

---

## Sample World

**`observer_disagreement_world`** — `sample_worlds/observer_disagreement_world/`

Scenes: `test-grin-basic-visual-minimal-offaxis-observe.tscn` + `test-grin-basic-visual-straight-offaxis-observe.tscn`

The world provides a three-mode transport toggle: curved / straight / disagreement delta. The visitor switches modes and watches the pixel-level delta appear. The Validation HUD shows live classification counts, which the visitor can compare directly to the promoted artifact's numbers (30,839 changed pixels, dominant transition geom\_hit→escaped).

The world is self-validating: a visitor who sees approximately 23.8% blue-tinted pixels in disagreement mode is reproducing the experiment in real time.

**Current status:** Design proposal. Both required scenes exist. The three-mode transport toggle does not yet exist as a unified UI element — it requires a new runtime control.

---

## Validation Question

*At this camera pose and transport configuration, what should the dominant disagreement transition be — and in which direction?*

Expected: `geom_hit → escaped_no_hit` (27,619 pixels, ~90% of changed pixels). If `escaped → geom_hit` dominates, the GRIN field is acting as a *focusing* lens in this configuration — unexpected, and worth investigating whether the field parameterization changed.

*Falsification condition:* If the total disagreement is less than 5% or more than 40%, the scene or transport settings differ significantly from the promoted artifact configuration. The dataset provides the reference values.

---

## Key Insight

**Curved transport does not just distort images — it selectively removes surface contacts, with a strong directional asymmetry. The GRIN field in this configuration is a defocusing lens that causes rays to escape rather than hit.**

---

## Next Chapter

**Chapter 3 — Hermetic Closure**

Chapter 2 established that different transport models produce measurably different results. The implicit assumption is that *one of them is correct*. Chapter 3 addresses a harder question: how do you know if either render is correct at all? The hermetic closure contract is the answer.

*Logical bridge:* "We've measured the disagreement. Now: how do we know the curved render is actually right, rather than just differently wrong?"
