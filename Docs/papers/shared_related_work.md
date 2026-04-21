---
title: Shared Related Work and Bibliography — xPRIMEray Invariant Series
description: Survey of foundational literature for photon transport in relativistic
  fields, GRIN optics, wormhole geometry, and deterministic rendering validation
---

# Shared Related Work and Bibliography

This document anchors the citation and framing conventions for the xPRIMEray paper family.
It maps the existing literature into five research traditions and establishes the precise
points of contact — and the precise points of departure — between prior art and the
contributions in Papers 001–004 and the Perceptual Curvature Threshold preprint.

Full BibTeX source: [`shared_bibliography.bib`](shared_bibliography.bib).

---

## I. The Gordon Metric and the GRIN–Spacetime Correspondence

The central physical claim underlying xPRIMEray is that a spatially varying refractive
index field defines an effective spacetime geometry for light. This is not an analogy; it
is exact at the level of the ray equation.

**Gordon (1923)** showed that Maxwell's equations in a dielectric medium with refractive
index $n$ and rest-frame four-velocity $u^\mu$ can be cast in the form of Maxwell's
equations in an effective curved spacetime with metric

$$\tilde{g}^{\mu\nu} = g^{\mu\nu} + \left(1 - \frac{1}{n^2}\right) u^\mu u^\nu.$$

This is the *Gordon metric*. For a static medium, the spatial part reduces to
$dl^2 = n^2(\mathbf{x})\,d\mathbf{x}^2$, and ray transport becomes null-geodesic tracing
in a Riemannian manifold whose curvature is set by $\nabla n$.

**Plebański (1960)** extended this to the full constitutive relations of electromagnetism
in curved spacetime — the bidirectional bridge: any curved spacetime produces an equivalent
dielectric response, and vice versa. **Leonhardt and Philbin (2009)** and
**Pendry, Schurig, and Smith (2006)** developed the engineering and conceptual implications:
any desired null-ray trajectory can be produced by appropriate choice of $n(\mathbf{x})$.

> **Relation to xPRIMEray.** The engine implements the eikonal ray equations
> $\dot{\mathbf{x}} = \mathbf{p}/n$, $\dot{\mathbf{p}} = \nabla n$,
> which are the characteristic ODEs of the Gordon metric's Hamilton–Jacobi equation.
> xPRIMEray renders **null geodesics of the Gordon effective metric**, not of the
> full Einstein metric. The approximation is explicit: it becomes exact for a static
> isotropic medium and degrades gracefully as the medium becomes dynamic or anisotropic.

The honest positioning against critics like Penrose, Thorne, or de Grasse Tyson
is therefore: *we do not solve the Einstein field equations, but the transport law we
integrate is the exact null-ray equation of an effective spacetime that is
mathematically well-defined, and the visual output is the correct image of that spacetime
within the eikonal limit.* That is a stronger claim than "inspired by GR," and a more
honest claim than "we render true wormholes."

---

## II. Wormhole Geometry and Causal Structure

**Morris and Thorne (1988)** established the canonical traversable-wormhole metric

$$ds^2 = -e^{2\Phi(r)}c^2\,dt^2 + \frac{dr^2}{1-b(r)/r} + r^2\,d\Omega^2,$$

with throat at $r_0$ where $b(r_0)=r_0$ and flare-out condition $b'(r_0)<1$.
The throat taxonomy in Paper 004 (`throat_entry`, `throat_exit`,
`throat_shell_transform`) is framed against this metric: each pixel label records
the causal status of the ray with respect to the $r = r_0$ surface.

**Visser (1995)** provides the most thorough treatment of wormhole stability and causal
structure. The causal-consistency validation in xPRIMEray — checking that transport
events respect a consistent Penrose-diagram ordering — is motivated by Visser's analysis
of which causal histories a traversable wormhole permits.

