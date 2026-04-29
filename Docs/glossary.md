# Glossary

Definitions for terms used throughout the xPRIMEray documentation and papers. Terms are listed in conceptual order rather than alphabetically, to preserve the logical dependencies between them.

---

## Ray Transport Fundamentals

### Null geodesic

A path through spacetime along which the spacetime interval $ds^2 = 0$ — the path followed by light in a curved or effective spacetime. In xPRIMEray, null geodesics are not computed using the full Einstein metric; instead, the engine integrates the eikonal ray equations of the **Gordon effective metric**, which are the characteristic ODEs of null-ray transport through a spatially varying refractive-index field $n(\mathbf{x})$.

The null-ray equations are:

$$\dot{\mathbf{x}} = \frac{\mathbf{p}}{n}, \qquad \dot{\mathbf{p}} = \nabla n$$

This is exact for a static isotropic medium and degrades gracefully as the medium becomes dynamic or anisotropic.

**See also:** Gordon metric, GRIN, eikonal approximation.

---

### GRIN (Gradient-Index Medium)

A medium in which the refractive index $n(\mathbf{x})$ varies continuously with position. In a GRIN medium, light rays follow curved paths determined by the gradient of $n$, not by interface refraction. Classical examples include optical fibre cores and the atmosphere (responsible for mirages and mirages near the horizon).

In xPRIMEray, GRIN fields are the primary physical model. The engine evaluates $n(\mathbf{x})$ at each integration step and advances the ray state according to the eikonal ODE. The GRIN field can be configured to approximate black-hole lensing, wormhole throat geometry, or arbitrary user-defined spatial optical fields.

**See also:** Gordon metric, null geodesic, FieldSource3D.

---

### Gordon metric

The effective Riemannian metric defined by a spatially varying refractive-index field:

$$\tilde{g}^{\mu\nu} = g^{\mu\nu} + \left(1 - \frac{1}{n^2}\right)u^\mu u^\nu$$

where $g^{\mu\nu}$ is the background metric and $u^\mu$ is the rest-frame four-velocity of the medium. For a static isotropic medium this reduces to $dl^2 = n^2(\mathbf{x})\,d\mathbf{x}^2$, and null rays of the Gordon metric are exactly the rays of geometric optics in the medium (Gordon 1923; Plebański 1960).

xPRIMEray renders null geodesics of the Gordon effective metric, not solutions to the Einstein field equations. This is a precise and bounded claim: the rendering is the correct image of an effective spacetime whose curvature is set by $\nabla n$, within the eikonal limit.

**Honest positioning:** "null geodesics of the Gordon metric" — not "physically accurate wormholes."

---

### Eikonal approximation

The limit in which the wavelength of light is short compared to the scale over which $n(\mathbf{x})$ varies, so that wave optics reduces to geometric optics. In this limit, light propagates along rays (null geodesics of the Gordon metric) rather than as a diffraction-limited wave field. All xPRIMEray transport is in the eikonal regime.

---

### Optical Path Length (OPL)

The integral $\int n \, ds$ along a ray, where $s$ is the geometric arc length and $n$ is the local refractive index. OPL is the optical equivalent of physical path length in a uniform medium. In xPRIMEray it is a per-pixel accumulated scalar produced during integration.

High OPL indicates a ray that passed through a high-$n$ region or traveled a long geometric path. OPL mean and OPL max are derived metrics computed from the stored-hit table.

---

## Wormhole Geometry

### Wormhole throat

In the Morris-Thorne metric, the throat is the minimum-radius surface $r = r_0$ where the shape function $b(r_0) = r_0$ and the flare-out condition $b'(r_0) < 1$ holds. A traversable wormhole requires that the throat be passable in both directions.

In xPRIMEray's throat taxonomy, the throat is not a simple boundary. Rays are labelled by their causal history relative to $r = r_0$:

| Label | Meaning |
|---|---|
| `throat_entry` | Ray crossed the throat boundary-layer shell and continued; final hit was not post-remap |
| `throat_exit` | Ray accumulated ≥ 2 boundary remaps, indicating a linked-shell exit traversal |
| `throat_shell_transform` | Final hit occurred after a `BoundaryLayerVolume` `SceneTransform` remap |
| `throat_inner_absorb` | Ray terminated inside the throat's absorbing inner-radius region |

**Key finding (paper 001):** The throat behaves as a transition hinge, not a discontinuity. The actual transport discontinuity is located at the bridge, one step post-throat.

---

### Portal hit

A ray that resolves against explicit portal-frame or portal-surface geometry. Distinct from a `geom_hit` (ordinary scene geometry) or a `throat_event` (throat-taxonomy event). Portal-hit density is a key metric for characterising wormhole interaction strength at each observer position.

---

## Rendering System

### Hermetic fixture

A validation scene in which every pixel must return a classified transport result within the configured transport budget. Requirements:

- `classified_coverage_ratio = 1.0`
- `escaped_no_hit = 0`
- `budget_exhausted = 0`

A pixel that fails to classify is a renderer gap, not a physical outcome. Penrose-style trapped-surface language is used here as analogy and design pressure only, not as validation evidence for the renderer diagnostics.

---

