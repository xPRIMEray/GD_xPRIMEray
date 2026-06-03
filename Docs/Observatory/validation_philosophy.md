---
title: Validation Philosophy
description: Why plausible ≠ correct, and what xPRIMEray measures to know the difference
---

# Validation Philosophy

> *Observation precedes explanation. Plausible ≠ Correct.*

This page explains why xPRIMEray treats transport validation as a first-class concern, not an afterthought.

---

## The Core Problem

A curved-ray renderer that produces a wrong image does not necessarily produce an obviously wrong image.

The two failure modes are:

1. **Silent budget exhaustion:** A ray runs out of integration steps before reaching a classification endpoint. The renderer records an unresolved pixel and displays it using whatever partial state it has. The result looks like a real render — shading, geometry presence, plausible color — but it is noise.

2. **Transport model mismatch:** A renderer that uses straight-line approximations in a curved-field scene will produce incorrect ray paths. The image looks like the scene but is not what the scene would actually look like under curved transport. The difference may be subtle (minor distortion) or large (wrong geometry visible, wrong surfaces in contact).

Both failures are silent. Neither produces a crash, an error message, or an obviously glitched output. Both pass casual visual inspection. This is what makes transport validation necessary.

---

## Plausible Render vs. Validated Render

| Property | Plausible render | Validated render |
|----------|-----------------|-----------------|
| Passes visual inspection | Yes | Yes |
| Every pixel classified | Not guaranteed | Guaranteed (hermetic contract) |
| Transport model known | Not necessarily | Explicit (curved or straight) |
| Closure measured | No | Yes (closure %) |
| Budget pressure known | No | Yes (HUD) |
| Regression detectable | No | Yes (pixel-level comparison) |

A plausible render is a render that looks correct.  
A validated render is a render that *is* correct — with the evidence to prove it.

xPRIMEray generates both. The difference is instrumentation.

---

## Why HUD Metrics Matter

The Validation HUD provides live per-frame transport health metrics. The key outputs:

**Closure percentage:** The fraction of rays that reached a definitive classification (geometry hit, escaped, portal event) before exhausting their step budget. 100% closure means every pixel is a real transport result. 0% means every pixel is unresolved budget noise.

At integration budget=32 and step=0.015 on the hermetic curved room scene: closure is 0.0%. The image looks plausible. The HUD is the only way to know the render is completely wrong.

**Budget pressure:** How close the average ray came to exhausting its budget. Low pressure = comfortable budget (additional steps would add zero cost). High pressure = approaching the cliff (the cliff is not visible until budget drops below the threshold, at which point closure collapses).

**Classification breakdown:** The exact count of geometry hits, escapes, portal events, and budget-exhausted pixels. Comparing these to a reference run establishes whether the render is within the expected operating range.

Without HUD metrics, transport correctness is unverifiable. With them, it is measurable.

---

## Why Pixel Disagreement Matters

Two renders of the same scene under different transport models will disagree at some pixels. The disagreement is not noise — it is measurement. The magnitude, direction, and spatial distribution of the disagreement characterize how much the transport model matters for this scene and this camera position.

From the [Observer Disagreement experiment](chapters/chapter_02.md): at an off-axis observer pose, curved and straight transport disagree on 23.8% of pixels. The dominant transition is geometry-hit → escaped (27,619 pixels vs. 3,220 in the reverse direction — a 9:1 asymmetry).

This tells us:
- Curved transport removes 33% of surface contacts compared to straight transport
- The removal is strongly directional — GRIN deflects rays away from geometry, not toward it
- The effect is not symmetric — it is not just "things are in different positions"

Pixel disagreement turns a visual impression ("these two renders look different") into a measurement ("23.8% of pixels, dominated by geometry hits becoming escapes"). The measurement is falsifiable: a different observer pose should produce a different disagreement fraction, and the angular dependence is a testable prediction.

---

## Why Closure Matters

The hermetic contract establishes the minimum correctness standard for a curved-ray renderer: every ray entering the scene volume must be classified before the integrator exits.

