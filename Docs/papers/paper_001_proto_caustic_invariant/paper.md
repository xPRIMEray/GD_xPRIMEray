---
title: Proto-Caustic Invariant in Geometry-Aware Wormhole Transport
authors:
date: 2026-04-05
invariant: proto_caustic_annulus_layer_1_radial_bin_3
status: validated
related_fixtures:
  - wormhole_validation_harness
---

# Paper 001: Proto-Caustic Invariant in Geometry-Aware Wormhole Transport

![Wormhole DualRealityTransport](../../assets/wormhole_inset_baseline.png)

*Current wormhole DualRealityTransport capture showing the curved main view, straight transport reference panel, and diagnostic overlays.*

## Abstract

We define a proto-caustic invariant for a deterministic wormhole rendering harness in which curved transport, topological remap, and observer-side film formation are treated as one optical process. The invariant is not an image-space score. It is a portal-local annular structure characterized by hit density, angular continuity, positive-overlap continuity, and radial transition strength that remain stable under fixed viewing and capture conditions. Conventional ray tracing rarely states correctness in these terms; its validation language usually rests on pixel agreement, stochastic convergence, or isolated hit statistics rather than on the preservation of structured focusing regions tied to transport geometry. We show that the wormhole prototype in `GD_xPRIMEray` admits a preserved destination-side annulus under deterministic validation and that this annulus can function simultaneously as a correctness condition and as a guide for geometry-aware performance work. The result is a rendering contract in which optical structure is treated as primary evidence rather than as a side effect of computation.

## 1. Motivation

Classical rendering language remains, even when numerically elaborate, largely committed to a straight-ray picture. Rays are sampled, traced, classified, and accumulated. When geometry is mild and topology fixed, that language is often sufficient. A wormhole scene makes it insufficient in two related ways. First, the ray does not merely move through a field; it may cross a shell, undergo scene remap, and continue in a different spatial context. Second, the observer does not merely receive isolated samples; the image is formed through coherent families of rays whose organization matters.

In such a system, stochastic validation is too weak. One may establish that remaps occur, that queries are dispatched, or even that film writes appear, and still fail to know whether the optical structure being formed is the one demanded by the geometry. The question is therefore not only whether the pipeline is live, but whether the image contains regions whose stability reveals that transport is behaving as a coherent geometric whole.

Not all image regions are equal in this setting. Some sectors are costly and largely sterile. Others encode concentration: a stable annulus, continuity of hit class, and a distinct radial transition. These are not conveniences of measurement. They are signatures that the geometry is selecting some optical pathways more strongly than others. An invariant becomes necessary here. Without it, optimization remains blind and correctness remains under-specified.

<!--
Perspective Alignment Notes
- Penrose: geometry should appear as the source of visible structure, not computation as an end in itself.
- Bandyopadhyay: propagation should be read as temporally coherent organization rather than as a bag of independent events.
- Orch OR: the observer-facing image is treated as a disciplined reduction of possibilities, not as a metaphysical claim.
-->

## 2. Related Work

### 2.1 Photon rings and caustic structure in relativistic optics

The visual phenomenon closest to the proto-caustic annulus in the published literature
is the *photon ring* of a black hole: the bright annular feature produced when null rays
orbit the photon sphere one or more times before escaping toward the observer.
**Luminet (1979)** computed the first image of a Schwarzschild black hole and identified
the characteristic ring as a consequence of geodesic focusing, not of any surface property
of the hole itself.
**Bozza (2002)** classified the strong-field lensing series and showed that the $n$th
relativistic image accumulates exponentially close to the photon-sphere critical impact
parameter — a geometric sequence whose limiting ring is a caustic in the sense of
Arnol'd catastrophe theory.
The EHT's resolved image of M87* (**Event Horizon Telescope Collaboration, 2019**)
confirmed this annular concentration observationally: the bright ring at $\sim 40\,\mu$as
is the observer-side trace of null-geodesic families that concentrated near the Kerr
photon sphere.

