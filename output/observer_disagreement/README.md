# Observer Disagreement

Short name: **Observer Disagreement — Curved vs. Straight Classification Delta**

## Purpose

Quantifies how much the curved GRIN transport and the straight reference transport disagree in ray classification (budget\_exhausted, escaped\_no\_hit, geom\_hit). A high disagreement rate is expected and desirable — it proves the GRIN field is actually bending rays to different outcomes, not just adding noise.

## Source / Generation Context

- Scene: straight off-axis observe scene vs. GRIN off-axis scene
- Outputs: `output/observer_disagreement/` with two subdirs

## What the Output Shows

- `classification_delta.png` — Pixel map of rays that classified differently between curved and straight transport.
- `straight_offaxis_observe_beauty.png` / transport classification PNGs — Per-transport rendered beauty and classification views.
- `classification_delta_summary.json` — Counts: curved scene had 22,464 budget\_exhausted + 60,295 escaped + 46,841 geom\_hit; straight had 38,336 budget\_exhausted + 20,964 escaped + 70,300 geom\_hit. Large swap between escaped and budget\_exhausted confirms significant trajectory divergence.

## Key Files

- `classification_delta.png` — Ray-outcome disagreement map
- `classification_delta_summary.json` — Per-class counts for curved vs. straight
- `straight_offaxis_observe_beauty.png` — Straight transport beauty render
- `straight_offaxis_observe_transport_classification.png` — Straight transport class map
- `packet_manifest.json` — Run metadata

## Suggested MisterY Labs Card Summary

A disagreement map: two transports, same scene, same rays — one straight, one bent by GRIN. The difference in ray outcomes (which ones escape, hit geometry, or run out of budget) makes the curvature field's effect measurable rather than just visible.

## Status

Validation candidate

## Notes / Next Steps

- High disagreement is the correct result. Document the expected range as a regression gate.
- Add a third panel: GRIN-only curved beauty for three-way comparison.
