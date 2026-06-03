# Wormhole Structure Observatory — Six Diagnostic Views of One Scene

**Hook:** The same wormhole rendered six ways — each mode reveals a different physical property that the beauty render hides.

## Scientific Context

A rendered image tells you what something *looks like*. An observatory tells you what it *is*. This artifact runs the wormhole fixture through six diagnostic rendering modes in sequence, building a composite picture of the transport structure that no single image can convey alone.

## Observation

Six panels, each answering a different question:

- **Clean curved** — baseline perception. What the observer sees.
- **Straight vs. curved** — side-by-side comparison isolating the wormhole's distortion contribution. The straight render is the counterfactual.
- **Depth heatmap** — ray travel distance per pixel, revealing which parts of the image required deep integration versus shallow hits.
- **Step budget heatmap** — integration steps consumed per pixel. The most expensive regions are not always the most visually prominent.
- **Domain diagnostics** — per-pixel domain ID, boundary confidence, normal discontinuity, and selection-flip maps. The domain resolver's internal state made visible.
- **Structure minimap** — a reduced spatial summary of the portal ring geometry and sector occupancy.

The resonance chamber overlay synthesizes boundary confidence, ring density, phase accumulation, and ownership transitions into a single annotated image using wave-packet-inspired vocabulary: resonant core (21% coverage), chamber wall interaction (34%), phase buildup (28%), ownership transition (31%).

## Why It Matters

Observatory mode is the core diagnostic philosophy of xPRIMEray: run the same scene with multiple interpretive layers and publish the full evidence set. No single mode is "the truth" — the truth is the ensemble. The resonance chamber overlay shows that physics-inspired vocabulary (tunneling, phase buildup, resonance) can be grounded in measurable renderer telemetry rather than speculation.

## Next Step

Run the observatory on additional scene types (GRIN room, mirror portal) to build a comparable multi-modal view of each. Add a panel-to-panel diff view so changes across runs are immediately visible.

---

*Source:* `output/wormhole_structure_observatory/20260514T045629Z/`  
*Key image:* `visuals/wormhole-structure-observatory.png`  
*Tier:* 1 — Homepage