Closure is the fraction of rays that satisfy the contract. A scene with 100% closure has been fully resolved — every pixel represents a real transport event. A scene with 0% closure has no reliable pixels — every pixel is unresolved budget noise that happens to look like a render.

**Closure is not gradual.** It falls off a cliff. At a fixed step length, there is a budget threshold below which closure collapses from near-complete to near-zero. The cliff is discovered experimentally — it varies by scene complexity, curvature field strength, and integration parameters.

**Closure is not sufficient alone.** 100% closure means every pixel was classified, but it does not specify which classification. A scene where every ray escapes the volume achieves 100% closure — but the image is blank. Closure + classification breakdown + pixel-level comparison to a reference is the complete correctness picture.

---

## Why Outputs Are Preserved as Research Artifacts

Every significant experiment output is preserved in `output/` and selected outputs are promoted to `misterylabs_artifacts/`. This is not archivism — it is method.

**Reproducibility:** A promoted artifact can be regenerated from the source folder's `README.md`, which specifies the exact script, scene, and parameters used. Any deviation from the reference run is detectable.

**Regression detection:** Artifact images with known pixel-level properties serve as regression baselines. If a code change shifts the observer disagreement from 23.8% to 31%, that is a meaningful signal — not noise.

**Evidence integrity:** The observatory approach requires that claims be grounded in specific experimental results. "The curvature hot zone is at the boundary, not the throat" is a claim backed by a specific heat map image from a specific run. The output folder is the evidence; the artifact is the promoted summary.

**Audit trail:** Git history records which outputs were generated from which code state. Output READMEs record the generation context. The `misterylabs_artifacts/manifest.json` records which outputs were promoted and why. The chain from claim to evidence to code is traceable.

---

## The Falsifiability Standard

Every quantitative claim in the Observatory Atlas comes with a falsification condition:

- "Curvature hot zone is at the boundary" → falsified if the throat appears hotter in a new heat map
- "23.8% pixel disagreement at this pose" → falsified if disagreement is outside 20–28%
- "Budget=700 achieves 100% closure" → falsified if closure falls below 95%
- "289 UNSEALED regions at epsilon=0.05" → falsified if the oracle finds a significantly different count
- "Stride=4 collapses band coverage to 0.2%" → falsified if band coverage remains above 5% at stride=4

Falsification conditions are documented in the [Atlas Manifest](https://github.com/xPRIMEray/GD_xPRIMEray/tree/main/observatory_atlas/atlas_manifest.json) (`validation_tests` array) and in each chapter's validation question.

The transport oracle does not assert physics beyond its implemented renderer, diagnostics, and visual models. The observatory treats its own output with the same skepticism it applies to any experimental instrument.

---

## The Validation Stack

```
Level 1: Visual inspection
  "Does it look correct?"
  Necessary but not sufficient. Budget=32 passes level 1; it fails levels 2 and 3.

Level 2: Hermetic closure
  "Is every pixel classified?"
  Required for any correctness claim. Closure < 100% = unresolved pixels.

Level 3: Classification breakdown
  "Which pixels are hits, escapes, budget-exhausted?"
  Establishes the transport outcome distribution.

Level 4: Reference comparison
  "Does this match the reference run within expected variance?"
  Required for regression detection. Uses pixel-level disagreement metrics.

Level 5: Oracle validation
  "Does the oracle confirm that convergence-uncertain regions match
   the expected instability map?"
  Research-grade validation. Chapter 4 (Coherence Basin) operates here.
```

xPRIMEray is built to reach Level 4 on every validated render and Level 5 on research runs.

---

## Summary

- A plausible render can be completely wrong. The only detector is measurement.
- HUD metrics make transport correctness observable without visual inspection.
- Pixel disagreement makes the transport model choice measurable.
- Closure makes render correctness contractual.
- Preserved outputs make claims falsifiable and reproducible.

The [Observatory Atlas](observatory_atlas.md) applies this philosophy in sequence: Chapter 1 shows the phenomenon; Chapter 2 measures it; Chapter 3 validates it; Chapters 4 and 5 diagnose the failure modes.
