# xPRIMEray Optical Transport Feature Map

> **Lens: Optical Transport Architect**
> Audit date: 2026-05-14
> Scope: Ray/path transport engine feature classification and gap analysis

---

## Overview

xPRIMEray is not a physics simulator asserting GR truth. It is a **transport observatory**: a renderer that lets propagation, boundary behavior, curvature domains, GRIN fields, wormhole seams, and observer-relative transport reveal their own structure through visualization and diagnostics.

This map catalogs every transport-relevant system found in the codebase, classifies its completeness, and identifies what should become a formal engine feature.

---

## System Completeness Summary

| System | Status | Completeness | Formal Feature? |
|--------|--------|--------------|-----------------|
| GRIN Fields | Mature | ~90% | Yes — core |
| Curved Ray Transport | Functional | ~80% | Yes — core |
| Hit Detection | Mature | ~95% | Yes — core |
| Boundary Crossing | Complete | ~100% | Yes — core |
| Step Convergence | Functional | ~75% | Needs upgrade |
| Domain Resolver / Transport Ownership | Mature | ~90% | Yes — core |
| Oracle / Reference Probe Systems | Complete (diagnostic) | ~100% | Diagnostic only |
| Wormhole / Portal / Multi-Universe | Prototype | ~70% | In progress |
| High-Curvature Detection | Functional | ~80% | Needs visual surface |
| Transport Memory / Residual Path Effects | Schema only | ~30% | Proposed |
| Scattering-like In/Out State Logging | Missing | ~0% | Proposed |
| Metric Tensor / GR Language Hooks | Partial | ~60% | Planned |

---

## GRIN Fields (~90% complete)

**What exists:**

| File | Purpose |
|------|---------|
| `FieldSystem.cs` (`RendererCore/Fields/`) | Core scalar field acceleration: `AccelAt(Vector3 pWorld)`; queries FieldTLAS; evaluates overlapping sources; `MetricModel` enum (GRIN=0, GordonMetric=1) |
| `FieldSource3D.cs` (root, 2451 lines) | Authoring node; power / inverse-power / Gaussian / shell profiles; inner/outer radius bounds with u-clamped parameterization; extensive debug visualization (density zones, vectors, overlay rays) |
| `FieldMath.cs` (root) | `EvalFieldAccel`: u-clamping, edge softness, beta-scaling, atomic orbital density; profile evaluation with edge ramp and safety guards |
| `FieldCurves.cs` (`RendererCore/Fields/`) | Switch-based profile evaluation: Linear, Power, Polynomial, Exponential, AtomicOrbital |
| `FieldTLAS.cs` (`RendererCore/Fields/`) | BVH spatial acceleration: `QueryPoint()` and `QueryAabb()` for candidate field source enumeration |
| `CurvatureBoundGrid.cs` (`RendererCore/Fields/`) | Pre-computed Kmax grid; 3D grid around camera position; enables curvature-aware step-size adaptation |
| `AtomicEigenmodeFieldSource3D.cs` (root) | V1 macro-scale atomic-orbital authoring node; `OrbitalRadius` (default 3.5), `SupportRadius = 4 × OrbitalRadius`; density: `exp(-2r / orbitalRadius)` |
| `FieldModels.cs` (`RendererCore/Fields/`) | Enums: `MetricModel`, `FieldShapeType` (SphereRadial, BoxVolume), `FieldCurveType` |
| `FieldGrid3D.cs` (root) | Field grid implementation |

**Known gaps:**
- BoxVolume distance model: marked TODO in `FieldSystem.cs:59`
- 1/r² behavior flags: defined in `FieldModels.cs`, not wired in `FieldMath.cs`
- TLAS candidate pruning: marked TODO in `FieldSystem.cs:19`
- Legacy `overrideBetaScale` controls in `FieldSource3D.cs` should be deprecated cleanly

---

## Curved Ray Transport (~80% complete)

**What exists:**

