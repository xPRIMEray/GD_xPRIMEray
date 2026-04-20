# Wormhole Observer Ladder

This note locks the approved non-interpolated wormhole traversal baseline as a five-checkpoint fresh-instance ladder. It is intentionally sparse: no interpolation, no geometry edits, and no transport changes. Each checkpoint is a standalone-equivalent observer state inside the sequenced fixture path.

Baseline run:
[fixture_011_wormhole_checkpoint_sequence/2026-04-20T18-27-07](/home/bb/code/godot_xPRIMEray/output/fixture_runs/fixture_011_wormhole_checkpoint_sequence/2026-04-20T18-27-07)

The approved sparse progression is now:

1. `mouth`
2. `mouth_to_throat_approach`
3. `throat`
4. `post_throat_exit_approach`
5. `exit_lookback`

## Approved Checkpoints

### 00 Mouth

![00 mouth](/home/bb/code/godot_xPRIMEray/output/fixture_runs/fixture_011_wormhole_checkpoint_sequence/2026-04-20T18-27-07/00_mouth_capture.png)

- Meaning: near-mouth witness showing the portal boundary and mapped far-side relation.
- Transform: `Transform3D(0.890906, 0, 0.454187, 0.062862, 0.990376, -0.123306, -0.449816, 0.138405, 0.882332, -2.35, 1.05, 3.55)`
- `runVerified = true`

### 01 Mouth-To-Throat Approach

![01 mouth-to-throat approach](/home/bb/code/godot_xPRIMEray/output/fixture_runs/fixture_011_wormhole_checkpoint_sequence/2026-04-20T18-27-07/01_mouth_to_throat_approach_capture.png)

- Meaning: conservative densification of the highest-effort leg between the mouth and throat witnesses.
- Transform: `Transform3D(0.907106, 0, 0.420902, 0.059122, 0.990086, -0.127417, -0.416729, 0.140466, 0.898113, -2.085, 0.985, 3.465)`
- `runVerified = true`

### 02 Throat

![02 throat](/home/bb/code/godot_xPRIMEray/output/fixture_runs/fixture_011_wormhole_checkpoint_sequence/2026-04-20T18-27-07/02_throat_capture.png)

- Meaning: throat-positive witness anchored at the validated observer pose.
- Transform: `Transform3D(0.922063, 0, 0.387039, 0.055236, 0.989764, -0.131592, -0.383077, 0.142715, 0.912625, -1.82, 0.92, 3.38)`
- `runVerified = true`

### 03 Post-Throat Exit Approach

![03 post-throat exit approach](/home/bb/code/godot_xPRIMEray/output/fixture_runs/fixture_011_wormhole_checkpoint_sequence/2026-04-20T18-27-07/03_post_throat_exit_approach_capture.png)

- Meaning: first valid post-throat checkpoint on the far side, replacing the failed linear throat-to-exit midpoint.
- Transform: `Transform3D(0.931609, 0, -0.363462, -0.052292, 0.989596, -0.134033, 0.35968, 0.143872, 0.921917, 23.4, 0.92, 3.35)`
- `runVerified = true`

### 04 Exit Look-back

![04 exit look-back](/home/bb/code/godot_xPRIMEray/output/fixture_runs/fixture_011_wormhole_checkpoint_sequence/2026-04-20T18-27-07/04_exit_lookback_capture.png)

- Meaning: far-side look-back witness confirming the exit-side observer relation.
- Transform: `Transform3D(0.931609, 0, -0.363462, -0.052292, 0.989596, -0.134033, 0.35968, 0.143872, 0.921917, 25.65, 0.92, 3.35)`
- `runVerified = true`

## Compact Comparison

| Checkpoint | portal_hit_pixels | throat_event_pixels | boundary_crossings_total | TotalEmittedRaySegCount | AverageSegmentsPerRay | runVerified |
|---|---:|---:|---:|---:|---:|---|
| `mouth` | 18969 | 12559 | 84174 | 12903836 | 99.5666 | true |
| `mouth_to_throat_approach` | 21155 | 13585 | 90554 | 12645509 | 97.5734 | true |
| `throat` | 22684 | 14770 | 96924 | 12423032 | 95.8567 | true |
| `post_throat_exit_approach` | 23313 | 27351 | 214410 | 10787760 | 83.2389 | true |
| `exit_lookback` | 33148 | 28487 | 184026 | 11219542 | 86.5705 | true |

## Interpretation

Transport effort by emitted segment count is highest at `mouth`, then steps down steadily through `mouth_to_throat_approach` and `throat`. The strongest wormhole interaction load appears at `post_throat_exit_approach`, where `throat_event_pixels` and `boundary_crossings_total` peak before settling slightly at `exit_lookback`.

## Next Step

The next small step is either:

1. discover one more valid sparse checkpoint between `post_throat_exit_approach` and `exit_lookback`, or
2. introduce sparse interpolation between these approved five anchors while keeping fresh-instance parity checks available as the reference discipline.
