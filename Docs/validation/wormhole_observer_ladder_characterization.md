# Wormhole Observer Ladder Characterization

This note provides a paper-ready characterization layer for the approved mixed-strategy wormhole observer ladder. It is derived entirely from existing fresh-instance checkpoint artifacts with optical-path reporting enabled. No rendering, transport, BLV, or traversal behavior was changed.

Source run:
[fixture_011_wormhole_checkpoint_sequence/2026-04-20T22-26-39](/home/bb/code/godot_xPRIMEray/output/fixture_runs/fixture_011_wormhole_checkpoint_sequence/2026-04-20T22-26-39)

## Derived Metrics

- `portal_hit_density = portal_hit_pixels / total_pixels`
- `throat_event_density = throat_event_pixels / total_pixels`
- `crossings_per_pixel = boundary_crossings_total / total_pixels`
- `segments_per_crossing = TotalEmittedRaySegCount / boundary_crossings_total`
- `OPL variance`: unavailable in the current checkpoint summaries, so it is intentionally left unreported rather than inferred from mean/max alone

## Canonical Table

| Checkpoint | Class | OPL mean | OPL max | portal_hit_density | throat_event_density | crossings_per_pixel | segments_per_crossing | AverageSegmentsPerRay | runVerified |
|---|---|---:|---:|---:|---:|---:|---:|---:|---|
| `mouth` | `near-side` | 9.9599 | 15.8071 | 0.1465 | 0.0969 | 0.6495 | 153.2590 | 99.5403 | true |
| `mouth_to_throat_approach` | `near-side` | 9.7287 | 15.5162 | 0.1633 | 0.1048 | 0.6987 | 139.6225 | 97.5569 | true |
| `throat` | `throat` | 9.5078 | 15.1926 | 0.1750 | 0.1139 | 0.7479 | 128.1728 | 95.8566 | true |
| `post_throat_backstep_01` | `bridge` | 7.5908 | 12.2611 | 0.0964 | 0.0555 | 0.2098 | 366.0292 | 76.7983 | true |
| `post_throat_exit_approach` | `far-side` | 8.1171 | 14.8980 | 0.1798 | 0.2111 | 1.6544 | 50.3105 | 83.2336 | true |
| `exit_lookback` | `far-side` | 8.4337 | 16.3070 | 0.2557 | 0.2198 | 1.4200 | 60.9614 | 86.5624 | true |

## Regime Classification

- `near-side`: `mouth`, `mouth_to_throat_approach`
- `throat`: `throat`
- `bridge`: `post_throat_backstep_01`
- `far-side`: `post_throat_exit_approach`, `exit_lookback`

## Transition Notes

- OPL mean decreases monotonically across the near-side leg: `mouth -> mouth_to_throat_approach -> throat`.
- OPL mean reaches its minimum at the bridge checkpoint `post_throat_backstep_01`.
- OPL mean then increases again on the far side: `post_throat_backstep_01 -> post_throat_exit_approach -> exit_lookback`.
- Portal-hit density peaks at `exit_lookback`.
- Throat-event density and crossings-per-pixel both peak at `post_throat_exit_approach`.
- Transport cost per crossing changes regime sharply at the bridge checkpoint:
  near-side values fall from `153.2590` to `128.1728`, then jump to `366.0292` at `post_throat_backstep_01`, before dropping to `50.3105` and `60.9614` on the far side.

## Interpretation

The ladder separates cleanly into two traversal regimes. The near-side regime is smooth and compression-like: optical path mean decreases while interaction densities rise and segment cost per crossing falls gradually toward the throat. This is consistent with an observer path that becomes progressively more interaction-rich without a disruptive change in transport character.

The throat itself behaves as the transition hinge rather than the far-side bridge. It completes the near-side decline in optical path mean while maintaining a monotone increase in portal-hit density, throat-event density, and crossings-per-pixel.

The bridge checkpoint behaves uniquely. `post_throat_backstep_01` is not simply a weaker far-side view; it marks a distinct sparse-transition regime with the lowest optical path mean, the lowest interaction densities after the throat, and the highest segment cost per crossing in the entire ladder. In paper terms, it is best interpreted as a topological bridge state rather than a linear continuation of the near-side interpolation family.

After that bridge, the far-side regime re-densifies. `post_throat_exit_approach` carries the strongest wormhole interaction load, while `exit_lookback` produces the largest portal coverage and the highest OPL max, indicating a broader tail of long optical paths on the stabilized far-side observer view.
