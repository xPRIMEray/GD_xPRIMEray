# DOE Sensitivity Experiment — Band Analysis

**Experiment dir:** `/home/bb/code/godot_xPRIMEray/output/doe_sensitivity/20260502T012251Z`

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
| 0.025 | 260 (0.5%) | 260 (0.5%) | 196 (0.3%) | 388 (0.7%) |
| 0.0125 | 8968 (15.6%) | 11672 (20.3%) | 10516 (18.3%) | 16532 (28.7%) |
| 0.00625 | 14414 (25.0%) | 15068 (26.2%) | 15442 (26.8%) | 15168 (26.3%) |

## Changed Pixels vs Baseline (OFF)

| Step length | OFF→tel | OFF→sconv | OFF→resolver |
| ----------- | ------- | --------- | ------------ |
| 0.025 | 0 | 64 | 384 |
| 0.0125 | 2752 | 2816 | 6976 |
| 0.00625 | 2048 | 1984 | 2688 |

## Mean Telemetry Map Values

| Step length | mode | boundary_mean | selection_flip | step_sensitivity | precision_req | sconv_conf |
| ----------- | ---- | ------------- | -------------- | ---------------- | ------------- | ---------- |
| 0.025 | off |  |  |  |  |  |
| 0.025 | telemetry_on | 0.3056 | 0.4378 |  |  |  |
| 0.025 | sconv_on | 0.3056 | 0.4378 | 0.0000 | 0.0000 | 0.4378 |
| 0.025 | resolver_on | 0.3056 | 0.4378 |  |  |  |
| 0.0125 | off |  |  |  |  |  |
| 0.0125 | telemetry_on | 0.2893 | 0.4144 |  |  |  |
| 0.0125 | sconv_on | 0.2877 | 0.4122 | 0.0000 | 0.0000 | 0.4122 |
| 0.0125 | resolver_on | 0.2459 | 0.3522 |  |  |  |
| 0.00625 | off |  |  |  |  |  |
| 0.00625 | telemetry_on | 0.1586 | 0.2272 |  |  |  |
| 0.00625 | sconv_on | 0.1609 | 0.2306 | 0.0000 | 0.0000 | 0.2306 |
| 0.00625 | resolver_on | 0.1555 | 0.2228 |  |  |  |

## Band vs Map Overlap (Precision / IoU)

| Step length | mode | vs boundary | vs sel-flip | vs step-sens | vs prec-req |
| ----------- | ---- | ----------- | ----------- | ------------ | ----------- |
| 0.025 | off | — | — | — | — |
| 0.025 | telemetry_on | 49.23% / 0.50% | 49.23% / 0.50% | — | — |
| 0.025 | sconv_on | 65.31% / 0.51% | 65.31% / 0.51% | 0.00% / 0.00% | 0.00% / 0.00% |
| 0.025 | resolver_on | 32.99% / 0.50% | 32.99% / 0.50% | — | — |
| 0.0125 | off | — | — | — | — |
| 0.0125 | telemetry_on | 12.56% / 4.30% | 12.56% / 4.30% | — | — |
| 0.0125 | sconv_on | 16.74% / 5.42% | 16.74% / 5.42% | 0.00% / 0.00% | 0.00% / 0.00% |
| 0.0125 | resolver_on | 20.30% / 10.03% | 20.30% / 10.03% | — | — |
| 0.00625 | off | — | — | — | — |
| 0.00625 | telemetry_on | 11.40% / 6.50% | 11.40% / 6.50% | — | — |
| 0.00625 | sconv_on | 11.95% / 6.86% | 11.95% / 6.86% | 0.00% / 0.00% | 0.00% / 0.00% |
| 0.00625 | resolver_on | 11.37% / 6.56% | 11.37% / 6.56% | — | — |

## Interpretation Guide

- **Does smaller step size reduce banding?** → Compare `band_pixels` across step lengths for the `off` mode.
- **Does resolver amplify instability?** → Compare `resolver_on` band_pixels vs `off` at same step length.
- **Do convergence maps predict banding?** → Check `band_vs_step_sensitivity_precision` and `band_vs_precision_required_precision` for `sconv_on` rows.
- **Convergence stabilization threshold?** → Find the step length where `precision_required_mean` drops to near zero.
- `hash_matches_off=false` rows indicate the telemetry is non-passive (flag run invalid).
- `—` means map not generated for that mode (resolver_on never gets step-conv maps).
