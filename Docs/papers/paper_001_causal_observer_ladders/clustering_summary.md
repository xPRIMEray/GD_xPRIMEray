# Observer Ladder Clustering Summary

Source artifact: [checkpoint_sequence_summary.json](/home/bb/code/godot_xPRIMEray/output/fixture_runs/fixture_011_wormhole_checkpoint_sequence/2026-04-20T22-26-39/checkpoint_sequence_summary.json)

This analysis uses only the saved wormhole ladder checkpoint summary. Features were standardized before clustering.

## Checkpoint Feature Table

| Checkpoint | Manual regime | Discovered cluster | OPL mean | OPL max | portal_hit_density | throat_event_density | crossings_per_pixel | segments_per_crossing | AverageSegmentsPerRay |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|
| `mouth` | `near-side` | `cluster_2` | 9.9599 | 15.8071 | 0.1465 | 0.0969 | 0.6495 | 153.2590 | 99.5403 |
| `mouth_to_throat_approach` | `near-side` | `cluster_2` | 9.7287 | 15.5162 | 0.1633 | 0.1048 | 0.6987 | 139.6225 | 97.5569 |
| `throat` | `throat` | `cluster_2` | 9.5078 | 15.1926 | 0.1750 | 0.1139 | 0.7479 | 128.1728 | 95.8566 |
| `post_throat_backstep_01` | `bridge` | `cluster_1` | 7.5908 | 12.2611 | 0.0964 | 0.0555 | 0.2098 | 366.0292 | 76.7983 |
| `post_throat_exit_approach` | `far-side` | `cluster_0` | 8.1171 | 14.8980 | 0.1798 | 0.2111 | 1.6544 | 50.3105 | 83.2336 |
| `exit_lookback` | `far-side` | `cluster_0` | 8.4337 | 16.3070 | 0.2557 | 0.2198 | 1.4200 | 60.9614 | 86.5624 |

## Clustering Comparison

| Method | k | ARI vs manual labels | Silhouette |
|---|---:|---:|---:|
| `agglomerative` | 2 | 0.1429 | 0.4320 |
| `agglomerative` | 3 | 0.5946 | 0.5547 |
| `agglomerative` | 4 | 0.2857 | 0.4020 |
| `agglomerative` | 5 | -0.0976 | 0.1067 |
| `kmeans` | 2 | 0.1429 | 0.4320 |
| `kmeans` | 3 | 0.5946 | 0.5547 |
| `kmeans` | 4 | 0.2857 | 0.4020 |
| `kmeans` | 5 | -0.0976 | 0.1067 |

## Interpretation

- The best alignment with the current manual regime labels was `agglomerative` at `k=3`, with `ARI=0.5946` and `silhouette=0.5547`.
- The bridge checkpoint `post_throat_backstep_01` falls in `cluster_1` with cluster size `1`.
- Bridge outlier verdict: `yes`. It is isolated as a singleton cluster under the best-performing automatic clustering pass.
- The automatic clustering still groups the near-side mouth approach and throat progression together more strongly than the manual taxonomy separates `throat` from the rest of the near-side leg.

Figures:

- [cluster_pca_scatter.png](figures/cluster_pca_scatter.png)
- [cluster_dendrogram.png](figures/cluster_dendrogram.png)
