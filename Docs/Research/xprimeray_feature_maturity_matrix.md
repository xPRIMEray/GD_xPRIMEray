# xPRIMEray Feature Maturity Matrix

*Separates live engine capability from engineering harness and post-process analysis. Code locations verified against current source.*

---

## How to Read This Table

- **Runtime/In-Game**: Available on a live render frame in Godot; interactable or visible during play.
- **Harness/Test**: Available via `scripts/run_*.sh` + CLI `--render-test` argument; requires an offline render pass.
- **Post-Process**: Available only by running a Python tool in `tools/` against saved harness output. No in-game path.
- **Gap to Smart Engine**: What is needed to bring the capability into adaptive in-game use.

---

## Feature Maturity Matrix

| Feature | Evidence Artifact(s) | Current Code Location | Runtime / In-Game | Harness / Test | Post-Process | Notes / Gap to In-Game |
|---|---|---|---|---|---|---|
| **Curved ray transport (GRIN)** | `Docs/screenshots/2026-02-26*` | `GrinFilmCamera.cs`, `RendererCore/Integrators/` | ✅ Live | ✅ All fixtures | — | Core transport. Fully integrated. |
| **Straight reference render** | `output/wormhole_DR_analysis/*/support/*straight_transport_reference.png` | `Wormhole/StraightRayReferenceCache.cs` | ✅ Live (async refresh) | ✅ | — | `StraightRayReferenceCache` runs a background re-render at straight step. Used by dual-reality inset. |
| **Dual-Reality inset (straight reference)** | `output/dual_reality/wormhole_inset_baseline.png`, `Docs/assets/wormhole_inset_baseline.png` | `WormholePrototypeRig.cs` (`DualRealityInsetEnabled`, `DualRealityInsetScale`) | ✅ Live | ✅ (`run_wormhole_dual_reality_analysis.sh`) | ✅ (`wormhole_dual_reality_analysis.py`) | In-game: F-key toggle in WormholePrototypeRig. No gap. |
| **Film heatmap overlay** | `output/wormhole_DR_Story/latest/images/03_curvature_map.png` | `WormholePrototypeRig.cs` (`DualRealityOverlayMode = FilmHeatmap`) | ✅ Live | ✅ | — | Toggled by hotkey cycle in WormholePrototypeRig. Mode: FilmHeatmap. |
| **Curvature heatmap overlay** | `output/dual_reality/wormhole_curvature_full_stack.png` | `WormholePrototypeRig.cs` (`EnableCurvatureHeatmap`, `CurvatureHeatmapMetric`) | ✅ Live | ✅ | — | Metric modes: CumulativeTurnAngle, MaxLocalTurnAngle, CurvatureMean, CurvatureMax. Opacity, normalization exported. |
| **Semantic wireframe glyph overlay** | `output/wormhole_DR_analysis/*/images/wormhole_reference_plus_semantic.png` | `Wormhole/WireframeReferenceOverlay.cs` (`ShowFieldGlyphs`, `ShowBoundaryLayerGlyphs`, `ShowWormholePortalGlyphs`) | ✅ Live | ✅ | — | Draws portal rings, field shells, BLV boundaries, backdropglyphs. |
| **Collision radar overlay** | `output/wormhole_DR_analysis/*/images/wormhole_reference_plus_collision.png`, `output/dual_reality/wormhole_collision_radar.png` | `Wormhole/CameraSpaceCollisionOverlay.cs` (`DualRealityCollisionRadarOverlayEnabled`) | ✅ Live | ✅ | — | Projects AABB/sphere bounds. Filter modes: AllVisible, HitConfirmed, Primary, Remapped, Background, Helpers. |
| **Research overlay (top-down / oblique)** | No dedicated output image | `Wormhole/WormholeResearchOverlay.cs`, `WormholeResearchOverlayCanvas.cs` | ✅ Live | — | — | Top-down/oblique 2D schematic of scene geometry. Distinct from curvature/wireframe overlays. |
| **Hit normal vectors (in-game)** | `FilmOverlay2D` | `FilmOverlay2D.cs` (`DrawHitNormals`, `WorldNormalLen`, `WorldNormalColor`) | ✅ Live | — | ✅ (`tools/hit_normal_vector_overlay.py`) | In-game: normal sticks drawn by FilmOverlay2D. Post-process: Python overlay on captured frame. Both available. |
| **Film gradient normals** | — | `FilmOverlay2D.cs` (`DrawFilmGradientNormals`), `GrinFilmCamera.cs:20168` | ✅ Live | — | — | Screen-space gradient normals approximated from film image; not physics normals. |
| **Debug ray overlay** | — | `FilmOverlay2D.cs` (`DrawRays`), `GrinFilmCamera.cs` (`DebugOverlayOwnedByFilm`) | ✅ Live | — | — | Per-ray polyline overlay in viewport. Ray count capped by `DebugMaxFilmRays`. |
| **Comparison grid / crosshair** | — | `FilmOverlay2D.cs` (`ShowComparisonGrid`, `ShowComparisonCrosshair`) | ✅ Live | — | — | Grid/crosshair overlay for alignment comparison. |
| **Fixture hit color coding** | `Docs/papers/paper_004_hermetic_throat_validation/figures/capture.png` | `GrinFilmCamera.cs` (`FixtureDebugHitColoringEnabled`) | ✅ Live (fixture mode) | ✅ | — | Colors pixels by hit class: source, portal, background, absorbed, miss. |
| **Coverage annotated render** | `output/fixture_runs/fixture_008*/coverage_annotated.png` | `GrinFilmCamera.cs`, `RendererCore/Testing/` | — | ✅ | — | Hermetic coverage annotation. Test harness only. |
| **Throat depth map** | `output/fixture_runs/fixture_008*/throat_depth_map.png` | `GrinFilmCamera.cs`, `RendererCore/Testing/` | — | ✅ | ✅ | Per-pixel wormhole depth heat. Test harness + post-process. |
| **Domain telemetry maps** | `output/domain_audit_visual/*/domain_id.png`, `domain_confidence.png`, `boundary_confidence.png`, `selection_flip.png`, `normal_discontinuity.png` | `GrinFilmCamera.cs` (`EnableDomainTelemetry`), exports to CSV/PNG per pixel | — | ✅ (via `run_domain_audit_visual.sh`) | ✅ (`tools/curvature_domain_ownership_analysis.py`) | **Gap:** Data exported but not composited as in-game overlay. Smart engine would read domain CSV and render heatmap in-game per-frame. |
| **Domain-aware first-hit resolver** | `output/domain_audit_visual/*/resolver_on/` | `GrinFilmCamera.cs` (`EnableDomainAwareFirstHitResolver`) | ✅ Live (flag gate) | ✅ | — | Experimental heuristic resolver. Off by default. Requires `EnableDomainTelemetry`. |
| **Tile metrics / persistent priors** | `output/tile_commit_traversal_comparison/` | `GrinFilmCamera.cs` (`EnableTileMetricsScaffold`, `EnableTileMetricsPersistentPriors`, `EnableTileMetricsBandSeed`) | ✅ Live (flag gate) | ✅ | ✅ | Tile reorder execution and persistent priors scaffold. Band-seed priors file (`band_tile_signals.json`) loaded at startup. |
| **Object-seeded tile scheduler** | `output/tile_commit_traversal_comparison/` | `GrinFilmCamera.cs` (`EnableObjectSeededTileScheduler`) | ✅ Live (flag gate) | ✅ | — | Tile ordering seeded from projected scene object centroids. |
| **Transport continuity vectors** | `output/tile_commit_traversal_comparison/*/layer5_transport_continuity_vectors.png`, `Docs/assets/cathedral_probe/continuity_vectors_row_0015.png` | `tools/diagnostic_wireframe_overlay.py` (post-process only) | ❌ Not in-game | ✅ (harness produces CSV) | ✅ (Python overlays PNG) | **Gap:** `local_continuity_vectors.csv` written by harness; post-process Python renders it. No in-game vector field rendering. |
| **Transport shape regions** | `output/tile_commit_traversal_comparison/*/transport_shape_regions_overlay.png` | `tools/diagnostic_wireframe_overlay.py` | ❌ Not in-game | ✅ | ✅ | Ownership contour extraction. Post-process only. |
| **Diagnostic wireframe overlay (6-layer)** | `output/tile_commit_traversal_comparison/*/combined_diagnostic_overlay.png`, `Docs/assets/cathedral_probe/cathedral_probe_overlay_row_0015.png` | `tools/diagnostic_wireframe_overlay.py` | ❌ Not in-game | ✅ (harness produces JSON/CSV) | ✅ (Python composites PNG) | **Gap:** In-game version would composite layers in real time. WireframeReferenceOverlay only covers semantic glyphs. |
| **Curvature/performance heatmaps** | `output/telemetry_heatmap_quick/*.heat_*.png`, `output/telemetry_heatmap_v2_sample/*.heat_curvature_*.png` | `GrinFilmCamera.cs` (telemetry CSV export), `tools/` Python analysis | — | ✅ (harness exports per-pixel CSV) | ✅ (Python renders PNG) | **Gap:** Curvature `heat_curvature_max`, `heat_d2k_max` exported as CSV but no in-game heatmap display. Film heatmap in `DualRealityOverlayMode.FilmHeatmap` covers work cost, not curvature. |
| **Phase coherence field** | `Docs/diagnostics/phase_coherence.md` | `tools/phase_coherence_field_analysis.py` | ❌ Not in-game | ✅ (harness produces hit_diagnostics.csv) | ✅ (Python analysis) | **Gap:** Phase coherence computed from hit diagnostics CSV. No in-game render. |
| **Camera cross-section minimap** | Tool exists: `tools/camera_cross_section_minimap_overlay.py` | Python-only | ❌ Not in-game | ✅ (reads harness output) | ✅ | **Gap:** Large gap. Would require in-game 2D minimap widget showing camera-space cross-section of the transport field. WormholeResearchOverlay shows top-down scene geometry but not camera-space field cross-section. |
| **GRIN field arrows / vector field** | No dedicated curated output found | `tools/` (inferred from camera_cross_section_minimap_overlay.py) | ❌ Not in-game | — | ✅ (Python tool) | **Gap:** Field gradient arrows not rendered in-game. WireframeReferenceOverlay draws field shells but not ∇n arrows. |
| **Transport ownership graph** | `output/transport_ownership_graph_precision_sweep/` | `tools/transport_ownership_graph_extractor.py`, `transport_ownership_graph_analysis.py`, `transport_ownership_graph_validation.py` | ❌ Not in-game | ✅ | ✅ | **Gap:** Graph built from hit_diagnostics.csv. No runtime graph construction or display. Architectural concept for adaptive precision budgeting. |
| **Reference transport oracle** | `output/reference_transport_oracle_*/`, `Docs/assets/transport_islands/` | `RendererCore/Validation/ReferenceTransportOracle.cs`, `tools/reference_transport_oracle_island_analysis.py` | ❌ Not in-game (guardrailed) | ✅ | ✅ | Guardrail: oracle outputs never feed rendering. Intended as offline validation only. |
| **Precision closure / island map** | `output/reference_transport_oracle_unresolved_island/*/first_stable_step_map.png` | `tools/reference_transport_oracle_island_analysis.py` | ❌ Not in-game | ✅ | ✅ | **Gap:** Island identification and precision budget map are post-process. Smart engine could read island masks to locally refine step length. Guardrail must remain (oracle does not feed step selection directly). |
| **Observer checkpoint sequence** | `output/fixture_runs/fixture_011_wormhole_checkpoint_sequence/` | `RendererCore/Testing/RenderTestRunner.cs`, `scripts/run_fixture_011_wormhole_checkpoint_sequence.sh` | — | ✅ (3-checkpoint automated) | ✅ (paper analysis) | Test harness only. Not triggered in interactive play. |
| **Wormhole portal rig** | `output/overspace_first_milestone/runtime_check.png` | `Wormhole/WormholePrototypeRig.cs`, `Wormhole/Overspace/` | ✅ Live | ✅ (overspace fixture) | — | Full dual-scene wormhole rig runs in interactive Godot. Teleportation on shell crossing. |

