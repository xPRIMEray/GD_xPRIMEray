# Chapter 3 — Hermetic Closure

**Act:** II — Trusting  
**Role in arc:** Validation. Reveals that "looks correct" and "is correct" are not the same thing. Introduces the hermetic contract as the correctness standard.  
**Status:** Ready — core artifacts promoted. Side-by-side budget comparison render is an enhancement.

---

## Core Question

*When is a render silently wrong — and what does the failure look like from the outside?*

---

## What Is the Visitor Investigating?

Chapters 1 and 2 showed that curved transport produces different images than straight transport. Chapter 3 addresses a more unsettling question: *how do we know the curved render is actually correct, rather than just differently wrong?*

The answer is the **hermetic contract**: in a hermetic scene, every ray that enters the integration volume must reach a definitive classification (geometry hit, escaped, portal event) before the integrator exits. A render that satisfies the contract is *closed* — every pixel is a real transport result. A render that violates it is *open* — some pixels are unresolved budget exhaustion, displayed as noise that looks like a legitimate render.

This chapter demonstrates the cliff edge between the two states.

---

## Observation

**Two renders, same scene, same step length, one parameter changed:**

**Budget = 32 (0% closure)**
The image looks reasonable. Geometry is visible, shading is present, no obvious artifacts. A developer looking at this image without instrumentation would not know anything was wrong.

The Validation HUD tells a different story: every one of the 200 test rays exhausted its step budget before reaching a classification endpoint. Closure: 0.0%. Phase: `budget_saturated`. Every pixel in this render is unresolved — the renderer stopped integrating before the ray found its result. The image is noise that happens to look plausible.

**Budget = 700 (100% closure)**
The image looks essentially similar. The HUD: 0 budget-exhausted rays. Closure: 100.0%. Phase: `plateau` (additional budget adds no cost — rays are resolving well before budget limit). Every pixel is a real transport result.

**The failure storyboard**
The spatial distribution of failure pixels at budget=32 is not random. Failures cluster in the high-curvature annular region — the same region that the curvature heat map from Chapter 1 identified as optically expensive. The integrator needs more steps in high-curvature zones to find the ray's endpoint. This is the causal link between curvature and closure failure.

**The recovery heatmap**
With adaptive budget scaling enabled, the integrator grants additional steps to rays in high-curvature zones. Recovery is high in the annular band and low at corners. The pattern matches the step-length sensitivity from the DOE overnight experiment (Chapter 4 context): fine steps reach more of the curvature band, and adaptive scaling helps most where the field is strongest.

---

## Artifacts

| File | Role |
|------|------|
| `misterylabs_artifacts/visuals/hermetic-hit-closure-storyboard.png` | **Primary.** Failure pixel distribution at budget=32. Shows spatial clustering. |
| `misterylabs_artifacts/visuals/hermetic-hit-closure-recovery.png` | **Key finding.** Adaptive recovery heatmap — where adaptive scaling succeeds. |
| `misterylabs_artifacts/validation/hermetic-hit-closure.md` | **Evidence.** Full summary: cell table, budget vs. closure, phase classification. |
| `misterylabs_artifacts/cards/hermetic-hit-closure.md` | **Card text.** Ready for site. |

**Missing:** A side-by-side render showing budget=32 (left) and budget=700 (right) simultaneously with both Validation HUDs visible — the most direct possible before/after comparison. This is the chapter's highest-priority screenshot gap.

---

## Sample World

**`hermetic_closure_world`** — `sample_worlds/hermetic_closure_world/`

Scene: `test-hermetic-curved-room.tscn`

The world provides a budget selector with three presets:
- **Budget=32** ("silent failure") — 0% closure, image looks reasonable
- **Budget=300** ("cliff edge") — transition zone, closure rising from 0 to ~50%
- **Budget=700** ("full closure") — 100% closure, plateau phase

The Validation HUD is mandatory in this world — it is the only mechanism by which the visitor can observe the failure mode. Without it, budget=32 and budget=700 look nearly identical.

The hermetic closure overlay colors pixels green (geom hit), blue (escaped), red (budget exhausted). At budget=32, the film is entirely red — every pixel unresolved. The combination of HUD showing 0% and a fully-red overlay makes the silent failure mode viscerally clear.

**Current status:** Design proposal. Scene exists. The three-preset budget selector is the primary missing runtime control; the `hermetic_closure` overlay layer exists as a diagnostic mode but needs the budget-selector UI.

---

## Validation Question

*At budget=700, step=0.015, row traversal: what percentage of pixels should be correctly classified?*

Expected: 100.0% (plateau phase). If closure is below 95%, either the scene configuration differs from the promoted artifact or the integrator has regressed.

*At budget=32, same settings:* Expected 0.0% closure, phase `budget_saturated`. If closure is above 5%, the scene or integrator is more efficient than the artifact baseline — worth investigating whether this is an improvement or a scene mismatch.

---

## Key Insight

**A renderer can fail completely and look fine. The only way to know is to measure closure. "Plausible" is not "correct."**

---

## Next Chapter

**Chapter 4 — Coherence Basin**

Chapter 3 showed that the integrator can fail when given insufficient budget. Chapter 4 asks a harder question: are there regions of the transport field where no amount of budget or precision can achieve convergence? The answer is yes — and their location is not random.

*Logical bridge:* "We know that budget determines closure. But what if the problem isn't budget at all — what if some regions of the field are fundamentally unstable?"
