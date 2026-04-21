# Observer Ladder Periodicity

This pass uses only existing wormhole ladder metrics and radial profiles reconstructed from the approved debug captures.

## Sequence FFT Summary

| Sequence | dominant_frequency | dominant_power_ratio | low_frequency_ratio | bridge_residual |
|---|---:|---:|---:|---:|
| `opl_mean` | 0.1667 | 0.7805 | 0.7805 | -1.2217 |
| `throat_event_density` | 0.1667 | 0.5277 | 0.5277 | -0.1070 |
| `crossings_per_pixel` | 0.1667 | 0.4525 | 0.4525 | -0.9913 |
| `segments_per_crossing` | 0.3333 | 0.4877 | 0.2649 | 276.7875 |

## Radial Wavelet Summary

| Checkpoint | dominant_scale | dominant_scale_power | scale_entropy |
|---|---:|---:|---:|
| `mouth` | 12.00 | 56.9771 | 5.7111 |
| `mouth_to_throat_approach` | 8.00 | 36.5074 | 5.7210 |
| `throat` | 20.00 | 266.6707 | 5.5688 |
| `post_throat_backstep_01` | 18.00 | 53.4481 | 5.4341 |
| `post_throat_exit_approach` | 11.00 | 29.6120 | 5.1445 |
| `exit_lookback` | 16.00 | 60.2352 | 5.7217 |

## Interpretation

- Nontrivial periodic ladder-wide structure verdict: `no strong evidence`.
- The ordered ladder sequences are dominated by low-frequency trend and regime shifts rather than repeated oscillation.
- Bridge singularity verdict: `singular outlier-like`.
- The bridge checkpoint behaves more like a localized excursion than a member of a repeating oscillatory family.
- Radial wavelet power indicates scale-structured morphology within checkpoints, but the dominant scales vary across the ladder instead of locking into a single repeating radial cadence.

Figures:

- [sequence_fft.png](figures/sequence_fft.png)
- [radial_profile_wavelets.png](figures/radial_profile_wavelets.png)
