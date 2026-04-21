# Checkpoint Anomaly Scoring

This analysis uses only the frozen wormhole ladder metric summary. Features were standardized before anomaly scoring.

## Feature Set

- OPL mean
- OPL max
- portal-hit density
- throat-event density
- crossings per pixel
- segments per crossing
- average segments per ray

## Ranked Results

| Checkpoint | z-score distance | isolation forest | local outlier factor | mean rank |
|---|---:|---:|---:|---:|
| `post_throat_backstep_01` | 4.3999 | 0.6160 | 1.3496 | 1.00 |
| `exit_lookback` | 2.9376 | 0.5157 | 0.9677 | 3.00 |
| `mouth` | 2.0098 | 0.4512 | 1.0020 | 3.67 |
| `post_throat_exit_approach` | 2.5274 | 0.4898 | 0.9622 | 4.00 |
| `throat` | 1.1279 | 0.4208 | 1.0489 | 4.33 |
| `mouth_to_throat_approach` | 1.5206 | 0.3949 | 1.0020 | 4.67 |

## Interpretation

- Dominant transport anomaly verdict: `yes`.
- The top-ranked checkpoint by combined anomaly ranking is `post_throat_backstep_01`.
- The bridge checkpoint `post_throat_backstep_01` has scores `z=4.3999`, `iforest=0.6160`, and `lof=1.3496`.
- In paper-ready terms, the bridge is the strongest multi-metric outlier when anomaly is defined by simultaneous sparsity, transport inefficiency, and depressed optical path mean relative to the rest of the ladder.

Figure:

- [checkpoint_anomaly_scores.png](figures/checkpoint_anomaly_scores.png)
