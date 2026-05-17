# Observatory Mode Architecture — v0.1-alpha

**Status:** Design proposal and initial implementation  
**Phase:** v0.1-alpha foundation  
**Scope:** Overlay orchestration only. No new rendering systems.  
**Constraint:** v0.0-pre workflows must remain fully stable.

---

## Background

xPRIMEray v0.0-pre established:

- a stable F1–F12 cockpit with individual overlay toggles
- a play-mode verification harness
- a fallback MP4 generation pipeline
- a manifest with section timing and checksums
- layered transport overlays across FilmOverlay2D and RayBeamRenderer

The next step is not adding more diagnostics. It is **orchestrating existing diagnostics
into coherent, named observatory modes** — overlay presets that answer a specific question
about transport behavior without requiring the viewer to know which F-keys to press.

---

## Design Philosophy

**Instruments, not effects.**  
Each mode surfaces a specific layer of transport evidence. None of them imply causality
beyond what the instrumentation measures.

**Calm switching.**  
Mode transitions change overlay state. They do not trigger animations, resets, or
scene changes. The underlying transport render is unaffected.

**Additive authority.**  
Individual F-key toggles retain priority. A mode sets a starting preset; the cockpit
operator can modify from there. Switching modes resets to the preset, which is the
expected instrument behavior.

**Optional presence.**  
ObservatoryModeController is an opt-in node. Scenes without it are identical to v0.0-pre.

---

## Mode Definitions

### Mode 1 — Observer  `Ctrl+1`

**Question answered:** What changed visually?

**Purpose:** The baseline observer view. Show only the observable signal — where rays
land and what the film surface captures — with minimal geometry annotation.

**Overlay preset:**

| Control | State |
| --- | --- |
| FilmOverlay2D.DrawRays | on |
| FilmOverlay2D.DrawHitNormals | off |
| FilmOverlay2D.DrawFilmGradientNormals | off |
| FilmOverlay2D.ShowComparisonGrid | off |
| FilmOverlay2D.ShowComparisonCrosshair | on (minimal reference) |
| RayBeamRenderer.DebugMode | RaysOnly |
| RayBeamRenderer.DebugDrawOnlyHits | on |

**Educational purpose:**  
Starting point for any comparison. The viewer sees the raw observable output — the
transport footprint — before any geometry annotation is added. Straight vs curved
comparison is most legible here.

**Future panels (v0.1-alpha+):**
- Minimap / overview inset
- Exposure histogram for film pixel distribution
- Frame-stable transport label overlay

---

### Mode 2 — Geometry  `Ctrl+2`

**Question answered:** How is geometry interacting with transport?

**Purpose:** Surface the boundary geometry, hit structure, and spatial reference grid
alongside the ray overlay. Normals are visible; comparison grid is active.

**Overlay preset:**

| Control | State |
| --- | --- |
| FilmOverlay2D.DrawRays | on |
| FilmOverlay2D.DrawHitNormals | on |
| FilmOverlay2D.DrawFilmGradientNormals | off |
| FilmOverlay2D.ShowComparisonGrid | on |
| FilmOverlay2D.ShowComparisonCrosshair | on |
| RayBeamRenderer.DebugMode | RaysAndNormals |
| RayBeamRenderer.DebugDrawOnlyHits | off |

**Educational purpose:**  
Shows the mechanical interaction between ray paths and scene geometry. Normals at
each hit point reveal the local boundary orientation. Grid provides a coordinate
anchor for identifying spatial displacement between scenes.

**Future panels (v0.1-alpha+):**
- WireframeReferenceOverlay (field glyphs, boundary layer, portal arcs)
- Hit projection structure overlay
- BoundaryLayerVolume glyph layer

---

### Mode 3 — Ownership  `Ctrl+3`

**Question answered:** Which regions changed transport ownership?

**Purpose:** Show the hit-filtered subset of rays — only those that successfully
resolved a hit — surfacing the ownership distribution across the film plane.

**Overlay preset:**