---

## Summary: What Is and Is Not Live In-Game

### Available in-game today (no code changes needed)

1. Curved ray transport (GRIN / Gordon metric)
2. Straight reference render (async StraightRayReferenceCache)
3. Dual-reality inset (F-key toggle)
4. Film heatmap overlay (key cycle)
5. Curvature heatmap overlay (5 metric modes, configurable opacity/normalization)
6. Semantic wireframe glyphs (portals, fields, BLVs, backdrops)
7. Collision radar overlay (7 filter modes, AABB/sphere projection)
8. Top-down research overlay (WormholeResearchOverlay)
9. Hit normal vectors (FilmOverlay2D)
10. Film gradient normals (FilmOverlay2D)
11. Debug ray overlay (FilmOverlay2D)
12. Comparison grid / crosshair (FilmOverlay2D)
13. Fixture hit color coding (flag gate)
14. Domain-aware resolver (flag gate)
15. Tile metrics / persistent priors (flag gate)
16. Object-seeded tile scheduler (flag gate)
17. Wormhole portal rig / teleportation

### Available via test harness only (offline render pass required)

18. Transport continuity vectors (CSV written, Python overlays PNG)
19. Diagnostic wireframe 6-layer composite (Python renders)
20. Domain telemetry maps (5 channels: domain_id, domain_confidence, boundary_confidence, selection_flip, normal_discontinuity)
21. Performance heatmaps (candidates, pass1 steps, query, resolve, work)
22. Curvature heatmaps (K_max, dK/dt, d²K/dt², work-minus-curvature)
23. Coverage annotation
24. Throat depth map
25. Observer checkpoint sequence (automated fixture 011)

