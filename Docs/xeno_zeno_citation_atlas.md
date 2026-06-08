# Xeno/Zeno Citation Atlas v1

**MisterY Labs — Observer Language v1 compatible**

> A citation is not a failure report. It is a structured record of an observation that the declared model did not predict, filed in enough detail that a future observer can reproduce, extend, or close it.

---

## Purpose

The Xeno/Zeno Citation Atlas is the classification system for anomalous observations in the Observatory. Every observation that cannot be accounted for by the declared scene contract — either because its source is outside the expected observer frame (Xeno) or because it refuses to converge under refinement (Zeno) — receives a citation card.

A citation card is an Observer Storyboard v1 card with constrained panel content: each of the nine panels carries specific required evidence for that citation type, and the Verdict is replaced by a tiered status drawn from the four confidence levels (Tier 0–3).

The Atlas has three functions:

1. **Classification.** Assign each anomaly a type (Xeno or Zeno), a subtype (X-B, X-C, X-S, X-F, Z-S, Z-B, Z-Fi, Z-Gi), and a tier (0–3).
2. **Tracking.** Provide a register where citations accumulate over time with stable IDs, so patterns across fixtures, runs, and parameters become visible.
3. **Closure.** Define what "explained" means for each citation type, so the register does not grow indefinitely with unresolvable open items.

---

## Definitions

### Xeno Citation

An observation that arrives from outside the declared observer frame.

**Declared observer frame:** the set of scene regions, directions, and model configurations that the Assumptions (Panel 2) declared would contribute to this measurement.

A Xeno citation occurs when a measurement receives contribution from outside that set. It has two defining characteristics:

- **Geometric or topological spatial signature.** The anomaly is not uniformly distributed. It aligns with field boundaries, receiver seams, caustic regions, or zone edges. This signature distinguishes it from random noise.
- **Persistence across observer positions.** The anomaly appears in the same spatial location across independent runs with matched parameters. A one-run anomaly is a candidate, not a citation.

A Xeno citation is not evidence that the renderer is wrong. It is evidence that the declared observer frame did not fully describe where contribution could arrive from.

### Zeno Citation

A region in the observation space where convergence is approached but never reached as measurement precision increases.

Named for Zeno's paradox of infinite subdivision: each refinement step halves the remaining distance to a stable answer, but the answer is never reached.

A Zeno citation has two defining characteristics:

- **Diverging refinement disagreement.** As step size decreases (or budget increases), the disagreement between successive measurements grows rather than shrinks. This is the inverse of normal convergence behavior.
- **Documented refinement sequence.** The divergence must be demonstrated across a minimum of three refinement levels. Two levels is ambiguous; three establishes a trend.

A Zeno citation is not evidence that the renderer has a bug. It is evidence that this region of the observation space does not have a stable answer at any tested finite precision within the declared budget.

---

## Confidence Tiers

A citation's tier reflects the completeness and reproducibility of its nine-panel card. Tiers are monotonically increasing: Tier 2 requires all Tier 1 criteria, Tier 3 requires all Tier 2 criteria.

Tiers are assigned by the observer who files the card. Tier upgrades require new evidence added to the card — not reassessment of existing evidence.

---

### Tier 0 — Candidate

**Meaning:** Observed once. Signature is visible but not yet verified as non-noise.

**Panel requirements:**

| Panel | Required content |
|---|---|
| 1. Observation | Present. Artifact available. |
| 2. Assumptions | Scene, budget, observer position declared. Parameter values recorded. |
| 3. Perspectives | Minimum 2 perspectives available (e.g., baseline + activated). |
| 4. Disagreements | Spatial signature present in at least one comparison. May be noise. |
| 5. Closure Basin | Closure status known for the anomaly region specifically. |
| 6. Lineage | Single run. Run ID, timestamp, parameters, exit code on record. |
| 7. Evidence Coverage | Coverage of the anomaly region documented. May be partial. |
| 8. Sensitivity Signature | Absent or single-value (no sweep yet). |
| 9. Verdict | **CANDIDATE** — observed, not yet verified. |

**Reproducibility:** Not yet confirmed.
**Xeno additional requirement:** Spatial signature must be localized (not diffuse across the full frame).
**Zeno additional requirement:** At least 2 refinement levels showing non-convergent behavior.

