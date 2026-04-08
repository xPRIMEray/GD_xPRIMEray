Dual Reality Research Mode Scaffold

Purpose

Create a lightweight, additive-only research/perceptual overlay that gives users a stable straight-ray reference while comparing xPRIMEray curved transport behavior in the same scene.

This mode should let a user:

See the baseline straight-ray / geometric reference view

See the current curved-ray perceptual result

Overlay field-derived diagnostics on top of the baseline reference

Norm the curved result against a straight-ray anchor

Inspect film heat maps and curvature intensity without changing the underlying scene setup


Core Experience Goal

A viewer should be able to say:

"This is the straight-ray world"

"This is the curved-ray world"

"This heat map shows where the field is doing the work"


The system should preserve the same:

scene

camera pose

seed / deterministic sampling conditions when possible

framing / viewport layout


Proposed Name

Suggested feature names:

DualRealityResearchMode

BaselinePerceptualOverlay

AcademicReferenceMode

StraightRayNormingHUD


Recommended working name: DualRealityResearchMode


---

Scope

In scope

Additive overlay/HUD mode

Straight-ray baseline preview generated from scene data or baseline transport

Curvature intensity overlay

Existing film heat map integration

Simple blending / toggle controls

Deterministic side-by-side or picture-in-picture compare mode


Out of scope for first pass

Major renderer architecture changes

New transport physics

Expensive dual full-resolution concurrent renders by default

Heavy UI framework work

Non-deterministic experimental compositing



---

Functional Requirements

1. Baseline straight-ray reference window

Provide a small overlay window showing a straight-ray reference view for the same camera and scene.

Possible implementations, in order of safety:

1. Reuse existing Straight transport branch to render a reduced-resolution inset


2. Reproject simple scene geometry / debug wireframe directly from scene data


3. Cache a baseline reference frame and reuse until camera/scene changes



2. Curved perceptual main view

Main viewport remains the current curved transport result.

3. Overlay modes

Support the following overlays on either main view or inset:

None

Wireframe reference

Film heat map

Curvature intensity heat map

Difference / norming view

Ghost baseline overlay


4. Norming / comparison controls

Support quick user controls to:

toggle baseline inset on/off

swap inset/main assignment

adjust overlay opacity

cycle overlay mode

freeze baseline reference

refresh baseline reference


5. Deterministic comparison behavior

When in research mode, comparison outputs should attempt to share:

identical camera pose

identical scene snapshot

deterministic seed or benchmark lock where practical



---

UX Layout Proposal

Option A: Picture-in-picture

Main: curved perceptual result

Top-right inset: straight-ray reference / wireframe baseline

Bottom-left mini panel: scalar diagnostics legend + numeric stats


Recommended for first implementation.

Option B: Split screen

Left: straight-ray baseline

Right: curved result


Useful later for demos.

Option C: Blend slider

Interactive blend between baseline and curved result


Best for later showcase mode.


---

Overlay Definitions

A. Straight-ray baseline overlay

A clean reference showing how the scene would project under straight transport.

Use cases:

anchor user intuition

reveal deviation caused by field transport

support educational demos


B. Wireframe overlay

Scene-derived geometric wireframe drawn in screen space for the baseline view.

Use cases:

emphasize geometric truth anchor

distinguish geometry from transport-induced perceptual distortion


C. Film heat map overlay

Reuse existing film/pixel activity heat map tooling.

Use cases:

show where the detector / film is accumulating energy or activity

correlate render behavior with scene structure


D. Curvature intensity overlay

A scalar field mapped to screen space showing local curved-ray deviation magnitude.

Possible metrics:

integrated turn angle along ray

cumulative curvature proxy

deviation from straight endpoint projection

adaptive substep count density

BLV / field interaction intensity count


Recommended first-pass scalar: cumulative turn angle per pixel

E. Difference / norming overlay

Compare baseline and curved outputs.

Possible first-pass metrics:

screen-space displacement magnitude

per-pixel luminance difference

per-pixel hit location delta


Recommended first-pass visual: screen-space displacement magnitude heat map


---

Suggested Architecture

1. Config surface

Add a small research-mode config group.

Example config names:

EnableDualRealityResearchMode

DualRealityInsetEnabled

DualRealityInsetScale

DualRealityOverlayMode

DualRealityOpacity

DualRealityFreezeBaseline

DualRealityRefreshBaseline

DualRealityShowWireframe

DualRealityShowFilmHeatmap

DualRealityShowCurvatureHeatmap

DualRealityNormMode


This can live near existing research/debug config wiring.

2. Baseline capture service

Add a small service/class responsible for producing and caching the straight-ray reference artifact.

Suggested class names:

BaselineReferenceRenderer

StraightRayReferenceCache

DualRealityOverlayComposer


Responsibilities:

decide when baseline needs refresh

render or derive baseline reference

store low-resolution texture/buffer

expose overlay-ready data to HUD/compositor


3. Screen-space overlay composer

A compositing layer that can draw:

inset texture

wireframe overlay

scalar heat maps

legends / stats


Suggested class names:

ResearchOverlayComposer

DualRealityHudRenderer


4. Scalar diagnostics pipeline

Provide a simple per-pixel or per-sample accumulation path for curvature intensity.

Suggested artifact names:

CurvatureHeatmapBuffer