### Post-process analysis only (Python tools, no in-game path at all)

26. Transport ownership graph (construction and visualization)
27. Phase coherence field
28. Camera cross-section minimap
29. GRIN field arrows / ∇n vector field
30. Reference transport oracle runs and island maps
31. Precision closure maps (first-stable-step per pixel)

---

## Recommended Next Integration Steps (Priority Order)

### Priority 1 — Domain telemetry overlay (in-game)

**Why:** Domain maps (`domain_id`, `domain_confidence`, `boundary_confidence`) are already exported per-pixel in the harness. The data is there. The gap is a visualization path.

**Implementation:** Add a `DualRealityOverlayMode.DomainId` / `DomainConfidence` mode to `DualRealityOverlayModeKind` in `WormholePrototypeRig.cs`. Read the domain channel from the film buffer during the same render pass that produces `FilmHeatmap`. No new rendering work; the domain data is already in memory as `_domainResolverIdBuffer` and friends.

### Priority 2 — Transport continuity vector field (in-game)

**Why:** Continuity vectors are already computed per-pixel in the harness; the visual is a 2D vector field over the screen. It would make transport instability visible interactively.

**Implementation:** Add a `FilmOverlay2D` mode that draws short line segments from the per-pixel transport disagreement vector. The disagreement data would need to be produced per-frame (currently only in harness). A lightweight single-pass version computing only collider-ownership disagreement between adjacent pixels would be enough for the interactive version.