The proto-caustic annulus in xPRIMEray is the GRIN-transport analogue.
The portal mouth acts as an effective potential barrier; rays that barely clear it
concentrate on the destination side in a dense ring.
We call this structure *proto-caustic* rather than caustic because the GRIN effective
metric is approximate (Gordon-metric limit of a static isotropic medium) and because we
do not claim to resolve the full catastrophe-theoretic hierarchy of the lensing series.
What we do claim is that the ring is geometrically stable, measurably distinct from its
surroundings, and a necessary condition for transport fidelity — not a rendering accident.

**Müller (2014)** provides the closest direct predecessor: exact null-geodesic families
in a Morris–Thorne wormhole spacetime, with ring-density maps of exit directions on the
downstream side of the throat. His Figure 5 is the analytic version of our Figure C.

### 2.2 The Gordon metric: GRIN transport as null-geodesic tracing

The physical legitimacy of treating GRIN-field transport as spacetime curvature rests on
a result due to **Gordon (1923)**: light in a medium with refractive index $n(\mathbf{x})$
satisfies the null-geodesic equation of the effective metric

$$\tilde{g}^{\mu\nu} = g^{\mu\nu} + \left(1 - n^{-2}\right)u^\mu u^\nu,$$

where $u^\mu$ is the medium's four-velocity.
For a static medium, the spatial ray equation reduces to

$$\frac{d^2\mathbf{x}}{ds^2} = \nabla n - \hat{\mathbf{x}}(\hat{\mathbf{x}} \cdot \nabla n),$$

the characteristic ODE xPRIMEray integrates via RK4.
**Plebański (1960)** showed that this correspondence is bidirectional: any static
isotropic curved spacetime is optically equivalent to a GRIN medium.
**Leonhardt and Philbin (2009)** and **Pendry, Schurig, and Smith (2006)** developed the
engineering consequences — transformation optics — and **Thompson, Cummer, and Frauendiener
(2011)** extended the framework to general linear media.

The proto-caustic annulus is therefore not merely a rendering artifact; it is the
observer-side signature of geodesic focusing in the Gordon effective spacetime defined by
the portal's GRIN field.

### 2.3 Rendering methodology and the validation gap

Standard physically-based rendering (**Pharr, Jakob, and Humphreys, 2023**; **Kajiya,
1986**) validates correctness through energy conservation, unbiasedness, and convergence
under Monte Carlo sampling.
These criteria are insufficient for a wormhole renderer because the transport is
deterministic and topologically non-trivial: there is no ground-truth image against which
to measure convergence, and stochastic correctness does not imply geometric fidelity.

**James et al. (2015)** encountered the analogous problem for the *Interstellar* DNGR
renderer and addressed it through analytic cross-checks on geodesic families rather than
through pixel agreement.
Their validation methodology — checking that the rendered lensing pattern matches the
analytic photon-sphere structure — is the direct precedent for our invariant-based
approach.

**Chan, Psaltis, and Özel (2013)** similarly validated *GRay* by comparing against
analytic Kerr geodesics rather than against a reference image.

The proto-caustic invariant is our entry in this tradition: a geometric contract derived
from the expected focusing behavior of the transport law, evaluated deterministically
against fixed thresholds, and falsifiable by any scene change that disrupts the annulus.

---

## 3. Concept: Proto-Caustic Invariant

The proto-caustic invariant is a portal-local, ring-indexed optical constraint. In the present harness, the target object is the destination-side annulus:

- `layer = 1`
- `radial_bin = 3`

This ring is not chosen arbitrarily. Portal-centric sector aggregation shows that it carries a stable high-density band, complete angular occupancy, strong hit continuity, strong positive-overlap continuity, and a pronounced radial transition relative to the adjacent inner ring. It is therefore reasonable to treat it as a proto-caustic structure: not a catastrophe-theoretic caustic in the strongest analytic sense, but a robust focusing signature in measured transport.

Operationally, the invariant is expressed through four quantities:

- hit density in the target annulus
- hit continuity ratio across neighboring theta sectors
- positive-overlap continuity ratio across neighboring theta sectors
- radial hit gradient from the preceding ring

