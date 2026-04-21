# Wormhole Morphology Summary

This artifact-only pass estimates visible wormhole morphology directly from the approved ladder debug captures.

| Checkpoint | ring_count | apparent_radius_px | contour_eccentricity | center_offset_px |
|---|---:|---:|---:|---:|
| `mouth` | 1 | 206.24 | 0.6850 | 23.77 |
| `mouth_to_throat_approach` | 1 | 204.92 | 0.9870 | 85.53 |
| `throat` | 2 | 202.28 | 0.9924 | 41.72 |
| `post_throat_backstep_01` | 0 | 206.24 | 0.1814 | 91.96 |
| `post_throat_exit_approach` | 1 | 200.96 | 0.6541 | 24.61 |
| `exit_lookback` | 0 | 203.60 | 0.9674 | 57.08 |

## Interpretation

- Smooth morphology verdict: `yes`.
- Apparent radius and center offset remain bounded across the ladder rather than diverging catastrophically.
- The bridge and far-side checkpoints remain morphologically detectable even when their contour shape changes relative to the near-side views.
- Ring count is an image-space proxy, so it should be read as radial band complexity rather than a direct physical shell count.

Annotated images and radial profiles are emitted alongside this summary.
