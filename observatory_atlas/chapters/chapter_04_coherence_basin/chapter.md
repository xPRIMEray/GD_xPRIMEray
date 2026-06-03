# Chapter 4 — Coherence Basin

**Act:** III — Understanding  
**Role in arc:** Instability map. Reveals that some transport failures are not solvable by adding budget — they are topological.  
**Status:** Partially ready — analytical artifacts promoted. Missing: a beauty render at resolution sufficient to distinguish individual regions. Chapter is presentable with charts; a full visual is pending.

---

## Core Question

*Where in the transport field does convergence fail regardless of precision — and does the spatial pattern of those failures reveal their cause?*

---

## What Is the Visitor Investigating?

Chapter 3 showed that insufficient budget causes transport failure. The implication was hopeful: add more budget, get better results. Chapter 4 challenges that implication. The coherence basin probe runs the transport oracle at progressively finer precision until it either converges or exhausts the finest available precision floor. In the domain-resolver-stress scene, 289 regions never converge — at any precision level the oracle can try.

These are the **UNSEALED_NONCONVERGENT** regions. They are not solvable by increasing budget. Something about the field geometry itself prevents the oracle from reaching a stable solution. The question is whether the spatial pattern of these regions reveals *why* they exist.

It does. The 289 regions are not randomly distributed. They cluster symmetrically around two horizontal bands at pixel coordinates (128, 58) and (128, 122) — symmetric around the scene's vertical midline, corresponding to the GRIN field's outer boundary annulus.

The uniformity of the failure is itself a finding: all 289 regions require the same precision floor (0.003125). If the instability were a smooth continuous property, we would expect a distribution of required precisions. The uniform floor suggests a topological feature — a geometric boundary in the transport field that produces a discontinuous precision requirement.

---

## Observation

**Three analytical views and one missing beauty render:**

**Radial risk profile** (`transport-coherence-radial.png`)
Risk magnitude as a function of distance from the band centers. The profile decays with distance but never reaches zero within the probed domain. The decay curve shows a step discontinuity — consistent with a topological feature rather than smooth field falloff.

**Risk vs. step by anchor** (`transport-coherence-risk-vs-step.png`)
Risk magnitude at each anchor node as a function of step length. All anchors show similar step-length sensitivity — further evidence that the instability is a field property rather than a numerical artifact that changes with step resolution.

**Risk node dataset** (`datasets/transport-coherence-risk-nodes.csv`)
289 rows. Each row: node ID, center pixel coordinate, high radius, stable radius, precision floor, max risk, sealed status. All 289 rows: `UNSEALED_NONCONVERGENT`, precision floor `0.003125`, sealed `no`. The uniformity is machine-readable.

**Missing: beauty render**
A beauty render of the scene at sufficient resolution to show the 289 individual regions rather than the merged symmetric bands. At 480×270 (the current artifact resolution), the 289 regions overlap into two continuous stripes. Higher resolution or zoom-in rendering would make individual regions distinguishable.

---

## Artifacts

| File | Role | Present |
|------|------|---------|
| `misterylabs_artifacts/visuals/transport-coherence-radial.png` | **Primary.** Radial risk profile. | ✓ |
| `misterylabs_artifacts/visuals/transport-coherence-risk-vs-step.png` | **Supporting.** Risk vs. step by anchor. | ✓ |
| `misterylabs_artifacts/datasets/transport-coherence-risk-nodes.csv` | **Data.** 289-node risk dataset. | ✓ |
| `misterylabs_artifacts/validation/transport-coherence-basin.md` | **Evidence.** Risk region report. | ✓ |
| `misterylabs_artifacts/cards/transport-coherence-basin.md` | **Card text.** | ✓ |
| *Beauty render at distinguishable resolution* | **Missing.** Critical for site use. | ✗ |

---

## Sample World

**`transport_coherence_basin_world`** — `sample_worlds/transport_coherence_basin_world/`

Scene: `test-domain-resolver-stress.tscn`

The world places the visitor in the domain-resolver-stress scene with the `transport_coherence` overlay active — a heatmap showing convergence status per pixel. The 289 instability regions appear as the two bright horizontal bands. The visitor can explore whether adjusting the IOR gradient at the GRIN outer shell changes the instability pattern (the critical "topological vs. parameterization" experiment).

**Current status:** Design proposal. Scene exists. The live transport oracle is not yet wired to the runtime world viewer — the current instability map is a static output from the smoke run. Implementing live oracle querying is the primary engineering gap.

---

## Validation Question

*At epsilon=0.05 and finest precision floor 0.003125: how many UNSEALED_NONCONVERGENT regions should the oracle find?*

Expected: 289. Any count significantly below this indicates either a scene change that reduced the instability (worth investigating as a potential improvement) or a precision floor change that declared regions "converged" at a lower standard.

*Key structural check:* Are the instability bands symmetric around the scene's vertical midline? If the geometry is radially symmetric (as expected from the GRIN field's structure), the two bands should mirror each other. Asymmetry would indicate either a field parameterization change or a numerical artifact.

---

## Key Insight

**Some transport failures are topological, not numerical. They cannot be solved by adding budget — the field geometry itself creates a precision floor that the oracle cannot cross.**

---

## Next Chapter

**Chapter 5 — Cathedral Probe**

Chapter 4 identified *where* the transport field is unstable. Chapter 5 provides the diagnostic toolkit for understanding *why* — and for finding these instability zones without running the oracle explicitly.

*Logical bridge:* "We know where the field is unstable. But in a production render, we don't run the oracle first. How do we find the same instability zones from the rendered output alone?"