**Penrose (1965)** proved that trapped surfaces lead to geodesic incompleteness.
In the rendering context, an *unclassified pixel* is the computational analogue:
a ray that entered the scene but never reached a classified terminal event.
The hermetic rule (`escaped_no_hit = 0`, `budget_exhausted = 0`) is therefore a
**renderer-domain trapped-surface avoidance condition**: no ray may be lost to the
computational equivalent of a singularity.

**Hawking and Ellis (1973)** develop the Penrose-diagram formalism for causal boundary
analysis. The observer-ladder validation protocol in xPRIMEray is modelled on this
construction: each rung places the virtual camera at a different causal relation to the
throat, testing whether classified pixel coverage remains hermetic at each depth.

---

## III. Relativistic Ray Tracing and Black Hole Imaging

**Luminet (1979)** produced the first computed image of a black hole, establishing the
visual tradition xPRIMEray approaches from the GRIN side.

**James et al. (2015)** is the closest published precedent for the
*rendering-as-physics-instrument* framing of this work.
Their DNGR code traced null geodesics in the Kerr metric at IMAX resolution for
*Interstellar*, publishing the methodology in *Classical and Quantum Gravity* because
the scientific rigor merited it even though the product was a cinematic image.
xPRIMEray occupies the same epistemic position: the physics is not incidental to the
rendering, and the rendering is not incidental to the physics.

**Chan, Psaltis, and Özel (2013)** developed *GRay*, a GPU-accelerated Kerr geodesic
integrator. Their adaptive step-size strategy — balancing per-ray accuracy against
compute budget — is the astrophysical counterpart of xPRIMEray's derivative-aware step
scaling and the Perceptual Curvature Threshold Hypothesis.

**Müller (2014)** derived exact geodesic families in a Morris–Thorne spacetime and
showed that null rays form ring-density structures on the downstream side of the throat.
His Figure 5 (density map of exit directions) is the direct analytic predecessor of
xPRIMEray's Figure C (portal-sector ring-density map).
The proto-caustic annulus of Paper 001 is the GRIN-harness correlate of his focusing rings.

**The Event Horizon Telescope (2019)** resolved the photon ring of M87*, confirming that
the annular concentration predicted by null-geodesic optics is observationally real.
The EHT photon ring is the astrophysical counterpart of xPRIMEray's proto-caustic annulus:
both are the observer-side trace of ray families that concentrated near a compact object's
effective optical potential barrier.

**Bozza (2002)** classified strong-field lensing and showed that relativistic images
accumulate geometrically near the photon sphere. The proto-caustic of Paper 001 is
the GRIN-harness analogue: rays that barely clear the effective GRIN potential form a
dense ring on the destination side, just as Schwarzschild photon-sphere grazing rays
form a series of relativistic Einstein rings.

---

## IV. Physically-Based Rendering and Adaptive Transport

**Kajiya (1986)** formulated the rendering equation as an energy-conserving integral.
In xPRIMEray's wormhole setting, the equivalent conservation law is the hermetic pixel
closure of Paper 004: every pixel must resolve to a classified transport outcome, ensuring
that the computational analogue of radiant energy is fully accounted for.

**Pharr, Jakob, and Humphreys (2023)** provide the standard treatment of physically-based
rendering. The xPRIMEray architecture — GRIN transport feeding a Godot BVH, with
per-sector hit statistics driving geometry-aware query scheduling — extends the PBR
stack with a curved-ray integration layer between the camera model and the scene BVH.

**Veach and Guibas (1995)** introduced multiple importance sampling (MIS), allocating
sampling effort proportional to where the integrand is large.
The dual-invariant system in Papers 001–002 is the geometry-aware analogue:
allocate query work where the portal-local transport kernel concentrates signal
(proto-caustic annulus), and suppress it where the kernel is demonstrably negligible
(low-value sector budget).

---

## V. Numerical Integration and Adaptive Step Control

**Luneburg (1964)** established the Hamiltonian ray equations for GRIN media.
xPRIMEray implements these as the characteristic ODE system, integrated via RK4.

