# MisterY Labs Inspiration Card Feature Links

> **Lens: MisterY Labs Inspiration Curator**
> Audit date: 2026-05-14
> Scope: Thinkers, scientists, artists, and concepts that connect to xPRIMEray features

---

## About This Document

These are inspiration cards, not endorsements. Each card links a real contributor's ideas to xPRIMEray engine concepts in a way that is honest, educational, and visually generative. The goal is to help visitors to the MisterY Labs site understand what xPRIMEray is doing through the lens of ideas that shaped the intellectual landscape it operates in.

The engine reveals structure — it does not assert physics. These cards are about the spirit of the inquiry, not claims about the people named.

---

## Card Format

Each card uses this structure:
- **Name** — who or what
- **Field / Contribution** — what they did or what the concept is
- **Why this matters to xPRIMEray** — the honest connection
- **Related Engine Concepts** — systems in the renderer that resonate
- **Related Features / Proposed Features** — specific overlays or tools
- **Suggested Visual Motif** — for card design
- **Suggested Card Quote** — brief, evocative
- **External Reference Link Placeholder** — to be filled with vetted links before publication

---

## CARD 001 — Sabrina Pasterski / Celestial Holography

**Name:** Sabrina Pasterski

**Field / Contribution:**
Theoretical physics. Celestial holography. Scattering amplitudes. Asymptotic symmetry groups. Celestial sphere encoding of bulk spacetime processes. Research-silo bridging across high-energy physics, gravity, and quantum information.

**Why this matters to xPRIMEray:**

Pasterski's working frame is a good one for understanding what xPRIMEray is doing without overclaiming: *gravity is hard — so look for an equivalent non-gravitational description of the same physics*.

The renderer doesn't try to prove gravity. It tries to make transport structure visible. When a ray terminates on a boundary, that is a bulk-to-boundary event. When the domain resolver finds a seam between curvature zones, that is a correspondence break — the same kind of event that celestial holography tries to catalog at the level of scattering amplitudes. When a wormhole mouth and throat disagree about where a ray came from, that is a correspondence failure across topological regions.

Her emphasis on encoding bulk behavior onto a celestial reference sphere maps naturally onto what the Celestial Boundary Overlay would do: take ray terminal angles and project them onto a reference sphere, building a visual celestial map of where the scene's transport sends each ray.

The research-silo bridging aspect also applies. xPRIMEray sits between optics (GRIN), general relativity (geodesic transport), and computer graphics (renderer architecture). It is more useful to borrow language carefully from each silo than to commit fully to any one.

**Related Engine Concepts:**
- Boundary layer volumes (`BoundaryLayerVolume.cs`) — the in/out crossing event is the analog of a bulk-to-boundary scattering event
- Domain resolver seam detection (`DomainTelemetry.CurvatureDomainKind`) — seam instability as correspondence failure
- Curved ray terminal angle — the celestial sphere coordinate of the ray's fate
- Observer-relative transport — the camera as a bulk observer whose rays terminate on a conceptual celestial sphere
- Reference transport oracle (`ReferenceTransportOracle.cs`) — the "best known" transport path as a baseline against which correspondence is measured

**Related Features / Proposed Features:**
- **Celestial Boundary Overlay** (proposed, High priority) — the most direct visual expression of this card's ideas
- **Bulk-to-Boundary Dual View** (proposed, High priority) — side-by-side: bulk scene vs boundary projection
- **Correspondence Failure Heatmap** (proposed, Medium priority) — use oracle comparison records to highlight where correspondence breaks
- **Boundary Confidence Map** (active) — existing heatmap for boundary layer detection confidence
- **Normal Discontinuity Heatmap** (active) — existing heatmap for surface normal discontinuities at resolver impact zones
- **S-Matrix Event Ledger** (proposed, Medium priority) — log boundary crossing in/out state for scatter-like analysis

**Suggested Visual Motif:**
A glowing celestial sphere shell — dark void, luminous terminal-angle dots forming constellations of transport structure — with a split-screen showing the 3D bulk scene on the left and its flattened boundary encoding on the right. The dots glow brighter where rays cluster, dimmer where transport diverges. The seam between the two views shimmers, unstable.

**Suggested Card Quote:**
> "Gravity is hard. Let's look for an equivalent description."