Taken together, these form a geometric-statistical object. Density alone would not suffice, since it can be inflated or flattened by unrelated scene changes. Continuity alone would not suffice, since a uniformly weak ring could still be continuous. The radial gradient matters because a caustic-like structure is not only present; it differentiates itself from its surroundings. The invariant therefore asks not merely whether a ring is occupied, but whether it stands out in the right way.

The appearance of an annulus is itself geometrically natural. Once transport bends around a mouth, crosses a remapping surface, and resolves against downstream geometry, neighboring rays do not populate the image plane uniformly. They arrive in portal-relative families. The ring is the observer-side trace of that family structure. It records a focusing tendency without claiming that the present prototype has solved the full analytic classification of wormhole caustics.

<!--
Perspective Alignment Notes
- Penrose: caustic structure should read as a consequence of geometry and congruence behavior.
- Bandyopadhyay: the invariant should be described as stable across repeated deterministic runs.
- Orch OR: structured selection remains tied to measurable continuity and gradient conditions.
-->

## 3. Method

The method is concrete. The wormhole harness runs as a deterministic static scene with fixed camera transform, locked input, stable framing, and repeatable film-path capture. Pass-1 transport integrates rays through a GRIN field around the portal mouth. Boundary-shell interaction triggers scene remap. Pass-2 performs geometry candidate generation and `OverlapOnly` query dispatch in the remapped scene before narrowphase hit resolution and film write.

Measurement is observer-centric but portal-local. Post-remap segments are aggregated into sector bins indexed by:

- portal layer
- radial bin
- theta sector
- direction bucket

This sector map supports three connected tasks:

1. congruence inspection  
   Neighboring post-remap rays can be evaluated as coherent families rather than as isolated segments.

2. ring-density analysis  
   Portal-local annuli can be examined for continuity, density, and radial transition behavior.

3. contract evaluation  
   The proto-caustic invariant can be checked directly against deterministic thresholds after each validation run.

The method also uses a figure quartet so that visual, geometric, and quantitative evidence remain coupled:

- Figure A: raw film-buffer result
- Figure B: composed result with research overlay
- Figure C: portal-centric ring-density map
- Figure D: compact contract and performance summary

GRIN transport, portal mapping, and sector binning are therefore treated as parts of one validation language. The observer image is not detached from the geometry; it is the geometry's report in observer coordinates.

<!--
Perspective Alignment Notes
- Keep this section concrete and implementation-facing.
- Avoid metaphor; let the structure of the harness carry the argument.
- Observer-centric interpretation is methodological here, not mystical.
-->

## 4. Results

The four figures should be read as four projections of one structure rather than as separate diagnostics.

### Figure A

![Figure A — Main Render](../../wormhole_test/figures/figure_A_main_render.png)

Figure A isolates the accumulated film result. It answers the narrow question: what does the observer-facing film contain once remap, hit resolution, and film write have completed? No explanatory overlay is present, so the image remains a pure rendering artifact.

### Figure B

![Figure B — Composed Render with Research Inset](../../wormhole_test/figures/figure_B_composed_overlay.png)

Figure B restores context. The composed render preserves the main film image while adding a research-view inset, a mode label, and contract status text. The inset makes the relation between visible film structure and underlying wormhole layout legible without disturbing the main image.

### Figure C

![Figure C — Ring Density](../../wormhole_test/figures/figure_C_ring_density.png)

Figure C reveals what Figures A and B imply but do not quantify directly: the optical bundle has portal-local structure. The destination-side outer ring stands out as a stable annular band with strong continuity and a marked radial separation from its interior neighbor. This is the image of the invariant in sector space.

### Figure D

![Figure D — Metrics Table](../../wormhole_test/figures/figure_D_metrics_table.png)

Figure D closes the loop by presenting the contract and timing state compactly. The proto-caustic invariant is shown as an explicit pass condition, the low-value budget is shown alongside it, and the active low-value throttle profile is made visible rather than implicit. The same artifact places optical metrics and pass-2 performance metrics in the same frame, which is essential if geometry-aware optimization is to remain accountable to geometry-aware correctness.

