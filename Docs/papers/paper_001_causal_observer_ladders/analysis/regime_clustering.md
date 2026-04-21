# Regime Clustering

This first-pass clustering uses standardized ladder metrics derived from the frozen checkpoint sequence summary. Inputs are portal-hit density, throat-event density, crossings per pixel, segments per crossing, and OPL mean.

## Cluster Table

| Checkpoint | Cluster | portal_hit_density | throat_event_density | crossings_per_pixel | segments_per_crossing | OPL mean |
|---|---|---:|---:|---:|---:|---:|
| `mouth` | near-side/throat | 0.1465 | 0.0969 | 0.6495 | 153.2590 | 9.9599 |
| `mouth_to_throat_approach` | near-side/throat | 0.1633 | 0.1048 | 0.6987 | 139.6225 | 9.7287 |
| `throat` | near-side/throat | 0.1750 | 0.1139 | 0.7479 | 128.1728 | 9.5078 |
| `post_throat_backstep_01` | bridge | 0.0964 | 0.0555 | 0.2098 | 366.0292 | 7.5908 |
| `post_throat_exit_approach` | far-side | 0.1798 | 0.2111 | 1.6544 | 50.3105 | 8.1171 |
| `exit_lookback` | far-side | 0.2557 | 0.2198 | 1.4200 | 60.9614 | 8.4337 |

## Summary

- The clustering separates a `near-side/throat` family, a singleton `bridge` state, and a `far-side` family.
- The bridge cluster is isolated by low interaction density, low OPL mean, and extremely high segments per crossing.
- The far-side cluster groups the two highest-interaction checkpoints despite their different portal coverage profiles.

Figure: [regime_clustering.png](figures/regime_clustering.png)
