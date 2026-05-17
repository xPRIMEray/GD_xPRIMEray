# Overlay and Panel Inventory — Runtime Capability Audit

**Status:** v0.0-pre audit baseline  
**Date:** 2026-05-17  
**Scope:** All overlay, panel, and visualization systems in the project  

---

## Summary

| Category | Count |
| --- | --- |
| Live runtime overlays (toggleable during play) | 6 |
| Offline / generated artifacts | 3 |
| Research / experimental | 2 |
| Verification-only (no visual output) | 1 |
| Total overlay classes audited | 12 |

---

## Category A — Live Runtime Overlays

These overlays render during play and respond to property changes without restart.
All are safe to toggle via the ObservatoryModeController or F-key cockpit.

---

### A1. FilmOverlay2D

**File:** `FilmOverlay2D.cs`  
**Node type:** `TextureRect`  
**Educational-safe:** Yes  
**Observatory modes active:** All  

**Live toggles:**

| Property | Type | Default | Controls |
| --- | --- | --- | --- |
| DrawRays | bool | true | Film ray polylines (2D overlay) |
| DrawHitNormals | bool | true | Physics hit normals projected to film |
| DrawFilmGradientNormals | bool | false | Film surface gradient normals |
| ShowComparisonGrid | bool | false | Reference gridlines for scene comparison |
| ShowComparisonCrosshair | bool | false | Center crosshair / minimal reticle |

**Numeric controls (not mode-managed):**

| Property | Type | Default | Controls |
| --- | --- | --- | --- |
| RayWidth | float | 1.0 | Ray line width |
| WorldNormalWidth | float | 2.0 | Hit normal line width |
| WorldNormalLen | float | 0.25 | Hit normal length (world space) |
| FilmGradientScale | float | 6.0 | Film gradient normal scale |
| ComparisonGridDivisions | int | 4 | Grid cell count |
| ComparisonLineThickness | float | 1.0 | Grid line thickness |

**Integration:** Fed by `GrinFilmCamera` via `SetData()` and `SetFilmImage()`. Snapshot
available via `GetOverlayRenderSnapshot()` for verification.

**F-key wiring:** F3 (rays), F4 (normals), F5 (grid+crosshair), F6 (comparison), F12 (crosshair only)

---

### A2. RayBeamRenderer (debug overlay)

**File:** `RayBeamRenderer.cs`  
**Node type:** `Node3D` (3D billboard layer)  
**Educational-safe:** Geometry and Ownership modes; not Presentation  
**Observatory modes active:** Observer, Geometry, Ownership, Risk, Oracle  

**Live toggles:**

| Property | Type | Default | Controls |
| --- | --- | --- | --- |
| DebugMode | DebugDrawMode enum | RaysAndNormals | Off / RaysOnly / RaysAndNormals |
| DebugDrawOnlyHits | bool | false | Filter overlay to hit-confirmed rays only |
| UpdateEveryFrame | bool | true | Continuous rebuild vs frozen |
| DebugHotkeysEnabled | bool | true | Legacy F1/F2 hotkeys (disabled in observe scenes) |

**DebugDrawMode enum:**
- `Off` (0) — No 3D debug billboards
- `RaysOnly` (1) — Ray trail billboards only
- `RaysAndNormals` (2) — Ray trails and hit normal indicators

**Integration:** Provides `GetDebugRayBundle()` to GrinFilmCamera. Calls
`UpdateDebugOverlayFromFilm()` for film-driven overlay refresh.

**F-key wiring:** F1/F2 when `DebugHotkeysEnabled=true` (set to false in v0.0-pre observe scenes)

---

### A3. GrinObserveDemoHud

**File:** `GrinObserveDemoHud.cs`  
**Node type:** `Control`  
**Educational-safe:** Presentation mode (F11 clean mode suppresses HUD)  
**Observatory modes active:** All (HUD persists across modes)  

**Live state (managed by F-key cockpit):**

| Field | Controls |
| --- | --- |
| _showHelp | F1 — help overlay visibility |
| _comparisonView | F6 — comparison view state |
| _cameraFrozen | F7 — camera lock |
| _cleanPresentation | F11 — minimal presentation mode |

**Public API for mode integration:**

| Member | Purpose |
| --- | --- |
| `ObservatoryModeName { get; set; }` | Mode label appended to HUD mode line |
| `TryRunControlForVerification(key, …)` | Verification-safe key simulation |
| `GetOverlaySnapshotForVerification()` | Read overlay state for verification |
| `CaptureScreenshotPacket()` | F9 equivalent |
| `ExportDiagnosticsPacket()` | F10 equivalent |

