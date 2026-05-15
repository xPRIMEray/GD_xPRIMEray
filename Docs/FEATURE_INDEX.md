# xPRIMEray Feature Index

> Master index for the Codex Quad audit — 2026-05-14
> Four detailed audit documents, one navigable summary

---

## Detailed Audit Documents

| Lens | Document | Focus |
|------|----------|-------|
| Release Captain | [Release/FEATURE_READINESS_AUDIT.md](Release/FEATURE_READINESS_AUDIT.md) | Ship-readiness classification; cleanup list; packaging exclusions |
| Optical Transport Architect | [Research/OPTICAL_TRANSPORT_FEATURE_MAP.md](Research/OPTICAL_TRANSPORT_FEATURE_MAP.md) | Transport system completeness; engine feature candidates; gap analysis |
| Observatory Overlay Designer | [Observatory/OVERLAY_MASTER_LIST.md](Observatory/OVERLAY_MASTER_LIST.md) | All existing and proposed overlay modes; full master table |
| MisterY Labs Inspiration Curator | [MisterYLabs/INSPIRATION_CARD_FEATURE_LINKS.md](MisterYLabs/INSPIRATION_CARD_FEATURE_LINKS.md) | Thinker-to-feature cards; Pasterski card + five stubs |

---

## Ready-to-Ship Features

These systems are stable, well-exercised, and suitable for demo or release packaging.

| Feature | Key Files |
|---------|-----------|
| GRIN field evaluation | `FieldSystem.cs`, `FieldMath.cs`, `FieldCurves.cs`, `FieldTLAS.cs`, `CurvatureBoundGrid.cs` |
| GRIN field authoring | `FieldSource3D.cs` (2451 lines) |
| Atomic orbital field source | `AtomicEigenmodeFieldSource3D.cs` |
| Boundary layer volumes | `BoundaryLayerVolume.cs` |
| Hit detection | `RendererCore/Geometry/GeometryTLAS.cs` + `RayBeamRenderer.cs` collision subsystem |
| Step pipeline interfaces | `IIntegrator.cs`, `IMetricField.cs`, `MetricTransportTypes.cs`, `StepResult.cs`, `StepPolicy.cs`, `MetricSegmentCompatibility.cs` |
| 2D film overlay | `FilmOverlay2D.cs` |
| 3D ray visualization | `RayViz.cs` |
| Screen-space curvature warp | `curved_view.gdshader` |
| Wormhole portal overlays | `Wormhole/WormholeResearchOverlay.cs`, `WireframeReferenceOverlay.cs`, `CameraSpaceCollisionOverlay.cs` |
| Field source editor gizmo | `addons/fieldsource_gizmo/` |
| Inspector help tooltips | `addons/grin_inspector_help/` |
| RenderBackends abstraction | `RenderBackends/` |
| Test fixture scenes (46) | `Fixtures/*.tscn` + controllers |
| Render test runner | `RendererCore/Testing/RenderTestRunner.cs` |
| Auto-calibration | `RendererCore/Testing/SceneAutoCalibrator.cs`, `LauncherAudit.cs` |
| TestBench UI | `UI/TestBenchController.cs`, `UI/TestBenchPanel.gd`, `UI/testbench_recipes.json` |
| Atomic Visual Observatory | `scripts/run_atomic_orbital_visual_observatory.sh` + `tools/atomic_orbital_visual_diff.py` |
| Wormhole Structure Observatory | `scripts/run_wormhole_structure_observatory_quick.sh` + `tools/wormhole_structure_observatory_report.py` |
| Domain audit visual heatmaps | `scripts/run_domain_audit_visual.sh` + Python chain (step budget, domain ownership, boundary confidence, normal discontinuity) |
| Python diagnostic toolchain | `tools/diagnostic_wireframe_overlay.py`, `hit_normal_vector_overlay.py`, `camera_cross_section_minimap_overlay.py`, `build_visual_contact_sheet.py`, `image_compare.py` |

---

## In-Progress Features

