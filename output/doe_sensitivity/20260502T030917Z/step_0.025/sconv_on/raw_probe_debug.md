# Step-Convergence Probe Audit

## Root-Cause Analysis

### Probe Redesign: Position-Shifted Step Windows

The original `ComputeStepConvergenceProbe` subdivided the SAME segment `[A,B]`
with 2×/4×/0.5× substep counts. Since the physics hit exists within `[A,B]`,
all probes found the same collider → confidence=1, sensitivity=0, precisionRequired=0.

**Root cause:** substep-count variation on a known-hit segment is blind to
step-position sensitivity. The fix uses four position-shifted probes at
±0.125s and ±0.5s offsets (where s=segLen). A shifted probe that finds a
different collider means the pixel result is sensitive to step-grid alignment.

### Expected Behavior After Fix

- `step_sensitivity_mean > 0` in sconv_on runs where banding is visible
- `probe_collider_mismatch_mean` scales with banding intensity
- `precision_required` shows ≥0.5 in boundary-region pixels
- `pearson(probe_collider_mismatch, step_sensitivity) ≈ 1.0` (they're derived together)

## Cell: step_0.025/sconv_on

**Step length:** `0.025`  
**Dir:** `output/doe_sensitivity/20260502T030917Z/step_0.025/sconv_on`

### Maps present

- `step_sensitivity`: mean=0.00000  std=0.00000  max=0.000  nonzero=0.000
- `step_convergence_confidence`: mean=0.25889  std=0.43802  max=1.000  nonzero=0.259
- `precision_required`: mean=0.00000  std=0.00000  max=0.000  nonzero=0.000
- `probe_hit_distance_delta`: mean=0.00000  std=0.00000  max=0.000  nonzero=0.000
- `probe_normal_delta`: mean=0.00000  std=0.00000  max=0.000  nonzero=0.000
- `probe_collider_mismatch`: mean=0.00000  std=0.00000  max=0.000  nonzero=0.000
- `boundary_confidence`: mean=0.09773  std=0.24221  max=0.698  nonzero=0.140
- `selection_flip`: mean=0.14000  std=0.34699  max=1.000  nonzero=0.140

### Diagnoses

- ⚠️ PROBE_ZERO: step_sensitivity_mean=0.00000 — probe is still returning all-zero sensitivity. Position-shifted probes may not be finding different colliders.
- ⚠️ PROBE_ZERO: probe_collider_mismatch_mean=0.00000 — no position-shifted probes found a different collider anywhere in the image.
- ✓ probe_hit_distance_delta_mean=0.00000
- ✓ probe_normal_delta_mean=0.00000
- ✓ pearson: one or both arrays are constant, skipping correlation
- ✓ precision_required: 0/57600 pixels ≥0.5 (0.0%), 0/57600 pixels ≥1.0 (0.0%)

## Cell: step_0.0125/sconv_on

**Step length:** `0.0125`  
**Dir:** `output/doe_sensitivity/20260502T030917Z/step_0.0125/sconv_on`

### Maps present

- `step_sensitivity`: mean=0.00000  std=0.00000  max=0.000  nonzero=0.000
- `step_convergence_confidence`: mean=0.10111  std=0.30148  max=1.000  nonzero=0.101
- `precision_required`: mean=0.00000  std=0.00000  max=0.000  nonzero=0.000
- `probe_hit_distance_delta`: mean=0.00000  std=0.00000  max=0.000  nonzero=0.000
- `probe_normal_delta`: mean=0.00000  std=0.00000  max=0.000  nonzero=0.000
- `probe_collider_mismatch`: mean=0.00000  std=0.00000  max=0.000  nonzero=0.000
- `boundary_confidence`: mean=0.00698  std=0.06945  max=0.698  nonzero=0.010
- `selection_flip`: mean=0.01000  std=0.09950  max=1.000  nonzero=0.010

### Diagnoses

- ⚠️ PROBE_ZERO: step_sensitivity_mean=0.00000 — probe is still returning all-zero sensitivity. Position-shifted probes may not be finding different colliders.
- ⚠️ PROBE_ZERO: probe_collider_mismatch_mean=0.00000 — no position-shifted probes found a different collider anywhere in the image.
- ✓ probe_hit_distance_delta_mean=0.00000
- ✓ probe_normal_delta_mean=0.00000
- ✓ pearson: one or both arrays are constant, skipping correlation
- ✓ precision_required: 0/57600 pixels ≥0.5 (0.0%), 0/57600 pixels ≥1.0 (0.0%)

## Cell: step_0.00625/sconv_on

**Step length:** `0.00625`  
**Dir:** `output/doe_sensitivity/20260502T030917Z/step_0.00625/sconv_on`

### Maps present

- `step_sensitivity`: mean=0.00000  std=0.00000  max=0.000  nonzero=0.000
- `step_convergence_confidence`: mean=0.10222  std=0.30294  max=1.000  nonzero=0.102
- `precision_required`: mean=0.00000  std=0.00000  max=0.000  nonzero=0.000
- `probe_hit_distance_delta`: mean=0.00000  std=0.00000  max=0.000  nonzero=0.000
- `probe_normal_delta`: mean=0.00000  std=0.00000  max=0.000  nonzero=0.000
- `probe_collider_mismatch`: mean=0.00000  std=0.00000  max=0.000  nonzero=0.000
- `boundary_confidence`: mean=0.00771  std=0.07294  max=0.698  nonzero=0.011
- `selection_flip`: mean=0.01104  std=0.10450  max=1.000  nonzero=0.011

### Diagnoses

- ⚠️ PROBE_ZERO: step_sensitivity_mean=0.00000 — probe is still returning all-zero sensitivity. Position-shifted probes may not be finding different colliders.
- ⚠️ PROBE_ZERO: probe_collider_mismatch_mean=0.00000 — no position-shifted probes found a different collider anywhere in the image.
- ✓ probe_hit_distance_delta_mean=0.00000
- ✓ probe_normal_delta_mean=0.00000
- ✓ pearson: one or both arrays are constant, skipping correlation
- ✓ precision_required: 0/57600 pixels ≥0.5 (0.0%), 0/57600 pixels ≥1.0 (0.0%)

