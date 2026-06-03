# Cathedral Probe World

**Status:** Design proposal  
**Scene:** `test-domain-resolver-stress.tscn`  
**Recommended build priority:** 4 (Tier 2 — after the first three Tier 1 worlds)

## Purpose

Make the Cathedral Probe methodology interactive — the full layered transport diagnostic observatory in navigable form. A developer or advanced visitor uses this world to understand how xPRIMEray diagnoses transport problems that are invisible to conventional debugging.

The Cathedral Probe world is the interactive version of the architecture document (`Docs/Research/cathedral_probe_architecture.md`) — a six-layer observatory that makes transport coherence structure legible as a visual space rather than a log file.

This world is explicitly aimed at a more technical audience than the first three Tier 1 worlds. The central narrative: *how do you debug a renderer that doesn't crash, just produces wrong pixels?* The six Cathedral Probe layers are the answer.

## What the Visitor Can Observe

The six Cathedral Probe layers, each activatable independently:

1. **Coherence vectors** — per-pixel transport continuity vectors showing the magnitude and direction of transport change at ownership boundaries. Dense clusters indicate instability zones.
2. **Normal discontinuity map** — pixel-level map of surface normal change across neighboring samples. Sharp edges in this map correspond to domain ownership transitions.
3. **Curvature accumulation** — cumulative absolute turn-angle per pixel. The same heat map used in other worlds, but here it's one diagnostic layer among six rather than the primary visualization.
4. **Domain ownership** — per-pixel color coding of which transport domain "owns" each ray's resolution. Boundaries between ownership regions are transport fault lines.
5. **Transport seam detection** — highlights pixels where ownership could plausibly be assigned to multiple domains — the ambiguous zone where first-hit instability lives.
6. **Step budget allocation** — how many integration steps each pixel consumed. Expensive regions are high-curvature and high-ownership-ambiguity regions simultaneously.

The composite of all six layers is the full Cathedral Probe image — the six-layer overlay shown in `Docs/assets/cathedral_probe/cathedral_probe_overlay_row_0015.png`.

Additionally: the DOE Scheduler Resonance finding is directly observable in this world by switching traversal modes (row vs. tile) and watching the step budget allocation heatmap change.

## Relevant Promoted Artifacts

- `doe-scheduler-resonance-heatmap.png` — the `band_by_row_mod_stride_heatmap.png` this world reproduces interactively
- `doe-scheduler-resonance-stride-plot.png` — the stride-vs-band-coverage plot referenced in the HUD
- `datasets/doe-scheduler-resonance.csv` — the underlying data this world draws from
- `transport-coherence-radial.png` — the radial risk profile (from the adjacent coherence basin world)

Also: `Docs/assets/cathedral_probe/cathedral_probe_overlay_row_0015.png` — the reference composite overlay this world should reproduce.

## Suggested Overlays

All six Cathedral Probe layers are the primary content:
1. `cathedral_probe` (composite) — or individual layers:
   - Coherence vectors
   - Normal discontinuity
   - Curvature accumulation
   - Domain ownership
   - Transport seam detection
   - Step budget allocation

Supporting:
- `validation_hud` — shows live transport health metrics
- `atlas_labels` — labels domain boundary regions
- `ray_traces` (sparse) — shows individual paths near identified instability zones

## Suggested Toggles

| Toggle | Options |
|--------|---------|
| Cathedral Probe layers | Each of the six layers independently, plus composite |
| Traversal mode | Row / Tile / Checkerboard — observe stride resonance live |
| Stride value | 1, 2, 4, 8 — watch band coverage collapse at stride=4 |
| Step length | 0.00625 to 0.025 — observe curvature band visibility change |
| Composite mode | All six / selected subset / one at a time |

## Validation Question

*"At stride=4 with row traversal and step=0.0125, what percentage of pixels should fall in the high-curvature band?"*

Expected answer from the DOE: approximately 0.22% (compared to 33.0% at stride=1). The visitor can observe the band-coverage collapse directly by switching traversal stride in this world.

Secondary question: *"Which Cathedral Probe layer best predicts where the stride resonance will hit hardest?"*
Expected answer: the step-budget-allocation layer, because resonance amplifies the work load on specific row offsets — which shows up as elevated budget consumption in periodic stripes.

## MisterY Labs Exhibit Connection

This world connects to the "DOE Scheduler Resonance" research atlas card and the Cathedral Probe architecture document. It is the most technically deep public demonstration of xPRIMEray's diagnostic methodology.

Exhibit caption suggestion:
> *"Six diagnostic layers assembled into one composite view: the Cathedral Probe. This is how xPRIMEray finds transport failures that look like correct renders."*

The Cathedral Probe world is primarily for research visitors and contributors. It should be discoverable from the research atlas pages, not placed on the homepage.

## What Is Missing

- [ ] A runtime implementation of individual Cathedral Probe layer toggle (currently the six layers are only available as a static composite PNG)
- [ ] An interactive stride slider that shows the band-coverage collapse in real time
- [ ] The composite overlay rendered at multiple step lengths side-by-side for comparison
- [ ] A "diagnostic replay" mode: step through the Cathedral Probe layers one at a time with automated captions explaining what each one shows
- [ ] Confirmation that `test-domain-resolver-stress.tscn` is the correct scene for reproducing the published composite overlay