| File | Purpose |
|------|---------|
| `IIntegrator.cs` (`RendererCore/Transport/`) | Interface: `Step(MetricRayState, dt, IMetricField, SceneSnapshot)` |
| `IMetricField.cs` (`RendererCore/Transport/`) | Interface: `AccelAt(Vector3, SceneSnapshot)` |
| `MetricHeuristicIntegrator.cs` (`RendererCore/Transport/`) | Primary integrator; 2-point midpoint with adaptive step; `MaxTurnRadiansPerStep = 0.35`; error estimation via acceleration delta and turn penalty; constraint drift monitoring (normalization proxy) |
| `MetricTransportTypes.cs` (`RendererCore/Transport/`) | `MetricRayState`: position, direction, affine parameter, path length, transport frame (U, V), constraint drift, step count, fallback cause |
| `StepResult.cs` (`RendererCore/Transport/`) | Step output: NewState, ErrorEstimate, ConstraintDrift, RecommendedDt |
| `StepPolicy.cs` (`RendererCore/Integrators/`) | `ComputeDt(kmax, epsPos) = sqrt(2·epsPos / kmax)` |
| `MetricSegmentCompatibility.cs` (`RendererCore/Transport/`) | Bridge: converts integrator outputs to segment payloads; `RaySegCompatibleSegmentPayload`, `MetricSegmentMetadata` |
| `RayBeamRenderer.cs` (root, 6616 lines) | Master ray generation, integration, and collision; 70+ control parameters; adaptive collision cadence; screen-space error budgeting; `UseDerivativeAwareStepping` (experimental, default off) |

**Open TODOs (from `MetricHeuristicIntegrator.cs`):**
1. Line 96–98: 3-space perpendicular projection needs metric-compatible replacement
2. Line 106–108: Curvature heuristic needs error-controlled policy
3. Line 114–117: Placeholder error bridge until Hamiltonian constraint available
4. Line 120–127: Constraint drift is normalization proxy only; needs true null constraint residual

**Open TODOs (from `MetricSegmentCompatibility.cs`):**
- Three "metric-pass1" TODOs for GR integrator threading

**Known gaps:**
- No true RK4 or embedded error pair; current midpoint method is heuristic
- Perpendicular projection in stepping not metric-compatible
- Hamiltonian null constraint not monitored — only normalization drift
- Metric pass-1 threading (for future GR-accurate geodesic path) incomplete
- DerivativeAwareStepping disabled by default; behavior under this flag untested in production

---

## Hit Detection (~95% complete)

**What exists:**

| File | Purpose |
|------|---------|
| `RendererCore/Geometry/GeometryTLAS.cs` | BVH AABB query for geometry collision candidates |
| `RayBeamRenderer.cs` (collision subsystem) | Sphere sweep + raycast; adaptive collision cadence; screen-space error budgeting; hit normal extraction |
| `HitPayload.cs` (root) | 1-byte empty stub — implementation absorbed elsewhere; **remove** |

**Known gaps:**
- `HitPayload.cs` should be deleted (misleading empty file)
- Collision cadence tuning remains heuristic; no principled budget for cadence adaptation

---

## Boundary Crossing (~100% complete)

**What exists:**

| File | Purpose |
|------|---------|
| `BoundaryLayerVolume.cs` (root) | Volumetric region affecting ray behavior |

**Capabilities:**
- `BoundaryExecutionMode`: Continuous, CrossingEvent
- `BoundaryCrossingPolicy`: EntryOnly, ExitOnly, EntryAndExit
- `BoundaryBehavior`: DirectionBias, SceneTransform
- Per-ray uint bitmask (`ComputeInsideMask`) for multi-region state tracking
- Linked destination shell for portal topology
- `DebugLogCrossings` flag

**Status:** This system appears production-complete. No TODOs found.

---

## Step Convergence (~75% complete)

**What exists:**
- 2-point midpoint method in `MetricHeuristicIntegrator.cs`
- Error estimation via acceleration delta and turn penalty
- `MaxTurnRadiansPerStep = 0.35` as primary angular constraint
- `StepPolicy.ComputeDt`: curvature-based dt sizing

**Known gaps:**
- Error-controlled adaptive policy missing; curvature heuristic is not rigorous
- Hamiltonian constraint monitoring is a placeholder; true null constraint residual not computed
- No embedded Runge-Kutta pair for automatic error control
- `MetricHeuristicIntegrator.cs` contains 4 explicit TODO notes calling out these gaps

**Formal feature candidate:** A proper error-controlled integrator (RK45 or similar embedded pair) should replace or extend the current midpoint heuristic for the metric pass.

---

## Domain Resolver / Transport Ownership (~90% complete)

**What exists:**