---

### Tier 1 — Confirmed

**Meaning:** Reproduced across independent runs. Signature is stable and non-noise.

**Panel requirements (all Tier 0, plus):**

| Panel | Required content |
|---|---|
| 3. Perspectives | Minimum 3 perspectives. For Zeno: minimum 3 refinement levels showing growing disagreement. |
| 4. Disagreements | Spatial signature stable across ≥ 2 independent runs. Run IDs recorded in lineage. |
| 6. Lineage | Minimum 2 independent runs with matched parameters. Both run IDs in lineage. |
| 7. Evidence Coverage | Full coverage of the anomaly region across both runs. |
| 8. Sensitivity Signature | Baseline comparison present. At minimum: baseline (0%) vs. one activated value. |
| 9. Verdict | **CONFIRMED** — signature verified, source unknown. |

**Reproducibility:** Confirmed across ≥ 2 runs.
**Xeno additional requirement:** Signature must persist at the same spatial location across both runs.
**Zeno additional requirement:** Divergence direction (growing, not shrinking) confirmed across the minimum 3 levels.

---

### Tier 2 — Characterized

**Meaning:** Source attributed to a named structural class. Mechanism not yet fully derived.

**Panel requirements (all Tier 1, plus):**

| Panel | Required content |
|---|---|
| 2. Assumptions | Model prediction for the anomaly region exists and is recorded. |
| 3. Perspectives | Full perspective set across the declared parameter range. |
| 4. Disagreements | Signature attributed to a named structural class from the Xeno or Zeno taxonomy (see below). Attribution rationale recorded in Panel 4 notes. |
| 7. Evidence Coverage | Full declared domain covered. No unsampled regions in or adjacent to the anomaly. |
| 8. Sensitivity Signature | Full sensitivity sweep complete. Field-induced vs. geometry-induced status determined. |
| 9. Verdict | **CHARACTERIZED** — attributed to named class, mechanism not yet derived. Subtype recorded. |

**Reproducibility:** Confirmed across ≥ 3 runs with varying parameters.
**Xeno additional requirement:** Spatial alignment with the named structural class documented (e.g., boundary overlap shown, caustic geometry identified).
**Zeno additional requirement:** Growth rate of divergence measured (not just direction). Field-induced / geometry-induced determination made via Panel 8.

---

### Tier 3 — Explained

**Meaning:** Source fully identified. Mechanism derived. Citation is either closed or designated persistent by design.

**Panel requirements (all Tier 2, plus):**

| Panel | Required content |
|---|---|
| 2. Assumptions | Model prediction was declared *before* the anomaly was found, and the explanation demonstrates that the anomaly follows from the model when the prediction is corrected or extended. |
| 4. Disagreements | Gap between prediction and observation closed by the explanation. The disagreement map is accounted for. |
| 9. Verdict | **EXPLAINED** with one of two status designations: |
| | **CLOSED** — The anomaly is no longer anomalous in the updated or corrected model. The citation is archived. |
| | **PERSISTENT** — The anomaly is a known, necessary property of the scene or field topology. It cannot be eliminated without changing the scene contract. Retained in the register as a labeled known. |

**Xeno Tier 3 requirement:** The specific geometric or topological source region outside the declared frame is named and its contribution path is traced.
**Zeno Tier 3 requirement:** The convergence singularity is identified — either as a specific scene geometry feature (corner, seam, point source) or as a field-induced singularity at a specific amplitude. For **IRRESOLVABLE** status (a Zeno variant of PERSISTENT): the singularity is proven to exist at all finite precisions within the declared contract, and no budget increase eliminates it.

---

## Xeno Citation Taxonomy

### Subtypes

#### X-B — Boundary Xeno

**Definition:** An observation at a zone, receiver, or field boundary where ray attribution becomes geometrically ambiguous. The contribution from outside the expected frame arrives from the adjacent zone leaking across the boundary.

**Cause:** Rays traveling nearly parallel to a boundary surface accumulate contribution from both sides. The declared observer frame assumes clean separation.

**Spatial signature:** A thin band of disagreement pixels following the boundary geometry. Linear or gently curved. Width typically 1–5 pixels at the observed scale. Symmetric about the boundary if both adjacent zones have similar transport properties.

