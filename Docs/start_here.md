---
title: Start Here
description: A no-jargon introduction to xPRIMEray and where to begin exploring the observatory
---

# Start Here

> **Observation precedes explanation. Plausible ≠ Correct.**

---

## What xPRIMEray Is

xPRIMEray is a renderer that traces curved light — not as a visual effect, but as physics. Every pixel is computed by integrating a ray's actual trajectory through a spatially varying medium. The ray bends because the medium curves it, the same way light bends in a gravitational lens or a gradient-index optical fibre.

The engine solves the eikonal transport equation:

$$\dot{\mathbf{x}} = \frac{\mathbf{p}}{n(\mathbf{x})}, \qquad \dot{\mathbf{p}} = \nabla n(\mathbf{x})$$

where $n(\mathbf{x})$ is the refractive index field. Rays are curved primitives. The renderer does not fake bending with post-process lens shaders. It solves the correct path.

**What it is not:** xPRIMEray is not a physics simulator or a general-purpose renderer. It is a research instrument — specifically, a transport observatory for studying how curved ray paths affect what an observer sees, and for validating that the computed paths are actually correct.

---

## Why Curved Transport Matters

A renderer that assumes straight rays will compute the wrong image whenever the medium bends light. The error is not always obvious. Two images that look similar can have dramatically different ray classifications underneath. The classic failure: a render that looks plausible but has zero correctly resolved pixels.

This is not hypothetical. [Chapter 3 — Hermetic Closure](Observatory/chapters/chapter_03.md) demonstrates it directly: at integration budget=32, a rendered image of a curved scene looks reasonable. The validation HUD shows 0.0% closure — every pixel is unresolved budget noise, not a transport result.

Curved transport matters because:

1. **The images are different.** At an off-axis observer pose, curved and straight transport classify 23.8% of pixels differently — most of them geometry hits that become escapes under GRIN deflection.
2. **The difference is measurable.** xPRIMEray quantifies the disagreement per pixel, per classification class, per transition direction.
3. **Incorrect renders look correct.** Without a validation layer (hermetic closure contract, Validation HUD, oracle reference), there is no reliable way to distinguish a correct curved render from a plausible-but-wrong one.

---

## What to Look at First

If you have 20 minutes, take the Observatory Tour below.

If you want to go deeper, explore by role:

=== "New Visitor"

    Start with the [Observatory Atlas](Observatory/observatory_atlas.md). Follow the five-chapter arc from Dual Reality through the Cathedral Probe. Each chapter is self-contained and builds on the previous.

    The most visual entry: [Chapter 1 — Dual Reality](Observatory/chapters/chapter_01.md). Toggle the Reference Reality inset and see the wormhole rendered twice simultaneously.

=== "Researcher"

    Start with the [Validation Philosophy](Observatory/validation_philosophy.md) to understand the correctness framework. Then go to [Chapter 4 — Coherence Basin](Observatory/chapters/chapter_04.md) for the transport stability map and [Chapter 5 — Cathedral Probe](Observatory/chapters/chapter_05.md) for the diagnostic methodology.

    The architecture paper is the canonical technical reference: [Cathedral Probe Architecture](Research/cathedral_probe_architecture.md).

=== "Developer / Contributor"

    Start with the [Feature Index](FEATURE_INDEX.md) and [System Architecture](architecture.md). The [Sample Worlds](Observatory/sample_worlds.md) design proposals specify the runtime world system that needs building. The [Overlay Master List](Observatory/OVERLAY_MASTER_LIST.md) is the overlay implementation reference.

=== "Site Visitor"

    This site is the technical observatory. The public-facing exhibit is **MisterY Labs** — the curated artifact and interactive world portal. Links from here go to the research evidence. Links from MisterY Labs go here for the underlying data.

---

## The 20-Minute Observatory Tour

Three chapters that communicate the essence of xPRIMEray without background.

---

### Step 1 — Dual Reality (8 min)

