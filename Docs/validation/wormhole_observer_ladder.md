# Wormhole Observer Ladder

This note locks the approved wormhole traversal baseline as a six-checkpoint fresh-instance ladder. It uses a mixed strategy:

- an interpolation-friendly near-side leg
- a discovered-checkpoint hard leg on the far side

The ladder remains sparse: no geometry edits, no transport changes, and no continuous traversal.

Near-side baseline run:
[fixture_011_wormhole_checkpoint_sequence/2026-04-20T18-27-07](/home/bb/code/godot_xPRIMEray/output/fixture_runs/fixture_011_wormhole_checkpoint_sequence/2026-04-20T18-27-07)

Hard-leg discovery confirmation:
[fixture_013_wormhole_throat_exit_interpolation/2026-04-20T19-52-15](/home/bb/code/godot_xPRIMEray/output/fixture_runs/fixture_013_wormhole_throat_exit_interpolation/2026-04-20T19-52-15)

The approved sparse progression is now:

1. `mouth`
2. `mouth_to_throat_approach`
3. `throat`
4. `post_throat_backstep_01`
5. `post_throat_exit_approach`
6. `exit_lookback`

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

### 03 Post-Throat Backstep 01

![03 post-throat backstep](/home/bb/code/godot_xPRIMEray/output/fixture_runs/fixture_013_wormhole_throat_exit_interpolation/2026-04-20T19-52-15/01_post_throat_backstep_01_capture.png)

- Meaning: discovered hard-leg checkpoint reached by a small backstep from the validated post-throat exit-side anchor. This is not a transform interpolation result.
- Transform: `Transform3D(0.931609, 0, -0.363462, -0.052292, 0.989596, -0.134033, 0.35968, 0.143872, 0.921917, 22.2, 0.92, 3.35)`
- `runVerified = true`

### 04 Post-Throat Exit Approach

![04 post-throat exit approach](/home/bb/code/godot_xPRIMEray/output/fixture_runs/fixture_011_wormhole_checkpoint_sequence/2026-04-20T18-27-07/03_post_throat_exit_approach_capture.png)

- Meaning: first strong far-side post-throat witness that keeps wormhole interaction high.
- Transform: `Transform3D(0.931609, 0, -0.363462, -0.052292, 0.989596, -0.134033, 0.35968, 0.143872, 0.921917, 23.4, 0.92, 3.35)`
- `runVerified = true`

### 05 Exit Look-back

![05 exit look-back](/home/bb/code/godot_xPRIMEray/output/fixture_runs/fixture_011_wormhole_checkpoint_sequence/2026-04-20T18-27-07/04_exit_lookback_capture.png)

- Meaning: far-side look-back witness confirming the exit-side observer relation.
- Transform: `Transform3D(0.931609, 0, -0.363462, -0.052292, 0.989596, -0.134033, 0.35968, 0.143872, 0.921917, 25.65, 0.92, 3.35)`
- `runVerified = true`

## Compact Comparison

| Checkpoint | portal_hit_pixels | throat_event_pixels | boundary_crossings_total | TotalEmittedRaySegCount | AverageSegmentsPerRay | runVerified |
|---|---:|---:|---:|---:|---:|---|
| `mouth` | 18969 | 12559 | 84174 | 12903836 | 99.5666 | true |
| `mouth_to_throat_approach` | 21155 | 13585 | 90554 | 12645509 | 97.5734 | true |
| `throat` | 22684 | 14770 | 96924 | 12423032 | 95.8567 | true |
| `post_throat_backstep_01` | 12482 | 7185 | 27192 | 9954012 | 76.8056 | true |
| `post_throat_exit_approach` | 23313 | 27351 | 214410 | 10787760 | 83.2389 | true |
| `exit_lookback` | 33148 | 28487 | 184026 | 11219542 | 86.5705 | true |

## Interpretation

The near-side leg is interpolation-friendly: `mouth`, `mouth_to_throat_approach`, and `throat` rise smoothly in portal, throat, and crossing load while segment cost steps down gradually.

The hard leg is discovery-driven: `post_throat_backstep_01` reintroduces valid wormhole interaction without relying on invalid world-space interpolation, then `post_throat_exit_approach` jumps to the strongest far-side interaction load before settling slightly at `exit_lookback`.

Transport effort by emitted segment count is still highest at `mouth`. The strongest wormhole interaction load appears at `post_throat_exit_approach`, while `post_throat_backstep_01` acts as the first stable sparse bridge on the hard leg.

## Next Step

The next small step is either:

1. discover one more valid sparse checkpoint between `post_throat_backstep_01` and `post_throat_exit_approach`, or
2. keep interpolation experiments constrained to the approved near-side leg while using discovered checkpoints on the hard leg.
