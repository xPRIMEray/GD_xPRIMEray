# Repository Narrative and Navigation Restructuring Proposal

**Status:** Proposal only. No files changed. For review and selective adoption.

**Purpose:** This document proposes a restructured root README, updated `Docs/index.md` landing narrative, and an updated `mkdocs.yml` navigation section that incorporate the Cathedral Probe architecture and the scheduler resonance DOE findings.

---

## A. Root README Landing Narrative (Proposed)

The current README landing text describes xPRIMEray as a curved ray transport engine. The proposed revision adds:

1. A concise statement of the transport instrumentation methodology
2. A hero image block pointing to the strongest visual evidence
3. A "What we've found" section grounded in measured results
4. An explicit epistemic posture block

---

### Proposed README Structure

```markdown
# xPRIMEray

**Curved Ray Transport Engine + Transport Coherence Observatory**

xPRIMEray renders null geodesics of the Gordon effective metric for spatially varying
refractive index fields:

    ẋ = p/n(x),    ṗ = ∇n(x)

Rays are curved primitives. Domain boundaries emerge from the transport field.
Artifacts are topological, not random.

---

## What We've Found

The most important result from the current investigation:

> Transport instability in curved ray rendering is **localized and topological**,
> not globally smoothable. It is amplified into frame-scale horizontal bands
> by row-major traversal scheduling.
>
> The fix is scheduler decorrelation, not precision increase.

**Measured evidence:**

| Finding | Measurement |
|---|---|
| Stride 1 → 31% band pixels; stride 4 → 0.4% | DOE `20260502T155725Z`, step 0.015 |
| Tile traversal: band% = 0.0; row: band% = 0.06% | Traversal comparison `20260503T175829Z` |
| Corner instability persists across all traversal modes | 89/89 samples, precision 0.003125 |
| Transport boundaries confirmed topological | 6,619 discontinuity vectors, all shape regions |

---

## The Cathedral Probe Framework

The Cathedral Probe is a layered transport observatory: multiple passive instrumentation
passes assembled into a composite diagnostic that makes transport coherence structure legible.

Layers:
- Layer 0: Beauty render (ground truth)
- Layer 1: Geometric wireframe
- Layer 2: Transport ownership map
- Layer 3: Risk probe markers
- Layer 4: Spacetime transport diagram
- Layer 5: Transport continuity vectors (newest)

→ See [Docs/Research/cathedral_probe_architecture.md](Docs/Research/cathedral_probe_architecture.md)

---

## What xPRIMEray Is (Precise Statement)

xPRIMEray integrates the null-geodesic ray equations of the Gordon effective metric
where n(x) is a spatially varying refractive index. This is the correct image of a
static isotropic medium (Gordon 1923; Plebański 1960) within the eikonal limit.

xPRIMEray does NOT:
- solve the Einstein field equations
- claim physical validation of wormhole geometry
- overstate artifact analysis as cosmology

xPRIMEray DOES:
- render null geodesics of an effective spacetime defined by ∇n
- diagnose transport instability with passive, reproducible instrumentation
- separate scheduler effects from transport physics with controlled DOE

---

## Visual Evidence

[Combined diagnostic overlay — full six-layer composite]
[Transport continuity vectors — ownership disagreement topology]
[Scheduler resonance DOE — stride sweep band percentages]
[Tile vs row traversal comparison — banding eliminated by scheduler decorrelation]

---

## Documentation

| Area | Document |
|---|---|
| Architecture paper | [Docs/Research/cathedral_probe_architecture.md](Docs/Research/cathedral_probe_architecture.md) |
| Scheduler decorrelation | [Docs/Research/scheduler_decorrelation_and_local_coherence.md](Docs/Research/scheduler_decorrelation_and_local_coherence.md) |
| Traversal review | [Docs/Research/architecture_design_council_traversal_review.md](Docs/Research/architecture_design_council_traversal_review.md) |
| Domain ownership | [Docs/Research/curvature_domain_ownership.md](Docs/Research/curvature_domain_ownership.md) |
| Phase coherence | [Docs/Research/phase_coherence_field.md](Docs/Research/phase_coherence_field.md) |
| Transport model review | [Docs/Research/curved_ray_transport_model_review.md](Docs/Research/curved_ray_transport_model_review.md) |
| Full docs index | [Docs/README.md](Docs/README.md) |
```

---

## B. Docs/index.md Landing Update (Proposed Additions)

The current `Docs/index.md` (served as the MkDocs homepage) should gain a new section after the Key Innovations table:

