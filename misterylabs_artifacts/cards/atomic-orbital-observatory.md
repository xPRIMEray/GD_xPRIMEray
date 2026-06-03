# Atomic Orbital GRIN Observatory — Light Bent by a Hydrogen Field

**Hook:** What does a hydrogen atom look like if you trace the light through it?

## Scientific Context

A hydrogen atom in s-state creates a radially symmetric GRIN (gradient-index) field — a spatially varying refractive index that curves light toward regions of high electron probability density. xPRIMEray treats this as an optical medium and integrates null geodesics through it. The observatory sweeps five configurations to show how the field shapes the image as parameters change.

## Observation

Five panels, from V0 to V4:

- **V0 — No field** — baseline geometry with no electron present. The room as a straight-ray renderer would see it.
- **V1 — Static hydrogen** — one electron at rest. The field is active; rays curve gently toward the orbital center. The effect is subtle at realistic strength.
- **V2 — Exaggerated hydrogen** — field strength and radius increased. The lens effect is now clearly visible as a focal distortion around the orbital region.
- **V3 / V4 — Temporal modulation, tick 0 / tick 1** — the electron's probability density oscillates with phase. Between V3 and V4, 2–11% of pixels change value — a visible fringe shift that marks the field's temporal dynamics.

Temporal diff metrics from the most recent run: changed\_fraction ≈ 0.11, mean\_abs\_channel\_delta ≈ 4.5, max\_channel\_delta 77.

## Why It Matters

This is the clearest demonstration that xPRIMEray renders *physics*, not approximations. The GRIN integrator makes the orbital's lensing effect measurable at the pixel level: turn the field off and get V0; turn it on and get V1. The difference is geometry, not a post-process filter.

## Next Step

Add a helium (two-electron) variant. Add a diff column to the contact sheet showing V0→V2 and V3→V4 pixel-level deltas inline. Render at higher resolution to resolve finer fringe detail.

---

*Source:* `output/atomic_orbital_visual_observatory/20260513T012903Z/`  
*Key image:* `visuals/atomic-orbital-observatory.png`  
*Tier:* 1 — Homepage
