---
po_doc_type: experiment
title: Tuning the Region Probe
status: partial
engine_commit: "5ce15c13"
scene_id: portal_gallery_seam_v0
scene_class: evidence_qualification
instrument_tier: semantic
instrument_id: SEM-OUT
channel_id: cathedral.probe.outcome
source_channel_deps:
  - cathedral.probe.outcome
  - cathedral.probe.region_label
  - cathedral.probe.refinement_level
units: "codes; counts; levels; planned heatmaps in steps/length"
validity_condition: "SNAPSHOT Complete; Unprocessed=0; RGB never used for selection; refine only on valid context."
visualization_mapping: "Categorical masks from outcome/region/level channels; continuous heatmaps planned from transport records when sealed."
claim_boundary: "Tuning adjusts Region Probe policy and reading of sealed channels; not physical truth convergence. Deeper refinement ≠ greater truth."
evidence_links: []
contract_links:
  - CathedralProbe/ProbeRegionRefinementEngine.cs
  - CathedralProbe/ProbeRefinementSummary.cs
  - CathedralProbe/ProbeSnapshotLifecycleModel.cs
  - CathedralProbe/CathedralProbeObservationFrameAdapter.cs
last_qualified_run: not yet qualified
generated: false
---

# Tuning the Region Probe

!!! abstract "Observation card"
    | Field | Value |
    |-------|-------|
    | Status | **partial** |
    | Verified against | plane/refine/lifecycle sealed path (`6e69d792` lineage; refine `5b9df901`) |
    | SceneId | `portal_gallery_seam_v0` (primary); hermetic secondary |
    | SceneClass | evidence_qualification |
    | Channels (now) | `cathedral.probe.outcome`, `.region_label`, `.refinement_level` |
    | Channels (planned) | `transport.final_step_count`, `transport.path_length`, field maps |
    | Dependencies | Complete SNAPSHOT outcome plane |
    | Units | enum / label / level |
    | Validity | Context match for refine; no RGB select |
    | Display mapping | overlays from sealed channels only |
    | Claim boundary | Deeper ≠ truer; complete ≠ “solved” |
    | Last qualified run | not yet qualified |

**Public name:** Region Probe · **Internal architecture:** Cathedral Probe Stage 0.

---

## Purpose

Teach operators how to tune **reading** and **Transport Effort** without confusing:

| Idea | Public term |
|------|-------------|
| What class is this sample? | Outcome plane / `ProbeOutcomeCode` |
| Did we stop for step budget? | **Unresolved Regions** (`MaxStepsExhausted`) |
| How hard did we work? | **Transport Effort** (policy steps—not time/energy) |
| How many deepenings? | **Region Refinement** level |
| Pink pixels | Legacy **display** only |

---

## Channel / view matrix

| View | What it shows | Exists now? | Source |
|------|----------------|-------------|--------|
| Categorical outcome mask | Per-sample `ProbeOutcomeCode` | **implemented** (plane + sealed); UI overlay **partial** | `cathedral.probe.outcome` |
| Max-step mask | `outcome == MaxStepsExhausted` | **implemented** as filter; UI **partial** | derived from outcome |
| Region labels | Connected Unresolved Regions | **implemented** | `cathedral.probe.region_label` |
| Refinement-level history | Per-sample level | **implemented** | `cathedral.probe.refinement_level` |
| Final-step-count heatmap | Steps used at termination | **planned** | future `transport.final_step_count` |
| Path-length heatmap | Integrated path length | **planned** | future `transport.path_length` |
| NormalRGB / Depth / NdotV | Display Mode shading | **display path** | host film—**not** probe truth |
| Legacy magenta fill | Fallback presentation | **observed** | display—**not** outcome |

!!! tip "Tuning order"
    1) Qualify SNAPSHOT complete → 2) outcome histogram → 3) Unresolved Regions → 4) refine (**P**) → 5) continuous heatmaps only when sealed channels ship.

---

## MaxStepsExhausted and Unresolved Regions

- **MaxStepsExhausted** — sample stopped at the ray **Transport Effort** budget under numerical policy.
- **Unresolved Regions** — connected components of those (and rules as coded) labeled via `region_label`.
- **Not** — all magenta RGB pixels; not empty space; not wormholes.

---

## Region Refinement and “resolved”

A sample is **resolved by refinement** only if it was **`MaxStepsExhausted` before** and becomes **`HitGeometry` or `BackgroundResolved` after**.

Not resolved: still max-steps, absorbed-secondary, fault, invalid.

!!! warning "Doctrine"
    **Deeper refinement ≠ greater truth.**
    Step count is **numerical effort**, not time or energy.
    Field dial is not evaluated field magnitude.

---

## Operator sequence (Stage 0)

1. Freeze pose · set Field · **SNAPSHOT** until Complete.
2. Confirm Unprocessed = 0.
3. Read **outcome** histogram.
4. Select Unresolved Region from **region_label** (largest default).
5. **P** refine once · read summary transitions.
6. Toggle Display Mode (**N**) — histogram must **not** change.
7. Change field or move → context **STALE** · new SNAPSHOT.

---

## Controls

| Key | Role |
|-----|------|
| E | Experiment (invalidates seal) |
| G | Formal SNAPSHOT |
| Q | Probe View (sealed remap) |
| P | Region Refinement |
| J / K | Cycle regions *(when wired)* |
| R | Reset refinement *(when wired)* |
| , . 0 1 | Field |
| N | Display Mode only |
| Tab | Inspector |

Full legend: [Controls](../reference/controls.md).

---

## Evidence to record

- `ProbeFrameSummary` before/after refine
- `ProbeRefinementSummary` (atomic flag, transitions)
- Context key + dimensions
- Explicit note: RGB not used for selection

---

## Related

- [Collider Contact Is Not Semantic Resolution](../learn/collider_contact_is_not_semantic_resolution.md) — MaxSteps can follow earlier contact when StopOnHit is false; film hit counters are pass-2.

## Interpretation boundary

!!! warning "Claim boundary"
    - Categorical mask ≠ continuous effort heatmap.
    - Max-step mask ≠ legacy magenta.
    - Step/path heatmaps are **planned**.
    - Complete all-max-steps plane is still a complete snapshot.
    - Region Probe does not prove wormholes.

---

## Historical title

Earlier docs said “Tuning the Cathedral Probe.” Same experiment; public default is **Region Probe**. Alias: [tuning_the_cathedral_probe.md](tuning_the_cathedral_probe.md).