**Panel population:**

| Panel | Expected content |
|---|---|
| 1. Observation | The raw render in the boundary region. |
| 2. Assumptions | Boundary geometry declared. Adjacent zone assignments documented. |
| 3. Perspectives | Baseline (boundary inactive or reference geometry) vs. activated (boundary present). |
| 4. Disagreements | Thin band aligned with boundary. Ownership seams artifact overlaps. |
| 5. Closure Basin | Inside the basin — rays classify, but zone assignment is ambiguous. Risk nodes rare unless the boundary coincides with a budget-stress region. |
| 8. Sensitivity Signature | Field-sensitive if the boundary is a field boundary; geometry-stable if it is a receiver geometry boundary. |
| 9. Verdict | At Tier 2: attributed to boundary geometry. At Tier 3: specific boundary surface named, leakage path documented. |

**Common misread:** The thin band is dismissed as aliasing or rendering artifact. Distinguishing feature: a Boundary Xeno aligns precisely with the declared zone boundary; aliasing does not.

---

#### X-C — Caustic Xeno

**Definition:** An observation in a caustic region where multiple ray paths converge from outside the expected contribution zone. Energy arrives from a direction the declared observer frame did not predict.

**Cause:** The curvature field focuses rays from a wide input region onto a small output region. Rays arriving at the caustic originate from directions outside the declared near-field contribution zone.

**Spatial signature:** A concentrated region — circular, lens-shaped, or arc-shaped. Does not align with zone boundaries. May appear as a bright hotspot, an anomalous color gradient, or a traversal cost spike without a corresponding boundary feature.

**Panel population:**

| Panel | Expected content |
|---|---|
| 1. Observation | The caustic region, ideally isolated with a crop. |
| 2. Assumptions | Near-field contribution zone declared. Field amplitude documented. |
| 3. Perspectives | Multiple field amplitudes — caustic geometry changes with field. |
| 4. Disagreements | Concentrated region. Does not follow zone boundaries. |
| 5. Closure Basin | Variable — caustic focusing can bring rays to terminal classification earlier (inside basin) or create convergence difficulty at the focus point (near or outside basin). |
| 8. Sensitivity Signature | Highly field-sensitive. Caustics are created and destroyed by field activation. At baseline (0% curvature), a Caustic Xeno should disappear entirely if field-induced. |
| 9. Verdict | At Tier 2: attributed to caustic geometry, focal region identified. At Tier 3: caustic source geometry named (field amplitude, focal length, origin). |

**Common misread:** The concentrated region is read as a scene lighting feature or an artifact of the beauty capture. Distinguishing feature: a Caustic Xeno appears in traversal cost diagnostics (Panel 5 / traversal heatmap), not only in the beauty render.

---

#### X-S — Seam Xeno

**Definition:** An observation at a receiver seam where domain ownership is geometrically sensitive. Small changes in ray direction produce large changes in receiver assignment because the seam is a high-gradient ownership boundary.

**Cause:** At receiver seams, rays traveling nearly perpendicular to the seam surface flip ownership assignment over sub-pixel distances. The declared observer frame does not account for the ownership sensitivity at the seam.

**Spatial signature:** 1–3 pixel width pattern at ownership seam locations. Highly sensitive to observation angle — the pattern may shift or disappear when the camera moves slightly. Directly overlaps the ownership_seams artifact.

**Panel population:**

| Panel | Expected content |
|---|---|
| 1. Observation | Seam region with ownership seams artifact overlay. |
| 2. Assumptions | Receiver geometry and seam locations declared. |
| 3. Perspectives | Multiple camera positions or field amplitudes — seam sensitivity may change. |
| 4. Disagreements | Thin, sharp pattern. Directly overlaps ownership_seams artifact. |
| 5. Closure Basin | Inside the basin. Rays classify, but classification flips at the seam boundary. |
| 8. Sensitivity Signature | May be field-sensitive (if the seam moves with field activation) or field-invariant (if it is a fixed geometry seam). |
| 9. Verdict | At Tier 2: attributed to seam geometry, seam surface named. At Tier 3: seam sensitivity (angle at which ownership flips) measured and documented. |

