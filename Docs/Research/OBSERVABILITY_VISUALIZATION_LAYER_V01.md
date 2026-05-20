# Observability Visualization Layer — v0.1 Architecture Proposal

**Status:** Design proposal  
**Scope:** Visual design architecture — how transport state becomes legible to operators
and educational audiences. Complements the measurement protocol in
`xprimeray_observability_language.md` and the overlay orchestration in
`observatory_modes_v0_1_alpha.md`.  
**Constraint:** Observability design only. No new rendering decisions, no transport
changes, no speculative physics framing.

---

## Design Premise

Transport state is invisible. Rays are not visible objects. The field has no surface.
Tile completion is a bitmask. Curvature is a derivative.

The visualization layer exists to make these invisible states legible — not to
dramatize them, and not to imply more than the instrumentation measures.

The governing question for every design decision:

> **Does this help the observer understand what the renderer did?**

If the answer is "no" or "I am not sure," the element does not belong.

---

## Inspirational Reference Frame

Four sources shape the design posture of this layer. Each contributes a specific
discipline.

**3Blue1Brown — geometric clarity.**  
Mathematical concepts revealed through deliberate spatial animation. Every motion
answers one question. The camera does not move for spectacle; it moves because the
answer requires a different vantage. Color is semantic: it encodes identity, not mood.
The audience learns to read the visual language before the content gets complex.

**PBS Space Time — accessible depth.**  
Rigorous concepts explained to a technically literate but non-specialist audience.
The narration never oversimplifies the actual constraint. Diagrams are purpose-built
for the claim being made, not borrowed from adjacent fields. Uncertainty is stated
explicitly — "we don't yet know" is a valid on-screen conclusion.

**Scientific instrument panels — information density with legibility.**  
Mission control, clinical monitoring, particle accelerator readouts: dense numeric
displays that reveal rather than overwhelm because every element has a fixed semantic
position. The operator knows where to look for each type of information before reading
the value. Urgency is signaled spatially, not through animation.

**Finite-element visualization — spatial field representation.**  
FEM color maps, stress contours, heat flow fields: the underlying continuous quantity
is approximated by a discrete mesh, and the visualization shows both the approximation
and the quality of that approximation. Regions of high gradient are legible. Regions
of poor mesh quality are visible. The gap between model and resolution is shown, not
hidden.

The combination produces a design posture:

```text
Make the invisible state legible. Show the approximation alongside the approximation quality.
Signal change through meaning, not spectacle. Let the observer build a mental model
before introducing numerical interpretation.
```

---

## Layer 1 — Field Presence Visualization

**What it shows:** Where the GRIN field is spatially active.

The field exists inside a radial boundary (ROuter). Beyond that boundary, transport
is straight. Inside, the gradient causes bending. This transition is not visible to
the ray; it is implicit in the physics. The visualization must make it explicit to
the observer.

**Design:**

- A soft radial halo centered on the field origin, fading from a low-opacity warm tint
  (field active) to transparent at the ROuter boundary. Not a hard edge — the field
  taper should look like concentration, not a wall.
- A thin boundary ring at exactly ROuter: one or two pixels, medium opacity, clearly
  not geometry. This distinguishes "field boundary" from "physical surface."
- No animation on the field presence layer during normal playback. The field is static
  unless Amp changes. If Amp is zero, the halo is absent. If Amp is live-tuned, the
  halo brightens or fades proportionally.
- Color: a desaturated warm amber, distinct from the cold blue used for transport
  vectors and the green used for geometry reference. Opacity range 0.08–0.25 — visible
  but not dominant.

**What it does not show:** Ray bending paths (those belong to Layer 3), field
equation parameters (those belong to numeric readouts), or physics causal claims.

---

## Layer 2 — Straight vs Curved Dual-Reality Presentation

**What it shows:** The departure between what a straight ray would do and what the
field-curved ray actually does, for the same starting condition.

The informational content is the *gap* between the two trajectories — not either path
alone. If the paths are identical (field off, or point outside ROuter), the gap is
zero and the overlay communicates "no effect here."

**Design — ghost-path mode:**

The actual ray path renders as a solid colored line (existing RayBeamRenderer behavior).
The straight-line counterfactual renders as a dashed or dotted version of the same
line in a faded variant of the same color — same direction from the camera, same
spatial endpoint projected on the film, but traveling in a straight line from origin
to that endpoint. The departure between solid and dashed encodes the bending.