The four figures therefore do not compete. Figure A shows what is seen. Figure B shows where it comes from. Figure C shows how it organizes. Figure D shows whether that organization remains preserved under the current operating profile.

<!--
Perspective Alignment Notes
- The reader should feel that the quartet is one object seen from four coordinate systems.
- Penrose-style emphasis: visual appearance is a projection of geometry.
- Keep the prose tight enough that the figures appear as evidence rather than decoration.
-->

## 5. Validation

The correctness claim is intentionally falsifiable. The proto-caustic invariant passes only if the designated annulus satisfies all threshold conditions:

- minimum hit density
- minimum hit continuity ratio
- minimum positive-overlap continuity ratio
- minimum radial gradient

The low-value sector budget adds a second gate. It asks whether the known low-value region retains a bounded query share relative to its baseline. Together these contracts define a positive condition and a negative condition: preserve the annulus that matters, and prevent wasted work from re-expanding where it has already been shown to be unproductive.

Failure is therefore easy to state. A run fails if the annulus weakens below threshold, if the low-value region reclaims too much query share, or if an apparent timing gain is purchased by drift in `geom_hits` or `final_write_px`. In practice, an instructive rejected case has already occurred: increasing the low-value throttle from period `2` to period `3` preserved the formal contracts, yet weakened the annulus metrics and reduced hits and final writes while worsening the target timing buckets. This is precisely the kind of case a serious validation language must reject.

Determinism is central here. A contract that passes only in distribution, or only under operator-dependent framing, is too weak for this kind of work. The same scene, camera, portal framing, and harness conditions must recur if the invariant is to serve as a truth condition rather than as a suggestion.

<!--
Perspective Alignment Notes
- Penrose: the question is not whether the system can narrate success, but whether the geometry makes a true demand that can be failed.
- Bandyopadhyay: stability must be exhibited across repeated temporal realizations of the run.
- Determinism should be treated as part of the epistemology, not merely as testing convenience.
-->

## 6. Discussion

The proto-caustic invariant may be understood in two simultaneous ways. It is first a geometric constraint: a preserved annulus in portal-local coordinates whose density and continuity indicate that wormhole transport has produced a stable optical concentration. It is second a computational guide: a statement about where work matters and where it does not.

This matters for adaptive rendering. If adaptive logic sees only cost, it may suppress precisely those regions in which geometry concentrates visible structure. If it sees only hits, it may fail to distinguish between diffuse support and organized annular concentration. The invariant provides a more disciplined target. It tells the system that some observer-facing regions are not merely productive, but structurally informative.

An information-flow interpretation is therefore available, though it must be stated carefully. The renderer does not literally acquire knowledge in the human sense. What it acquires is a stable separation between sectors that preserve the optical signature and sectors that consume work without materially supporting it. In that limited but useful sense, the system learns where reality concentrates. The phrase is defensible only because the concentration is made explicit by deterministic structure and measured thresholds.

Observer-centric language becomes legitimate at precisely this point. The observer is not introduced as a metaphysical extra. The observer is simply the image-plane frame in which continuity, density, and ring transition become measurable. The coupling is therefore between transport geometry and observer-visible structure. That coupling is exactly what a wormhole renderer must preserve if it is to remain accountable both as a physical model and as a rendering system.

<!--
Perspective Alignment Notes
- Introduce observer-system coupling gently and only through measurable structure.
- Bandyopadhyay influence: emphasize coherence and temporal persistence without invoking unverifiable mechanism.
- Orch OR influence: permit structured emergence only as a framing idea secured by current measurements.
-->

## 7. Conclusion

We defined a proto-caustic invariant as a portal-local annular constraint expressed through density, continuity, and radial transition metrics.  
We showed that the deterministic wormhole harness preserves this annulus under the current kept geometry-aware profile while also supporting performance-aware regression gates.  
This matters because wormhole rendering can now be validated not only by whether it produces pixels, but by whether it preserves the optical structure those pixels are meant to represent.

## References