*(Paraphrase of Pasterski's general research motivation — verify and attribute carefully before publication.)*

**External Reference Link Placeholder:**
- `[Pasterski.com — Research](https://pasterski.com)` — her published work index
- `[Celestial Holography — arXiv search]` — link to representative papers
- `[Celestial Amplitudes, Raclariu 2021 — arXiv:2107.02075]` — accessible review

---

## Card Stubs for Future Expansion

The following thinkers/concepts are identified as strong candidates for future cards. Stubs provided here to preserve the connection for later development.

---

### STUB — Emmy Noether / Symmetry and Conserved Currents

**Connection:** Every symmetry of the transport system corresponds to a conserved quantity. In xPRIMEray, transport invariants (path length, constraint drift stability, domain coherence scores) are the observable signatures of the underlying symmetries — or their breaking. The domain resolver seam is where a symmetry breaks.

**Related engine concepts:** `MetricTransportTypes.MetricRayState` constraint drift; `SceneTransportMemory.TransportCoherenceBasinRecord`; EpsilonStabilityClass in `ReferenceTransportOracle.cs`

**Proposed overlay:** Transport Memory Overlay; S-Matrix Event Ledger

---

### STUB — MTW (Misner, Thorne, Wheeler) / Metric Tensor Language

**Connection:** *Gravitation* (1973) established the tensor language that xPRIMEray borrows selectively. The transport frame (U, V) in `MetricRayState` is a partial tetrad. The `MetricHeuristicIntegrator` is a heuristic placeholder for the full geodesic equation MTW writes as d²x/dλ² + Γẋẋ = 0. The engine uses the language without yet implementing the full machinery — which is honest about where it is.

**Related engine concepts:** `MetricTransportTypes.cs`; `MetricHeuristicIntegrator.cs`; `IMetricField.cs`

**Proposed overlay:** Metric Grid / Stress-Tensor Style Overlay

---

### STUB — Maxwell / GRIN Optics Tradition

**Connection:** Gradient-index optics predates general relativity by decades. Maxwell's fish-eye lens is an exact GRIN analog of a flat-space gravitational lensing geometry. xPRIMEray's `FieldSystem.cs` and `FieldMath.cs` are direct descendants of the GRIN optics tradition — curved rays through varying refractive index, not curved spacetime per se.

**Related engine concepts:** `FieldSystem.cs`, `FieldMath.cs`, `FieldCurves.cs`, `FieldSource3D.cs`

**Proposed overlay:** Density Contour Overlay (active); Curvature Contour Overlay (active)

---

### STUB — Gauss / Riemann / Differential Geometry

**Connection:** The curvature bound grid (`CurvatureBoundGrid.cs`) and the turn-angle budget in `MetricHeuristicIntegrator.cs` are informal discretizations of Gaussian curvature control. Riemann's generalization — that curvature is an intrinsic property of a space, not a property of how it sits in an ambient space — is the philosophical foundation of GR and of why xPRIMEray treats curvature as a field property rather than a scene-graph hierarchy.

**Related engine concepts:** `CurvatureBoundGrid.cs`; `MetricHeuristicIntegrator.MaxTurnRadiansPerStep`; `DomainTelemetry.CurvatureDomainKind`

**Proposed overlay:** High-Curvature Oracle Overlay; Curvature Domain Map

---

### STUB — Richard Feynman / Path Integral / All Paths Contribute

**Connection:** Transport memory and coherence basins in `SceneTransportMemory.cs` are a practical echo of path-integral thinking: the "correct" transport path is not a single geodesic, it is a basin of nearby paths whose coherence determines the stability of the hit. Unresolved islands — where no coherent basin forms — are the renderer's equivalent of destructive interference.

**Related engine concepts:** `SceneTransportMemory.cs`; `ReferenceTransportOracle.EpsilonStabilityClass` (Unresolved / MultiSolution); `ObjectSeededTileScheduler.TransportCoherenceBasinRecord`

**Proposed overlay:** Transport Memory Overlay; Correspondence Failure Heatmap

---

## Editorial Notes for MisterY Labs

- **Before publishing any card:** verify quotes, attribution, and external links. These stubs use paraphrase-style language that needs editorial review.
- **Tone:** inspiration-based, not endorsement-based. The cards connect ideas to the engine, not people to the project.
- **Expansion:** the stub format above is designed to be filled in iteratively. Each card can grow independently.
- **Visual motifs:** the suggested motifs are designed to be interpretable by a graphic designer without physics knowledge. They describe a visual scene, not a physics diagram.
- **Accessibility:** each card should be readable by a curious non-physicist. The engine concept names can be hyperlinked to glossary entries.

---

*See also: [FEATURE_INDEX.md](../FEATURE_INDEX.md) | [OVERLAY_MASTER_LIST.md](../Observatory/OVERLAY_MASTER_LIST.md)*
