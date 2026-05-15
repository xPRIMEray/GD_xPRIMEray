# xPRIMEray Feature Readiness Audit

> **Lens: Release Captain**
> Audit date: 2026-05-14
> Scope: First public / demo release readiness classification

---

## Classification Key

| Label | Meaning |
|-------|---------|
| Ready to Ship | Stable, exercised, suitable for demo or release packaging |
| Needs Cleanup | Works but has rough edges that should be resolved before shipping |
| In Progress | Actively changing; not safe to treat as stable |
| Experimental / Research Only | Functional internally but not production-ready; guarded by diagnostic-only annotations |
| Proposed / Not Started | Specified intent, no implementation |
| Deprecated / Confusing / Duplicate | Stale, empty, or misleading; flag for removal |

---

## Ready to Ship

### Core Rendering

| Feature | Key Files | Notes |
|---------|-----------|-------|
| GRIN field evaluation | `FieldSystem.cs`, `FieldMath.cs`, `FieldCurves.cs`, `FieldTLAS.cs`, `CurvatureBoundGrid.cs` | Full profile suite; well-exercised |
| GRIN field authoring node | `FieldSource3D.cs` (2451 lines) | Comprehensive debug visualization |
| Atomic orbital field source | `AtomicEigenmodeFieldSource3D.cs` | V1 hydrogen density model; exp(-2r/orbitalRadius) |
| Boundary layer volumes | `BoundaryLayerVolume.cs` | Complete enum set; per-ray bitmask state tracking |
| Hit detection | `RendererCore/Geometry/GeometryTLAS.cs` | BVH AABB query; collision subsystem in `RayBeamRenderer.cs` |
| Step infrastructure | `StepResult.cs`, `StepPolicy.cs`, `IIntegrator.cs`, `IMetricField.cs`, `MetricTransportTypes.cs`, `MetricSegmentCompatibility.cs` | Stable transport pipeline interfaces |
| RenderBackends abstraction | `RenderBackends/IRenderBackend.cs`, `CoreBackend.cs`, `LegacyBackend.cs`, `BackendSelector.cs` | Clean backend swap |

### Visualization

| Feature | Key Files | Notes |
|---------|-----------|-------|
| 2D film overlay | `FilmOverlay2D.cs` | Ray polylines, hit normals, comparison grid |
| 3D ray visualization | `RayViz.cs` | 9-point screen sample; ImmediateMesh polylines |
| Screen-space curvature warp | `curved_view.gdshader` | beta / gamma / warp_scale uniforms |
| Wormhole portal overlays | `Wormhole/WormholeResearchOverlay.cs`, `WireframeReferenceOverlay.cs`, `CameraSpaceCollisionOverlay.cs` | Production-quality research overlays |
| Field source editor gizmo | `addons/fieldsource_gizmo/` | 3D gizmo handles in Godot editor |
| Inspector help tooltips | `addons/grin_inspector_help/` | GrinFilmCamera tooltip system |

### Test Infrastructure

| Feature | Key Files | Notes |
|---------|-----------|-------|
| Fixture scenes (46 total) | `Fixtures/*.tscn` | Black hole, Einstein ring, GRIN basic visual, boundary shell, overspace/wormhole, atomic orbital variants |
| Fixture controllers | `Fixtures/GrinBasicVisualController.cs`, `BlackHoleMinimalFingerprint.cs`, `EinsteinRingMinimalFingerprint.cs`, `AtomicOrbitalGrinRoomController.cs`, `WormholeCheckpointSequencer.cs`, etc. | Well-exercised |
| Render test runner | `RendererCore/Testing/RenderTestRunner.cs` (284KB) | Core of CI test pipeline |
| Auto-calibration | `RendererCore/Testing/SceneAutoCalibrator.cs`, `LauncherAudit.cs` | Integrated calibration flow |
| TestBench UI | `UI/TestBenchController.cs`, `UI/TestBenchPanel.gd`, `UI/testbench_recipes.json` | 8 test recipes; dry-run, smoke/review/full presets |
| Performance tracking | `PerfScope.cs`, `PerfStats.cs` | Frame timing with overlay cost tracking |

### Observatory Scripts & Python Tools

