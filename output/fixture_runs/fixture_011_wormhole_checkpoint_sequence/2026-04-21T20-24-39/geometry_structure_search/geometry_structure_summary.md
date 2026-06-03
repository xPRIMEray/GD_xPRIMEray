# Geometry Structure Search

Exploratory geometric morphology analysis only; no physical interpretation is asserted.

## Method
- Primary detection image: debug normal RGB when present, otherwise debug capture.
- Detection preprocessing: grayscale normalization, Gaussian blur for feature detection only, Canny/Sobel edges.
- Features: Hough circles, Hough line orientations, contour hierarchy/eccentricity, connected components in available masks/coherence artifacts.

## Metrics
| checkpoint | rings/arcs | large contours | largest contour area | mean eccentricity | spacing mean | Hough lines | dominant angles | components |
|---|---:|---:|---:|---:|---:|---:|---|---|
| mouth | 12 | 8 | 126.5 | 0.938 | 1.45 | 86 | 5.0, 175.0, 95.0 | visible_band_mask:91, adaptive_tile_heatmap:1, polar_coherence_heatmap:1 |
| mouth_to_throat_approach | 12 | 13 | 804.5 | 0.942 | 2.36 | 85 | 5.0, 175.0, 95.0 | none |
| throat | 12 | 11 | 256.0 | 0.932 | 3.00 | 71 | 5.0, 175.0, 95.0 | none |
| post_throat_backstep_01 | 12 | 5 | 412.0 | 0.663 | 0.82 | 214 | 95.0, 5.0, 85.0 | visible_band_mask:190 |
| post_throat_exit_approach | 12 | 18 | 725.0 | 0.909 | 0.36 | 90 | 5.0, 175.0, 95.0 | none |
| exit_lookback | 12 | 11 | 1101.5 | 0.962 | 3.64 | 106 | 5.0, 175.0, 55.0 | none |

## Interpretation
Detected structures are recurring but not uniform: contour/edge families persist across the ladder, while the throat-to-exit side changes contour density and component structure. Treat this as morphology, not physical proof.

Regime label: `mixed regime behavior`

Best next metric: log-polar edge-orientation persistence: compare radial/tangential edge histograms after registering each checkpoint to its aperture center