| Feature | Key Files | Status |
|---------|-----------|--------|
| TestBench recipe system | `UI/TestBenchController.cs` | Modified May 13; API still evolving |
| Wormhole prototype rig | `Wormhole/WormholePrototypeRig.cs` | Modified May 13; active experiment; needs decomposition |
| Atomic orbital visual observatory fixture | `Fixtures/AtomicOrbitalVisualObservatoryController.cs` | Modified May 11; pipeline stabilizing |
| Atomic orbital GRIN room fixture | `Fixtures/AtomicOrbitalGrinRoomController.cs` | Modified May 10; recently revised |

---

## Proposed Overlays

Highest-priority proposed overlays only. Full list with all 34 entries: [Observatory/OVERLAY_MASTER_LIST.md](Observatory/OVERLAY_MASTER_LIST.md)

| Priority | Overlay | Data Already Available | What's Missing |
|----------|---------|----------------------|----------------|
| High | Celestial Boundary Overlay | Ray terminal angles in renderer | Python render pass + visual |
| High | Curvature Domain Map | `DomainTelemetry.CurvatureDomainKind` CSV | Python heatmap color-map pass |
| High | Bulk-to-Boundary Dual View | `wormhole_dual_reality_analysis.py` chain | Boundary projection layer |
| High | Wormhole Seam Observatory (seam-isolated) | `SceneTransportMemory.UnstableSeamRecord` | Seam-specific extraction pass |
| Medium | Transport Memory Overlay | `SceneTransportMemory.cs` schema | Renderer hookup + heatmap |
| Medium | S-Matrix Event Ledger | `BoundaryLayerVolume` crossing events | Logging schema + replay visualization |
| Medium | Correspondence Failure Heatmap | `ReferenceTransportOracle` comparison records | Python heatmap pass |
| Medium | Transport Ownership Graph Overlay | `transport_ownership_graph_*.py` Python tools | In-engine render path |
| Medium | High-Curvature Oracle Overlay | `CurvatureBoundGrid` cells | Python heatmap + threshold glyphs |
| Low | Soft Mode / IR Glow Overlay | Field density + curvature magnitude | Glow render pass |
| Low | Boundary Crossing Glyphs | `BoundaryLayerVolume` events | Glyph renderer |
| Low | Metric Grid / Stress-Tensor Style Overlay | `MetricRayState` transport frame (U, V) | Tensor glyph field renderer |

---

## Research-Only Systems

These have explicit "diagnostic-only" guardrails. They feed the Python analysis toolchain — they are not part of the rendering pipeline for end users.

| System | Key File | Guardrail |
|--------|----------|-----------|
| Reference transport oracle | `RendererCore/Validation/ReferenceTransportOracle.cs` | "diagnostic-only" file header annotation |
| Scene transport memory | `RendererCore/Scheduling/SceneTransportMemory.cs` | "diagnostic-only" file header annotation |
| Metric heuristic integrator | `RendererCore/Transport/MetricHeuristicIntegrator.cs` | 4 open TODOs; heuristic approximation, not full GR |
| Overspace trophy room demo | `Wormhole/Overspace/OverspaceTrophyRoomDemo.cs` | Demo-quality; not a validated transport test |
| Derivative-aware stepping | Flag in `RayBeamRenderer.cs` | `UseDerivativeAwareStepping` defaults to `false` |

---

## Inspiration Card Links

Full cards with visual motifs and engine feature links: [MisterYLabs/INSPIRATION_CARD_FEATURE_LINKS.md](MisterYLabs/INSPIRATION_CARD_FEATURE_LINKS.md)

| Card | Thinker / Concept | Key Connection | Related Proposed Overlays |
|------|-------------------|----------------|--------------------------|
| 001 (Full) | Sabrina Pasterski / Celestial Holography | Bulk-to-boundary translation; celestial sphere encoding; "gravity is hard — find an equivalent description" | Celestial Boundary Overlay, Bulk-to-Boundary Dual View, Correspondence Failure Heatmap, S-Matrix Event Ledger |
| Stub | Emmy Noether / Symmetry | Transport invariants and their breaking; coherence basin stability | Transport Memory Overlay, S-Matrix Event Ledger |
| Stub | MTW / Metric Tensor Language | Transport frame (U, V) as partial tetrad; honest distance from full GR | Metric Grid / Stress-Tensor Style Overlay |
| Stub | Maxwell / GRIN Optics | Gradient-index optics tradition; curved rays through varying n | Density Contour Overlay, Curvature Contour Overlay |
| Stub | Gauss / Riemann / Differential Geometry | Curvature as intrinsic field property; curvature bound grid | Curvature Domain Map, High-Curvature Oracle Overlay |
| Stub | Feynman / Path Integral | Transport coherence basins as interference; unresolved islands as destructive interference | Transport Memory Overlay, Correspondence Failure Heatmap |