| File | Purpose |
|------|---------|
| `RendererCore/Scheduling/ObjectSeededTileScheduler.cs` (665 lines) | Transport-aware scheduling; `TransportAnchorMode` (Centroid, AabbCorner, PrincipalAxis, PortalThroatCenter, DomainBoundarySample, EdgeMidpoint); `TransportObserverKind` (GeometryObject, Domain, Portal); `DecisionRisk` struct (collider/domain/portal/distance/normal/path/resolver/boundary mismatches); `CharacteristicProbeResult` |
| `RendererCore/Common/DomainTelemetry.cs` | `CurvatureDomainKind` enum: MouthNear, ThroatBridge, FarWall, TangentialFar, Background, BoundaryMixed; `DomainSignature` struct with confidence and stability scores |
| `FieldTLAS.cs` | Spatial acceleration for field source → domain lookup |
| `GrinFilmCamera.cs` (25,468 lines) | Main resolver logic lives here; too large to fully audit in isolation |

**Diagnostic heatmaps produced by domain_audit_visual pipeline:**
- `domain_id.png` — domain index map
- `domain_confidence.png` — resolver confidence per pixel
- `boundary_confidence.png` — boundary layer confidence
- `normal_discontinuity.png` — surface normal discontinuity at resolver impact zones
- `selection_flip.png` — domain selection change events

**Known gaps:**
- Resolver internals inside `GrinFilmCamera.cs` are not isolated; difficult to audit or test in isolation
- `CurvatureDomainKind` telemetry not yet surfaced as a visual overlay (data exists; render path missing)

---

## Oracle / Reference Probe Systems (~100% as diagnostic)

These systems are intentionally diagnostic-only and are not part of the rendering pipeline.

| File | Purpose |
|------|---------|
| `RendererCore/Validation/ReferenceTransportOracle.cs` | Best-known reference transport paths; `OracleIntegrationSettings`, `ParentTrajectoryRecord`, `OracleSegmentRecord`, `EpsilonStabilityClass` (Stable / ThresholdSnap / Unresolved / MultiSolution); `ProductionOracleComparisonRecord` |
| `FieldProbe3D.cs` (root, 930 lines) | Debug field sampling; finite-difference gradient; density ring visualization; telemetry readout overlay |
| `RendererCore/Scheduling/SceneTransportMemory.cs` | Passive validation records: `TransportCoherenceBasinRecord`, `UnstableSeamRecord`, `RequiredPrecisionRegionRecord`, `LocalTransportFingerprintRecord` |
| `Wormhole/StraightRayReferenceCache.cs` | Async straight-ray reference cache; dual-reality comparison baseline |

**Status:** All four systems work as intended. Their "diagnostic-only" guardrails should be preserved. They feed the Python analysis toolchain, not the renderer.

---

## Wormhole / Portal / Multi-Universe (~70% complete)

**What exists:**

| File | Purpose |
|------|---------|
| `Wormhole/WormholePortal.cs` (450 lines) | Throat geometry; handedness + spin; SubViewport-based portal rendering; domain boundary linking |
| `Wormhole/WormholePrototypeRig.cs` (3798 lines) | Main orchestrator; multi-universe graph topology; portal anchor/link tracking; throat reconstruction; collision overlay; domain resolution |
| `Wormhole/Overspace/UniverseGraph.cs` | Hierarchical graph of worlds, density layers, anchors, portal links |
| `Wormhole/Overspace/WorldNode.cs`, `PortalLink.cs`, `PortalAnchor.cs`, `OverspaceProfile.cs`, `DensityLayer.cs` | Complete graph schema (small files, 20–108 lines each) |
| `Fixtures/WormholeCheckpointSequencer.cs` (30KB) | Checkpoint-by-checkpoint wormhole traversal test |
| `Wormhole/WormholeResearchOverlay.cs`, `WireframeReferenceOverlay.cs`, `CameraSpaceCollisionOverlay.cs`, `Wormhole/Overspace/OverspacePortalDebugOverlay.cs` | Visualization overlays for portal research |

**Scene coverage (Fixtures/):**
- witness (mouth / throat / exit variants)
- checkpoint sequence
- mouth-throat interpolation
- throat-exit interpolation
- hermetic overspace room (+ field/topology variants)