**Design — split-frame mode (educational contexts):**

Two half-frames, left/right or top/bottom, separated by a thin neutral divider (1–2
pixels, gray). Left frame: straight transport. Right frame: curved transport. Camera
position, geometry, and film exposure are identical. Only the transport model differs.
The divider is stable; it does not slide or animate unless the user is interactively
morphing between modes (which requires an explicit interaction, not idle motion).

For the hermetic chamber educational context, split-frame is the canonical presentation.
The sealed geometry is identical in both halves; the only observable difference is
where rays terminate, and whether the field reorganizes the color pattern on the walls.

**What it does not show:** Which frame is "correct" (both are instrumentation outputs),
or any causal claim about physical space.

---

## Layer 3 — Scheduler Traversal Visualization

**What it shows:** Which regions the renderer is currently working on, and in what order.

This is about the renderer's computational behavior, not the transport physics.
The traversal pattern is interesting because it reveals when information becomes
available — in tile mode, isolated patches light up across the frame simultaneously;
in row mode, a band sweeps downward linearly.

**Design:**

A low-opacity overlay on the film plane showing per-tile completion state using a
three-value scale:

| State | Color | Opacity |
|---|---|---|
| Not yet visited this pass | neutral gray | 0.10 |
| Pass 1 complete / pass 2 pending | cool blue | 0.20 |
| Fully complete (pass 1 + pass 2) | transparent (film shows through) | 0.00 |

As tiles complete, the overlay fades to transparent, revealing the underlying film
pixels. The film surface is the reward for completion; the overlay is the remaining
work.

The transition from "pending" to "transparent" should be an immediate cut, not a fade.
Fading would imply a physical process. The tile either completed or it did not.

**Active tile marker:** A thin bright border (one pixel, white at 0.6 opacity) around
whichever tile band the renderer is currently visiting. This gives operators a
real-time position indicator without requiring a numeric readout.

**What it does not show:** Why the scheduler chose that tile (that belongs to the Oracle
layer), or any prediction of which tile comes next.

---

## Layer 4 — Tile Completion Visualization

**What it shows:** The state of `traversalRowsCompleted` and the per-tile bitmask across
the entire frame, at any point in the render.

This is the global completion view — what percentage of the film surface has been
through both rendering passes, and whether the distribution is spatially uniform.

**Design:**

A compact progress bar anchored to the top edge of the film view, showing
`traversalRowsCompleted / filmHeight` as a fraction. Not a percentage; not a countdown.
A fraction, because the raw numbers are what the hermetic gate checks.

Below the progress bar: a miniaturized tile completion grid, 1:N scaled from film
resolution (e.g., every 4×4 film pixels → 1 grid pixel), colored by completion state
(matching Layer 3 colors). This grid fits in a corner without overlapping the primary
film view.

The minimap is the expert-mode artifact. In beginner mode, only the fraction bar
is shown.

**Connection to hermetic gate:** The hermetic gate checks `traversalRowsCompleted >=
minRows`. This bar makes that check visually explicit during live capture. When the
bar is full, the capture fires. If the bar stalls (as it did before the
EnsureForwardProgress fix), the stall is immediately legible — a static bar despite
active rendering — without requiring log inspection.

---

## Layer 5 — Ownership Basin Visualization

**What it shows:** Which scene nodes each film pixel "belongs to" — which collider,
background group, or source group was hit by that pixel's ray.

This is the spatial topology of transport decisions. In a straight scene, ownership
basins roughly follow geometric projection. In a GRIN scene, they reorganize: the field
bends rays toward or away from regions, warping the basin boundaries in a way that
cannot be predicted from geometry alone.

**Design:**

Each hit category is assigned a distinct hue from the project's color semantic table:

| Category | Color |
|---|---|
| Source hit (fixture_source group) | amber / warm gold |
| Background hit (fixture_background group) | cool blue |
| Miss / step exhaustion | deep violet |
| Unclassified | neutral gray |

The film pixel is tinted by its ownership category at low opacity (0.3), layered over
the transport-shaded color. The shading remains visible through the tint; the basin
color is an annotation, not a replacement.