**F-key wiring:** F1–F12 fully mapped (see `BuildKeymapMarkdownRows()`)

---

### A4. FieldSource3D (debug viz)

**File:** `FieldSource3D.cs`  
**Node type:** Node3D  
**Educational-safe:** No (diagnostics-only; not part of observer-facing overlay stack)  
**Observatory modes active:** Not currently managed by ObservatoryModeController  

**Live toggles:**

| Property | Type | Default | Controls |
| --- | --- | --- | --- |
| DebugVizOpacityMode | DebugVizOpacityModeKind | Wireframe | Field debug visualization style |

**DebugVizOpacityModeKind:** Wireframe / Heatmap / DensityMap / Solid / Transparent

**Note:** Field debug viz is useful in Geometry and Oracle modes but currently requires
manual Inspector toggle. Future pass: expose via mode preset if field viz is needed
in standard observatory modes.

---

### A5. WireframeReferenceOverlay

**File:** `Wormhole/WireframeReferenceOverlay.cs`  
**Node type:** `Control`  
**Educational-safe:** Geometry mode only (glyphs are semantic, not numeric)  
**Observatory modes active:** Geometry (future wiring — not in GRIN Basic Visual scenes)  
**Scene availability:** Wormhole scenes only  

**Live toggles:**

| Property | Controls |
| --- | --- |
| OverlayEnabled | Master enable |
| ShowFieldGlyphs | 3 orthogonal field circles |
| ShowBoundaryLayerGlyphs | Outer/inner/cross boundary circles |
| ShowWormholePortalGlyphs | Portal arc pairs + notch |
| ShowBackdropAndProbeHelpers | Projected plane boxes |
| ShowCenterAnchor | Center crosshair + rings |
| OverlayOpacity | Global opacity |

**Future integration:** Add to Geometry and Ownership mode presets when used in
scenes that include the wormhole research rig.

---

### A6. CameraSpaceCollisionOverlay

**File:** `Wormhole/CameraSpaceCollisionOverlay.cs`  
**Node type:** `Control`  
**Educational-safe:** Ownership mode only (hit-confirmed filter)  
**Observatory modes active:** Ownership (future wiring — wormhole scenes only)  
**Scene availability:** Wormhole scenes only  

**Live toggles:**

| Property | Controls |
| --- | --- |
| OverlayEnabled | Master enable |
| DisplayFilterMode | AllVisible / HitConfirmedOnly / PrimaryOnly / RemappedOnly / BackgroundOnly / HelpersOnly |
| BoundsMode | CenterOnly / Sphere / Aabb / LabelOnly |
| ShowLabels | Object labels |
| ShowLegend | Legend panel |
| ShowLeaderLines | Label leader lines |

**Future integration:** Ownership mode (Ctrl+3) preset should set
`DisplayFilterMode = HitConfirmedOnly` when this overlay is present.

---

## Category B — Offline / Generated Artifacts

These visualizations produce files as output but do not render live during play.

---

### B1. GRIN Observe PNG stills

**Generated by:** `GrinObserveDemoHud.CaptureScreenshotPacket()` (F9) and `GrinObservePlayModeVerifier`  
**Artifacts:**
- `output/v0.0-pre/straight_control_verify.png`
- `output/v0.0-pre/curved_grin_verify.png`
- `output/v0.0-pre/curved_grin_final_smoke.png`

**Educational-safe:** Yes — primary v0.0-pre evidence artifacts

---

### B2. Diagnostics JSON

**Generated by:** `GrinObserveDemoHud.ExportDiagnosticsPacket()` (F10)  
**Artifacts:**
- `output/v0.0-pre/grin_observe_*_diagnostics.json`

**Content:** Mode label, scene path, fixture label, overlay state, field state, cockpit state, timestamp

**Educational-safe:** Yes — structured instrument report

---

### B3. Play-mode verification report

**Generated by:** `GrinObservePlayModeVerifier` via `scripts/run_grin_observe_playmode_verify.sh`  
**Artifacts:**
- `output/v0.0-pre/GRIN_OBSERVE_PLAYMODE_VERIFY.md`
- `output/v0.0-pre/playmode_verify_straight_control.json`
- `output/v0.0-pre/playmode_verify_curved_grin.json`

**Educational-safe:** Yes — structured pass/fail verification report

---

## Category C — Research / Experimental

These visualizations exist in the codebase and are functional but are not part of
the standard observatory mode stack.

---

### C1. RayViz

