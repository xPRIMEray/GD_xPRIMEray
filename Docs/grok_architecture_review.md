xPRIMEray is a Godot 4.5 C# research platform / "optical transport observatory" for solving and visualizing true curved-ray null-geodesic transport through gradient-index (GRIN) media and Gordon effective metric fields. It does not fake curvature with post-process distortion or lens shaders.

Every render is held to a strict hermetic fixture contract: 100% pixel classification, zero unresolved exits. Pass 1 (transport) is authoritative truth; Pass 2 only analyzes it.

High-Level Architecture

Godot Scene Graph (authoring + display)
          │
          ▼
GodotAdapter (SnapshotBuilder)
          │
          ▼
RendererCore (engine-agnostic, data-oriented core)
    ├── SceneSnapshot (immutable SOA)
    ├── Acceleration (GeometryTLAS + FieldTLAS + CurvatureBoundGrid)
    ├── FieldSystem (GRIN/Gordon evaluation)
    ├── Integrators + RayChunks
    ├── Intersection (BVH over conservative envelopes)
    ├── Scheduling (ObjectSeededTileScheduler, domain-aware anchors)
    └── Validation / Telemetry
          │
          ▼
RenderBackends (abstraction layer — currently thin)
          │
          ▼
Film + Overlays (FilmOverlay2D, RayViz, shaders, Python tooling)

Godot owns: scene extraction, input, UI, final display, some GDScript (camera, options, world env).
RendererCore owns: simulation, acceleration, integration, intersection, scheduling. It is deliberately portable.

Two-Pass Model

┌──────┬─────────────┬───────────────────┬──────────────────────────────────────┐
│ Pass │ Name        │ Purpose           │ Guarantees / Rules                   │
├──────┼─────────────┼───────────────────┼──────────────────────────────────────┤
│ 1    │ Transport / │ Authoritative ray │ Hermetic: classified_coverage_ratio  │
│      │ Scout       │ integration +     │ == 1.0, no escaped_no_hit, no        │
│      │             │ classification    │ unclassified. Produces the stored    │
│      │             │                   │ -hit table (position, normal,        │
│      │             │                   │ materialId, transportClass, OPL,     │
│      │             │                   │ crossings, segments).                │
├──────┼─────────────┼───────────────────┼──────────────────────────────────────┤
│ 2    │ Research /  │ Everything else   │ Operates only on the stored-hit      │
│      │ Diagnostic  │                   │ table from Pass 1. Never modifies    │
│      │             │                   │ classifications.                     │
└──────┴─────────────┴───────────────────┴──────────────────────────────────────┘

Core Data & Execution