**Common misread:** The seam pattern is read as a closure failure. Distinguishing feature: a Seam Xeno shows inside the closure basin — rays classify, they just classify inconsistently at the seam. A closure failure would show outside the basin.

---

#### X-F — Far-field Xeno

**Definition:** An observation where rays traverse substantially farther than the declared scene contract predicted before classifying, receiving contribution from a region outside the declared near-field zone.

**Cause:** Open-scene fixtures with large spatial extent. The declared observer frame assumed contribution from the near field; far-field receivers capture rays that travel past the near-field boundary.

**Spatial signature:** Diffuse, not aligned with boundaries. Spatially distributed across open-scene regions. Low per-pixel disagreement magnitude but wide spatial extent. Appears primarily in open-target fixtures (e.g., curved_minimal) rather than sealed fixtures.

**Panel population:**

| Panel | Expected content |
|---|---|
| 1. Observation | Full frame — the far-field contribution is distributed, not localized. |
| 2. Assumptions | Near-field boundary declared. Far-field receiver geometry documented. |
| 3. Perspectives | Near-field only vs. near-field + far-field enabled. |
| 4. Disagreements | Widespread, low-magnitude. Not concentrated. |
| 5. Closure Basin | At or near the basin boundary — far-field rays are more likely to approach budget exhaustion due to longer traversal. |
| 8. Sensitivity Signature | Field-sensitive if the field deflects rays toward far-field receivers. Geometry-stable if the far-field receivers are at fixed positions. |
| 9. Verdict | At Tier 2: attributed to far-field geometry, far-field receiver named. At Tier 3: path length from near-field boundary to far-field receiver measured. |

**Common misread:** Far-field contribution is assumed to be noise because it is diffuse. Distinguishing feature: a Far-field Xeno produces elevated traversal step counts (visible in the traversal heatmap) without concentrated disagreement.

---

## Zeno Citation Taxonomy

### Subtypes

#### Z-S — Step-size Zeno

**Definition:** A region where disagreement between successive measurements grows as step size decreases. Halving the step size produces more disagreement, not less. The region has no stable answer at any tested finite step resolution.

**Cause:** A computational singularity at this region — either field-induced (a curvature singularity) or geometry-induced (a sharp corner, edge, or point source). As step size decreases, the integration samples the singularity more finely and produces increasingly divergent results.

**Refinement sequence:** Panel 3 must show the sequence s₁, s₁/2, s₁/4, ... with disagreement measured at each level. A valid Z-S citation requires a minimum of 3 levels showing monotonically increasing disagreement.

**Panel population:**

| Panel | Expected content |
|---|---|
| 1. Observation | The non-converging region, ideally cropped. |
| 2. Assumptions | Step sizes tested declared. Convergence criterion declared (what would stable look like?). |
| 3. Perspectives | The refinement sequence. Each step size is a perspective. Minimum 3; Tier 2 requires 5. |
| 4. Disagreements | Panel 4 for a Z-S citation shows the disagreement between consecutive pairs in the sequence. The disagreement map must grow from pair to pair. |
| 5. Closure Basin | By definition, outside the closure basin at all tested resolutions. The basin boundary is the edge of the Z-S region. |
| 7. Evidence Coverage | The spatial extent of the Z-S region at each refinement level (does it grow, shrink, or stay constant as resolution increases?). |
| 8. Sensitivity Signature | Critical: does the Z-S region disappear at 0% curvature? If yes: Z-Fi (field-induced). If no: Z-Gi (geometry-induced). |
| 9. Verdict | CONFIRMED at Tier 1. CHARACTERIZED (with Z-Fi or Z-Gi sub-designation) at Tier 2. |

**Growth rate:** At Tier 2, the disagreement growth rate should be documented. Linear growth (doubling at each halving of step size) suggests a first-order singularity. Superlinear growth suggests a higher-order singularity.

---

#### Z-B — Budget Zeno

**Definition:** A region where the closure rate increases as budget increases, but asymptotes below 100% — the region approaches but never reaches full closure as budget grows. The parameter is total step budget (not step size).

**Distinction from Z-S:** Step-size Zeno concerns measurement precision (step resolution); Budget Zeno concerns measurement depth (total computation allowed). They can co-occur but are documented separately.