### Fresh-instance observer ladder

A sequence of observer positions (checkpoints) through a wormhole-like system, where each checkpoint is evaluated on a fully fresh renderer instance — zero accumulated state, zero budget exhaustion, zero inferred classification artifacts from prior checkpoints. This guarantees that differences between checkpoints reflect transport structure rather than simulation history.

The six-checkpoint wormhole ladder: `mouth → mouth_to_throat_approach → throat → post_throat_backstep_01 → post_throat_exit_approach → exit_lookback`.

---

### Stored-hit table

The per-pixel record of the first accepted intersection from Pass 1: position, normal, material ID, transport class, OPL, crossing count, and segment count. The authoritative truth source for all downstream diagnostics. Pass 2 analysis operates on this table without re-running the integrator.

---

### RayChunk

The output of one integration step: a conservative bounding envelope (tube) over the ray's path between two integration points. RayChunks are the units submitted to BVH traversal. The chunk's bounding volume guarantees that any geometry intersection along the true curved path is captured.

---

## Diagnostics and Analysis

### Domain boundary

A transition between two transport regimes that differ in their metric-derived transport signatures (OPL, interaction density, crossing cost). Domain boundaries in xPRIMEray are not assigned — they emerge from clustering of the stored-hit metric table. The k = 3 partition of the wormhole ladder produces: near-side, bridge, and far-side domains, with the bridge as a singleton anomaly.

**Core finding:** visible banding correlates with domain-boundary transitions, not with final-hit selection artifacts.

---

### Phase coherence

A per-pixel score estimating whether a pixel's transport solution is consistent with its immediate neighbourhood. Constructed from neighbour-normal delta (angular deviation of the stored-hit normal from its 3 × 3 neighbourhood average) and first-hit divergence. High coherence = smooth, coherent solution field. Low coherence = phase boundary, where two distinct solution families meet.

Measured at two checkpoints:
- mouth: 0.639 (bands) vs 0.801 (outside) — gap 0.162
- bridge: 0.764 (bands) vs 0.796 (outside) — gap 0.032

---

### Curvature centre

A candidate structural anchor estimated from Hough circle/arc-fit candidates in the image. Curvature centres are the xPRIMEray analogue of nodes of silence in phase-field models: loci from which phase relationships radiate and where the surrounding coherence gradient is highest. Used as tile centres in curvature-centred polar tiling.

Curvature-centred polar tiling achieved direction similarity 0.656 (mouth) and 0.660 (bridge), compared to 0.628/0.642 for aperture-centred polar — a modest improvement that confirms curvature centres are better structural anchors than the global aperture estimate, though still below adaptive-square direction fidelity (0.836/0.875).

---

### Node of silence

*From Bandyopadhyay's Geometric Musical Language (GML), adopted as design grammar only.* A locus in the phase field where the phase cancels or crosses zero — not an absent region but a structural anchor. In xPRIMEray, candidate nodes of silence are: curvature-centre candidates, Hough line-intersection clusters, band-boundary junctions, and the estimated aperture centre. Used in the proposed geometric phase memory framework to define anchored polar-tile centres for each observer domain. This term is adopted as an analogical vocabulary, not as a claim of physical or biological equivalence.

---

### Attractor basin (transport)

*From the regime-clustering analysis, not from dynamical-systems theory.* The set of observer positions that produce the same dominant transport signature (same cluster assignment). The near-side and far-side are stable attractor basins — observer positions within each basin produce geometrically coherent, reproducible transport signatures. The bridge is a transition state between the two basins. Attractor-basin membership determines interpolation validity: world-space interpolation is valid within a basin and undefined across a basin boundary.

---

## Preferred Phrasing

| Instead of… | Say… |
|---|---|
| "physically accurate wormhole" | "null geodesic of the Gordon effective metric" |
| "observable flux map" | "transport-distortion diagnostic" |
| "proves," "demonstrates" | "observed," "measured," "correlated," "hypothesised" |
| "the throat is the discontinuity" | "the bridge is the dominant transport anomaly; the throat extends the near-side trend" |
| "banding is a sampling artifact" | "banding correlates with domain-boundary transitions" |
| "geometric phase memory is real" | "geometric phase memory is a proposed future layer, framed as design grammar" |

---

## Key References

| Source | Relevance |
|---|---|
| Gordon (1923) | Effective metric for light in a dielectric medium |
| Plebański (1960) | Full constitutive-relation bridge between GR and dielectrics |
| Morris & Thorne (1988) | Canonical traversable wormhole metric |
| Penrose (1965) | Geodesic incompleteness / trapped surfaces |
| Hawking & Ellis (1973) | Causal boundary formalism |
| Kajiya (1986) | Rendering equation — energy-conserving transport integral |
| Luneburg (1964) | Hamiltonian ray equations for GRIN media |
| James et al. (2015) | DNGR: null geodesic tracing for Interstellar |
| Bandyopadhyay (2020) | Nanobrain: geometric phase memory, GML, PPM — adopted as design grammar |

Full BibTeX: [papers/shared_bibliography.bib](papers/shared_bibliography.bib)  
Full related-work survey: [papers/shared_related_work.md](papers/shared_related_work.md)