| Control | State |
| --- | --- |
| FilmOverlay2D.DrawRays | on |
| FilmOverlay2D.DrawHitNormals | on |
| FilmOverlay2D.DrawFilmGradientNormals | off |
| FilmOverlay2D.ShowComparisonGrid | on |
| FilmOverlay2D.ShowComparisonCrosshair | on |
| RayBeamRenderer.DebugMode | RaysOnly |
| RayBeamRenderer.DebugDrawOnlyHits | on |

**Educational purpose:**  
By filtering to hit-confirmed rays, the viewer can identify which regions of the film
plane are actively receiving transport — and which are not. This is the foundation for
seam behavior and split/merge analysis in future passes.

**Future panels (v0.1-alpha+):**
- Ownership graph visualization (per-region transport path lineage)
- Seam boundary overlay
- Transport split/merge map
- CameraSpaceCollisionOverlay in HitConfirmedOnly filter mode

---

### Mode 4 — Risk / Continuity  `Ctrl+4`

**Question answered:** Where does transport become unstable or ambiguous?

**Purpose:** Activate film gradient normals alongside the full overlay stack. Film
gradient normals encode local continuity of the film surface response — discontinuities
are visible as normal direction anomalies.

**Overlay preset:**

| Control | State |
| --- | --- |
| FilmOverlay2D.DrawRays | on |
| FilmOverlay2D.DrawHitNormals | on |
| FilmOverlay2D.DrawFilmGradientNormals | on |
| FilmOverlay2D.ShowComparisonGrid | on |
| FilmOverlay2D.ShowComparisonCrosshair | on |
| RayBeamRenderer.DebugMode | RaysAndNormals |
| RayBeamRenderer.DebugDrawOnlyHits | off |

**Educational purpose:**  
The film gradient normal layer surfaces regions where the transport response is
non-smooth — potential seams, transition zones, or unresolved islands. This mode
is not a claim about instability; it surfaces observable measurement anomalies for
further investigation.

**Future panels (v0.1-alpha+):**
- Unresolved island overlay (regions with zero hit return)
- Epsilon risk color map (coverage gaps near oracle threshold)
- Continuity vector field visualization
- Topology instability markers

---

### Mode 5 — Oracle / Microscopy  `Ctrl+5`

**Question answered:** Did transport stabilize under refinement?

**Purpose:** Maximum diagnostic density. All available overlays are active. This mode
is intended for post-session analysis, not real-time recording.

**Overlay preset:**

| Control | State |
| --- | --- |
| FilmOverlay2D.DrawRays | on |
| FilmOverlay2D.DrawHitNormals | on |
| FilmOverlay2D.DrawFilmGradientNormals | on |
| FilmOverlay2D.ShowComparisonGrid | on |
| FilmOverlay2D.ShowComparisonCrosshair | on |
| RayBeamRenderer.DebugMode | RaysAndNormals |
| RayBeamRenderer.DebugDrawOnlyHits | off |

**Educational purpose:**  
Used when inspecting specific transport behaviors at high resolution. Not suitable
for recording — too much visual information for a first-pass viewer. Intended for
investigators comparing specific rays, hit patterns, or convergence properties.

**Future panels (v0.1-alpha+):**
- GrinFilmCamera.DebugRenderHealthRollingOverlay (render health metrics)
- Oracle replay overlay (convergence trace per pixel)
- Precision closure map (per-ray closure cost)
- Epsilon/cost diagrams (convergence budget visualization)

---

### Mode 6 — Presentation  `Ctrl+6`

**Question answered:** Can a technical viewer understand the phenomenon without repo knowledge?

**Purpose:** Educational/public observatory capsule. Minimal overlays, readable labels,
no dense debug data. Safe for recording, streaming, or public demos.

**Overlay preset:**

| Control | State |
| --- | --- |
| FilmOverlay2D.DrawRays | on |
| FilmOverlay2D.DrawHitNormals | off |
| FilmOverlay2D.DrawFilmGradientNormals | off |
| FilmOverlay2D.ShowComparisonGrid | off |
| FilmOverlay2D.ShowComparisonCrosshair | off |
| RayBeamRenderer.DebugMode | Off |
| RayBeamRenderer.DebugDrawOnlyHits | off |

**Note:** Combine with F11 (Clean Presentation) in GrinObserveDemoHud to suppress
the cockpit HUD for a fully clean frame.