**Panel population:**

| Panel | Expected content |
|---|---|
| 3. Perspectives | Budget levels tested. Each budget is a perspective. Minimum 3; plotted as closure % vs. budget. |
| 4. Disagreements | The residual risk-node region at each budget level. The region should shrink with budget but never disappear. |
| 5. Closure Basin | The persistent risk-node region — the outer boundary of the basin that budget cannot eliminate. |
| 8. Sensitivity Signature | Does the persistent risk-node region change with curvature? Field-sensitive → Z-Fi variant. Geometry-stable → Z-Gi variant. |
| 9. Verdict | CONFIRMED when asymptote below 100% is demonstrated. CHARACTERIZED when the asymptotic closure rate is measured. |

**Asymptote measurement:** At Tier 2, the closure % vs. budget curve should be recorded with a fitted asymptote. A curve that approaches 99.8% and flattens is a Budget Zeno; a curve still rising steeply at the maximum tested budget is inconclusive.

---

#### Z-Fi — Field-induced Zeno

**Designation modifier**, not a standalone subtype. A Z-S or Z-B citation receives the `-Fi` designation when Panel 8 (Sensitivity Signature) shows the Zeno region is absent at baseline (0% curvature) and appears at some threshold field amplitude.

**Diagnostic test:** Run baseline (0% curvature) at the same step size / budget levels. If the Zeno region does not appear in the baseline run, the citation is field-induced.

**Panel 9 addition:** "Field-induced. Absent at baseline. Appears at [X]% field amplitude."

**Implication:** A Z-Fi citation means the curvature field creates a convergence singularity that does not exist in the straight-transport baseline. This is a property of the field configuration for this scene, not of the scene geometry itself.

---

#### Z-Gi — Geometry-induced Zeno

**Designation modifier.** A Z-S or Z-B citation receives the `-Gi` designation when Panel 8 shows the Zeno region is present in the baseline (0% curvature) run. The scene geometry creates the singularity; the field may modulate its extent but does not create it.

**Diagnostic test:** Run baseline (0% curvature) at the same step sizes / budgets. If the Zeno region appears in the baseline run at the same spatial location, the citation is geometry-induced.

**Panel 9 addition:** "Geometry-induced. Present at baseline. Field modulates extent by [N]%."

**Implication:** A Z-Gi citation identifies a structural feature of the scene geometry — a corner, edge, degenerate surface, or point source — that creates non-convergent transport regardless of field activation. These are the most informative Zeno citations because they reveal scene geometry properties independent of the curvature system.

---

## The Nine-Panel Mapping for Citations

How Observer Language v1 panels adapt for citation cards. Required content differs from standard storyboard panels.

| Panel | Standard storyboard | Xeno citation | Zeno citation |
|---|---|---|---|
| 1. Observation | Instrument output for full scene | Cropped to anomaly region; full frame as supplementary | Cropped to non-converging region; full frame supplementary |
| 2. Assumptions | Full scene contract | Scene contract + declared observer frame (what was expected to contribute) | Scene contract + declared convergence criterion + step sizes / budgets tested |
| 3. Perspectives | Observer positions or parameter values | Expected frame vs. observed; additional field amplitudes | Refinement sequence (step sizes or budget levels). Minimum 3. |
| 4. Disagreements | Divergence map between perspectives | Spatial signature map. Must show structural alignment (boundary, caustic, seam, or diffuse) | Disagreement between consecutive refinement pairs. Must show growth direction. |
| 5. Closure Basin | Spatial convergence map | Inside/outside basin determination for the anomaly region; basin position relative to signature | By definition outside basin. Basin boundary = edge of Zeno region. Boundary extent at each refinement level. |
| 6. Lineage | Run ID, parameters, timestamp | Run IDs for all reproducibility runs; matched parameters documented | Run IDs for all refinement levels; step sizes / budgets for each level |
| 7. Evidence Coverage | Domain fraction sampled | Spatial extent of the anomaly region and its coverage across runs | Spatial extent of the Zeno region at each refinement level (growing, stable, or shrinking) |
| 8. Sensitivity Signature | Signed map of parameter response | Field-induced vs. geometry-induced determination. Does the anomaly disappear at 0% curvature? | Field-induced (Z-Fi) vs. geometry-induced (Z-Gi) determination. Zeno region at baseline. |
| 9. Verdict | PASS / FAIL against scene contract | Tiered status: CANDIDATE / CONFIRMED / CHARACTERIZED / EXPLAINED / CLOSED / PERSISTENT | Tiered status: CANDIDATE / CONFIRMED / CHARACTERIZED / EXPLAINED / IRRESOLVABLE |