---

## Recommended Next 5 Actions Before First Release

These are the five highest-leverage items identified across all four audit lenses.

- [ ] **1. Stub & orphan file cleanup**
  Remove `HitPayload.cs` (1-byte empty), `RendererCore/Transport/MetricRayState.cs.uid` (orphan UID), `RayEmitter3D.cs` (stub), the two empty `RendererCore/Accel/` and `RendererCore/Scheduler/` directories, all `*.bak`/`*.tmp` files, and obsolete `.tscn` variants (`test.tscn.obs`, `test.tscn.bbNew`, `test_BB.tscn.bak`, `test - New.tscn.bak`).

- [ ] **2. Root-level scene organization**
  Move the 60 root-level `.tscn` test scenes into `Fixtures/` or a new `Scenes/` directory. Leave only `overspace_trophy_room_demo.tscn` at root as the demo entry point. The current proliferation is confusing and makes the root directory hard to navigate.

- [ ] **3. WormholePrototypeRig decomposition**
  Split `Wormhole/WormholePrototypeRig.cs` (3798 lines, 148KB) into logical subsystems — TopologyManager, PortalRenderer, ValidationBridge — before release packaging. This is the single largest structural risk in the codebase. The file is active (modified May 13) so coordinate with the wormhole experiment before restructuring.

- [ ] **4. Curvature Domain Map overlay**
  Wire `DomainTelemetry.CurvatureDomainKind` into a Python heatmap pass. The data is fully available in the domain telemetry CSV. This is one Python script away from being a production overlay — and it is the clearest visual signal of transport domain structure in the scene.

- [ ] **5. Celestial Boundary Overlay (first proposed overlay)**
  Implement the Celestial Boundary Overlay. Ray terminal angles are already available from the renderer. The overlay maps them onto a reference sphere shell, building a visual celestial map of where the scene's transport sends each ray. This is the highest-priority proposed overlay, the most direct visual expression of the Pasterski inspiration card, and the clearest demonstration that xPRIMEray is an observatory — not just a renderer.

---

## Quick Reference: File Classification

| File | Status |
|------|--------|
| `HitPayload.cs` | Deprecated — remove |
| `RendererCore/Transport/MetricRayState.cs.uid` | Deprecated — remove |
| `GrinFilmCamera.cs.bak*` | Cleanup — archive or delete |
| `GrinFilmCamera_RESEARCHMODE.cs.ref` | Experimental reference — do not compile or ship |
| `WormholePrototypeRig.cs` | In progress + Needs cleanup |
| `MetricHeuristicIntegrator.cs` | Experimental — 4 open TODOs |
| `ReferenceTransportOracle.cs` | Research only — diagnostic-only guardrail |
| `SceneTransportMemory.cs` | Research only — diagnostic-only guardrail |
| `BoundaryLayerVolume.cs` | Ready to ship |
| `FieldSystem.cs` / `FieldMath.cs` | Ready to ship |
| `FilmOverlay2D.cs` / `RayViz.cs` | Ready to ship |
| `RendererCore/Testing/RenderTestRunner.cs` | Ready to ship |
| `UI/TestBenchController.cs` | In progress |
| `output/` (4.1GB) | Exclude from packaging |
| `logs/` (419MB) | Exclude from packaging |
| `site/` (192MB) | Exclude from packaging (rebuild on deploy) |

---

*Generated by Codex Quad Prompt audit — xPRIMEray / MisterY Labs*
*All file paths relative to repo root: `/home/bb/code/godot_xPRIMEray/`*
