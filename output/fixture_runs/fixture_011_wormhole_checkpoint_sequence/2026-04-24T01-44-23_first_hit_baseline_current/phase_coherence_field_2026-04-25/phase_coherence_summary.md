# Phase Coherence Field

Exploratory analysis only. No renderer changes, hit-selection changes, or simulation reruns were performed.

## Method
- Used existing adaptive tile summaries and hit diagnostic CSVs.
- Per tile, computed normal variance, first-accepted segment-index variance, Canny/Sobel edge-orientation circular variance, and collider switch rate.
- Each component was normalized to `[0, 1]` by checkpoint 95th percentile clipping.
- Phase coherence is `1 - mean(component incoherence)`; low coherence is treated as a phase-boundary proxy.
- Neighbor-normal delta was derived from adjacent first-accepted normal vectors in the CSV artifacts.

## Results
| checkpoint | mean coherence | band coherence | outside coherence | incoh vs band r | incoh vs normal-delta r | boundary in band | band on boundary |
|---|---:|---:|---:|---:|---:|---:|---:|
| `mouth` | 0.752 | 0.639 | 0.801 | 0.309 | 0.200 | 0.570 | 0.288 |
| `post_throat_backstep_01` | 0.788 | 0.764 | 0.796 | 0.071 | 0.194 | 0.275 | 0.163 |

## Verdict
Visible bands align with low-coherence phase-boundary structure in this adaptive-tile field.

Outputs:
- [phase_coherence_heatmap.png](phase_coherence_heatmap.png)
- [phase_boundary_overlay.png](phase_boundary_overlay.png)
- [phase_coherence_summary.json](phase_coherence_summary.json)