---

## Citation Card Format

Every citation in the Atlas register carries a header block followed by the nine panels.

### Header

```
Citation ID:    [X|Z]-[subtype]-[NNN]        (e.g., X-B-001, Z-S-002, Z-S-Fi-003)
Type:           Xeno | Zeno
Subtype:        X-B | X-C | X-S | X-F | Z-S | Z-B | Z-S-Fi | Z-S-Gi | Z-B-Fi | Z-B-Gi
Tier:           0 | 1 | 2 | 3
Status:         CANDIDATE | CONFIRMED | CHARACTERIZED | EXPLAINED | CLOSED | PERSISTENT | IRRESOLVABLE
Fixture(s):     [fixture names where observed]
First observed: [ISO date, run ID]
Last updated:   [ISO date, run ID]
Filed by:       [observer name or handle]
```

### Panel Cards (nine, in order)

Each panel card is a section headed by the panel number and name, followed by:
- **Artifact:** the filename or artifact key for this panel's evidence (MISSING if absent)
- **Status:** PRESENT | PARTIAL | MISSING | N/A
- **Notes:** one or two sentences specific to this citation

### Verdict Block

```
Tier:       [0|1|2|3] — [CANDIDATE|CONFIRMED|CHARACTERIZED|EXPLAINED]
Status:     [CLOSED|PERSISTENT|IRRESOLVABLE] (if applicable)
Scope:      This verdict applies to [fixture], [parameter values], [coverage fraction]%.
Next step:  [what evidence would advance this citation to the next tier]
```

---

## Confidence Tier Quick Reference

| Tier | Name | Panels required | Reproducibility | Sensitivity | Verdict |
|---|---|---|---|---|---|
| 0 | Candidate | 1, 2, 3 (min 2), 4, 5, 6, 7 | single run | absent or single value | CANDIDATE |
| 1 | Confirmed | all of Tier 0; P3 min 3; P8 baseline present | ≥ 2 matched runs | baseline vs. one active | CONFIRMED |
| 2 | Characterized | all of Tier 1; full perspective set; full sweep; model prediction in P2 | ≥ 3 varied runs | full sweep, Fi/Gi determined | CHARACTERIZED |
| 3 | Explained | all of Tier 2; prediction declared before anomaly found; mechanism derived | predictive | predictive | EXPLAINED + CLOSED or PERSISTENT or IRRESOLVABLE |

---

## Atlas Register

The register is the living list of all filed citations. It lives alongside this document and is updated as citations are filed, upgraded, or closed.

**Register format (one row per citation):**

| Citation ID | Type | Subtype | Tier | Status | Fixture | First observed | Panels |
|---|---|---|---|---|---|---|---|
| *(no entries — register opens with first filed citation)* | | | | | | | |

**Register rules:**

1. Citations are never deleted. Closed citations are marked CLOSED with a date; they remain in the register for audit purposes.
2. A citation ID, once assigned, is permanent. If a citation is reclassified (e.g., X-B reclassified as X-C after Tier 2 characterization), the ID is retained with the reclassification noted in the citation card lineage.
3. Tier upgrades require new evidence added to the card. Reassessment of existing evidence without new runs does not constitute a tier upgrade.
4. Two citations at the same spatial location in the same fixture may be distinct if they are in different subtypes. File separately; note the relationship in each card's lineage.

---

## Relationship to Observer Storyboard v1

A citation card is an Observer Storyboard v1 card with constrained panel content and a tiered Verdict.