**Boundary emphasis:** At pixels where ownership changes between adjacent cells
(seam pixels), increase opacity to 0.7. The basin boundaries are the diagnostic
signal — where ownership transitions is where the field's organizational effect is
measurable. Seam pixels form the visible fingerprint of transport topology.

**GRIN vs straight comparison:** In split-frame mode, the ownership basins in each
half show the reorganization directly. Operators do not need to compute the difference;
they see it as a spatial rearrangement of colored regions.

---

## Layer 6 — Curvature-Intensity Visualization

**What it shows:** Where along a ray path the bending is strongest, and how that
intensity varies across the film plane.

The bending magnitude at each ray segment is a scalar derived from the field gradient
at that point. High bending occurs near the field center when Amp is high. Low bending
occurs near the ROuter boundary. Zero bending occurs outside ROuter.

**Design — path coloring:**

Along each ray polyline (already rendered by RayBeamRenderer), modulate the color
from the segment's neutral transport color toward a warm accent (amber to red) based on
local bending magnitude, normalized against the maximum observed bending this frame.
The color is normalized per-frame to avoid frames with low-curvature scenes appearing
monochromatic.

Normalization is frame-local. This is an intensity annotation, not a physical
measurement. The legend must include "normalized to frame maximum."

**Design — field heatmap:**

A full-film heatmap showing the maximum bending magnitude experienced along each
pixel's ray path, rendered as a cool-to-warm color gradient (blue → green → amber →
red). Useful as a standalone diagnostic panel in measurement mode.

The heatmap is overlay-only — it does not affect the film pixels it is drawn over.
Toggling it off must restore the exact film appearance.

**What it does not show:** The physical magnitude of bending in any unit system. The
normalization is visual. It shows relative intensity within the frame, not absolute
curvature values.

---

## Layer 7 — Temporal Coherence and Motion Language

**What it shows:** How state changes over time — which parts of the visualization
change together, at what rate, and what kind of change it is.

This is not a discrete visualization overlay. It is a set of rules governing how all
other overlays behave over time.

**Motion rules:**

*Completion reveals the film.* As tiles complete, the overlay fades to transparent,
revealing underlying pixels. This is the primary motion in the visualization — the
physical surface emerging from the computational process. It is spatially local to the
completing tile.

*Mode transitions are cuts, not fades.* When the operator switches between observatory
modes (Observer → Ownership, etc.), the overlay state changes immediately. Fading
between modes implies a physical interpolation between states that does not exist.
The renderer is not computing a blend; neither should the visualization.

*Field state changes are proportional.* If Amp is tuned interactively, the field
presence halo and the path curvature coloring update proportionally. No easing.
The mapping from physics parameter to visual intensity is monotonic and direct.

*Stalls are shown as stillness.* If the traversal bar stops advancing, it simply stops.
No throbber, no loading indicator, no animated "working" state. A still completion bar
is the diagnostic signal. Adding idle animation would obscure the signal.

*No idle animation in measurement mode.* The visualization is at rest when the renderer
is at rest. Motion means something changed. In diagnostic contexts, unexpected motion
is a bug signal, not a feature.

---

## Layer 8 — Minimal Cinematic Motion Principles

The film and scientific instrument traditions agree on one point: motion must be
earned. This section codifies that agreement for this project.

**Principles:**

1. **Motion answers a question.** Before introducing any animation, specify which
   question the observer can answer by watching the motion that they could not answer
   from a static image.

2. **Spatial motion encodes spatial meaning.** If the overlay moves to the left,
   something in the transport moved to the left (or the camera did, and the overlay
   should follow). Motion direction is not arbitrary.

3. **Temporal extent matches cognitive load.** A complex overlay transition that
   introduces a new concept may take 0.5–1 second. A simple state flip (on/off, 
   completion reveal) takes one frame. Do not slow down what is already understood.

4. **Default state is static.** The visualization does not require motion to be
   "alive." Static overlays that accurately show a stable state are the correct
   default. Motion is the exception, triggered by state change.

5. **No physics simulation for decoration.** Particle effects, flowing gradients, and
   pulsing halos applied for aesthetic reasons are rejected. If the field is not
   radiating energy in a physical sense (it is not), the visualization should not
   imply that it is.