```markdown
## Cathedral Probe: Transport Coherence Observatory

The Cathedral Probe is the central diagnostic methodology for xPRIMEray's transport
instability investigation. It is a layered passive instrumentation framework — not a
post-processing pass — that makes transport coherence structure legible as a spatial
field.

**Core finding:** Transport instability is localized and topological, amplified into
row-global bands by scheduler traversal resonance. The correct architectural response
is scheduler decorrelation (tile traversal) combined with local coherence management
at ownership boundaries.

| DOE result | Measurement |
|---|---|
| Stride 1 band% at step 0.015 | 31.2% |
| Stride 4 band% at step 0.015 | 0.67% |
| Stride 8 band% at step 0.015 | 0.19% |
| Tile traversal band% at step 0.015 | 0.0% |
| Corner probe: samples requiring precision 0.003125 | 89 / 89 |
| Transport discontinuity vectors (step 0.015, row) | 6,619 high-discontinuity pixels |
| Phase coherence gap at band locations | 0.162 |

→ [Full architecture paper](Research/cathedral_probe_architecture.md)
```

---

## C. mkdocs.yml Navigation Proposal

The following section should be added to `mkdocs.yml` under `nav:`. It should be inserted after the existing `Research:` section and before `Papers:`:

```yaml
  - Cathedral Probe:
      - Architecture Paper: Research/cathedral_probe_architecture.md
      - Scheduler Decorrelation: Research/scheduler_decorrelation_and_local_coherence.md
      - Traversal Council Review: Research/architecture_design_council_traversal_review.md
      - Object-Seeded Scheduler: Research/object_seeded_null_geodesic_tiling_scheduler.md
      - Reference Geodesic Probe: Research/reference_precision_null_geodesic_probe.md
      - Domain Ownership: Research/curvature_domain_ownership.md
      - Phase Coherence Field: Research/phase_coherence_field.md
      - Dual Reality Workflow: Research/wormhole_dual_reality_transport_workflow.md
```

The existing Research section should also be updated to include the new architecture paper:

```yaml
  - Research:
      - Cathedral Probe Architecture: Research/cathedral_probe_architecture.md   ← ADD
      - Dual Reality Framework: Research/DualRealityFramework.md
      # ... rest unchanged
```

---

## D. Stale Document Flags

The following documents should be reviewed for staleness before the next documentation cycle:

| Document | Issue |
|---|---|
| `Docs/Research/curved_minimal_persistent_priors_*.md` | Proposes domain memory solutions that predate the scheduler resonance DOE; the memory-first framing is superseded by scheduler-decorrelation-first |
| `Docs/Research/curved_minimal_resolution_audit.md` | Implies resolution increase is the fix; the DOE shows step-size refinement at stride 1 does not reduce banding |
| `Docs/Research/DualRealityFramework.md` | Refers to external validation partner (GateBreaker) that has not produced outputs; should note current primary use is internal transport diagnostics |
| `Docs/CalibRoadmap/PatchLogs/C1_7_g_X.md` | AutoCal stopgap predates the understanding that global calibration cannot address topologically localized instability |

---

## E. Proposed Image Caption Inventory

For use in documentation, papers, and README visual blocks. All paths relative to `output/`.

### Caption Templates

**Combined diagnostic overlay:**
> "Six-layer Cathedral Probe diagnostic overlay for domain resolver stress scene at step_length=0.015, row traversal. Layer 0: beauty render. Layer 1: geometric wireframe. Layer 2: transport ownership map. Layer 3: risk probe markers. Layer 4: spacetime transport diagram. Layer 5: transport continuity vectors showing ownership disagreement topology at 6,619 high-discontinuity pixel locations."

**Scheduler resonance DOE:**
> "Scheduler stride DOE result (56 cells). At step_length=0.015: stride 1 → 31.2% band pixels, 73.3% row coverage; stride 4 → 0.67% band pixels, 3.3% row coverage; stride 8 → 0.19% band pixels, 2.2% row coverage. Band coverage is approximately constant across step lengths 0.0075–0.018 at stride 1, confirming that scheduler traversal cadence — not transport precision — is the primary amplifier of row-global artifacts."

**Tile vs row traversal:**
> "Tile traversal eliminates horizontal banding at step_length=0.015. Row mode (hash 85a4cff79a20): band% = 0.056%, h_score = 0.1. Tile mode (hash c18c4fad04aa): band% = 0.0%, h_score = 0.0. Corner probe results unchanged across both modes: required_precision = 0.003125, ownership_change_samples = 360. Scheduler decorrelation removes global amplification while local transport instability persists."

**Corner required precision map:**
> "Corner transport probe at edge ROI `geometry:25836914057:edge_midpoint:6`. 89/89 sampled points require reference precision (step 0.003125) or show ownership changes. 39 collider ownership flips in 89 samples (44%). Mean maximum decision risk: 4.04. Local transport ambiguity is genuine and persists independently of traversal mode."

**Transport continuity vector layer:**
> "Layer 5: Transport continuity vectors for domain resolver stress scene, step_length=0.015, row traversal. Each vector measures pixel-to-pixel ownership disagreement: collider change, domain change, hit distance delta, normal angle delta, path length delta. 6,619 high-discontinuity vectors (score ≥ 1.0). All 6 shape regions confirmed boundary_aligns_with_high_vector_density = true. Transport ownership boundaries are real topological structures, not noise."
