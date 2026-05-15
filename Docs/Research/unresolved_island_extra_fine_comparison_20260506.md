# Unresolved-Island Extra-Fine Oracle Comparison, 2026-05-06

This note compares two focused `ReferenceTransportOracle` unresolved-island runs over the same `17x17` patch centered at `(40,34)`:

- Baseline oracle step `0.0015625`: `output/reference_transport_oracle_unresolved_island/20260506T034644Z/`
- Extra-fine oracle step `0.00078125`: `output/reference_transport_oracle_unresolved_island/20260506T035920Z/`

Both runs are diagnostic-only renderer-reference comparisons. The oracle is treated as the best-known renderer reference, not physical truth.

## Executive Summary

Halving the oracle step did not change the core topology result: the island is sealed in both runs. All `289` sampled pixels are stable by production step `0.00625`, unresolved count remains `0`, oracle replay failures remain `0`, normal-angle deltas remain unchanged, and ownership transition maps are identical.

The extra-fine oracle did change the threshold map: `46 / 289` pixels moved to a finer first-stable production step. Decision-risk and path-length deltas also rose slightly. Interpretation: the island is not ghosting or expanding, but its exact precision floor is sensitive to the reference baseline. The robust claim is topology/epsilon sealing by `0.00625`; the cautious claim is that per-pixel first-stable labels should be treated as tolerance-dependent until replayed with a tighter epsilon ladder.

## Side-By-Side Image Table

| Layer | `0.0015625` oracle | `0.00078125` oracle | Assessment |
|---|---|---|---|
| First stable step | `first_stable_step_map.png` (20260506T034644Z) | `first_stable_step_map.png` (20260506T035920Z) | Changed: 46 pixels shifted to finer first-stable labels. |
| Decision risk gradient | `decision_risk_gradient.png` (20260506T034644Z) | `decision_risk_gradient.png` (20260506T035920Z) | Changed in scalar intensity, not class outcome. |
| Path length delta | `path_length_delta_map.png` (20260506T034644Z) | `path_length_delta_map.png` (20260506T035920Z) | Changed in scalar intensity. |
| Normal angle delta | `normal_angle_delta_map.png` (20260506T034644Z) | `normal_angle_delta_map.png` (20260506T035920Z) | Identical. |
| Ownership transition | `ownership_transition_map.png` (20260506T034644Z) | `ownership_transition_map.png` (20260506T035920Z) | Identical. |
| Convergence ladder | `island_convergence_ladder.png` (20260506T034644Z) | `island_convergence_ladder.png` (20260506T035920Z) | Changed only where first-stable threshold labels shifted. |

*All figures are in `output/reference_transport_oracle_unresolved_island/<timestamp>/` — not versioned in the docs site.*

## Metric Deltas

| Metric | `0.0015625` | `0.00078125` | Delta |
|---|---:|---:|---:|
| Sample count | 289 | 289 | 0 |
| Comparison count | 3468 | 3468 | 0 |
| Oracle replay failures | 0 | 0 | 0 |
| Stable class count | 289 | 289 | 0 |
| Unresolved at `0.003125` | 0 | 0 | 0 |
| Stable at `0.00625` | 289 | 289 | 0 |
| Sealed at `0.00625` | true | true | unchanged |
| Mean risk delta, `0.00625` vs `0.003125` | 0.000188 | 0.000189 | +0.000001 |
| Max risk delta, `0.00625` vs `0.003125` | 0.000391 | 0.000691 | +0.000300 |
| Local continuity vectors in patch | 0 | 0 | 0 |
| Local transport shape regions in patch | 0 | 0 | 0 |

First-stable step distribution:

| First Stable Step | `0.0015625` count | `0.00078125` count | Delta |
|---|---:|---:|---:|
| `0.02` | 153 | 138 | -15 |
| `0.018` | 73 | 59 | -14 |
| `0.016` | 35 | 47 | +12 |
| `0.015` | 26 | 33 | +7 |
| `0.014` | 2 | 12 | +10 |

