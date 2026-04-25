# Phase-Coherence-Guided Hit Selection Proxy

Analysis-only simulation. No geometry, renderer, hit-selection, or simulation rerun changes were made.

## Limitation
The available CSV artifacts do not contain actual candidate hit lists. This proxy lets each pixel choose among accepted hits in its local 3x3 neighborhood when `first_accepted_candidate_count > 1`, minimizing mismatch to the adaptive tile phase prototype.

## Results
| checkpoint | changed px frac | phase score before | phase score after | band corr before | band corr after | band boundary before | band boundary after |
|---|---:|---:|---:|---:|---:|---:|---:|
| `mouth` | 0.440 | 0.170 | 0.121 | 0.309 | 0.276 | 0.288 | 0.277 |
| `post_throat_backstep_01` | 0.435 | 0.130 | 0.096 | 0.071 | 0.039 | 0.163 | 0.160 |

## Verdict
Proxy phase-guided selection shows weak or inconclusive band-reduction evidence.

Outputs:
- [phase_coherence_map.png](phase_coherence_map.png)
- [phase_guided_render_preview.png](phase_guided_render_preview.png)
- [phase_guided_hit_selection_diff.png](phase_guided_hit_selection_diff.png)
- [band_reduction_metrics.json](band_reduction_metrics.json)
