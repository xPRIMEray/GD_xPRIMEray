# Next-Generation Metric Transport Roadmap

This note summarizes the current `Metric_NullGeodesic` path, the gap to a persistent geodesic transport model, and a bounded implementation sequence that preserves the validated GRIN path.

## 1. Current State

### What `Metric_NullGeodesic` computes today

- `StepTransport_MetricStub()` computes GRIN acceleration first, then derives a weak-field scalar proxy from the first enabled source: roughly `|Amp| * betaScaleEff * BendScale * FieldStrength`.
- The metric branch uses that scalar plus the first source `ROuter` to evaluate a small direction change in `EvaluateMetricDirectionDeltaStub()`.
- The turn law is a heuristic envelope, not a geodesic solve. It bends only in the plane perpendicular to the current direction and source-center radial vector.
- The resulting direction delta is converted back into an equivalent 3-vector acceleration for the existing stepper.
- If the delta is zero or non-finite, the path falls back to GRIN.
- If `FieldGrid3D` is active and returns a cached acceleration sample, metric mode can be bypassed for that step and GRIN acceleration is used directly.

### What state is preserved vs not preserved

Preserved per ray today:

- World-space position `p`
- Unit direction `v`
- Scalar traveled distance
- Adaptive step length
- Output segment chain `RaySeg { A, B, TraveledB, RadiusBound }`
- Basic hit payload (`Position`, collider id/name, distance, absorbed flag)

Not preserved as persistent metric state:

- 4-position / coordinate time
- 4-momentum or wavevector
- Affine parameter
- Null-constraint drift or integration error
- Persistent bend basis / transport frame
- Impact parameter or other conserved quantities
- Source association beyond the first-source scalar proxy
- Segment-local tangent metadata at hit time

### How hit testing currently interacts with metric steering

- Pass-1 and pass-2 hit logic consume piecewise-linear world-space segments, not a metric ray state.
- `BuildRaySegmentsCamera_Pass1()` emits `RaySeg` polylines and optional probe raycasts on each straight segment.
- `SubdividedRayHit()` and related helpers raycast or sweep between segment endpoints; they do not consume geodesic invariants, tangent transport, or metric error bounds.
- As a result, metric steering influences hit testing only indirectly through the emitted polyline shape.
- The current metric path is therefore "heuristic steering into straight-segment hit tests," not persistent geodesic transport.

## 2. Validation Findings

- The GRIN visual ladder now works as a teaching baseline: straight vs minimal vs stronger is visually obvious, and GRIN is described as trustworthy enough for comparison.
- Metric diagnostics are active and useful for debugging, but the basic ladder still produces no useful source/background hit recovery. The March 15 sweep reports `16 / 16` metric captures with zero source/background hits.
- Off-axis symmetry breaking improved observability of metric steering itself: nonzero turn diagnostics appear off-axis, and the metric image is less locked into the radial zero-turn condition.
- That did not materially solve the actual visibility problem. The off-axis report still records zero source/background hits in the metric subset.
- The observe harness improved capture readiness and measurability, but again did not produce useful metric hits: `0 / 3` cases had nonzero source/background hits at capture, and `0 / 3` were judged easier to read.

## 3. Architectural Gap

### Additional per-ray state needed

At minimum, a next-generation metric path needs a persistent `MetricRayState` carrying:

- `x`: projected world-space position
- `k` or `p`: transported ray direction / momentum
- `lambda`: affine-like integration parameter
- `pathLength`: accumulated physical path length used by the renderer
- `dtLast` or `stepLast`: last accepted step
- `constraintDrift`: null-condition or normalization drift
- `errorEstimate`: local integration error
- `transportFrame`: persistent perpendicular basis for stable off-axis bending / rotation
- Optional fixture-specific invariants such as impact parameter or dominant source id

For the full Tier-2 destination, this should extend naturally to `RayState4 { x^mu, k^mu, lambda, constraintDrift }`, with 3-space projection remaining an output step.

### Where that state should live

- The state should live in the transport layer, not in `FieldSource3D` and not inside hit payloads.
- Near-term: define `MetricRayState` and `MetricStepResult` adjacent to the current segment builder, then thread them through the metric-only branch.
- Target architecture: move them behind the planned `IRayTransport` / `IMetricField` boundary in `RendererCore/Transport/`, with `RaySeg[]` retained as the universal output contract.

