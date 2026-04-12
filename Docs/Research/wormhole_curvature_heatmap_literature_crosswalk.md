# Wormhole Curvature Heat Map: Literature Crosswalk for DualRealityTransport

This note places the current wormhole-side `DualRealityTransport` curvature heat map in the context of published wormhole imaging and gravitational-lensing literature. The goal is not to claim that the current render is already an observationally faithful wormhole image. The goal is to state, as precisely as possible, what the current diagnostic measures, where it qualitatively lines up with the literature, and where it still differs from the standard observable quantities used in academic wormhole-image papers.

## Current Metric in Our Implementation

The current default scalar is:

- per-pixel cumulative absolute turn angle along the pass-1 integrated ray path

In repo terms, this is a pass-1 transport-distortion diagnostic used by the wormhole-side `DualRealityTransport` stack and surfaced through the same telemetry heat-map path used elsewhere in the renderer. The user-facing label is `Distortion Heat Map`, but the underlying quantity is not brightness, emissivity, or observed flux. It is a path-geometry summary.

Operationally, the metric answers a question like:

- how much total directional bending did the camera ray accumulate before the pass-1 path completed

That makes it useful for identifying transport structure, high-bending annuli, radial transitions, and camera-space regions where the wormhole geometry is causing families of rays to spend more of their path near strong bending zones.

## What the Current Heat Map Is Showing

The present heat map should be interpreted as a camera-space map of transport complexity, not as a synthetic telescope image.

What it is showing:

- regions where rays accumulate large total turning during pass-1 integration
- nested annular high-curvature regions around the wormhole-facing structure
- strong radial transitions between lower-bending and higher-bending path families
- likely concentration of throat-skimming and photon-sphere-adjacent trajectories

What it is not directly showing:

- radiative transfer through an emitting medium
- observed intensity or flux
- redshift-weighted brightness
- thin-disk emissivity
- a transfer function in the standard image-formation sense
- photon arrival multiplicity or hit density unless those are added as separate overlays

This distinction matters because much of the literature plots observables or quasi-observables derived from image formation, whereas the current overlay is closer to a transport-side geometric diagnostic.

## Literature Crosswalk

### 1. Camera-space backward ray tracing

The closest methodological connection is the `Interstellar` wormhole visualization work, which explicitly constructs a map from a camera's local sky backward along null geodesics into the wormhole spacetime and then renders what that camera would see. That is conceptually close to the repo's camera-origin ray integration mindset, even though the current implementation is using a transport diagnostic rather than a final radiance image.

Practical crosswalk:

- published work: backward ray tracing from camera sky to emitting/background celestial spheres
- current repo result: backward-style camera-ray integration summarized as cumulative turn angle

So the present overlay is best understood as a diagnostic on the same broad ray family that later observable-style rendering would use, not as a substitute for the observable itself.

### 2. Wormhole throats acting as effective photon spheres

Several wormhole lensing papers emphasize that the throat can act as an effective photon sphere, or that wormholes can support multiple relevant light-ring structures. This is one of the strongest conceptual links to the current heat map.

If a family of rays skims the throat or approaches unstable light-ring behavior, one expects:

- elevated bending
- compressed angular structure in camera space
- ring-like transitions between path families

Those are exactly the kinds of features a cumulative-turn diagnostic is likely to accentuate. This does not prove that any one bright annulus in the current heat map is a literal photon ring. It does support the interpretation that annular high-curvature regions are qualitatively consistent with the transport behavior expected near throat-dominated or photon-sphere-adjacent ray families.

### 3. Multiple Einstein-ring systems

Shaikh et al. argue that some Morris-Thorne-type wormholes can exhibit two photon-sphere-relevant structures and therefore two relativistic Einstein-ring systems. In the literature, these appear as image-plane ring systems in a lensing setup. In the current repo output, we do not yet have direct relativistic Einstein-ring photometry, but nested annular curvature concentrations are qualitatively in the same structural family: they suggest multiple transition zones between ray bundles with different throat interaction histories.

Practical crosswalk:

- published work: multiple Einstein-ring systems in image brightness/lensing observables
- current repo result: multiple annular high-curvature bands in transport-distortion space

The alignment here is structural and qualitative, not one-to-one observational.

### 4. Lensing bands and photon ring groups in thin-shell wormholes

Recent thin-shell wormhole image papers often discuss:

- additional photon rings
- lensing bands
- photon ring groups
- extra ring structure relative to black-hole-only images

Those papers usually derive the result from ray-traced observables with an emitting thin disk or equivalent background source model. Our current heat map does not yet reproduce those observables directly. However, a transport-distortion scalar should be expected to become large near the same ray families that generate lensing bands or higher-order ring groups, because those features are also produced by rays that linger near unstable or throat-mediated strong-lensing regions before escaping to the camera.

That makes the current heat map a plausible precursor diagnostic for those structures:

- not the ring-group image itself
- but a map of where the camera ray bundle is geometrically stressed in ways that often underlie those image features

### 5. Shadow and thin-disk image papers

A large share of the wormhole literature plots quantities such as:

- shadow boundary
- thin-disk brightness image
- transfer-function-style decompositions
- direct, lensed, and higher-order image components
- Einstein ring positions

Those are observer-facing image products. By contrast, the current heat map is closer to a transport-side state variable. It is therefore more appropriate to compare it against:

- lensing structure
- ring-supporting ray families
- strong-bending zones

than against:

- absolute shadow diameter
- exact brightness contrast
- direct flux profiles
- EHT-style observable signatures

## Where Our Result Aligns

The current wormhole-side `DualRealityTransport` result appears qualitatively aligned with several repeated themes in the literature:

- annular organization rather than diffuse unstructured bending
- nested ring-like zones rather than a single monotone falloff
- strong radial transitions suggestive of separatrices between ray families
- enhanced structure where throat-skimming or near-light-ring trajectories are likely to accumulate turning

In repo terms, the current render can therefore be described honestly as:

- a transport-distortion visualization whose annular high-curvature structure is qualitatively consistent with published wormhole ring/lensing phenomenology
- a useful intermediate diagnostic for identifying candidate ring-supporting ray bundles
- evidence that the wormhole transport stack is producing structured, nontrivial lensing-like organization rather than only generic curved-ray smear

## Where It Differs From Standard Observables

The main differences from standard literature figures should be stated plainly.

Academic wormhole imaging papers usually plot some combination of:

- observed intensity on the image plane
- shadow or central brightness-depression geometry
- transfer functions
- redshifted thin-disk emission
- image decomposition by orbit count or emission order
- Einstein rings, relativistic rings, photon ring groups, and lensing bands as radiative image features

The current heat map does not yet include:

- an emitting source model on both sides of the throat
- radiative transfer or emissivity weighting
- redshift or Doppler weighting
- transfer-function decomposition
- hit-count or orbit-count classification
- brightness normalization against any published observable image

Accordingly, the current result should be framed as:

- qualitatively aligned with published ring and strong-lensing structure
- not yet a direct observable-flux match
- not yet sufficient to claim agreement with any specific wormhole shadow or thin-disk image paper at the level of photometry

## Interpretation of Current Render

For the current repo render, the most defensible interpretation is:

- the heat map shows nested annular high-curvature regions in camera space
- the render exhibits strong radial transitions between adjacent transport regimes
- the highlighted annuli likely correspond to ray families that skim the throat, accumulate large bending near the wormhole mouth, or pass near effective photon-sphere-like structures
- the result is therefore useful as a transport-structure diagnostic even though it is not yet a direct synthetic observation

This is a good research outcome, but it should be described as an intermediate result. It supports the claim that the present wormhole transport stack is generating organized lensing-like structure. It does not by itself prove that the repo has already reproduced the observable image products shown in thin-disk or wormhole-shadow papers.

## Next Research Steps

- add masked `SSIM` and `MAD` comparisons for the main curved region versus HUD, inset, and panel regions so overlay clutter is not treated as primary optical signal
- compare the curvature heat map directly against semantic glyph and collision-radar overlays to see which annuli correspond to stable geometric or remap landmarks versus purely transport-side bending
- add optional transfer-function-style overlays so that impact-parameter classes, side-of-throat origin, or path-family labels can be inspected beside the scalar heat map
- add optional hit-density or remap-density overlays to separate "high bending" from "high observable contribution"
- compare `validation_nearfield` framing against tolerated near-field poses to see which annular structures are stable under small camera-space changes and which are pose-fragile
- test whether the annular high-curvature regions correlate with existing proto-caustic and radial-transition measurements already used in the deterministic wormhole harness
- if a thin-disk or background-sky source model is added later, compare curvature maxima against emergent brightness rings rather than assuming they coincide everywhere

## References

- Morris, M. S., and Thorne, K. S. (1988). "Wormholes in spacetime and their use for interstellar travel: A tool for teaching general relativity." *American Journal of Physics* 56(5), 395-412.
- James, O., von Tunzelmann, E., Franklin, P., and Thorne, K. S. (2015). "Visualizing Interstellar's Wormhole." *American Journal of Physics* 83(6), 486-499. arXiv:1502.03809. DOI: 10.1119/1.4916949.
- Shaikh, R., Banerjee, P., Paul, S., and Sarkar, T. (2019). "A novel gravitational lensing feature by wormholes." *Physics Letters B* 789, 270-275. arXiv:1811.08245. DOI: 10.1016/j.physletb.2018.12.030.
- Shaikh, R. (2018). "Shadows of rotating wormholes." *Physical Review D* 98, 024044. arXiv:1803.11422. DOI: 10.1103/PhysRevD.98.024044.
- Paul, S., Shaikh, R., Banerjee, P., and Sarkar, T. (2020). "Observational signatures of wormholes with thin accretion disks." *Journal of Cosmology and Astroparticle Physics* 2020(03), 055. arXiv:1911.05525. DOI: 10.1088/1475-7516/2020/03/055.
- Guo, S., Li, G.-R., and Liang, E.-W. (2023). "Optical appearance of a thin-shell wormhole with a Hayward profile." *European Physical Journal C* 83, 479. arXiv:2210.03010. DOI: 10.1140/epjc/s10052-023-11842-y.
- Huang, H., Chen, Y.-P., and Jing, J. (2021). "Observational Signature and Additional Photon Rings of Asymmetric Thin-shell Wormhole." *Physical Review D* 104, 084005. arXiv:2102.05488. DOI: 10.1103/PhysRevD.104.084005.
- Müller, T., and Grave, F. (2009). "Catalogue of Spacetimes." Background reference for camera-based visualization workflows and rendered spacetime examples; already cited in repo architecture notes.