**Born and Wolf (1999)** provide the canonical treatment of ray congruences and geodesic
deviation in the eikonal limit. The geodesic deviation equation

$$\frac{D^2\xi^\mu}{d\lambda^2} = R^\mu{}_{\nu\rho\sigma}\,T^\nu\xi^\rho T^\sigma$$

motivates the Perceptual Curvature Threshold Hypothesis: when the effective curvature
$\kappa$ is small relative to the sampling interval, neighboring rays remain perceptually
indistinguishable and adaptive step suppression is safe.

**Dormand and Prince (1980)** and **Hairer, Nørsett, and Wanner (1993)** provide the
embedded Runge–Kutta framework for error-controlled adaptive integration. The
`error_tolerance` and `turn_threshold` parameters in xPRIMEray are the renderer-specific
calibration of this general adaptive-step philosophy.

---

## VI. Shared Terminology

| Term | Meaning |
|------|---------|
| `proto-caustic invariant` | Positive contract: destination-side annulus preserves density, continuity, and radial gradient |
| `low-value sector budget` | Negative contract: outer-ring query share bounded below baseline |
| `coupled invariant system` | Both contracts evaluated simultaneously |
| `stable operating region` | Regime where $I_1$ and $I_2$ both pass without hit/write drift |
| `deterministic harness` | Fixed-view, fixed-input validation run with reproducible artifacts |
| `portal-local sectors` | Bins indexed by `layer`, `radial_bin`, `theta_bin` |
| `hermetic fixture` | Scene sealed so every pixel must classify within the transport budget |
| `throat taxonomy` | Per-pixel causal label for rays crossing the $r=r_0$ surface |

**Preferred phrasing:**

- "preserve optical structure" — not "keep the image looking right"
- "bound low-yield expenditure" — not "kill waste rays"
- "selected by constraints" — not "found by tuning"
- "null geodesics of the Gordon metric" — not "physically accurate wormholes"

---

## VII. Shared Notation

| Symbol | Meaning |
|--------|---------|
| $n(\mathbf{x})$ | Refractive index (GRIN field strength) |
| $\tilde{g}^{\mu\nu}$ | Gordon effective metric |
| $b(r)$ | Morris–Thorne shape function |
| $r_0$ | Throat radius |
| $I_1$ | Proto-caustic invariant (Paper 001) |
| $I_2$ | Low-value sector budget (Paper 002) |
| $\kappa$ | Local ray curvature $= |\ddot{\mathbf{x}}|$ |
| $\kappa_p$ | Perceptual curvature threshold |
| $\lambda$ | Affine parameter along ray |
| $\xi^\mu$ | Geodesic deviation (Jacobi) field |

---

## VIII. BibTeX Key Index

Full entries in [`shared_bibliography.bib`](shared_bibliography.bib).

| Topic | BibTeX keys |
|-------|-------------|
| Gordon metric / GRIN–GR bridge | `gordon1923`, `plebanski1960`, `leonhardt_philbin2009`, `pendry2006` |
| Wormhole geometry | `morris_thorne1988`, `visser1995`, `einstein_rosen1935`, `morris_thorne_yurtsever1988` |
| Causal structure | `penrose1965`, `penrose1969`, `hawking_ellis1973`, `mtw1973` |
| Black hole imaging | `luminet1979`, `james2015`, `chan2013`, `muller2014`, `eht2019`, `muller_grave2010`, `younsi2016` |
| Gravitational lensing / caustics | `schneider1992`, `bozza2002`, `broderick_loeb2006` |
| GRIN optics | `luneburg1964`, `born_wolf1999`, `leonhardt_piwnicki1999`, `thompson2011` |
| Physically-based rendering | `pharr2023`, `kajiya1986`, `veach_guibas1995`, `wald2007` |
| Adaptive ODE / stepping | `dormand_prince1980`, `hairer1993` |
| Analog gravity / metamaterials | `greenleaf2007`, `novello2002` |