### How traversal / hit testing should consume it

- Traversal should continue to consume `RaySeg[]` for compatibility, but metric transport should also emit segment-local metadata such as endpoint tangents, accepted step size, and local error / radius bounds.
- Pass-1 quick probes can remain simple initially, but should use tighter cadence driven by metric curvature and accepted integration error.
- Pass-2 narrowphase should become "segment-chain aware" rather than "endpoint-line only": adaptive subdivision, swept-envelope testing, or future BLAS intersection should use the segment metadata produced by the metric integrator.
- Hit payloads should eventually retain segment index and local tangent at hit time so shading/debug can reason about the transported ray, not just the collider point.

## 4. Proposed Staged Roadmap

### Stage A: Improved persistent metric ray state

- Introduce `MetricRayState` and `MetricStepResult` without replacing the whole pipeline.
- Keep the current weak-field steering law as a temporary RHS, but stop treating each step as stateless.
- Persist a stable transport frame, affine/path parameter, last step, and drift/error diagnostics.
- Make metric-mode fallback explicit in telemetry instead of silently borrowing GRIN state.
- Prevent `FieldGrid3D` from acting as authoritative metric transport unless it can supply metric-specific RHS data.

Exit criteria:

- Metric mode still emits the same `RaySeg[]` contract.
- Diagnostics can report state drift, accepted/rejected steps, and fallback cause per ray.

### Stage B: Bounded geodesic integration interface

- Add a metric-step interface that advances `MetricRayState` from a metric RHS instead of from a direction-delta heuristic.
- Start with a bounded weak-field implementation, not full Kerr/Schwarzschild scope: a fixture-local metric evaluator plus RK2/RK4 or another stable non-stiff integrator is enough.
- Include null-state normalization / projection and step adaptation from curvature or constraint drift.
- Keep projection to 3-space explicit and continue emitting `RaySeg[]`.

Exit criteria:

- Metric stepping no longer depends on GRIN acceleration magnitude as a floor.
- The stepper owns its own accepted state and error budget.

### Stage C: Hit-test integration strategy

- Leave existing GRIN hit testing untouched.
- For the new metric path, consume emitted segment metadata in pass-1/pass-2 instead of assuming a plain straight probe is always enough.
- Short term: adaptive straight-segment probing using metric error bounds and tangent change.
- Medium term: use `RadiusBound` plus tangent metadata for swept-segment or capsule-style narrowphase.
- Record hit-on-segment details so detector/source/background classification can be debugged against the transported path.

Exit criteria:

- Metric fixtures produce segment/hit diagnostics that explain misses in geometric terms, not only steering terms.

### Stage D: Validation fixtures for the new path

- Keep the current GRIN ladder as the control path.
- Add a bounded metric fixture with known expected behavior: weak off-axis deflection, closest-approach trend, and at least some recoverable source/background hits.
- Promote the current metric ladder, off-axis ladder, and observe harness into acceptance tests for regression detection.
- Add numeric checks for:
  - nonzero source/background hits in the bounded metric fixture
  - monotonic deflection or closest-approach behavior
  - constraint-drift budget
  - reproducible segment-count / step-count envelopes

## 5. Risks and Compatibility Notes

- Preserve the current GRIN path as the validated baseline; do not route GRIN through the new metric stepper.
- Keep `RaySeg[]` as the interoperability boundary so renderer, film, and debug views remain stable while metric transport evolves.
- Land the new metric implementation behind an opt-in path or fixture flag until it can reproduce stable segment chains.
- Avoid changing pass-2 narrowphase and shading in the same stage as the new metric integrator; first prove transport, then tighten hit consumption.
- Treat current `FieldGrid3D` use in metric mode as a compatibility hazard, because it caches GRIN acceleration rather than metric state evolution.

## Recommended implementation order

1. Stage A: add persistent metric state and telemetry without changing GRIN.
2. Stage B: swap the metric RHS from heuristic delta to bounded geodesic stepping.
3. Stage C: make hit testing consume segment-local metric metadata.
4. Stage D: rebaseline with dedicated metric fixtures plus the existing GRIN/off-axis/observe ladders.
