# DOE Sensitivity Experiment — Band Analysis

**Experiment dir:** `/home/bb/code/godot_xPRIMEray/output/doe_sensitivity_smoke/20260502T011216Z`

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
| 0.025 | — | — | — | — |
| 0.0125 | 1832 (3.2%) | 3054 (5.3%) | 2286 (4.0%) | 3518 (6.1%) |
| 0.00625 | — | — | — | — |

## Changed Pixels vs Baseline (OFF)

| Step length | OFF→tel | OFF→sconv | OFF→resolver |
| ----------- | ------- | --------- | ------------ |
| 0.025 | — | — | — |
| 0.0125 | 1152 | 704 | 3456 |
| 0.00625 | — | — | — |

## Mean Telemetry Map Values

| Step length | mode | boundary_mean | selection_flip | step_sensitivity | precision_req | sconv_conf |
| ----------- | ---- | ------------- | -------------- | ---------------- | ------------- | ---------- |
| 0.0125 | off |  |  |  |  |  |
| 0.0125 | telemetry_on | 0.0712 | 0.1019 |  |  |  |
| 0.0125 | sconv_on | 0.0766 | 0.1097 | 0.0000 | 0.0000 | 0.1097 |
| 0.0125 | resolver_on | 0.0446 | 0.0639 |  |  |  |

## Band vs Map Overlap (Precision / IoU)

| Step length | mode | vs boundary | vs sel-flip | vs step-sens | vs prec-req |
| ----------- | ---- | ----------- | ----------- | ------------ | ----------- |
| 0.0125 | off | — | — | — | — |
| 0.0125 | telemetry_on | 15.82% / 5.72% | 15.82% / 5.72% | — | — |
| 0.0125 | sconv_on | 15.88% / 4.40% | 15.88% / 4.40% | 0.00% / 0.00% | 0.00% / 0.00% |
| 0.0125 | resolver_on | 14.53% / 7.64% | 14.53% / 7.64% | — | — |

## Interpretation Guide

- **Does smaller step size reduce banding?** → Compare `band_pixels` across step lengths for the `off` mode.
- **Does resolver amplify instability?** → Compare `resolver_on` band_pixels vs `off` at same step length.
- **Do convergence maps predict banding?** → Check `band_vs_step_sensitivity_precision` and `band_vs_precision_required_precision` for `sconv_on` rows.
- **Convergence stabilization threshold?** → Find the step length where `precision_required_mean` drops to near zero.
- `hash_matches_off=false` rows indicate the telemetry is non-passive (flag run invalid).
- `—` means map not generated for that mode (resolver_on never gets step-conv maps).