6. **Ambient motion budget is zero in measurement mode.** In presentation mode,
   a single subtle ambient element may be permitted (e.g., a slow boundary-ring
   shimmer at 0.3 opacity, 4-second period, amplitude 5% opacity) to signal a live
   system versus a screenshot. That element must be immediately suppressible and
   absent in measurement mode.

---

## Layer 9 — Non-Intrusive Overlay Principles

**The film is the primary artifact.** All overlays are annotations on the film.
They must never occlude the film to the point that the film cannot be evaluated.

**Spatial conventions:**

- Status bars and progress readouts: top edge, full width, height ≤ 20px.
- Minimap / tile completion grid: bottom-right corner, ≤ 15% of viewport width.
- Mode label and transport indicators: top-left, small typography, 12–14px equivalent.
- Numeric readouts (expert mode): right column, fixed-width monospace, ≤ 200px wide.
- Never center-blocking: no overlay element occupies the center 60% of the frame
  height and 40% of the frame width simultaneously, except in full-panel diagnostic
  mode where the film is not the primary artifact.

**Opacity conventions:**

- Active overlays: 0.15–0.35 (tints) or 0.6–0.8 (lines and glyphs) — visible but
  not dominant.
- Completion state (fully done): 0.00 — the film shows through.
- Seam boundaries: 0.7 — the diagnostic signal deserves emphasis.
- Labels: white text on dark backing at 0.85 — readable against any background.

**Toggle behavior:**

Every overlay element is individually suppressible via existing F-key cockpit or
mode preset. If an element cannot be turned off, it should not be added.

The overlay state on disk (mode preset) is the default; the operator's live
adjustments override it for the session without persisting. Switching modes
resets to the preset — this is instrument behavior, expected and documented.

---

## Layer 10 — Presentation vs Measurement Separation

These are two distinct operational contexts with incompatible requirements.

| Dimension | Measurement Mode | Presentation Mode |
|---|---|---|
| Primary audience | Operator, engineer | Newcomer, student, collaborator |
| Information density | Maximum — all available readouts | Minimum — one idea at a time |
| Color semantics | Strict canonical palette | Simplified: two or three colors max |
| Motion | None (static diagnostic default) | Minimal reveals only |
| Labels | All, with values | Conceptual labels only, no raw values |
| Grid / reference | Active | Off |
| Hermetic gate readout | Fraction bar visible | Hidden |
| Mode transition | Operator-triggered | Scripted or mode-managed |
| Ambient animation | Zero | Permitted (one element, suppressible) |
| Numeric overlays | Full | Hidden |

The mode switch is explicit. There is no automatic detection of audience type.
The operator selects the mode. In scripted demos (the `GrinObserveDemoHud` path),
the demo scene sets the starting mode; the operator can override it.

The two modes must never blend. An operator who switches from Presentation to
Measurement sees all suppressed elements appear immediately. An audience watching
Presentation never sees raw numeric readouts unless the operator deliberately opens
them.

---

## Layer 11 — Hermetic Chamber Educational Mode

The hermetic observatory — a sealed 12-unit box with no escape geometry — is the
canonical educational tool for this system. It has three properties that make it
uniquely suitable.

**Property 1: No ambiguity about outcome.** Every ray must terminate inside the box.
`missHits == 0` is not a calibration success; it is a geometric certainty for a
sealed box with adequate step budget. This removes the "did the ray do something
wrong?" question, leaving only "where did it land, and why?"

**Property 2: Direct observability.** The six walls are visually distinct (distinct
colors in the fixture). The observer can see which wall was hit and how the pattern
changes when the field is active. No external reference is needed.

**Property 3: Comparative clarity.** The straight and GRIN fixtures are identical in
geometry, differing only in field presence. Switching between them (or using split-frame
mode) directly demonstrates field effect without confounding variables.

**Educational mode design:**

Step 1 — Sealed geometry: Show the hermetic box in Observer mode, straight transport.
Label the six walls. Let the pattern stabilize. The observer learns: "every ray lands
on a wall."

Step 2 — Field introduction: Activate the field presence halo (Layer 1) with no
transport change (Amp = 0 still, field present but inactive). The halo shows where the
field is spatially. No transport effect yet — the transport output is identical to Step 1.