**Known gaps:**
- Phase-lock remapping stability in `WormholePortal.cs` not documented or tested
- `WormholePrototypeRig.cs` (3798 lines) is a monolith; needs decomposition before release
- Portal rendering uses SubViewport, not ray transport — the wormhole is visual but not optically consistent with the curved-ray transport
- No formal definition of "seam stability" vs "seam instability" in transport terms

---

## High-Curvature Detection (~80% complete)

**What exists:**

| File | Purpose |
|------|---------|
| `RendererCore/Fields/CurvatureBoundGrid.cs` | Pre-computed Kmax grid; 3D cells around camera; drives adaptive step sizing |
| `MetricHeuristicIntegrator.cs` | `MaxTurnRadiansPerStep` cutoff; `RecommendDt` scales from curvature |
| `FieldSource3D.cs` | Curvature visualization debug modes (density zones, vectors) |

**Known gaps:**
- No formal high-curvature region event detection surfaced to diagnostics
- No visual overlay for Kmax grid (data exists; render path missing — see High-Curvature Oracle Overlay proposal)
- Curvature detection is heuristic (turn-angle budget), not a rigorous bound

---

## Transport Memory / Residual Path Effects (~30% — schema only)

**What exists:**
- `RendererCore/Scheduling/SceneTransportMemory.cs` — Data schema: `TransportCoherenceBasinRecord`, `UnstableSeamRecord`, `RequiredPrecisionRegionRecord`, `LocalTransportFingerprintRecord`

**What is missing:**
- No visual overlay for coherence basins or seam records
- No integration into the rendering pipeline (records are populated diagnostically; not fed back to transport decisions)
- No "memory-informed transport" feature where prior path data influences current step decisions

**Formal feature candidate:** Transport Memory Overlay — render coherence basins as a heatmap, seam fragmentation as red zones, precision floors as contour bands.

---

## Scattering-like In/Out State Logging (~0% — missing)

No file in the codebase logs scatter-like in/out state at domain crossings. `BoundaryLayerVolume.cs` tracks which volumes a ray is inside (bitmask), but does not log entry/exit events in a form suitable for scatter-matrix style analysis.

**Proposed feature:** S-Matrix Event Ledger — per-ray per-crossing log of: crossing position, incoming domain, outgoing domain, normal direction, transport state before and after. Would enable bulk-boundary correspondence analysis without a full oracle replay.

---

## Metric Tensor / GR Language Hooks (~60% partial)

**What exists:**
- `MetricTransportTypes.cs`: `MetricRayState` includes transport frame (U, V) — partial tetrad-like structure
- `IMetricField.cs`: Interface for metric-compatible acceleration
- `MetricHeuristicIntegrator.cs`: References to "metric-compatible transport once bounded geodesic RHS available"
- `RayBeamRenderer.cs`: `MetricSteeringLaw` enum; `MetricAdaptive*` parameters

**What is missing:**
- Full metric tensor components (g_μν) not stored or evaluated
- Christoffel symbols / connection coefficients not computed
- True geodesic equation (d²x/dλ² + Γ·ẋ·ẋ = 0) not integrated
- Steering law is still a heuristic surrogate, not a metric contraction

**Formal feature candidate:** Metric Pass 2 — implement a proper bounded geodesic integrator using numerical Christoffel symbols from field data, replacing the current midpoint heuristic for research-grade accuracy.

---

## What Should Become a Formal Engine Feature

Based on this audit, the following items are candidates for formalization (adding to the engine's public feature set, not just internal diagnostics):

1. **Error-controlled adaptive integrator** — Replace midpoint heuristic with RK45 or similar embedded pair
2. **Curvature Domain Map overlay** — Wire `CurvatureDomainKind` into a visual heatmap pass
3. **Transport Memory Overlay** — Surface `SceneTransportMemory` coherence basins visually
4. **S-Matrix Event Ledger** — Log boundary crossing in/out state for scatter-like analysis
5. **High-Curvature Oracle Overlay** — Render `CurvatureBoundGrid` Kmax as a threshold heatmap
6. **Celestial Boundary Overlay** — Map ray terminal angles onto a reference sphere shell
7. **Metric Pass 2 integrator** — True geodesic integration for research-grade transport

---

*See also: [FEATURE_INDEX.md](../FEATURE_INDEX.md) | [FEATURE_READINESS_AUDIT.md](../Release/FEATURE_READINESS_AUDIT.md) | [OVERLAY_MASTER_LIST.md](../Observatory/OVERLAY_MASTER_LIST.md)*
