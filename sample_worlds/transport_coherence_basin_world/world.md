# Transport Coherence Basin World

**Status:** Design proposal  
**Scene:** `test-domain-resolver-stress.tscn`  
**Recommended build priority:** 5 (Tier 2 — most technically specialized)

## Purpose

Let the visitor walk around in the transport field's instability landscape — not as a data chart but as a spatial experience. The 289 UNSEALED_NONCONVERGENT regions from the coherence basin experiment are not abstract statistics; they are specific locations in the rendered film, clustered in a symmetric annular pattern. This world makes their geometry observable and their cause investigatable.

The central experience: the visitor sees a rendered scene with a risk region overlay active. Bright regions are unstable — the oracle cannot converge on a transport solution there regardless of how fine the precision floor is set. The visitor can explore why those regions are unstable (high curvature, ownership ambiguity, topological feature), and can experiment with field parameterization changes to see whether the instability moves or disappears.

This is an advanced world, primarily for contributors and researchers. The phenomena it demonstrates are real but require some background to appreciate.

## What the Visitor Can Observe

1. **Risk region overlay** — 289 instability zones visualized as bright regions on the film. Two symmetric horizontal bands at pixel coordinates (128, 58) and (128, 122) — the GRIN field boundary annulus.
2. **Precision floor map** — all 289 regions require the same precision floor (0.003125). The uniformity of this floor is a finding: it points to a topological feature, not continuous degradation.
3. **Radial risk profile** — risk magnitude as a function of distance from the band center. The profile decays but never reaches zero in the probed domain.
4. **Coherence decay curve** — shows a step discontinuity rather than smooth decay. Consistent with a topological feature in the transport field.
5. **Field parameterization experiment** — (proposed) the visitor can adjust the IOR gradient at the GRIN outer shell and see whether the instability region moves, shrinks, or disappears. This is the critical "is it the parameterization or the topology?" experiment.

## Relevant Promoted Artifacts

- `transport-coherence-radial.png` — radial risk profile this world displays
- `transport-coherence-risk-vs-step.png` — risk vs. step-by-anchor plot
- `datasets/transport-coherence-risk-nodes.csv` — 289-node risk dataset this world visualizes
- `validation/transport-coherence-basin.md` — the risk region report

## Suggested Overlays

Core:
1. `transport_coherence` — instability heatmap over the film render
2. `validation_hud` — risk region count, precision floor, sealed vs. unsealed summary

Supporting:
- `heatmap_normals` — shows why instability is where it is (high curvature at the same locations)
- `cathedral_probe` — full six-layer diagnostic for contributors investigating specific seam locations
- `atlas_labels` — labels the GRIN field source, outer shell boundary, and instability band regions

## Suggested Toggles

| Toggle | Options |
|--------|---------|
| Risk display | Off / Heatmap / Region outlines / Ranked list |
| Precision floor | Show all regions at any precision / Show only at finest floor (0.003125) |
| Anchor filter | All anchors / Top 10 risk nodes / Selected region |
| Field IOR gradient | Current value / Softened / Sharp — compare instability response |

## Validation Question

*"How many instability regions should the oracle identify at epsilon=0.05?"*

Expected answer: 289 UNSEALED_NONCONVERGENT regions, all at precision=0.003125. If the count or the required precision changes, either the oracle or the scene parameterization has changed — this is a meaningful regression signal.

*"Are the instability bands symmetric around the vertical midline?"*

Expected answer: yes — the two bands center on y=58 and y=122, which are symmetric around the scene midline (y=135 for a 270-row film). Asymmetry would indicate a change in the GRIN field's radial structure.

## MisterY Labs Exhibit Connection

This world supports the "Transport Coherence Basin — 289 Instability Regions" research atlas card. The card presents the finding; this world makes the spatial structure navigable.

This world is best presented as a researcher tool rather than a general visitor experience. The exhibit entry should indicate: *"For contributors investigating transport stability. Requires familiarity with the coherence basin methodology."*

Exhibit caption suggestion:
> *"289 locations where the transport oracle runs out of precision before it can converge. All of them in the same place, for the same reason — the GRIN field boundary is a topological feature the oracle cannot smooth over."*

## What Is Missing

- [ ] A runtime instability map that updates live as scene parameters change (currently only available as static PNGs from the smoke run)
- [ ] An interactive IOR gradient slider for testing "is it the parameterization?" live
- [ ] A "convergence replay" mode showing the oracle's precision refinement sequence for a selected risk node
- [ ] A risk region annotation overlay that shows individual region IDs, centers, radii, and precision floors from the risk\_nodes.csv
- [ ] Rendering at a resolution that makes the 289 individual regions distinguishable (currently at 480×270, the annular bands overlap into continuous stripes)
- [ ] The `test-domain-resolver-stress.tscn` scene needs the same parameterization as the promoted artifact confirmed (step=0.00625 or step=0.015 depending on which run produced the 289 regions)