**Educational purpose:**  
Shows the transport output in its cleanest form. The instrument is still running;
only the diagnostic annotation is suppressed. A technical viewer can see the
observable difference between straight and curved transport without needing to
interpret normals, grids, or debug billboards.

**Future panels (v0.1-alpha+):**
- Simplified mode label overlay (large, readable)
- Observable difference caption bar
- Restrained instrument language caption system

---

## Keyboard Map Summary

| Shortcut | Mode | Primary question |
| --- | --- | --- |
| Ctrl+1 | Observer | What changed visually? |
| Ctrl+2 | Geometry | How is geometry interacting with transport? |
| Ctrl+3 | Ownership | Which regions changed ownership? |
| Ctrl+4 | Risk | Where does transport become unstable? |
| Ctrl+5 | Oracle | Did transport stabilize under refinement? |
| Ctrl+6 | Presentation | Can a non-expert viewer understand this? |

F1–F12 remain active at all times. Mode switching sets a preset; individual F-keys
modify from there. Switching mode again resets to the preset.

---

## Architecture

### ObservatoryModeController

**File:** `ObservatoryModeController.cs`  
**Node type:** `Node` (attach as sibling to PlayModeVerifier)  
**Required nodes:** FilmOverlay2D (required), RayBeamRenderer (required), GrinObserveDemoHud (optional)

**Interface:**

```csharp
public ObservatoryMode CurrentMode { get; }
public void SetMode(ObservatoryMode mode);
// _UnhandledInput responds to Ctrl+1 through Ctrl+6
```

**Node path exports** (adjust in Inspector per scene layout):

```
OverlayPath  = "../CanvasLayer/FilmOverlay2D"
HudPath      = "../CanvasLayer/DemoHud"
RendererPath = "../FixtureGrinBasicVisual/RayBeamRenderer"
```

### GrinObserveDemoHud integration

`GrinObserveDemoHud` exposes:

```csharp
public string ObservatoryModeName { get; set; }
```

When set, the HUD appends the mode name to the mode label line:
`"Curved GRIN Transport  ·  Geometry Mode"`

The property is empty by default — no behavioral change in scenes without the controller.

---

## Complexity and Risk Notes

**What this is:**  
A thin orchestration layer. ObservatoryModeController is ~200 lines. It sets properties
on existing nodes. It does not own any rendering.

**What this is not:**  
A new rendering system. A plugin. A scene manager. A state machine for the transport
simulator. It cannot affect physics computation.

**Risk: mode state vs F-key state divergence.**  
If a user presses F3 to toggle rays after entering a mode, the mode label in the HUD
will still show the last mode set, but the overlay state no longer matches the preset.
This is intentional — the operator controls the instrument; modes are starting presets,
not locked states. If this becomes confusing, a future pass can add a "custom" sentinel
mode that fires when F-key state diverges from the last preset.

**Risk: node paths.**  
Node paths in exports are defaults matching the GRIN Basic Visual Off-Axis Observe
scene layout. Scenes with different hierarchies must adjust these in the Inspector.
The controller degrades gracefully — missing nodes are logged and skipped.

**Risk: Ctrl+number conflicts.**  
Ctrl+1–6 may conflict with OS or IDE shortcuts in some environments. The `EnableModeHotkeys`
export can disable all Ctrl+number handling. Individual mode calls via `SetMode()` remain
available regardless.

---

## v0.0-pre compatibility guarantee

- No existing scene files are modified
- No existing scripts are modified except a backward-compatible property addition to GrinObserveDemoHud
- play-mode verifier passes are unaffected
- MP4 generation pipeline is unaffected
- F1–F12 cockpit behavior is unchanged
- ObservatoryModeController is a new optional file; scenes that do not include it see no change

---

## Design inspirations

- Aircraft cockpit ergonomics: modes as named instrument configurations, not raw switches
- Scientific microscopy: objective lens selection as a mode, not a manual aperture adjustment
- Observatory instrumentation: filter wheel selection as a semantic choice
- 3Blue1Brown / ScienceClic pacing: one question per frame, not all questions simultaneously