<figure markdown>
  ![Wormhole Dual Reality — six-panel sequence](assets/observatory/wormhole-dual-reality-story.png)
  <figcaption>Six frames: bare curved render → reference reality inset → curvature heat map → semantic glyphs → collision radar → full stack.</figcaption>
</figure>

**What you see:** A wormhole rendered twice — once with curved transport, once with straight — so the bending is directly visible as the gap between the two.

**What it proves:** The curvature is not a visual effect. It is geometry: the portal boundary ring glows in the heat map because that is where light actually bends most, not because it was painted that way.

**Where to go:** [Chapter 1 — Dual Reality](Observatory/chapters/chapter_01.md)

---

### Step 2 — Observer Disagreement (7 min)

<figure markdown>
  ![Observer Disagreement Hero — curved GRIN vs straight vs delta, labeled](assets/observatory/observer-disagreement-hero.png)
  <figcaption>Curved GRIN (left) · straight reference (center) · disagreement delta (right). Blue pixels: geometry hits that became escapes under curved transport. 8.6:1 asymmetry — the GRIN field is defocusing, not focusing.</figcaption>
</figure>

**What you see:** 30,839 pixels that classify differently between curved and straight transport at the same camera position.

**What it proves:** Curved transport is not just different-looking — it is measurably different. The dominant transition (27,619 pixels) is geometry-hit → escaped: the GRIN field deflects rays away from surfaces they would otherwise have hit. This is a measurable, directional effect.

**Where to go:** [Chapter 2 — Observer Disagreement](Observatory/chapters/chapter_02.md)

---

### Step 3 — Hermetic Closure (5 min)

<figure markdown>
  ![Hermetic Closure Hero — budget=32 (0% closure) vs budget=700 (100% closure) side by side](assets/observatory/hermetic-closure-hero.png)
  <figcaption>Left: budget=32 — 0.0% closure, every pixel is unresolved noise. Right: budget=700 — 100.0% closure, every pixel real. The images look identical. The labels are the proof.</figcaption>
</figure>

**What you see:** A render that looks correct. The Validation HUD shows 0% closure — every pixel is unresolved budget noise.

**What it proves:** Plausible ≠ Correct. A renderer that runs out of integration steps produces noise that passes casual visual inspection. The only reliable detector is the closure metric.

**Where to go:** [Chapter 3 — Hermetic Closure](Observatory/chapters/chapter_03.md)

---

### Where the Tour Goes Next

The 20-minute arc covers *perception → measurement → validation*. Two more chapters complete the picture:

- **[Chapter 4 — Coherence Basin](Observatory/chapters/chapter_04.md):** Where in the transport field does convergence fail regardless of budget or precision?
- **[Chapter 5 — Cathedral Probe](Observatory/chapters/chapter_05.md):** How do you find those failure zones from the rendered output alone, without running the oracle?

---

## Repository Structure

| Location | What it is |
|----------|-----------|
| [`output/`](https://github.com/xPRIMEray/GD_xPRIMEray/tree/main/output) | Active lab bench: all experiment outputs, logs, renders, validation runs |
| [`misterylabs_artifacts/`](https://github.com/xPRIMEray/GD_xPRIMEray/tree/main/misterylabs_artifacts) | Curated export layer: promoted images, cards, datasets, manifest |
| [`sample_worlds/`](https://github.com/xPRIMEray/GD_xPRIMEray/tree/main/sample_worlds) | Interactive world design proposals |
| [`observatory_atlas/`](https://github.com/xPRIMEray/GD_xPRIMEray/tree/main/observatory_atlas) | Atlas chapters, manifest, dependency graph |
| [`Docs/`](https://github.com/xPRIMEray/GD_xPRIMEray/tree/main/Docs) | This site's source — research documentation, specs, papers |

---

## Motto

> *Observation precedes explanation. Plausible ≠ Correct.*

Every page in this observatory is grounded in a specific experimental result. Where a claim is a hypothesis, it is labeled as such. Where an image is a visualization rather than a validation, the caption says so. The transport oracle does not assert physics beyond its implemented renderer and diagnostics.
