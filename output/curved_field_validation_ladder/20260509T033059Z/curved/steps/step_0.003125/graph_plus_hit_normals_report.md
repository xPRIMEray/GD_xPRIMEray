# Graph Plus Hit Normals Report

Post-process diagnostic report only. This does not modify renderer behavior and does not claim physical truth.

## Outputs

- full_frame_hit_normals: `full_frame_hit_normals.png`
- roi_hit_normals: not generated
- unstable_subgraph_hit_normals: `unstable_subgraph_hit_normals.png`
- merge_split_hit_normals: not generated

## Normal Coherence Inside Ownership Nodes

| node | hit samples | mean angle | max angle | label |
|---:|---:|---:|---:|---|
| 0 | 1986 | 30.359118 | 126.920145 | incoherent |
| 1 | 82 | 0.0 | 0.0 | coherent |
| 2 | 80 | 0.0 | 0.0 | coherent |
| 3 | 80 | 0.0 | 0.0 | coherent |
| 4 | 85 | 0.0 | 0.0 | coherent |
| 5 | 110 | 0.0 | 0.0 | coherent |
| 6 | 115 | 0.0 | 0.0 | coherent |
| 7 | 52 | 0.0 | 0.0 | coherent |
| 8 | 40 | 0.0 | 0.0 | coherent |
| 9 | 38 | 0.0 | 0.0 | coherent |
| 10 | 24 | 12.147964 | 19.9555 | mixed |
| 11 | 32 | 0.0 | 0.0 | coherent |
| 12 | 27 | 0.0 | 0.0 | coherent |
| 13 | 58 | 21.644836 | 46.482994 | incoherent |
| 14 | 27 | 0.0 | 0.0 | coherent |
| 15 | 22 | 0.0 | 0.0 | coherent |
| 16 | 76 | 28.683376 | 74.137171 | incoherent |
| 17 | 22 | 0.0 | 0.0 | coherent |
| 18 | 17 | 0.0 | 0.0 | coherent |
| 19 | 88 | 33.495205 | 94.950028 | incoherent |
| 20 | 17 | 0.0 | 0.0 | coherent |
| 21 | 13 | 0.0 | 0.0 | coherent |
| 22 | 94 | 35.032996 | 97.528768 | incoherent |
| 23 | 13 | 0.0 | 0.0 | coherent |
| 24 | 8 | 0.0 | 0.0 | coherent |
| 25 | 98 | 36.372633 | 107.955715 | incoherent |
| 26 | 8 | 0.0 | 0.0 | coherent |
| 27 | 100 | 37.112029 | 102.187679 | incoherent |
| 28 | 2 | 0.0 | 0.0 | coherent |
| 29 | 100 | 38.103774 | 117.868736 | incoherent |
| 30 | 2 | 0.0 | 0.0 | coherent |
| 31 | 4 | 0.0 | 0.0 | coherent |
| 32 | 94 | 35.19783 | 97.519768 | incoherent |
| 33 | 4 | 0.0 | 0.0 | coherent |
| 34 | 4 | 0.0 | 0.0 | coherent |
| 35 | 88 | 33.657704 | 91.432369 | incoherent |
| 36 | 4 | 0.0 | 0.0 | coherent |
| 37 | 6 | 0.0 | 0.0 | coherent |
| 38 | 78 | 30.447974 | 78.119042 | incoherent |
| 39 | 6 | 0.0 | 0.0 | coherent |

## Seam Alignment

- Edge count: 46
- Edges with normal deltas: 46
- Max normal-angle delta: 0.0
- Assessment: No strong seam/normal-discontinuity alignment was measured.

## Unresolved Islands

- Unstable rows: 0
- Assessment: No unstable subgraph rows were present; no abnormal island normal behavior can be assessed.

## Merge/Split Regions

- Merge/split rows: 0
- Assessment: No merge/split rows were present in this graph packet.
