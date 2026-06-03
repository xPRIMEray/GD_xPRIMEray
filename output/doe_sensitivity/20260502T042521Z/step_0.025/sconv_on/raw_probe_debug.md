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
**Dir:** `output/doe_sensitivity/20260502T042521Z/step_0.025/sconv_on`

### Maps present

- `step_sensitivity`: mean=0.07526  std=0.17838  max=0.498  nonzero=0.151
- `step_convergence_confidence`: mean=0.20304  std=0.35219  max=1.000  nonzero=0.279
- `precision_required`: mean=0.07526  std=0.17838  max=0.498  nonzero=0.151
- `probe_hit_distance_delta`: mean=0.07526  std=0.17838  max=0.498  nonzero=0.151
- `probe_normal_delta`: mean=0.00000  std=0.00000  max=0.000  nonzero=0.000
- `probe_collider_mismatch`: mean=0.07526  std=0.17838  max=0.498  nonzero=0.151
- `boundary_confidence`: mean=0.10548  std=0.25001  max=0.698  nonzero=0.151
- `selection_flip`: mean=0.15111  std=0.35816  max=1.000  nonzero=0.151

### Diagnoses

- ✓ OK: step_sensitivity_mean=0.07526 (non-zero, probe is active)
- ✓ OK: probe_collider_mismatch_mean=0.07526 max=0.498 nonzero_frac=0.151
- ✓ probe_hit_distance_delta_mean=0.07526
- ✓ probe_normal_delta_mean=0.00000
- ✓ pearson(probe_collider_mismatch, step_sensitivity)=1.0000
- ✓ precision_required: 8704/57600 pixels ≥0.5 (15.1%), 0/57600 pixels ≥1.0 (0.0%)

## Cell: step_0.0125/sconv_on

**Step length:** `0.0125`  
**Dir:** `output/doe_sensitivity/20260502T042521Z/step_0.0125/sconv_on`

### Maps present

- `step_sensitivity`: mean=0.01771  std=0.09223  max=0.498  nonzero=0.036
- `step_convergence_confidence`: mean=0.11549  std=0.30539  max=1.000  nonzero=0.133
- `precision_required`: mean=0.01771  std=0.09223  max=0.498  nonzero=0.036
- `probe_hit_distance_delta`: mean=0.01771  std=0.09223  max=0.498  nonzero=0.036
- `probe_normal_delta`: mean=0.00000  std=0.00000  max=0.000  nonzero=0.000
- `probe_collider_mismatch`: mean=0.01771  std=0.09223  max=0.498  nonzero=0.036
- `boundary_confidence`: mean=0.02482  std=0.12926  max=0.698  nonzero=0.036
- `selection_flip`: mean=0.03556  std=0.18518  max=1.000  nonzero=0.036

### Diagnoses

- ✓ OK: step_sensitivity_mean=0.01771 (non-zero, probe is active)
- ✓ OK: probe_collider_mismatch_mean=0.01771 max=0.498 nonzero_frac=0.036
- ✓ probe_hit_distance_delta_mean=0.01771
- ✓ probe_normal_delta_mean=0.00000
- ✓ pearson(probe_collider_mismatch, step_sensitivity)=1.0000
- ✓ precision_required: 2048/57600 pixels ≥0.5 (3.6%), 0/57600 pixels ≥1.0 (0.0%)

## Cell: step_0.00625/sconv_on

**Step length:** `0.00625`  
**Dir:** `output/doe_sensitivity/20260502T042521Z/step_0.00625/sconv_on`

### Maps present

- `step_sensitivity`: mean=0.00495  std=0.04938  max=0.498  nonzero=0.010
- `step_convergence_confidence`: mean=0.09613  std=0.29052  max=1.000  nonzero=0.101
- `precision_required`: mean=0.00495  std=0.04938  max=0.498  nonzero=0.010
- `probe_hit_distance_delta`: mean=0.00495  std=0.04938  max=0.498  nonzero=0.010
- `probe_normal_delta`: mean=0.00000  std=0.00000  max=0.000  nonzero=0.000
- `probe_collider_mismatch`: mean=0.00495  std=0.04938  max=0.498  nonzero=0.010
- `boundary_confidence`: mean=0.00693  std=0.06921  max=0.698  nonzero=0.010
- `selection_flip`: mean=0.00993  std=0.09916  max=1.000  nonzero=0.010

### Diagnoses

- ✓ OK: step_sensitivity_mean=0.00495 (non-zero, probe is active)
- ✓ OK: probe_collider_mismatch_mean=0.00495 max=0.498 nonzero_frac=0.010
- ✓ probe_hit_distance_delta_mean=0.00495
- ✓ probe_normal_delta_mean=0.00000
- ✓ pearson(probe_collider_mismatch, step_sensitivity)=1.0000
- ✓ precision_required: 572/57600 pixels ≥0.5 (1.0%), 0/57600 pixels ≥1.0 (0.0%)