Per-pixel changes:

| Field | Changed Pixels |
|---|---:|
| Epsilon stability class | 0 / 289 |
| First stable step | 46 / 289 |
| Ownership transition | 0 / 289 |
| Stable at `0.00625` | 0 / 289 |
| Stable at `0.003125` | 0 / 289 |

Map image deltas:

| Map | Visual Difference |
|---|---:|
| `first_stable_step_map.png` | 5.11% RGB pixels changed |
| `decision_risk_gradient.png` | 32.18% RGB pixels changed |
| `path_length_delta_map.png` | 29.11% RGB pixels changed |
| `normal_angle_delta_map.png` | 0.00% RGB pixels changed |
| `ownership_transition_map.png` | 0.00% RGB pixels changed |
| `island_convergence_ladder.png` | 2.27% RGB pixels changed |

## Claims Vs Evidence

| Claim | Evidence | Confidence |
|---|---|---|
| The unresolved island is sealed by production step `0.00625`. | Both oracle baselines report `289 / 289` stable pixels at `0.00625` and `0` unresolved pixels at `0.003125`. | High |
| The island is not expanding under the extra-fine oracle. | Stable class count stays `289`, unresolved count stays `0`, and ownership transition map is identical. | High |
| The island is not ghosting as a pure oracle artifact. | Halving oracle step does not resurrect unresolved pixels or ownership flips. | Moderate-high |
| The exact first-stable precision floor is reference-sensitive. | `46 / 289` pixels move to finer first-stable labels, mostly from `0.02/0.018` toward `0.016/0.015/0.014`. | High |
| Transport topology is more robust than scalar path metrics in this patch. | Ownership and normal maps are unchanged while decision-risk/path-length maps change. | High |
| Existing continuity/shape overlay extraction is not centered on this island. | Both runs report `0` local continuity vectors and `0` local shape regions in `x=32..48,y=27..43`. | High |

## Sealed, Shrinking, Or Ghosting?

Classification: sealed.

The island is not shrinking in the sense of fewer unstable pixels because both runs already show zero unresolved pixels at `0.003125` and full stability by `0.00625`. It is also not ghosting: the extra-fine reference does not introduce new ownership flips, normal discontinuities, or unresolved classes.

The refined interpretation is threshold sharpening. The extra-fine oracle nudges some pixels toward a finer first-stable step, but the whole island remains inside the same stable topology envelope.

## Oracle Baseline Robustness

The oracle baseline appears robust for topology-level validation:

- replay failures: `0` in both runs
- stability classes: unchanged
- ownership transition map: unchanged
- normal angle map: unchanged
- sealed-at-`0.00625`: unchanged

It is less invariant for scalar precision-floor labeling:

- max risk delta between `0.00625` and `0.003125` rose from `0.000391` to `0.000691`
- first-stable labels shifted for `46` pixels
- path-length deltas changed in the map, while topology did not

That means the oracle is suitable for deciding whether a region is topologically sealed, but not yet sufficient for treating a single first-stable-step label as an absolute per-pixel truth.

## Recommended Next Action

Archive this island as topologically sealed at `0.00625`, but keep it as a calibration fixture for precision-floor sensitivity.

Next run should not repeat the whole patch. Instead:

1. Extract the `46` pixels whose first-stable labels changed under the extra-fine oracle.
2. Run only those pixels with a tighter epsilon ladder around the transition band:
   - production steps: `0.018,0.017,0.016,0.015,0.0145,0.014,0.0135`
   - oracle step: `0.00078125`
   - optional oracle step check: `0.000390625` for changed pixels only
3. Add local continuity vector extraction at stride `1` inside `x=32..48,y=27..43`, because the current overlay pass produced no local vectors in the patch.

Decision rule: if ownership, normal, and stability class stay fixed while only scalar path-length/risk labels move, classify this region as an epsilon-stable sealed basin with a tolerance-sensitive precision floor.