### Priority 3 — Performance / curvature heatmap (in-game)

**Why:** `heat_pass1_steps` and `heat_curvature_max` already exist as telemetry. The curvature data (`_curvatureBuffer` etc.) is in-memory during render. The `DualRealityOverlayMode.FilmHeatmap` already projects a film-plane buffer; adding a `CurvatureK` mode would require only a second normalization + color-mapping step.

**Implementation:** Extend `DualRealityOverlayModeKind` to include `CurvatureKMax`. Read from curvature buffer already available in `GrinFilmCamera`. Normalize and colormap the same as `FilmHeatmap`.

### Priority 4 — Camera cross-section minimap widget

**Why:** Most valuable for interactive debugging of field geometry. Currently Python-only.

**Implementation:** A `Control` node that renders a 2D cross-section of the ∇n field at the camera plane, similar to `WormholeResearchOverlay` but in camera space rather than world space. Read field values from `FieldSource3D` nodes. Small footprint widget, can be toggled.

### Priority 5 — GRIN field arrows (∇n vector field)

**Why:** Makes the refractive-index gradient visually navigable in real time. Important for scene setup and debugging.

**Implementation:** Extend `WireframeReferenceOverlay` to draw ∇n arrows sampled on a regular grid. Field values available from `FieldSource3D`. Arrow length proportional to |∇n|. Toggle with existing `ShowFieldGlyphs`.

### Priority 6 — Precision budget display (island awareness)

**Why:** If island masks are precomputed (from oracle sweeps) and stored as textures, a shader could read them and display per-pixel required-step as a heatmap. This makes the island topology visible during interactive rendering.

**Implementation:** Load a precomputed `first_stable_step_map.png` as a texture. Display via `DualRealityOverlayMode` as a colormap over the film plane. Read-only; no oracle feedback to renderer (guardrail preserved).
