# Wormhole Observer Ladder

This note presents a minimal static observer progression across the validated wormhole witness checkpoints. It is intentionally descriptive rather than cinematic: no traversal, no animation, and no extra rendering beyond the existing fixture outputs.

## Checkpoint A: Mouth Witness

![Checkpoint A capture](/home/bb/code/godot_xPRIMEray/output/fixture_runs/fixture_008_wormhole_witness_mouth/2026-04-18T17-21-36/capture.png)

- Run: [fixture_008_wormhole_witness_mouth/2026-04-18T17-21-36](/home/bb/code/godot_xPRIMEray/output/fixture_runs/fixture_008_wormhole_witness_mouth/2026-04-18T17-21-36)
- Meaning: stable near-mouth witness. The observer sees the near portal boundary and far-side mapping, but not yet active throat interaction.
- `throat_event_pixels = 0`
- `portal_hit_pixels = 3742`
- `boundary_crossings_total = 0`
- `run_verified = true`

## Checkpoint B: Throat Witness

![Checkpoint B capture](/home/bb/code/godot_xPRIMEray/output/fixture_runs/fixture_009_wormhole_witness_throat/2026-04-18T17-22-00/capture.png)

- Run: [fixture_009_wormhole_witness_throat/2026-04-18T17-22-00](/home/bb/code/godot_xPRIMEray/output/fixture_runs/fixture_009_wormhole_witness_throat/2026-04-18T17-22-00)
- Meaning: throat-positive static witness. The observer still sees the portal boundary, but the pose now samples active throat transport.
- `throat_event_pixels = 3933`
- `portal_hit_pixels = 6242`
- `boundary_crossings_total = 14878`
- `run_verified = true`

## Checkpoint C: Exit-side Look-back

![Checkpoint C capture](/home/bb/code/godot_xPRIMEray/output/fixture_runs/fixture_010_wormhole_witness_exit/2026-04-18T17-22-20/capture.png)

- Run: [fixture_010_wormhole_witness_exit/2026-04-18T17-22-20](/home/bb/code/godot_xPRIMEray/output/fixture_runs/fixture_010_wormhole_witness_exit/2026-04-18T17-22-20)
- Meaning: far-side observer looking back through the wormhole relation. This confirms the observer ladder can be completed from the exit side without traversal animation.
- `throat_event_pixels = 4742`
- `portal_hit_pixels = 9181`
- `boundary_crossings_total = 14290`
- `run_verified = true`

## Summary

The causal progression is visible and numeric:

| Checkpoint | Role | throat_event_pixels | portal_hit_pixels | boundary_crossings_total | run_verified |
|---|---|---:|---:|---:|---|
| A | Mouth witness | 0 | 3742 | 0 | true |
| B | Throat witness | 3933 | 6242 | 14878 | true |
| C | Exit look-back | 4742 | 9181 | 14290 | true |

Taken together, the ladder reads as:

1. establish the mouth and far-side mapping,
2. move into a throat-positive static witness,
3. confirm the exit-side observer can look back through the same causal relation.