| Feature | Key Files | Notes |
|---------|-----------|-------|
| Atomic Visual Observatory | `scripts/run_atomic_orbital_visual_observatory.sh`, `tools/atomic_orbital_visual_diff.py` | V0-V4 cells × shadings × optional contours; contact sheet output |
| Wormhole Structure Observatory | `scripts/run_wormhole_structure_observatory_quick.sh`, `tools/wormhole_structure_observatory_report.py` | Multi-panel visual; clean_curved, dual-reality, depth, domain_diagnostics, minimap |
| Domain audit visual | `scripts/run_domain_audit_visual.sh` | Off / telemetry_on / resolver_on comparison; heatmap suite |
| Diagnostic wireframe overlay | `tools/diagnostic_wireframe_overlay.py` | Multi-layer composite: beauty + wireframe + ownership + risk markers |
| Hit normal vector overlay | `tools/hit_normal_vector_overlay.py` | Normal glyph arrows over capture |
| Camera cross-section minimap | `tools/camera_cross_section_minimap_overlay.py` | Cartesian minimap inset |
| Visual contact sheet builder | `tools/build_visual_contact_sheet.py` | Labeled montage from sweep summary.json |
| Render health parse/regress | `tools/renderhealth_parse.py`, `tools/renderhealth_regress.py` | Telemetry extraction and regression |
| Image comparison | `tools/image_compare.py` | SSIM + MAD comparison |
| Characterization ledger | `tools/characterization_ledger/ledger_writer.py`, `ledger_analyze.py` | Fixture result ledger |

---

## Needs Cleanup

| Item | Location | Issue |
|------|----------|-------|
| WormholePrototypeRig | `Wormhole/WormholePrototypeRig.cs` (3798 lines, 148KB) | Functional monolith; needs decomposition into TopologyManager / PortalRenderer / ValidationBridge before release packaging |
| Backup files at root | `GrinFilmCamera.cs.bak`, `GrinFilmCamera.cs.bak0` (268KB), `GrinFilmCamera_RESEARCHMODE.cs.ref` (308KB), `RayBeamRenderer.cs.bak1`, `test_BB.tscn.bak`, `test - New.tscn.bak` | Confirm safe to remove; then delete or archive outside repo |
| Temp / copy files | `test.tscn7783285466.tmp`, `RayViz.cs - Copy.uid`, `options.gd - Copy.uid` | Clean before release |
| Root-level .tscn proliferation | 60 `.tscn` files at repo root | Most belong in `Fixtures/`; only `overspace_trophy_room_demo.tscn` is demo-worthy at root |
| `codex_patch.diff` | `codex_patch.diff` (32KB) | Stale patch artifact; review and remove if no longer needed |
| `%PROJECT_PATH%/` | Repo root | Phantom symlink directory; investigate origin and remove |
| Stale root-level notes | `dual reality.md`, `film_render_report.md` | Not part of the Docs structure; move or remove |

---

## In Progress

| Feature | Key Files | Last Modified | Status |
|---------|-----------|---------------|--------|
| TestBench recipe system | `UI/TestBenchController.cs` | May 13, 2026 | Recipe API still evolving; don't treat as stable API |
| Wormhole prototype rig | `Wormhole/WormholePrototypeRig.cs` | May 13, 2026 | Active experiment; not ready for packaging |
| Atomic orbital visual observatory | `Fixtures/AtomicOrbitalVisualObservatoryController.cs` | May 11, 2026 | New fixture; contact sheet pipeline stabilizing |
| Atomic orbital GRIN room | `Fixtures/AtomicOrbitalGrinRoomController.cs` | May 10, 2026 | Paired room fixture; recently revised |

---

## Experimental / Research Only

These systems have explicit "diagnostic-only" guardrails in their source or are explicitly marked experimental. Do not surface them as user-facing features in release packaging.

| Feature | Key Files | Guardrail |
|---------|-----------|-----------|
| Reference transport oracle | `RendererCore/Validation/ReferenceTransportOracle.cs` | File header: "diagnostic-only" annotation |
| Scene transport memory | `RendererCore/Scheduling/SceneTransportMemory.cs` | File header: "diagnostic-only" annotation |
| Metric heuristic integrator | `RendererCore/Transport/MetricHeuristicIntegrator.cs` | 4 open TODOs: metric-compatible RHS, curvature error control, Hamiltonian constraint, null constraint residual |
| GrinFilmCamera research mode | `GrinFilmCamera_RESEARCHMODE.cs.ref` | Reference file; not compiled |
| Derivative-aware stepping | Flag in `RayBeamRenderer.cs` | `UseDerivativeAwareStepping` defaults to `false` |
| Overspace trophy room demo | `overspace_trophy_room_demo.tscn`, `Wormhole/Overspace/OverspaceTrophyRoomDemo.cs` | Demo-quality; visually interesting but not a validated transport test |