| Observer Storyboard v1 concept | Citation atlas adaptation |
|---|---|
| 9-panel card structure | Retained; panel content is constrained by citation type |
| Panel 9 Verdict: PASS / FAIL | Replaced by tiered status: CANDIDATE → CONFIRMED → CHARACTERIZED → EXPLAINED |
| MISSING panel = red placeholder | Retained; a citation card with missing panels has a lower evidence ceiling |
| Scene contract (Panel 2) | Extended to include the declared observer frame (Xeno) or declared convergence criterion (Zeno) |
| Sensitivity Signature (Panel 8) | Mandatory for Tier 2; determines Fi / Gi designation |
| Lineage (Panel 6) | Extended to include all reproducibility runs and their matched parameters |

A citation card with all nine panels fully populated is eligible for Tier 2 characterization. A Tier 3 explanation requires additional content in Panels 2 and 4 (model prediction and explanation) beyond what a standard storyboard card requires.

---

## Failure Modes

**Filing a Tier 0 candidate without a declared observer frame (Xeno) or convergence criterion (Zeno):**
Panel 2 is incomplete. A Xeno citation without a declared observer frame cannot advance past Tier 0 because "outside the expected frame" has no meaning without declaring what the expected frame was. A Zeno citation without a declared convergence criterion cannot advance because "non-convergent" has no meaning without declaring what convergence would look like.

**Conflating a Boundary Xeno (X-B) with a Seam Xeno (X-S):**
Boundary Xenos occur at zone/field boundaries and are symmetric; Seam Xenos occur at receiver ownership seams and are sensitive to observation angle. The distinguishing test: move the camera slightly and re-run. An X-S signature shifts or disappears; an X-B signature stays aligned with the boundary geometry.

**Reporting a Budget Zeno (Z-B) with only two budget levels:**
Two levels show a direction but not a trend. A citation with two budget levels cannot be confirmed as a Zeno pattern — it is only a candidate. Do not file as Tier 1 until the third level demonstrates the asymptote.

**Upgrading a Tier 1 citation to Tier 2 by reassessing existing evidence:**
Tier 2 requires a full sensitivity sweep and a model prediction. Neither can be derived from reassessing runs already filed for Tier 1. New runs are required.

**Filing a CLOSED verdict without a declared explanation:**
CLOSED means the anomaly is no longer anomalous in the updated or corrected model. If the "explanation" is only "we stopped seeing it," the correct status is PERSISTENT (the model may have changed) or the citation should be left at CHARACTERIZED pending a real explanation.

---

## Glossary Extensions

The following terms are used in this document and extend Observer Language v1:

**Observer frame** — The set of scene regions, directions, and model configurations that the Assumptions panel declared would contribute to a given measurement. A Xeno citation is defined as arriving from outside this set.

**Convergence criterion** — The declared condition that would constitute a stable answer for a Zeno citation: for Z-S, the step size at which disagreement should fall below a threshold; for Z-B, the budget at which closure rate should plateau at 100%.

**Refinement sequence** — The ordered list of step sizes or budget levels tested in a Zeno citation, from coarsest to finest. Minimum length 3 for Tier 1; minimum length 5 for Tier 2.

**Growth rate** — The rate at which disagreement increases across a Zeno refinement sequence. Measured as the ratio of disagreement at level N+1 to disagreement at level N. A growth rate > 1 at every level confirms a Z-S citation. A growth rate approaching 1 suggests the citation may be resolving.

**Field-induced (Fi)** — Designation for a Zeno citation where the Zeno region is absent at baseline (0% curvature) and appears only with field activation.

**Geometry-induced (Gi)** — Designation for a Zeno citation where the Zeno region is present at baseline (0% curvature) and persists regardless of field activation.

**Focal region** — For X-C (Caustic Xeno): the spatial region where ray paths from multiple source directions converge. Named in the Panel 9 attribution at Tier 3.

**Ownership sensitivity** — For X-S (Seam Xeno): the rate at which receiver ownership assignment changes with small changes in ray direction at a seam location. High ownership sensitivity produces wide Seam Xeno signatures; low sensitivity produces narrow ones.

**Irresolvable** — A Tier 3 status designation for Zeno citations where the convergence singularity is proven to exist at all finite precisions within the declared contract. The citation is explained and cannot be eliminated without changing the scene contract. Distinct from PERSISTENT (which applies to Xeno citations that are known and cannot be eliminated without changing the scene geometry or observer frame).