Step 3 — Field activation: Increase Amp to the test value (0.6). The transport output
reorganizes. The wall color pattern shifts. The ownership basins (Layer 5) show which
regions changed. The curvature intensity (Layer 6) shows where the bending was
strongest. `missHits` remains 0 throughout — the box is still sealed.

Step 4 — Dual reality: Split-frame mode with straight (left) and GRIN (right) side by
side. The observer can see the ownership basin reorganization directly. Seam boundaries
highlight where the field's effect is spatially concentrated.

Each step introduces one new idea. No step requires numerical literacy to follow.
Numerical interpretation is available in Measurement mode for the operator who wants it.

---

## Layer 12 — Beginner vs Expert Observatory Modes

The observatory mode system (`observatory_modes_v0_1_alpha.md`) defines overlay presets
answering specific questions. This layer defines the information-density axis that
cuts across all modes.

**Beginner axis — qualitative understanding:**

- Color encodes category: transport hit type, region ownership, field presence.
- Labels name concepts, not values.
- One active overlay at a time. Each mode activates one primary overlay.
- Completion is shown as a visual reveal (film emerges), not a fraction.
- No numeric readouts, no coordinate systems, no log-scale axes.
- Mode sequence is linear: Observer → Geometry → Ownership → Field → Curvature.
  Each step adds one idea.

**Expert axis — quantitative interpretation:**

- All overlay layers available simultaneously (within the operator's chosen mode).
- Canonical color semantics from `xprimeray_observability_language.md` apply strictly.
- Fraction bars, numeric readouts, hermetic gate status, tile bitmask minimap.
- Seam pixel count, ownership region count, traversalRowsCompleted value, frame number.
- Non-monotonic access: operator jumps directly to any mode, not a linear sequence.
- `[TileScheduler]`, `[GrinBasicVisual]` log line parsing mapped to overlay state.

**Transition between axes:**

The Beginner / Expert distinction is not a mode — it is an overlay density preset that
compounds with the mode presets. A mode in Expert density shows all available
diagnostics for that mode. A mode in Beginner density shows only the primary overlay.

The density setting persists across mode switches within a session. The operator
who sets Expert density and switches from Ownership to Risk mode sees all Risk
diagnostics without re-selecting Expert density.

The density setting does not persist across sessions. Default is Beginner.

---

## Implementation Sequencing

The architecture proposes visualization layers, not a single deliverable. Implementation
proceeds in phases, each of which must be completable and testable independently.

| Phase | Layers | Deliverable | Prerequisite |
|---|---|---|---|
| v0.1-a | 3, 4 | Traversal and completion overlay using existing `FilmOverlay2D` hooks | Phase 3.0 tile gate |
| v0.1-b | 1, 2 | Field presence halo, split-frame dual-reality mode | v0.1-a stable |
| v0.1-c | 5 | Ownership basin tinting using existing hit category data | v0.1-b stable |
| v0.2-a | 6 | Curvature-intensity path coloring | Per-segment curvature data available |
| v0.2-b | 11 | Hermetic chamber educational mode sequence | All prior layers |
| v0.2-c | 12 | Beginner/Expert density axis | Observatory mode controller wired |

Layers 7, 8, 9, 10 are not implementation phases — they are constraints applied
to all phases above.

---

## What This Document Is Not

- Not a physics claim. The visualization shows renderer outputs. It does not claim
  that GRIN transport reproduces any physical phenomenon.
- Not a scheduling change. No layer here modifies traversal order, step budget,
  or rendering decisions.
- Not a new rendering system. Every layer here draws on data the renderer already
  produces: hit categories, traversal state, row completion count, ray path polylines.
- Not final. Every design decision above is open to revision when implementation
  reveals constraints the design did not anticipate.

The measure of success for this layer is simple: does an observer who has never seen
this system before understand, within two minutes of observation, what the renderer
did and where the field had an effect? If yes, the layer works. If no, the design
needs revision.

---

*Related documents:*  
*`xprimeray_observability_language.md` — measurement protocol and color semantics*  
*`observatory_modes_v0_1_alpha.md` — overlay orchestration and mode presets*  
*`overlay_inventory_and_runtime_capability.md` — runtime overlay audit*  
*`ReleaseToProduction/PHASE3_ROADMAP.md` — implementation phase sequencing*
