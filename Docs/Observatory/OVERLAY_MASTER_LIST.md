# xPRIMEray Observatory Overlay Master List

> **Lens: Observatory Overlay Designer**
> Audit date: 2026-05-14
> Scope: All existing and proposed visual overlay modes

---

## How to Read This List

**Status values:**
- `Active` — Implemented and producing output today
- `Active (editor only)` — Works in the Godot editor; not part of rendering output
- `Partial` — Data exists; visual render path missing
- `Proposed` — Specified; no implementation

**Release Priority:**
- `High` — Should be available at first public demo
- `Medium` — Valuable for research; not blocking first release
- `Low` — Future / aesthetic / speculative

---

## Overlay Master Table

| # | Overlay Name | Status | Repo Location | Purpose | Visual Behavior | Required Data Inputs | Priority | Inspiration Links |
|---|-------------|--------|---------------|---------|-----------------|---------------------|----------|-------------------|
| 1 | FilmOverlay2D | Active | `FilmOverlay2D.cs` | Ray polylines and hit normals on film plane | Colored line draws over film texture | Camera; `DrawRays`, `DrawHitNormals` toggles; color/width params | High | — |
| 2 | RayViz 3D | Active | `RayViz.cs` | 3D ray bend visualization | ImmediateMesh polylines; 9-point screen sample; responds to Beta/Gamma changes | Camera Beta/Gamma; `RayCount`, `StepsPerRay`, `MaxDistance` | High | — |
| 3 | Curved View Shader | Active | `curved_view.gdshader` | Screen-space curvature warp post-process | Radial power-law UV distortion applied to screen texture | `beta` (−2..2), `gamma` (0..8), `warp_scale` (0..1) uniforms | High | — |
| 4 | Wormhole Research Overlay | Active | `Wormhole/WormholeResearchOverlay.cs` | Portal research diagnostics | Research readout canvas with portal metrics | Portal geometry; crossing event data | Medium | Wormhole topology |
| 5 | Camera Space Collision Overlay | Active | `Wormhole/CameraSpaceCollisionOverlay.cs` (924 lines) | Collision boundary visualization in camera space | Camera-space hit geometry rendering | Collision detection results | Medium | — |
| 6 | Wireframe Reference Overlay | Active | `Wormhole/WireframeReferenceOverlay.cs` | Wireframe reference comparison against curved render | Wireframe line draws over scene | Scene geometry; reference camera | Medium | — |
| 7 | Overspace Portal Debug Overlay | Active | `Wormhole/Overspace/OverspacePortalDebugOverlay.cs` | Overspace portal link diagnostics | Debug text and geometry annotations | Portal link graph (`UniverseGraph`) | Low | Multi-universe topology |
| 8 | Field Source Node Gizmo | Active (editor only) | `addons/fieldsource_gizmo/FieldSourceNodeGizmo.cs` | Field source extent and density vector visualization in Godot editor | 3D gizmo handles; density zone rings; density vector arrows | `FieldSource3D` node properties | High | GRIN optics |
| 9 | Diagnostic Wireframe Overlay | Active | `tools/diagnostic_wireframe_overlay.py` | Multi-layer diagnostic composite | Combines: beauty + cartesian_wireframe + transport_ownership + risk_probe_markers + spacetime_transport_diagram + transport_continuity_vectors + budget_exhaustion_heatmap | Capture directory; ROI bboxes; telemetry CSV | High | — |
| 10 | Hit Normal Vector Overlay | Active | `tools/hit_normal_vector_overlay.py` | Physics hit normal glyph arrows | Arrow glyphs drawn over capture image at hit points | CSV hit packet with `normal_x`, `normal_y`, `normal_z` columns | Medium | — |
| 11 | Camera Cross-Section Minimap | Active | `tools/camera_cross_section_minimap_overlay.py` | Cartesian cross-section minimap inset | Small composite minimap inserted into corner of main image | Capture directory; panel size; ROI bounding box | Medium | — |
| 12 | Atomic Visual Observatory | Active | `scripts/run_atomic_orbital_visual_observatory.sh` + `tools/atomic_orbital_visual_diff.py` | Multi-cell atomic orbital visual comparison | Contact sheet: V0–V4 cells × normal_rgb/depth_heatmap shadings × optional contour overlays; generates `atomic_visual_observatory_report.md` | Scene; fixture name; cell list; shading mode; contour config | High | Atomic orbital / quantum eigenmodes |
| 13 | Wormhole Structure Observatory | Active | `scripts/run_wormhole_structure_observatory_quick.sh` + `tools/wormhole_structure_observatory_report.py` | Multi-panel wormhole transport structure visualization | Panel contact sheet: clean_curved, straight_vs_curved, depth_heatmap, step_budget_heatmap, domain_diagnostics, structure_minimap | Scene; quality preset (quick_review); panel timeout | High | Wormhole / topological transport |
| 14 | Step Budget Heatmap | Active | `scripts/run_domain_audit_visual.sh` + domain audit Python chain | Step budget exhaustion per pixel | False-color heatmap; red = exhausted budget | Render log telemetry (step count per pixel) | High | — |
| 15 | Domain Ownership Map | Active | Domain audit visual chain | Which domain owns each pixel | False-color heatmap; unique color per domain ID | `DomainTelemetry` CSV per-pixel domain ID | High | — |
| 16 | Boundary Confidence Map | Active | Domain audit visual chain | Boundary layer detection confidence | Greyscale confidence map; bright = high confidence | Domain resolver telemetry (boundary confidence score) | High | — |
| 17 | Normal Discontinuity Heatmap | Active | Domain audit visual chain | Surface normal discontinuities at resolver impact zones | False-color heatmap; highlights where resolver changes the hit normal | Normal telemetry before and after resolver | High | — |
| 18 | Selection Flip Heatmap | Active | Domain audit visual chain | Domain selection change events | False-color event map; highlights pixels where resolver chose differently from telemetry-off baseline | Domain resolver per-pixel selection | Medium | — |
| 19 | Depth Heatmap | Active | Atomic orbital observatory; `depth_heatmap` shading mode | Depth buffer as false-color gradient | False-color depth map; blue=near, red=far convention | Depth buffer from renderer | Medium | — |
| 20 | Density Contour Overlay | Active | Atomic orbital observatory; `ATOMIC_ORBITAL_VISUAL_CONTOURS=1`, `ATOMIC_ORBITAL_VISUAL_CONTOUR_MODE=density` | Contour isolines of atomic orbital density | Isoline rendering over beauty render | Analytic density field from `AtomicEigenmodeFieldSource3D` | Medium | Atomic orbital / quantum |
| 21 | Curvature Contour Overlay | Active | Atomic orbital observatory; `ATOMIC_ORBITAL_VISUAL_CONTOUR_MODE=curvature` | Contour isolines of field curvature strength | Isoline rendering over beauty render | Curvature magnitude field | Medium | — |
| 22 | Celestial Boundary Overlay | **Proposed** | No candidate file yet | Show where bulk-to-boundary correspondence surfaces appear — the "celestial sphere" of the scene | Glowing boundary shell at ray terminal angles; luminous dots for hit positions on sphere; heatmap of angular hit density | Ray terminal angles + hit type (geometry / boundary / miss) | **High** | Pasterski / celestial holography; bulk-to-boundary translation |
| 23 | Bulk-to-Boundary Dual View | **Proposed** | Candidate: extend `wormhole_dual_reality_analysis.py` toolchain | Side-by-side: 3D bulk scene render vs flattened boundary projection | Split-screen or animated fade between bulk perspective and boundary-sphere encoding | Full-scene ray set + boundary-mapped terminal angle positions | **High** | Pasterski / holography; celestial sphere encoding |
| 24 | Transport Memory Overlay | **Partial** | Data: `RendererCore/Scheduling/SceneTransportMemory.cs`; no render path | Visualize residual path coherence basins and seam fragmentation | Heatmap of coherence score per region; red overlay for unstable seam records; contour bands for precision floor regions | `SceneTransportMemory` records (CoherenceBasin, UnstableSeam, PrecisionFloor) | Medium | Path memory / residual effects |
| 25 | Correspondence Failure Heatmap | **Proposed** | No file yet; data: `ReferenceTransportOracle.cs` comparison records | Highlight pixels where bulk-boundary transport correspondence breaks | Red-coded failure zone overlay; intensity = divergence magnitude | `ProductionOracleComparisonRecord` from `ReferenceTransportOracle` | Medium | Holography correspondence; oracle validation |
| 26 | Soft Mode / IR Glow Overlay | **Proposed** | No file yet | Render transport "warmth" as a soft glowing field — aesthetic mode for demos | Additive glow pass over beauty; glow intensity = field density × curvature magnitude | Field density + curvature magnitude per pixel | Low | — |
| 27 | S-Matrix Event Ledger | **Proposed** | No file yet | Log and visualize scatter-like in/out events at domain crossings | Per-pixel event ledger; replay mode shows crossing glyphs frame by frame | `BoundaryLayerVolume` crossing events (entry/exit domain, position, transport state) | Medium | Scattering amplitudes; S-matrix; Pasterski |
| 28 | Curvature Domain Map | **Partial** | Data: `RendererCore/Common/DomainTelemetry.CurvatureDomainKind`; no dedicated overlay | Map MouthNear / ThroatBridge / FarWall / TangentialFar / Background / BoundaryMixed zones | False-color zone map; unique color per `CurvatureDomainKind` value | `DomainTelemetry` CSV per-pixel domain kind | **High** | Domain topology; wormhole structure |
| 29 | Boundary Crossing Glyphs | **Proposed** | No file yet | Animated glyphs at boundary crossing events per ray | Arrow or burst glyphs at crossing pixel positions; playback mode steps through frames | `BoundaryLayerVolume` crossing events; per-ray crossing position | Medium | — |
| 30 | Wormhole Seam Observatory | **Partial** | `scripts/run_wormhole_structure_observatory_quick.sh` covers structural view; seam-specific diagnostics not isolated | Visualize unstable seam regions in wormhole topology | Heatmap of seam divergence / fragmentation scores; red for high-fragmentation zones | `SceneTransportMemory.UnstableSeamRecord` | High | Wormhole seam stability |
| 31 | Transport Ownership Graph Overlay | **Partial** | `tools/transport_ownership_graph_extractor.py`, `transport_ownership_graph_analysis.py`, `tools/graph_plus_hit_normals_report.py`; no in-engine render | Graph of which transport domain owns each ray segment | Graph edge lines drawn over scene; node glyphs at domain boundaries | `ObjectSeededTileScheduler` probe results; `TransportObserverKind` per segment | Medium | Transport ownership; domain resolver |
| 32 | Reference Ray / Straight-Ray Comparison | Active (as Python tool) | `Wormhole/StraightRayReferenceCache.cs`; `tools/wormhole_dual_reality_analysis.py`, `wormhole_dual_reality_storytelling.py` | Curved vs straight ray comparison — dual-reality view | Side-by-side or diff composite; straight-ray reference cache async refresh | Reference cache + curved render capture | High | Geodesic deviation; dual reality |
| 33 | High-Curvature Oracle Overlay | **Proposed** | Data: `RendererCore/Fields/CurvatureBoundGrid.cs`; no visual path | Show Kmax grid as heatmap with high-curvature region markers | False-color Kmax heatmap; threshold glyph layer (e.g. circles where Kmax > cutoff) | `CurvatureBoundGrid` cell values | Medium | High-curvature detection; geodesic focusing |
| 34 | Metric Grid / Stress-Tensor Style Overlay | **Proposed** | No file yet | Render local metric components as a grid of ellipses or deformation glyphs | Tensor glyph field over scene; glyph shape = local transport frame deformation | `MetricRayState` transport frame (U, V) data per pixel | Low | GR / metric formalism; MTW visualization style |

