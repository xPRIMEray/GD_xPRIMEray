# Radial Structure and Horizon-Like Feature Detection

This pass reconstructs radial profiles from the approved observer ladder debug images and computes first and second derivatives to detect radius-localized structure.

| Checkpoint | apparent_radius_px | feature_radius_px | gradient_sign_change_radius_px | peak_count | inflection_count | feature_minus_apparent_px | slope_steepness_near_apparent |
|---|---:|---:|---:|---:|---:|---:|---:|
| `mouth` | 206.24 | 202.00 | 202.00 | 9 | 67 | -4.24 | 0.0168 |
| `mouth_to_throat_approach` | 204.92 | 193.00 | 205.00 | 7 | 50 | -11.92 | 0.0163 |
| `throat` | 202.28 | 252.00 | 194.00 | 6 | 59 | 49.72 | 0.0251 |
| `post_throat_backstep_01` | 206.24 | 213.00 | 203.00 | 10 | 54 | 6.76 | 0.0149 |
| `post_throat_exit_approach` | 200.96 | 227.00 | 193.00 | 9 | 56 | 26.04 | 0.0140 |
| `exit_lookback` | 203.60 | 144.00 | 214.00 | 5 | 64 | -59.60 | 0.0117 |

## Interpretation

- Consistent feature-radius verdict: `mixed`.
- Throat sharpening verdict: `yes`.
- The detected feature radius is defined as the nearest local peak to the apparent radius, with a gradient sign-change fallback when no nearby peak is present.
- The derivative structure remains bounded across checkpoints, but the bridge and far-side checkpoints shift the detected feature away from a simple single-ring interpretation.

Figures:

- [normalized_profile_overlay.png](figures/normalized_profile_overlay.png)
- [radial_derivative_panels.png](figures/radial_derivative_panels.png)