| Key | Citation |
|-----|----------|
| [gordon1923] | Gordon, W. (1923). Zur Lichtfortpflanzung nach der Relativitätstheorie. *Annalen der Physik*, 377(22), 421–456. |
| [plebanski1960] | Plebański, J. (1960). Electromagnetic waves in gravitational fields. *Physical Review*, 118(5), 1396–1408. |
| [leonhardt_philbin2009] | Leonhardt, U. & Philbin, T.G. (2009). Transformation optics and the geometry of light. *Progress in Optics*, 53, 69–152. |
| [pendry2006] | Pendry, J.B., Schurig, D. & Smith, D.R. (2006). Controlling electromagnetic fields. *Science*, 312(5781), 1780–1782. |
| [morris_thorne1988] | Morris, M.S. & Thorne, K.S. (1988). Wormholes in spacetime and their use for interstellar travel. *American Journal of Physics*, 56(5), 395–412. |
| [penrose1965] | Penrose, R. (1965). Gravitational collapse and space-time singularities. *Physical Review Letters*, 14(3), 57–59. |
| [hawking_ellis1973] | Hawking, S.W. & Ellis, G.F.R. (1973). *The Large Scale Structure of Space-Time*. Cambridge University Press. |
| [mtw1973] | Misner, C.W., Thorne, K.S. & Wheeler, J.A. (1973). *Gravitation*. W.H. Freeman. |
| [luminet1979] | Luminet, J.-P. (1979). Image of a spherical black hole with thin accretion disc. *Astronomy and Astrophysics*, 75, 228–235. |
| [james2015] | James, O., von Tunzelmann, E., Franklin, P. & Thorne, K.S. (2015). Gravitational lensing by spinning black holes in astrophysics, and in the movie *Interstellar*. *Classical and Quantum Gravity*, 32(6), 065001. |
| [chan2013] | Chan, C.-K., Psaltis, D. & Özel, F. (2013). GRay: A massively parallel GPU-based code for ray tracing in relativistic spacetimes. *Astrophysical Journal*, 777(1), 13. |
| [muller2014] | Müller, T. (2014). Exact geometric optics in a Morris–Thorne wormhole spacetime. *Physical Review D*, 90(12), 124013. |
| [eht2019] | Event Horizon Telescope Collaboration (2019). First M87 Event Horizon Telescope Results. I. *Astrophysical Journal Letters*, 875(1), L1. |
| [bozza2002] | Bozza, V. (2002). Gravitational lensing in the strong field limit. *Physical Review D*, 66, 103001. |
| [schneider1992] | Schneider, P., Ehlers, J. & Falco, E.E. (1992). *Gravitational Lenses*. Springer-Verlag. |
| [pharr2023] | Pharr, M., Jakob, W. & Humphreys, G. (2023). *Physically Based Rendering* (4th ed.). MIT Press. |
| [kajiya1986] | Kajiya, J.T. (1986). The rendering equation. *ACM SIGGRAPH Computer Graphics*, 20(4), 143–150. |
| [thompson2011] | Thompson, R.T., Cummer, S.A. & Frauendiener, J. (2011). Generalized transformation optics of linear materials. *Journal of Optics*, 13(5), 055105. |

*Full BibTeX: [`../shared_bibliography.bib`](../shared_bibliography.bib)*

---

## Appendix A

Current deterministic harness configuration for the active invariant and throttle profile:

- proto-caustic target ring: `layer = 1`, `radial_bin = 3`
- invariant thresholds:
  - `min_hit_density = 800`
  - `min_hit_continuity_ratio = 0.95`
  - `min_positive_overlap_continuity_ratio = 0.95`
  - `min_radial_gradient = 600`
- low-value budget:
  - `layer = 0`
  - `radial_bin = 3`
  - `baseline_query_share = 0.4011`
  - `max_query_share_scale = 0.9`
  - `maximum_allowed_query_share = 0.361`
- active kept low-value throttle profile:
  - `layer = 0`
  - `radial_bin = 3`
  - `theta bins = {13,14,15,0}`
  - `period = 2`

<!--
Perspective Alignment Notes
- Appendix remains purely operational.
- No interpretation should be smuggled into the config summary.
-->