---

## Overlay Ecosystem Map

```
In-Engine (C#/GDShader)
├── FilmOverlay2D           (film-plane 2D: rays, normals)
├── RayViz 3D               (scene-space 3D: bend polylines)
├── Curved View Shader      (screen post-process: UV warp)
├── WormholeResearchOverlay (portal research readout)
├── CameraSpaceCollisionOverlay (collision geometry)
├── WireframeReferenceOverlay   (wireframe comparison)
├── OverspacePortalDebugOverlay (portal link graph)
└── FieldSourceNodeGizmo    (editor gizmo: field extent)

Python Post-Process (tools/)
├── Diagnostic Wireframe Overlay   (multi-layer composite)
├── Hit Normal Vector Overlay      (normal glyphs)
├── Camera Cross-Section Minimap   (minimap inset)
├── Atomic Visual Observatory      (V0-V4 cell × shading × contour)
├── Wormhole Structure Observatory (panel contact sheet)
└── Domain Audit Visual Chain
    ├── Step Budget Heatmap
    ├── Domain Ownership Map
    ├── Boundary Confidence Map
    ├── Normal Discontinuity Heatmap
    ├── Selection Flip Heatmap
    ├── Depth Heatmap
    ├── Density Contour Overlay
    └── Curvature Contour Overlay

Reference / Comparison (Python tools/ + Wormhole/StraightRayReferenceCache.cs)
└── Reference Ray / Straight-Ray Comparison

Partial (data exists, render path missing)
├── Transport Memory Overlay    (SceneTransportMemory.cs → heatmap)
├── Curvature Domain Map        (DomainTelemetry.CurvatureDomainKind → color map)
├── Wormhole Seam Observatory   (UnstableSeamRecord → fragmentation heatmap)
└── Transport Ownership Graph Overlay (transport_ownership_graph_*.py → in-engine render)

Proposed (not yet built)
├── Celestial Boundary Overlay  [HIGH PRIORITY]
├── Bulk-to-Boundary Dual View  [HIGH PRIORITY]
├── Curvature Domain Map        [HIGH PRIORITY - partial]
├── S-Matrix Event Ledger
├── Correspondence Failure Heatmap
├── Boundary Crossing Glyphs
├── High-Curvature Oracle Overlay
├── Wormhole Seam Observatory (seam-isolated pass)
├── Soft Mode / IR Glow Overlay
└── Metric Grid / Stress-Tensor Style Overlay
```

---

## Top Proposed Overlays by Priority

| Priority | Overlay | Why Now |
|----------|---------|---------|
| 1 | Celestial Boundary Overlay | Core MisterY Labs narrative; ray terminal angle data already available |
| 2 | Curvature Domain Map | Data fully available in `DomainTelemetry.cs`; one Python pass away |
| 3 | Bulk-to-Boundary Dual View | Extends existing dual-reality toolchain; high narrative impact |
| 4 | Wormhole Seam Observatory (seam-isolated) | `UnstableSeamRecord` exists; seam-specific pass not yet written |
| 5 | Transport Memory Overlay | `SceneTransportMemory.cs` schema complete; needs renderer hookup |

---

*See also: [FEATURE_INDEX.md](../FEATURE_INDEX.md) | [FEATURE_READINESS_AUDIT.md](../Release/FEATURE_READINESS_AUDIT.md) | [OPTICAL_TRANSPORT_FEATURE_MAP.md](../Research/OPTICAL_TRANSPORT_FEATURE_MAP.md)*