• SceneSnapshot: Immutable per-frame contract. SOA layout (InstanceSOA, FieldEntitySOA, GeometryEntitySOA, PackedParamBuffer, dual TLASes, CurvatureBoundGrid).
• Fields: Multiple FieldSource3D (and AtomicEigenmodeFieldSource3D) nodes define local metric/GRIN regions (sphere radial, box, various profiles). Evaluated via FieldSystem.AccelAt + FieldTLAS.
• Integration: Adaptive RK4-style stepping (curvature-aware step controller using κ and κ̇). Emits conservative RayChunks (p0/p1 + radius bound) for safe BVH traversal. Supports both pure GRIN and Metric_NullGeodesic (Gordon proxy with steering laws).
• Intersection: Renderer-owned BVH (not Godot's). TLAS → instance → BLAS → triangle test over RayChunks.
• Rendering style: Progressive RenderStep (band/row-based, time/pixel/segment budget-capped). Supports SmartScale, adaptive envelopes, long-running sweeps, and "hermetic observatory" fixtures. Not a simple per-frame full render.

Key Runtime Nodes

• GrinFilmCamera (central orchestrator, ~large): All the quality modes, preset systems, RenderStep caps, research toggles, Pass 1/2 controls, telemetry, debug overlay hookup.
• RayBeamRenderer (6.6k LOC heart): Ray marching params, field controls, BuildRaySegmentsCamera_Pass1, collision, film writing, debug ray bundles.
• ObservatoryModeController: Ctrl+1..7 hotkeys for coherent overlay presets (Observer, Geometry, Ownership, Risk, Oracle, Presentation, TraversalEmergence).
• FilmOverlay2D + RayViz: 21+ active diagnostic overlay modes (step budget heatmaps, domain maps, curvature contours, transport ownership, seam visualization, etc.).
• Wormhole / Overspace subsystem: WormholePortal, WormholePrototypeRig, Overspace/* (UniverseGraph, PortalLink, WorldNode, throat/mouth/exit witness fixtures), dual-reality transport.

Specialized Research Systems

• Domain emergence: Domains (Near-side / Bridge / Far-side in wormhole ladders) are discovered from transport signatures (k-means on stored-hit metrics), not assigned by geometry.
• Hermetic validation: ReferenceTransportOracle, SceneTransportMemory, BoundaryLayerVolume, fixture fingerprinting, auto-calibration (SceneAutoCalibrator).
• Scheduler: ObjectSeededTileScheduler, TransportAnchor modes (centroid, throat center, domain boundary, etc.), characteristic probes.
• Cathedral Probe infrastructure and multi-layer diagnostics.
• TraversalEmergenceSequencer for staged research reels.
• Heavy Python tooling chain (contact sheets, wireframe overlays, domain audits, image comparison, etc.).

Design Invariants (sacred)

• Scene snapshots are immutable per frame.
• Pass 1 classifications are never overridden by diagnostics.
• Curved rays are first-class (RayChunks + adaptive integration), not straight-line approximations.
• Intersection is renderer-owned.
• Hot paths are allocation-free / data-oriented (SOA, frame arenas).
• Telemetry is always on.
• Subsystems communicate only through explicit contracts.

Current Maturity (Phase 0)

• 20+ production-ready systems
• 46 fixture scenes (hermetic curved rooms, minimal blackhole/Einstein ring, off-axis observe, atomic orbital GRIN, multiple wormhole witness variants, boundary shell policies, overspace topology variants, etc.)
• 21 active overlay modes + many proposed
• Strong research focus on wormhole observer ladders, atomic eigenmode fields, transport islands, phase coherence, scheduler resonance, and causal boundary visualization

Key Documentation

• Docs/architecture/overview.md (Docs/architecture/overview.md) — pipeline + design invariants
• Docs/architecture_overview.md (Docs/architecture_overview.md) — detailed subsystem breakdown
• Docs/FEATURE_INDEX.md (Docs/FEATURE_INDEX.md) + Release/FEATURE_READINESS_AUDIT.md
• Docs/Observatory/OVERLAY_MASTER_LIST.md (Docs/Observatory/OVERLAY_MASTER_LIST.md)
• Docs/Research/ (Docs/Research/) — many deep technical notes (cathedral probe, domain ownership, curved ray transport model review, etc.)
• Docs/SPEC_INDEX.md (Docs/SPEC_INDEX.md) and individual spec_*.md files

The project is deliberately structured as a visual research instrument first and a renderer second. The "truth" is whatever the hermetic transport pass classifies; everything else is interpretation and discovery.


=============================================================================
=============================================================================
=============================================================================


Grok; Key files I can see (fresh exploration):

scripts/ (49 shell scripts — the primary Linux harness entrypoints):
• godot_local.sh (universal launcher)
• run_atomic_orbital_visual_observatory.sh, run_atomic_orbital_grin_ladder.sh
• run_wormhole_structure_observatory_quick.sh + several run_fixture_00*.sh (001–013 covering topology/field/wormhole-witness/checkpoint/interpolation variants)
• run_domain_audit_visual.sh, run_domain_audit_quick.sh
• run_transport_ownership_graph_precision_sweep.sh, run_tile_commit_traversal_*, run_reference_transport_oracle_*, run_hermetic_*, run_doe_* (overnight/sensitivity/resonance), run_grin_observe_*, run_resonance_chamber_overlay_quick.sh, etc.
• Supporting: launch_*, record_*, build_*_from_frames.sh, setup_grin_observe_capture_tools.sh, check_gpu_runtime.sh

tools/ (~70 .py + heavy __pycache__):
• Paired analysis/report scripts matching the run_*.sh (e.g. atomic_orbital_visual_diff.py, wormhole_structure_observatory_report.py, transport_ownership_graph_*, reference_transport_oracle_*, doe_*_analysis.py, tile_commit_traversal_analysis.py, hermetic_*_analysis.py)
• Shared utilities: image_compare.py, build_visual_contact_sheet.py, diagnostic_wireframe_overlay.py, renderhealth_parse.py + renderhealth_regress.py, smartscale_*.py, probe_audit.py, characterization_ledger/, etc.
• README_test_harness.md, README_renderhealth.md, requirements.txt (minimal, Pillow+)

output/ (78 dated study folders, 4.2 GB total):
• atomic_orbital_visual_observatory/, atomic_orbital_grin_ladder/, wormhole_structure_observatory/, wormhole_DR_*, transport_ownership_graph_precision_sweep/, domain_audit*, telemetry_* (many adaptive variants), tile_commit_traversal_*, reference_transport_oracle_*, doe_*, hermetic_*, fixture_*, testbench/, v0.0-pre/, render_test_*, observer_disagreement/, etc.
• Typical layout inside each: <study>.log, per-cell/case/panel subdirs with run.log + metadata.json + status.txt + *.png (beauty/heatmaps with encoded config in names) + *.csv/ *.json telemetry + post-proc contact sheets / reports / graphs.

Root:
• 30+ run_*.bat (Windows equivalents: visual sweeps, render_test blackhole/curved/Einstein pairs + fast_compare, smartscale, overspace)
• Physical Light and Camera Units.csproj + .sln
• Many test-*.tscn / fixture_*.tscn + Fixtures/*Controller.cs

Godot core:
• RendererCore/Testing/RenderTestRunner.cs (CLI parser, matrix driver, capture engine)
• GrinFilmCamera.cs (TestRunConfig, RenderStep, capture hooks)
• GodotAdapter/SnapshotBuilder.cs

Other:
• Docs/Research/ (many notes hard-reference specific output/ paths and harness commands)
• .venv (symlink to system python3; some harnesses use .venv_image_compare)

No Makefile/justfile or top-level AGENTS.md visible in root scan.

1–2. Main Test Harness Scripts & How output/ Folders Are Generated

Primary mechanism (Linux/WSL daily driver): The run_*.sh scripts in scripts/.

Categories:
• Visual observatory / multi-cell (atomic orbital, wormhole panels): rich per-cell metadata, shading variants, fixed-camera captures.
• Fixture baselines (run_fixture_00*.sh): simpler, ledger-oriented or single-artifact + report.
• Domain / telemetry / coherence (domain_audit_, tile_commit_, transport_coherence_basin_*): telemetry toggles, resolver on/off, step-convergence maps.
• Long-running research sweeps (DOE overnight/resonance/sensitivity, transport_ownership_graph_precision_sweep, reference_transport_oracle_*): hours-long param matrices (steps, strides, traversals, ROIs, radii).
• Hermetic / validation ladders + RenderHealth regression (via RenderTestRunner matrix + renderhealth_* Python).
• Specialized: grin_observe recording, resonance chamber overlays, dual-reality storytelling.

Windows side: Root run_*.bat files (mostly direct Godot scene + arg launches for quick visual or paired straight-vs-curved compares). Less structured output layout than the .sh harnesses.

Python post-processors are first-class: almost every serious .sh ends by invoking its matching tools/<name>.py $OUTPUT_DIR (sometimes with env vars). Some export-style tools are driven directly (GODOT_EXE=... python tools/xxx_export.py --output-dir ...).

3. Workflow: Fixture Scene → Render Run → Post-processing → output/ Folder

1. Fixture definition — .tscn (e.g. test-atomic-orbital-visual-observatory.tscn) + optional Fixtures/*Controller.cs + FieldSource3D (or AtomicEigenmode...) nodes in scene. Some registered as RenderTestFixture enum values in RenderTestRunner.cs. CLI overrides (--atomic-*, --wormhole-*, domain flags) allow runtime variation without scene edits.

2. Harness launch — bash scripts/run_<study>.sh (or with SMOKE=1, MAX_HOURS=..., --medres/--hires, env vars for res/frames/budget/filters). Script:
   • Computes timestamped or labeled OUTPUT_DIR under output/<study>/...
   • dotnet build
   • Loops over cells / panels / param combos / ROIs
   • Invokes scripts/godot_local.sh --headless --path . --scene $SCENE -- --render-test --render-test-fixture=XXX [~15–30 flags: capture dir/mode, film res/scale/shading, frames/warmup, step-length, steps-per-ray, pixel-stride, first-pass-traversal, camera-fixed, enable-domain-telemetry, atomic-*, wormhole-*, benchmark-deterministic, etc.] > per_case/run.log 2>&1

3. Inside Godot (headless) — RenderTestRunner (attached to scene or via test.tscn) parses args, applies TestRunConfig overrides to GrinFilmCamera, drives progressive RenderStep (budget-capped bands), writes:
   • PNG frame captures (named with full config + runid)
   • Sidecar telemetry (CSV/JSON per cell)
   • Logs with [RenderTestRunner] markers and optional forced exit codes for harness control
   • Deterministic seeding when requested

4. Capture + per-invocation artifacts land directly in the harness-provided --render-test-capture-dir (cells/, panels/, $label/ subdirs) alongside metadata.json, status.txt, run.log.

5. Post-processing phase (still inside the .sh) — python tools/<matching_analysis>.py "$OUTPUT_DIR" (or subdir). Walks tree, reads metadata/PNGs/CSVs/run logs, produces:
   • Contact sheets / grids
   • Diffs, classification deltas, heatmaps
   • Statistical summaries, graphs, ownership graphs, reports (.md + .json)
   • All written back into the same timestamped output/ dir (self-contained, auditable)

6. Result — Timestamped, fully reproducible, metadata-rich folder ready for comparison, git (sometimes), or further Python tooling. Older runs stay for regression.

Godot local launcher (godot_local.sh) is critical: forces .NET/mono build, sets GALLIUM_DRIVER=d3d12 for WSL GPU accel, handles PATH + common install locations + Windows /mnt/c/ fallback (with safeguards).

Direct Python-driven paths exist for lighter export/visual-diff cases (see observer disagreement docs).

4. Top 5 Most Valuable Things You Can Do Right Now (prioritized)

1. Analyze the latest high-value runs (highest leverage for insight):
   • output/wormhole_structure_observatory/ (recent)
   • output/transport_ownership_graph_precision_sweep/ (latest timestamp)
   • output/atomic_orbital_visual_observatory/ (latest)
   • output/tile_commit_traversal_* or domain_audit_visual/
   • Command: python tools/wormhole_structure_observatory_report.py output/wormhole_structure_observatory/<latest> --quality quick_review (or equivalent for the study). Then inspect the emitted .md / contact sheets.

2. Run a fresh controlled harness (smoke first):
   • SMOKE=1 bash scripts/run_atomic_orbital_visual_observatory.sh
   • bash scripts/run_domain_audit_visual.sh --medres
   • GRAPH_SWEEP_SMOKE=1 bash scripts/run_transport_ownership_graph_precision_sweep.sh
   • These are safe, produce clean timestamped output/, and exercise the full capture + postproc pipeline.

3. Compare two runs (straight vs curved, telemetry on/off, scheduler variants, etc.):
   • Use tools/image_compare.py, tools/build_visual_contact_sheet.py, or study-specific diff tools.
   • Example: pick two atomic or domain_audit_visual runs and generate side-by-side + delta sheets.
   • RenderHealth regression: python tools/renderhealth_regress.py logs/your.log (or the scheduler compare harness).

4. Reproduce / extend a specific research packet (e.g. observer disagreement, hermetic hit closure, reference oracle island):
   • Follow the exact commands in the corresponding Docs/Research/*.md (many give full GODOT_EXE=... python tools/... --output-dir output/... invocations).
   • Then run the matching contact-sheet builder.

5. Audit / clean / inventory the output/ corpus:
   • Identify largest or oldest folders.
   • Run python tools/probe_audit.py or similar against recent telemetry_* / domain_* dirs.
   • Generate a summary manifest of all 78 runs (study + date + key flags from metadata.json).
   • This surfaces what is still scientifically valuable vs. cruft.

Bonus high-ROI: Drive a new "observer ladder" or "curvature domain map" visual packet using the existing atomic/wormhole/dual-reality tooling + one of the proposed overlays from Docs/Observatory/OVERLAY_MASTER_LIST.md.

5. Useful Aliases & Agent Commands for Daily Research Flow (WSL)

Add to ~/.bashrc (or project-local):

# Core
alias gdl='~/code/godot_xPRIMEray/scripts/godot_local.sh'
alias godot-headless='~/code/godot_xPRIMEray/scripts/godot_local.sh --headless --path ~/code/godot_xPRIMEray'

# Quick harnesses (smoke by default for safety)
alias ao-smoke='SMOKE=1 bash ~/code/godot_xPRIMEray/scripts/run_atomic_orbital_visual_observatory.sh'
alias worm-smoke='bash ~/code/godot_xPRIMEray/scripts/run_wormhole_structure_observatory_quick.sh'
alias da-viz='bash ~/code/godot_xPRIMEray/scripts/run_domain_audit_visual.sh --medres'

# Navigation + inspection
alias out='cd ~/code/godot_xPRIMEray/output && ls -1tr | tail -20'
alias latest-out='ls -1d ~/code/godot_xPRIMEray/output/*/$(ls -1tr ~/code/godot_xPRIMEray/output/ | tail -1) 2>/dev/null | tail -1'

# Common post-proc patterns
alias contact='python3 ~/code/godot_xPRIMEray/tools/build_visual_contact_sheet.py'
alias imgcmp='python3 ~/code/godot_xPRIMEray/tools/image_compare.py'

Agent / daily prompt patterns you can reuse:
• "List the 5 most recent output/* directories with their top-level files and sizes. Then summarize the newest wormhole_structure_observatory run."
• "Compare the two latest atomic_orbital_visual_observatory runs: generate a delta contact sheet and note any classification or step-budget differences."
• "Run the smoke version of run_transport_ownership_graph_precision_sweep.sh, then after it finishes analyze the output with the matching Python tool and extract the key precision findings into a short report."
• "Audit all output/telemetry_* folders from the last 7 days and flag any that are missing post-processing reports."

Workflow hygiene tips:
• Always start with SMOKE=1 or short frame counts when exploring new params.
• Use dotnet build explicitly before big sweeps (harnesses usually do it).
• Capture full command + env in the run log (the good .sh scripts already do this).
• For very long runs, use nohup ... > harness.out 2>&1 & or screen/tmux and monitor the per-cell run.log files.
• Python discovery prefers .venv/bin/python3 — the symlinks here are minimal, so have system numpy/Pillow/scikit-image available or pip install into the venv as needed.

The harness system is mature, self-documenting via metadata + logs, and designed for exactly this kind of iterative research + visual validation loop. The output/ corpus is the real asset.

Ready for the next command — pick a specific run to analyze, a harness to launch, or a comparison to perform.