---

## Proposed / Not Started

| Feature | Notes |
|---------|-------|
| Celestial Boundary Overlay | Highest-priority proposed overlay; ties directly to MisterY Labs narrative |
| Bulk-to-Boundary Dual View | Extend wormhole_dual_reality toolchain with boundary projection |
| Transport Memory visual overlay | Data model in `SceneTransportMemory.cs`; needs render path |
| S-Matrix Event Ledger | No file yet; log scatter-like in/out events at domain crossings |
| Correspondence Failure Heatmap | Oracle comparison data in `ReferenceTransportOracle.cs`; needs heatmap pass |
| Metric Grid / Stress-Tensor Style Overlay | Transport frame (U, V) data in `MetricTransportTypes.cs`; no visual |
| Soft Mode / IR Glow Overlay | Aesthetic; low priority |
| Curvature Domain Map overlay | Data in `DomainTelemetry.CurvatureDomainKind`; needs Python heatmap pass |

---

## Deprecated / Confusing / Duplicate

| File | Issue |
|------|-------|
| `HitPayload.cs` | 1-byte empty file; implementation never written; absorbed into transport types elsewhere — remove |
| `RendererCore/Transport/MetricRayState.cs.uid` | Orphan UID; implementation file (`MetricRayState.cs`) missing; absorbed into `MetricTransportTypes.cs` — remove UID |
| `RayEmitter3D.cs` | ~0.6KB stub; appears to be an abandoned placeholder — investigate and remove if unused |
| `RendererCore/Accel/` | Empty directory — remove |
| `RendererCore/Scheduler/` | Empty directory — remove |
| `test.tscn.obs`, `test.tscn.bbNew` | Obsolete scene variants — remove |
| `test - New.tscn.bak`, `test_BB.tscn.bak` | Backup scenes — remove |

---

## Files to Exclude from Release Packaging

The following should be excluded from any release archive, CI artifact, or demo distribution:

```
output/                          # 4.1GB accumulated test results
logs/                            # 419MB execution logs
site/                            # 192MB generated MkDocs site (rebuild on deploy)
.venv/                           # Python virtual environment (regenerate)
.venv_image_compare/             # Python virtual environment (regenerate)
tools/__pycache__/               # 1517 pycache directories
**/*.bak                         # All backup files
**/*.bak0, **/*.bak1
**/*.ref (GrinFilmCamera_RESEARCHMODE.cs.ref)
**/*.tmp
render_test_log_*.txt            # Root-level accumulated test logs
.env.local                       # Contains local machine paths
_Wormhole_figures/               # 192KB intermediate visualization figures
.claude/                         # Claude Code session data
.appdata/                        # Godot app data cache
```

---

## Recommended Next 5 Actions Before First Release

- [ ] **1. Stub & orphan cleanup** — Remove `HitPayload.cs`, `MetricRayState.cs.uid`, `RayEmitter3D.cs`, both empty `RendererCore/` directories, all `*.bak`/`*.tmp` files, and obsolete `.tscn` variants
- [ ] **2. Scene organization** — Move root-level test `.tscn` files into `Fixtures/` or a new `Scenes/` directory; keep only `overspace_trophy_room_demo.tscn` at root as the demo entry point
- [ ] **3. WormholePrototypeRig decomposition** — Split `WormholePrototypeRig.cs` (3798 lines) into logical subsystems before release packaging; it is the single largest structural risk
- [ ] **4. Curvature Domain Map overlay** — Wire `DomainTelemetry.CurvatureDomainKind` into a Python heatmap pass (data ready; render path missing)
- [ ] **5. Celestial Boundary Overlay** — First proposed overlay to implement; highest MisterY Labs narrative value and uses existing ray terminal angle data

---

*See also: [FEATURE_INDEX.md](../FEATURE_INDEX.md) | [OPTICAL_TRANSPORT_FEATURE_MAP.md](../Research/OPTICAL_TRANSPORT_FEATURE_MAP.md) | [OVERLAY_MASTER_LIST.md](../Observatory/OVERLAY_MASTER_LIST.md)*