**File:** `RayViz.cs`  
**Node type:** `MeshInstance3D`  
**Status:** Research prototype  
**Educational-safe:** No (analytic approximation, not instrument-accurate)  

**Purpose:** Analytic curved-ray prototype. Uses an implicit bend formula
`p(t) = o + d*t + bendDir * (beta * t^gamma) * BendScale` — not the actual transport solver.

**Live toggles:** `DrawEveryFrame` (bool)

**Note:** Do not use in observatory modes. The bend visualization is not derived from
the ray transport system; it is a research sketch.

---

### C2. WormholeResearchOverlay

**File:** `Wormhole/WormholeResearchOverlay.cs`  
**Node type:** `Node3D` (SubViewport-based)  
**Status:** Research / wormhole scenes only  
**Educational-safe:** No (requires wormhole scene graph)  

**Purpose:** SubViewport-based orthogonal research view (top-down or oblique) of
the wormhole scene geometry.

**Live toggles:** ViewMode (TopDown/Oblique), ShowFieldShells, ShowProbeGeometry, ShowBackdrops

**Note:** Not wired to observatory mode system. Future consideration for wormhole
instrument validation pass.

---

## Category D — Verification Only

### D1. GrinObservePlayModeVerifier

**File:** `GrinObservePlayModeVerifier.cs`  
**Node type:** `Node`  
**Status:** Automated play-mode verification harness  
**Educational-safe:** N/A (no visual output)  

**Purpose:** Verifies scene boot state, HUD presence, F1–F12 cockpit map, key conflicts,
and pixel coverage. Runs when `--grin-observe-playmode-verify=1` is passed.

**Not an overlay.** Verification harness only.

---

## Toggle Reference — ObservatoryModeController Managed

The table below shows which properties ObservatoryModeController sets per mode.  
`—` means the property is not changed by the mode preset.

| Property | Observer | Geometry | Ownership | Risk | Oracle | Presentation |
| --- | :---: | :---: | :---: | :---: | :---: | :---: |
| FilmOverlay2D.DrawRays | on | on | on | on | on | on |
| FilmOverlay2D.DrawHitNormals | off | on | on | on | on | off |
| FilmOverlay2D.DrawFilmGradientNormals | off | off | off | on | on | off |
| FilmOverlay2D.ShowComparisonGrid | off | on | on | on | on | off |
| FilmOverlay2D.ShowComparisonCrosshair | on | on | on | on | on | off |
| RayBeamRenderer.DebugMode | RaysOnly | RaysAndNormals | RaysOnly | RaysAndNormals | RaysAndNormals | Off |
| RayBeamRenderer.DebugDrawOnlyHits | on | off | on | off | off | off |

---

## Properties NOT Currently Mode-Managed

These exist in live-render-capable systems but are not set by the current mode presets.
Candidates for v0.1-alpha or v0.2 mode expansion:

| Property | System | Reason deferred |
| --- | --- | --- |
| GrinFilmCamera.DebugRenderHealthRollingOverlay | GrinFilmCamera | Oracle mode future addition |
| GrinFilmCamera.FixtureDebugHitColoringEnabled | GrinFilmCamera | Ownership mode future addition |
| FieldSource3D.DebugVizOpacityMode | FieldSource3D | Geometry mode future addition |
| WireframeReferenceOverlay.* | Wormhole scenes | Not present in GRIN observe scenes |
| CameraSpaceCollisionOverlay.* | Wormhole scenes | Not present in GRIN observe scenes |
| FilmOverlay2D.RayColor / HitRayColor | FilmOverlay2D | Style preset future addition |
| RayBeamRenderer.QuadSize / Alpha | RayBeamRenderer | Style preset future addition |

---

## Integration Notes

### Adding ObservatoryModeController to a scene

1. Add a `Node` child of the scene root
2. Attach `ObservatoryModeController.cs`
3. Adjust the three NodePath exports to match the scene hierarchy
4. Set `InitialMode` to the desired starting mode (or leave as `None` for passive start)
5. Optionally set `EnableModeHotkeys = false` for capture runs

### v0.0-pre compatibility

The controller is not present in any v0.0-pre scene. All v0.0-pre scenes are unmodified.
The only change to existing code is a backward-compatible `ObservatoryModeName` property
added to `GrinObserveDemoHud` — if never set, HUD behavior is identical to v0.0-pre.

### Verification impact

`GrinObservePlayModeVerifier` does not check for or interact with `ObservatoryModeController`.
Adding the controller to a scene does not affect play-mode verification pass/fail status.
