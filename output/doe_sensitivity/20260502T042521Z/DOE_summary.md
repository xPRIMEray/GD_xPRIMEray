# DOE Sensitivity Experiment — Band Analysis

**Experiment dir:** `/home/bb/code/godot_xPRIMEray/output/doe_sensitivity/20260502T042521Z`

## Design

| Factor | Levels |
| ------ | ------ |
| A — Base step length | 0.025, 0.0125, 0.00625 |
| B — Resolver | off, on |
| C — Step-conv telemetry | off, on (only when B=off) |

Fixed: 320×180, 90 frames, domain_resolver_stress fixture, camera fixed.

## Beauty Hash Validity

| Step length | OFF vs tel | OFF vs sconv | OFF vs resolver |
| ----------- | ---------- | ------------ | --------------- |
| 0.025 | — | — | — |
| 0.0125 | — | — | — |
| 0.00625 | — | — | — |

## Band Pixel Count by Step Length and Mode

| Step length | off | telemetry_on | sconv_on | resolver_on |
| ----------- | --- | ------------ | -------- | ----------- |
| 0.025 | 196 (0.3%) | 196 (0.3%) | 11524 (20.0%) | 260 (0.5%) |
| 0.0125 | 12714 (22.1%) | 13092 (22.7%) | 15484 (26.9%) | 16306 (28.3%) |
| 0.00625 | 15382 (26.7%) | 15582 (27.1%) | 11772 (20.4%) | 16790 (29.1%) |

## Changed Pixels vs Baseline (OFF)

| Step length | OFF→tel | OFF→sconv | OFF→resolver |
| ----------- | ------- | --------- | ------------ |
| 0.025 | 128 | 35840 | 192 |
| 0.0125 | 1280 | 35328 | 6528 |
| 0.00625 | 2432 | 20288 | 2176 |

## Mean Telemetry Map Values

| Step length | mode | boundary_mean | selection_flip | step_sensitivity | precision_req | sconv_conf |
| ----------- | ---- | ------------- | -------------- | ---------------- | ------------- | ---------- |
| 0.025 | off |  |  |  |  |  |
| 0.025 | telemetry_on | 0.3056 | 0.4378 |  |  |  |
| 0.025 | sconv_on | 0.1055 | 0.1511 | 0.0753 | 0.0753 | 0.2030 |
| 0.025 | resolver_on | 0.3056 | 0.4378 |  |  |  |
| 0.0125 | off |  |  |  |  |  |
| 0.0125 | telemetry_on | 0.2804 | 0.4017 |  |  |  |
| 0.0125 | sconv_on | 0.0248 | 0.0356 | 0.0177 | 0.0177 | 0.1155 |
| 0.0125 | resolver_on | 0.2141 | 0.3067 |  |  |  |
| 0.00625 | off |  |  |  |  |  |
| 0.00625 | telemetry_on | 0.1507 | 0.2158 |  |  |  |
| 0.00625 | sconv_on | 0.0069 | 0.0099 | 0.0049 | 0.0049 | 0.0961 |
| 0.00625 | resolver_on | 0.1516 | 0.2172 |  |  |  |

## Probe Diagnostic Map Values (sconv_on mode)

| Step length | probe_dist_delta | probe_normal_delta | probe_collider_mismatch |
| ----------- | ---------------- | ------------------ | ----------------------- |
| 0.025 | 0.0753 | 0.0000 | 0.0753 |
| 0.0125 | 0.0177 | 0.0000 | 0.0177 |
| 0.00625 | 0.0049 | 0.0000 | 0.0049 |

## Band vs Map Overlap (Precision / IoU)

| Step length | mode | vs boundary | vs sel-flip | vs step-sens | vs prec-req | vs probe-mismatch |
| ----------- | ---- | ----------- | ----------- | ------------ | ----------- | ----------------- |
| 0.025 | off | — | — | — | — | — |
| 0.025 | telemetry_on | 65.31% / 0.51% | 65.31% / 0.51% | — | — | — |
| 0.025 | sconv_on | 14.39% / 8.93% | 14.39% / 8.93% | 14.39% / 8.93% | 14.39% / 8.93% | 14.39% / 8.93% |
| 0.025 | resolver_on | 49.23% / 0.50% | 49.23% / 0.50% | — | — | — |
| 0.0125 | off | — | — | — | — | — |
| 0.0125 | telemetry_on | 17.45% / 6.73% | 17.45% / 6.73% | — | — | — |
| 0.0125 | sconv_on | 13.23% / 13.23% | 13.23% / 13.23% | 13.23% / 13.23% | 13.23% / 13.23% | 13.23% / 13.23% |
| 0.0125 | resolver_on | 15.40% / 7.98% | 15.40% / 7.98% | — | — | — |
| 0.00625 | off | — | — | — | — | — |
| 0.00625 | telemetry_on | 10.90% / 6.46% | 10.90% / 6.46% | — | — | — |
| 0.00625 | sconv_on | 4.86% / 4.86% | 4.86% / 4.86% | 4.86% / 4.86% | 4.86% / 4.86% | 4.86% / 4.86% |
| 0.00625 | resolver_on | 10.17% / 6.19% | 10.17% / 6.19% | — | — | — |

## Interpretation Guide

- **Does smaller step size reduce banding?** → Compare `band_pixels` across step lengths for the `off` mode.
- **Does resolver amplify instability?** → Compare `resolver_on` band_pixels vs `off` at same step length.
- **Do convergence maps predict banding?** → Check `band_vs_step_sensitivity_precision` and `band_vs_precision_required_precision` for `sconv_on` rows.
- **Probe collider mismatch vs banding?** → `band_vs_probe_collider_mismatch_precision` — if high, position-shifted probes correctly identify banding pixels.
- **Convergence stabilization threshold?** → Find the step length where `precision_required_mean` drops to near zero.
- `hash_matches_off=false` rows indicate the telemetry is non-passive (flag run invalid).
- `—` means map not generated for that mode (resolver_on never gets step-conv maps).
- `probe_dist_delta_mean` and `probe_collider_mismatch_mean` should be non-zero in sconv_on runs if probe redesign is working.

## Conclusions

- The regenerated position-shifted probe is active: `step_sensitivity_mean` is nonzero in all `sconv_on` cells (0.0753, 0.0177, 0.0049 for step lengths 0.025, 0.0125, 0.00625).
- `probe_collider_mismatch_mean` exactly tracks `step_sensitivity_mean` and `probe_hit_distance_delta_mean` in this run, with audit Pearson correlation 1.0.
- The mismatch rate does **not** scale with the observed OFF banding. OFF banding rises as step length decreases (0.3% → 22.1% → 26.7%), while probe mismatch falls (0.0753 → 0.0177 → 0.0049).
- Probe overlap with band masks is weak to moderate, not decisive localization: `band_vs_probe_collider_mismatch` precision/IoU is 14.39%/8.93%, 13.23%/13.23%, and 4.86%/4.86%.
- This supports that position-shifted first-hit ambiguity exists and is measurable, but the primary instability behind the finer-step OFF band growth is likely not explained by this probe alone.
- `sconv_on` beauty hashes differ from OFF for all step lengths. The probe writes only telemetry buffers and does not mutate selected hit state, but its extra work changes frame/budget timing in this harness, so beauty comparisons for `sconv_on` should be treated as timing-contaminated validation artifacts.
