# Resonance Chamber Overlay Trial

## Purpose

Create a first-pass post-process overlay for the wormhole structure observatory that frames transport behavior as a chamber-like traversal: inbound packet, resonant throat, accumulated path density, tunnel exit, and domain ownership transition.

The tool is intentionally lightweight and screenshot-oriented. It reuses fixture images and telemetry maps already emitted under `output/` instead of introducing a new render path.

## Physics Inspiration

The visual language borrows from Schrodinger wave-packet tunneling, double-barrier resonant tunneling, and familiar double-slit / wave-interference imagery. The analogy is interpretive, not a claim that the renderer is solving quantum dynamics.

In this pass, high repeated transport cost stands in for phase buildup, throat-local density stands in for a resonant cavity, and ownership boundary changes stand in for transitions between outside, chamber, and exit domains.

## xPRIMEray Interpretation

For xPRIMEray, the chamber is a transport ownership basin around the wormhole throat. Boundary confidence and domain-id changes mark where the renderer repeatedly negotiates ownership. Portal ring density and sector telemetry identify where traversal concentrates near the throat. Step budget and usefulness maps hint at repeated or precision-sensitive paths.

The resulting overlay is a communication artifact: a compact way to show outside to chamber to exit behavior without changing the underlying fixture.

## Inputs Used

The first implementation looks for the latest wormhole fixture directory under `output/`, or accepts `--input <dir>`.

Preferred inputs are:

- Clean curved or composed wormhole fixture image.
- Domain ownership maps such as `domain_id`, `domain_confidence`, `boundary_confidence`, `selection_flip`, and `normal_discontinuity`.
- Portal ring-density and sector reports.
- Step budget, usefulness, hit, normal, convergence, or transport telemetry maps when present.

If a direct map is missing, the overlay creates a soft placeholder region and records the missing input in `resonance_chamber_summary.md`.

## Metrics Produced

The optional `resonance_chamber_metrics.csv` records one row per interpreted region:

- Region id and label.
- Source map or placeholder source.
- Approximate active coverage percentage.
- Mean signal strength on a 0 to 255 scale.
- Short note describing the interpretation.

## Known Limitations

- Inbound and exit plume regions are spatial guides until the fixture emits direct per-pixel transport direction or entry/exit telemetry.
- The phase buildup signal is a proxy derived from sector query density, step budget imagery, or usefulness maps, not a phase integral.
- Domain ownership transitions are estimated from image-space domain-id edges and selection flips.
- The overlay is tuned for quick research screenshots; it is not yet a quantitative validation harness.

## Next Steps

- Emit explicit throat-event and boundary-crossing density maps from the fixture.
- Add per-pixel entry, chamber, and exit ownership state transitions.
- Distinguish stable resonance from unstable precision debt in the phase buildup layer.
- Add a small golden-output smoke test once the fixture output schema is stable.