RayDeviationAccumulator

ResearchDiagnosticFrame



---

Data Model Sketch

Diagnostic frame

A lightweight snapshot object could hold:

baseline texture handle / image

curved texture handle / image

film heat map buffer

curvature intensity buffer

displacement norm buffer

stats summary

camera hash / scene hash / seed


Suggested type: ResearchDiagnosticFrame

Stats summary

Useful quick values:

max curvature

mean curvature

p90 curvature

source hits

miss hits

BLV crossings

mean displacement from baseline

camera pose hash



---

Implementation Plan

Phase 1: Minimal scaffold

Goal: ship an additive skeleton with very low risk.

1. Add config flags


2. Add inset HUD region


3. Render/cache straight-ray low-res baseline using existing Straight transport


4. Display inset in main viewport


5. Add toggle keybindings



Definition of done: User can press a key and see a straight-ray reference inset for the current scene/camera.

Phase 2: Overlay integration

1. Reuse film heat map in inset or main overlay


2. Add opacity slider / cycle key


3. Add baseline freeze/refresh behavior


4. Add small legend text with current mode



Definition of done: User can cycle baseline, film heat map, and no-overlay modes.

Phase 3: Curvature intensity heat map

1. Define scalar metric


2. Accumulate per-pixel curvature proxy


3. Normalize to display range


4. Render as heat map overlay



Definition of done: User can visually inspect where curved transport deviates most strongly.

Phase 4: Norming / displacement view

1. Compute baseline vs curved displacement metric


2. Display screen-space norm heat map


3. Add numeric summary panel



Definition of done: User can compare baseline and curved views quantitatively and perceptually.


---

Recommended First-Pass Technical Choices

Baseline generation

Prefer using the existing Straight transport path at reduced resolution.

Why:

lowest conceptual risk

matches actual renderer baseline

avoids premature scene-projection shortcuts

keeps comparison honest


Curvature metric

Prefer cumulative turn angle.

Why:

easy to explain

tied directly to curved transport behavior

naturally scalar

likely already close to available adaptive stepping telemetry


Rendering budget

Use reduced-size inset and refresh only on:

camera movement stop

manual refresh

scene hash change

explicit research mode refresh cadence


This avoids a large performance penalty.


---

Keybinding Proposal

Example bindings:

F7: toggle Dual Reality Research Mode

F8: cycle overlay mode

F9: freeze/unfreeze baseline

F10: refresh baseline

[ / ]: decrease/increase overlay opacity



---

Demo Narrative Value

This feature supports several communication goals:

gives users a normal-world anchor

reveals that distortion comes from transport, not geometry edits

provides an academic/engineering legitimacy layer

creates a fun perceptual mode for videos and social demos


Suggested narrative line: "Here is the straight-ray expectation. Here is the curved-ray perception. Here is where the field does the work."


---

Suggested Repo Artifact Set

1. Design note

Create a short markdown spec in docs.

Suggested path:

Docs/DualRealityResearchMode.md


2. Config wiring

Add new config fields to the existing research/debug config surface.

3. Overlay implementation

Create additive classes for:

baseline capture/cache

HUD composition

diagnostic frame buffers


4. Fixture coverage

Add at least one fixture for deterministic screenshots.

Suggested fixture targets:

curved_minimal

curved_minimal_backdrop

wormhole prototype fixture later


5. Screenshot artifact convention

Suggested output folder:

output/dual_reality/


Naming examples:

curved_minimal_baseline_inset.png

curved_minimal_curvature_heatmap.png

curved_minimal_norm_overlay.png



---

Suggested Acceptance Criteria

Visual acceptance

Baseline inset appears in a stable screen location

Baseline inset matches same camera framing as main view

Overlay opacity control works

Film heat map and curvature overlay are distinguishable


Technical acceptance

Additive-only change set

No regression to baseline render path when feature disabled

Deterministic screenshot outputs for at least one fixture

Overlay refresh budget does not tank interactivity in normal mode


Communication acceptance

A user unfamiliar with the renderer can understand the difference between straight and curved transport within 10 seconds



---

Codex Prompt Seed

Implement an additive-only scaffold for a new DualRealityResearchMode in GD_xPRIMEray.

Goals:

1. Add config flags to enable a research HUD mode.


2. Render or cache a reduced-resolution straight-ray baseline inset for the same scene/camera using the existing Straight transport path.


3. Keep the main view as the current curved perceptual render.


4. Add overlay cycling support for: none, wireframe placeholder, film heat map, and curvature heat map placeholder.


5. Keep the feature dormant by default and avoid changing existing benchmark behavior unless explicitly enabled.


6. Add deterministic screenshot support for one existing fixture, ideally curved_minimal.


7. Document all new fields/classes with concise comments and add a markdown design note under Docs/DualRealityResearchMode.md.



Constraints:

additive-only

preserve current baseline behavior when disabled

low-resolution inset is acceptable for first pass

placeholder curvature heat map is acceptable if full metric wiring is not yet available

prefer cache/refresh-on-demand over continuous heavy re-rendering


Deliverables:

code changes

short design note

screenshot artifact path conventions

concise test/run instructions



---

Final Design Principle

Do not present this as extra debug clutter. Present it as a reference reality anchor that lets the user feel what curved transport is changing.