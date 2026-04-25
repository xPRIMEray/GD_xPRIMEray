# Tile Orientation Guidance Field

Exploratory analysis only. No renderer changes, hit-selection changes, or simulation reruns were performed.

## Method
- Adaptive tile summaries define the tile lattice.
- Dominant tile edge orientation is computed from Canny/Sobel edge tangents inside each tile.
- Tiles are classified as radial, tangential, or oblique relative to the visible-band centroid.
- Segment direction is approximated from the local image-space isoline tangent of `first_accepted_segment_index`, because the CSV artifacts do not store literal ray-segment direction vectors.
- Mismatch is the undirected angular difference between segment-selection orientation and tile orientation, normalized so `0` is aligned and `1` is orthogonal.

## Results
| checkpoint | radial tiles | oblique tiles | tangential tiles | mean mismatch | in-band mismatch | out-band mismatch | Pearson vs band | high mismatch in band | band covered by high mismatch |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `mouth` | 330 | 104 | 76 | 0.197 | 0.199 | 0.195 | 0.142 | 0.422 | 0.051 |
| `post_throat_backstep_01` | 265 | 139 | 106 | 0.147 | 0.125 | 0.160 | 0.054 | 0.312 | 0.034 |

## Verdict
Only a weak or mixed association was found; banding is not strongly explained by tile/segment orientation mismatch under this proxy.

Outputs:
- [tile_orientation_field.png](tile_orientation_field.png)
- [segment_orientation_mismatch_heatmap.png](segment_orientation_mismatch_heatmap.png)
- [tile_orientation_guidance_summary.json](tile_orientation_guidance_summary.json)
