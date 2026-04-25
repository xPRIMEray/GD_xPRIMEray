# Log-Polar Edge-Orientation Persistence

Exploratory geometric morphology analysis only; no physical interpretation is asserted.

## Method
- Registered each checkpoint to an edge-energy centroid used as the aperture/visible-band center proxy.
- Built Canny edge maps from normalized grayscale debug-normal images; Gaussian blur was used only for edge detection.
- Converted edge maps to log-polar coordinates for visual inspection.
- Computed weighted edge-tangent orientation histograms relative to the local radial direction: 0 degrees is radial, 90 degrees is tangential.
- Measured persistence with cosine similarity between normalized orientation histograms.

## Metrics
| checkpoint | center x | center y | edges | radial frac | tangential frac | radial/tangential |
|---|---:|---:|---:|---:|---:|---:|
| `mouth` | 285.3 | 193.3 | 6762 | 0.312 | 0.202 | 1.543 |
| `mouth_to_throat_approach` | 285.0 | 193.5 | 6498 | 0.301 | 0.195 | 1.541 |
| `throat` | 278.4 | 203.3 | 5941 | 0.335 | 0.187 | 1.796 |
| `post_throat_backstep_01` | 208.1 | 158.8 | 13658 | 0.336 | 0.166 | 2.029 |
| `post_throat_exit_approach` | 319.5 | 186.7 | 7153 | 0.194 | 0.257 | 0.754 |
| `exit_lookback` | 200.4 | 181.8 | 7719 | 0.177 | 0.305 | 0.581 |

## Persistence
- Non-bridge pairwise mean cosine: `0.961`.
- Bridge to near-side mean cosine: `0.988`.
- Bridge to far-side mean cosine: `0.915`.
- Bridge to rest mean cosine: `0.972`.
- Bridge persistence drop vs non-bridge mean: `-0.010`.

## Verdict
No strong disruption: the bridge changes the radial/tangential balance, but its normalized orientation histogram remains broadly persistent with the rest of the ladder.

Figures:
- [orientation_histograms.png](orientation_histograms.png)
- [log_polar_edge_contact_sheet.png](log_polar_edge_contact_sheet.png)
