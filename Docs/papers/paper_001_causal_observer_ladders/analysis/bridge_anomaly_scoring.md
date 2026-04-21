# Bridge Anomaly Scoring

Two artifact-only scores are reported below:

- `overall anomaly score`: root-mean-square robust z-score across the five characterization features
- `bridge signature score`: a directed score favoring low densities, low OPL mean, and high segments per crossing

## Score Table

| Checkpoint | Overall anomaly | Bridge signature | segments_per_crossing | throat_event_density | OPL mean |
|---|---:|---:|---:|---:|---:|
| `post_throat_backstep_01` | 2.1948 | 1.9527 | 366.0292 | 0.0555 | 7.5908 |
| `mouth` | 0.5832 | 0.3248 | 153.2590 | 0.0969 | 9.9599 |
| `post_throat_exit_approach` | 1.4857 | 0.1429 | 50.3105 | 0.2111 | 8.1171 |
| `mouth_to_throat_approach` | 0.3092 | 0.0941 | 139.6225 | 0.1048 | 9.7287 |
| `exit_lookback` | 2.0584 | 0.0899 | 60.9614 | 0.2198 | 8.4337 |
| `throat` | 0.2356 | 0.0000 | 128.1728 | 0.1139 | 9.5078 |

## Summary

- The strongest bridge-signature checkpoint is `post_throat_backstep_01`.
- The second-highest bridge-signature checkpoint is `mouth`.
- The bridge score is intentionally asymmetric: it rewards sparse, expensive transport rather than generic extremeness.

Figure: [bridge_anomaly_scores.png](figures/bridge_anomaly_scores.png)
