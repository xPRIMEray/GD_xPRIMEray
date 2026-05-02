# DOE Sensitivity Experiment — Band Analysis

**Experiment dir:** `/home/bb/code/godot_xPRIMEray/output/doe_sensitivity/20260502T030917Z`

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
| 0.025 | 196 (0.3%) | 196 (0.3%) | 14996 (26.0%) | 196 (0.3%) |
| 0.0125 | 11634 (20.2%) | 12370 (21.5%) | 11772 (20.4%) | 18300 (31.8%) |
| 0.00625 | 14920 (25.9%) | 16082 (27.9%) | 11900 (20.7%) | 15580 (27.0%) |

## Changed Pixels vs Baseline (OFF)

| Step length | OFF→tel | OFF→sconv | OFF→resolver |
| ----------- | ------- | --------- | ------------ |
| 0.025 | 0 | 36992 | 128 |
| 0.0125 | 3392 | 37248 | 6336 |
| 0.00625 | 3008 | 21056 | 2688 |

## Mean Telemetry Map Values

| Step length | mode | boundary_mean | selection_flip | step_sensitivity | precision_req | sconv_conf |
| ----------- | ---- | ------------- | -------------- | ---------------- | ------------- | ---------- |
| 0.025 | off |  |  |  |  |  |
| 0.025 | telemetry_on | 0.3056 | 0.4378 |  |  |  |
| 0.025 | sconv_on | 0.0977 | 0.1400 | 0.0000 | 0.0000 | 0.2589 |
| 0.025 | resolver_on | 0.3056 | 0.4378 |  |  |  |
| 0.0125 | off |  |  |  |  |  |
| 0.0125 | telemetry_on | 0.2719 | 0.3896 |  |  |  |
| 0.0125 | sconv_on | 0.0070 | 0.0100 | 0.0000 | 0.0000 | 0.1011 |
| 0.0125 | resolver_on | 0.2342 | 0.3356 |  |  |  |
| 0.00625 | off |  |  |  |  |  |
| 0.00625 | telemetry_on | 0.1501 | 0.2150 |  |  |  |
| 0.00625 | sconv_on | 0.0077 | 0.0110 | 0.0000 | 0.0000 | 0.1022 |
| 0.00625 | resolver_on | 0.1523 | 0.2181 |  |  |  |

## Probe Diagnostic Map Values (sconv_on mode)

| Step length | probe_dist_delta | probe_normal_delta | probe_collider_mismatch |
| ----------- | ---------------- | ------------------ | ----------------------- |
| 0.025 | 0.0000 | 0.0000 | 0.0000 |
| 0.0125 | 0.0000 | 0.0000 | 0.0000 |
| 0.00625 | 0.0000 | 0.0000 | 0.0000 |

## Band vs Map Overlap (Precision / IoU)

| Step length | mode | vs boundary | vs sel-flip | vs step-sens | vs prec-req | vs probe-mismatch |
| ----------- | ---- | ----------- | ----------- | ------------ | ----------- | ----------------- |
| 0.025 | off | — | — | — | — | — |
| 0.025 | telemetry_on | 65.31% / 0.51% | 65.31% / 0.51% | — | — | — |
| 0.025 | sconv_on | 12.95% / 9.20% | 12.95% / 9.20% | 0.00% / 0.00% | 0.00% / 0.00% | 0.00% / 0.00% |
| 0.025 | resolver_on | 65.31% / 0.51% | 65.31% / 0.51% | — | — | — |
| 0.0125 | off | — | — | — | — | — |
| 0.0125 | telemetry_on | 15.34% / 5.76% | 15.34% / 5.76% | — | — | — |
| 0.0125 | sconv_on | 4.89% / 4.89% | 4.89% / 4.89% | 0.00% / 0.00% | 0.00% / 0.00% | 0.00% / 0.00% |
| 0.0125 | resolver_on | 19.29% / 10.35% | 19.29% / 10.35% | — | — | — |
| 0.00625 | off | — | — | — | — | — |
| 0.00625 | telemetry_on | 10.44% / 6.27% | 10.44% / 6.27% | — | — | — |
| 0.00625 | sconv_on | 5.34% / 5.34% | 5.34% / 5.34% | 0.00% / 0.00% | 0.00% / 0.00% | 0.00% / 0.00% |
| 0.00625 | resolver_on | 11.09% / 6.54% | 11.09% / 6.54% | — | — | — |

## Interpretation Guide

- **Does smaller step size reduce banding?** → Compare `band_pixels` across step lengths for the `off` mode.
- **Does resolver amplify instability?** → Compare `resolver_on` band_pixels vs `off` at same step length.
- **Do convergence maps predict banding?** → Check `band_vs_step_sensitivity_precision` and `band_vs_precision_required_precision` for `sconv_on` rows.
- **Probe collider mismatch vs banding?** → `band_vs_probe_collider_mismatch_precision` — if high, position-shifted probes correctly identify banding pixels.
- **Convergence stabilization threshold?** → Find the step length where `precision_required_mean` drops to near zero.
- `hash_matches_off=false` rows indicate the telemetry is non-passive (flag run invalid).
- `—` means map not generated for that mode (resolver_on never gets step-conv maps).
- `probe_dist_delta_mean` and `probe_collider_mismatch_mean` should be non-zero in sconv_on runs if probe redesign is working.
