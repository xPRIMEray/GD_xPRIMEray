# Multi-Object Causal Stress Fixture (test-multi-object-causal.tscn)

**Purpose**: Controlled verification of the Causal Probe + Turbo Scheduler upgrade (STEP 4+).

**Contents**:
- 5 objects (boxes + spheres) placed at deliberately varying depths along the optical axis (-6 to -35 units).
- One weak AtomicEigenmodeFieldSource3D (Amp ≈ 0.008, small ROuter) to introduce subtle metric perturbation without dominating transport.
- Standard GrinFilmCamera + RenderTestRunner harness with causal flags pre-enabled for easy `--causal-threads=N` testing.

**Usage**:
```bash
CAUSAL_THREADS=16 godot --headless --path . --scene "res://test-multi-object-causal.tscn" -- --render-test --render-test-frames=40 ...
```

**Expected observations**:
- `causal_object_count` should reflect real scene objects (≈5 in baseline).
- `probe_phase_ms` reports actual physics coarse ray cost (should be small but measurable).
- Causal ordering (MinDistance) should correlate with true geometric depth ordering, perturbed slightly by the weak field.
- No change to final rendered pixels when only used for scheduling diagnostics.

**Created**: 2026-05 during Causal Probe + Turbo Scheduler rollout.
**Stability note**: Fixture is intentionally simple to isolate probe quality from complex scene interactions.
