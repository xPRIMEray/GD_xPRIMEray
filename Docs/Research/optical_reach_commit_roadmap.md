---
title: Optical Reach and Probe Profiles Commit Roadmap
description: Canonical living roadmap for transport authority, per-pixel inspection, visibility benchmarking, adaptive reach, and bounded optical continuation
---

# Optical Reach and Probe Profiles Commit Roadmap

**Canonical living roadmap | established 2026-11-09**  
**Document status:** approved research sequence, not a claim that planned milestones are implemented

This roadmap reconciles the dated [Current Research Trajectory — 2026-11-01](../current_research_trajectory_2026-11-01.md) with the original [Optical Reach working proposal](xPRIMEray_Commit_Roadmap_2026-11-09.md). The dated trajectory remains the scientific checkpoint. The working proposal remains the historical design input. This page is the maintained source for milestone order and dependencies.

> The instrument must report how it searched, what it measured, what remains unresolved, and which policy owns each conclusion.

## Authority and status

Roadmap labels have precise meanings:

- **Qualified locally** — implemented in a local commit and supported by retained qualification evidence.
- **Provisional observation** — deterministic recorder evidence that is not a sealed scientific ledger or a new Q channel.
- **Planned** — an approved place in the research sequence, not implemented behavior.
- **Experimental policy** — behavior that requires an explicit policy identity and independent qualification before it can support scientific claims.

### Dated implementation snapshot

The following source-control state is a dated checkpoint, not a durable definition of what is published:

- **Remote `main` observed 2026-11-09:** `742199a4400fa7c0effb00d68a6e1157a639a948`.
- **Qualified local PQG-R1 commit:** `c13266e011cea1abfdc0ca778eec47326ad252b0`.
- **Qualified local PQG-R1.1A commit and roadmap base:** `4b864fe9d6e51b4021f500ded0a7c03ac40a379d`.

Remote publication can change independently of this roadmap. Durable milestone status is carried by the labels and acceptance gates below, not by assuming these dated refs remain current.

The qualified local control checkpoint consists of:

- **PQG-R1** — locally committed and qualified: P selects the human profile, Q owns Formal acquisition and sealed-plate interrogation, and G owns LIVE transitions.
- **PQG-R1.1A** — locally committed and qualified: request-owned Formal policy provenance, consistent interactive Q/G authority, and deterministic restoration of effective LIVE configuration.

Their presence does not qualify any later milestone in this roadmap.

## Scientific evidence checkpoint

The November 1 Canonical Workbench acquisition remains the authority for the following measurements:

| Measurement | Qualified result |
|---|---:|
| Film samples | 14,400 (160×90) |
| Validated geometric queries | 521,819 |
| Godot/kernel mismatches | 0 |
| BVH mismatches | 0 |
| Recorded portal remaps | 14,057 events across 3,709 pixels |
| Pixels with diagnostic contact activity | 6,266 |
| Formal outcomes | 14,400 `MaxStepsExhausted` |
| Qualified terminal optical hits | 0 |

The 6,266 contacted pixels are **not qualified terminal optical hits**. Contact activity is an intermediate diagnostic observation. The recorder remains provisional and non-sealable, question dispositions remain pending, and no sealed scientific-ledger hash is claimed.

**Historical Disposition checkpoint:** the provisional transport-disposition exporter records reach, query coverage, first-contact activity, termination inputs, and completeness without changing transport. It established the first step in the instrumentation chain, but it did not qualify terminal optical hits or seal the transport ledger.

The original instrumentation dependency is therefore preserved:

```text
Disposition
  → Pixel Inspector
  → 3D Trajectory
  → Qualified Termination
  → Adaptive Coverage
```

New authority and benchmark work can support this sequence, but cannot reverse it.

## Research measures

Optical reach is not a raw hit-count contest. Every reach experiment must report these separately:

1. **Classification closure** — whether every eligible sample has an honest terminal or unresolved classification.
2. **Reference-visible recall** — detection of geometry that an independent precision ladder indicates is reachable, including disagreement and alternate-path cases.
3. **False-positive and wrong-object rate** — whether reported contacts belong to the correct object, primitive, and domain authority.
4. **Cost to discover** — queries, integrated steps, candidate scans, latency, and coverage over time under a declared compute envelope.

The long-range aspiration of high reference-visible recall is not a claim that a corresponding percentage of all image pixels should hit geometry, nor a claim that such recall has already been achieved.

### Benchmark detection predicate

Before TERMINATION-R0, REACH-B0 may qualify only **reference-visible diagnostic contact detection**, not terminal-hit recall.

- A **reference-positive sample** is a declared film sample for which an independent fixture-specific source — an analytical Euclidean solution or the separately configured Reference-Precision Null Geodesic precision ladder — reports reachable canonical geometry under documented position, direction, object/primitive identity, tolerance, and multi-solution rules.
- A **tested detection** is present only when the tested acquisition records validated spatial-contact authority for the same canonical geometry identity under the fixture's declared tolerance and comparison rule.
- A missing tested detection is a diagnostic false negative; a mismatched identity or unsupported contact is reported separately as wrong-object or false-positive evidence.
- Contact detection, path observation, surface classification, and terminal outcome are separate channels. Until TERMINATION-R0 is GREEN, the benchmark must label terminal-hit recall **UNQUALIFIED / NOT AVAILABLE**.

The reference source, precision ladder, tolerances, eligible sample mask, ambiguity policy, and independence from the tested policy must be versioned per fixture. The finest numerical reference remains a tested oracle, not unquestionable truth.

## Intended instrument workflow

```text
Scene selection
  → P strategy
  → G exploration
  → Q acquisition
  → Pixel / Trajectory inspection
```

- **Scene selection** chooses the world and eligible instrument context.
- **P strategy** selects the human-facing LIVE strategy; it does not retroactively rewrite request-owned or sealed Formal evidence.
- **G exploration** controls transient LIVE rendering and search behavior.
- **Q acquisition** obtains a frozen Formal request or navigates an existing sealed plate.
- **Pixel / Trajectory inspection** explains recorded evidence with completeness, sampling, and provenance limits intact.

## Ordered milestone plan

All milestones after PQG-R1.1A are **Planned**. Each requires a separately reviewed, bounded change. The historical Disposition checkpoint is implemented only as provisional, non-sealable observation.

| Order | Milestone | Purpose and scientific boundary | Acceptance or stop gate |
|---:|---|---|---|
| 1 | **DISPOSITION-R0 — Historical provisional checkpoint** | Record reach, query coverage, first-contact activity, terminal inputs, and completeness without influencing transport. | Deterministic provisional artifacts exist; recorder remains non-sealable and no terminal-hit authority is claimed. |
| 2 | **PQG-R1 / PQG-R1.1A — Qualified locally at the dated snapshot** | Establish separated P/Q/G ownership, request-frozen Formal provenance, and deterministic effective LIVE restoration. | Existing Formal policy, locked identities, Q arrays, and observation artifacts remain unchanged. |
| 3 | **PQG-R1.1B — Planned** | Expand authority regressions: complete effective-LIVE comparison, disabled-input cases, programmatic acquisition under disabled interactive input, differing-policy fixtures, and fail-closed completion checks. | Tests execute the negative branches; no production numerical change or new Formal identity. |
| 4 | **PQG-R1.2 — Planned** | Add an explicit LIVE → Q handoff: release transient LIVE authority, restore Formal defaults, obtain a new request-owned acquisition, then seal. | No mixed buffers, inherited LIVE settings, stale ledgers, or implicit generation replacement. Formal parity remains exact. |
| 5 | **PQG-R1.3A — Planned Probe Policy Inspector** | Read-only HUD for selected, applied LIVE, request-owned, and sealed profiles/policies. Itemize effective deltas for steps, step bounds, range, query cadence, broadphase, pass policy, film scheduling, and budgets, then translate each delta into user-facing consequences. | Values come from authoritative effective configuration and acquisition state. It states whether P is immediate, pending, or frozen and whether a P choice can affect the next Q request; it never becomes a policy input. |
| 6 | **PQG-R1.3B — Planned SmartScale isolation** | Audit SmartScale ownership and scheduling separately from profile display and Formal provenance before any activation. | SmartScale cannot mutate active Formal settings, silently rewrite LIVE policy identity, or invalidly label provenance. Prefer shadow-only qualification first. |
| 7 | **SCENE-R0 — Planned scene-selection baseline** | Investigate why the hanging textured portal scene is not selectable and define the minimum navigation contract connecting a user-visible scene choice to an eligible instrument context. | Report the actual discovery/binding gap and a minimal contract. Do not claim the scene is selectable until real navigation and ownership tests pass. |
| 8 | **TEXTURE-R0 — Planned mapped-surface baseline** | Qualify mapped surfaces in LIVE and Formal output using controlled fixtures. Keep geometry contact, material sampling, portal remapping, and future discrete optical interaction as separate observations. | Texture/material presentation cannot create geometry authority. LIVE and Formal mappings identify their producer, sampled inputs, missing-data behavior, and comparison limits. |
| 9 | **INSPECT-R0 — Planned** | Inspect one current pixel’s ordered questions, events, attempts, completeness, authority, and terminal inputs. | No event reordering or promotion of provisional contacts. Retry/replacement ownership is visible and unambiguous. |
| 10 | **TRAJECTORY-R0 — Planned** | Visualize recorded 3D positions and segments with their sampling, gap, remap, generation, and authority limits. | Sparse observations are not presented as a continuous solved trajectory. Missing and incomplete intervals remain explicit. |
| 11 | **REACH-B0 — Planned independent benchmark** | Build the near/far visibility atlas using the declared reference-positive and tested-detection predicates above: Euclidean controls, intense-curvature detail, portal fields, weak long-range deflection, tiny targets, occlusion, and empty-space negatives. | Per-fixture reference identity, precision, tolerances, eligible mask, diagnostic-contact recall, wrong-object rate, classification, cost, uncertainty, and baseline hashes are retained. Terminal-hit recall remains unqualified until TERMINATION-R0. |
| 12 | **TERMINATION-R0 — Planned** | Define qualified contact, continuation, terminal outcome, capacity termination, and unresolved semantics. | Intermediate contact cannot masquerade as a terminal hit; reference disagreements and incomplete evidence fail closed. GREEN requires fixtures that join recorded contact, authority, disposition, and terminal outcome without inference. |
| 13 | **PROFILE-R0 — Planned** | Preserve Cathedral and Motion; specify Detail/Micro, Deep Field/Tele, and a future Adaptive profile as versioned LIVE policies. | Full effective manifests expose budget, step control, range, query cadence, broadphase, curvature risk, and fairness. Existing `cathedral.formal.v1` remains unchanged unless separately authorized. |
| 14 | **REACH-R0 — Planned** | Produce a shadow-only `ReachPlan` from eligible scene/domain anchors, projected size, curvature/error risk, candidate opportunity, and independently qualified reference/outcome evidence. | Recommendations cannot change ray paths, geometry results, Formal evidence, or per-pixel authority. Unknown regions retain exploration reserve. INSPECT-R0 and TRAJECTORY-R0 expose why each recommendation exists. |
| 15 | **REACH-R1 — Planned** | Activate bounded adaptive LIVE discovery for near detail and distant weak-deflection targets only after inspection, trajectory, benchmark, and termination gates are qualified. | Requires INSPECT-R0 GREEN, TRAJECTORY-R0 GREEN, REACH-B0 independent-reference qualification, and TERMINATION-R0 GREEN. Improve qualified recall per cost without increasing false contacts or contaminating Formal. No pixel inherits another pixel’s evidence. |
| 16 | **OPTICS-R0 — Planned** | Audit existing mirror and dielectric boundary behavior, normals, IOR, Snell/TIR handling, ordering, and loop/re-entry bounds. | Analytical single-mirror and single-interface controls pass. Existing implementation is not treated as proof of recursive continuation. |
| 17 | **OPTICS-R1 — Planned** | Add bounded reflected/refracted continuation with explicit interaction index, media, path state, termination, and reproducible event ordering. | Start with one continuation and a deterministic depth ladder. Analytical mirror, slab, TIR, nesting, and resource-bound fixtures pass under a separately authorized policy when authority changes. |
| 18 | **PORTAL-R1 — Planned** | Qualify a recursive mirror/GRIN portal benchmark with ordered material, continuous-field, and topology events. | Finite caps, honest unresolved states, stable event traces, and scientific diagnostics accompany any presentation render. |

## Parallel work without dependency reversal

Some work can proceed in parallel, but the scientific gates remain serial:

- **SCENE-R0 and TEXTURE-R0 investigation** may proceed alongside policy-inspector work because they establish baseline navigation and presentation contracts without changing transport.
- **REACH-B0 fixture design** may proceed alongside INSPECT-R0 and TRAJECTORY-R0 because it establishes independent references rather than changing transport. Active reach remains blocked on all three plus TERMINATION-R0.
- **PROFILE-R0 design** may proceed alongside TERMINATION-R0, but active adaptive behavior remains blocked on qualified evidence and complete effective-policy manifests.
- **OPTICS-R0 analytical fixtures** may be prepared while reach work advances. OPTICS-R1 remains blocked on trustworthy ordered-event inspection and termination semantics.
- Documentation and visualization may describe proposals at any time, provided they retain explicit Planned or Provisional labels.

## Three transport concepts that must remain distinct

- **Continuous GRIN curvature** changes direction during field integration.
- **Discrete material interaction** applies a reflection or refraction decision at an interface and may create a bounded continuation.
- **Portal remapping** changes chart, topology, or scene position through an explicit mapping event.

They may eventually share an ordered transport ledger, but they are not interchangeable physical or numerical events. A portal remap is not a material bounce, and continuous curvature is not a sequence of invented surface interactions.

## Relationship to Project Optical Closure

[Project Optical Closure](../xPRIMEray/optical_closure_roadmap.md) remains a separate post-hit interpretation program. Its existing stages do not acquire new intersection authority. Optical Reach and Transport Research owns visibility benchmarking, hit recovery, continuation, and adaptive search. Results may be handed to Optical Closure only after their authority and termination semantics are qualified.

## Publication and audit rules

Each milestone should retain:

```text
research question
→ policy identity and effective configuration
→ exact source scope and fingerprint
→ fixture and expected measure
→ actual evidence
→ scientific parity or new identity
→ source-to-test chronology
→ independent review
→ GREEN/HOLD
→ commit and publication status
→ next unresolved risk
```

Do not optimize unchecked hit counts, promote provisional observations, imply that a proposal is deployed, stage generated site output, or publish without explicit authority.

## Historical inputs

- [Current Research Trajectory — 2026-11-01](../current_research_trajectory_2026-11-01.md) — dated scientific evidence and the original instrumentation dependency chain.
- [Optical Reach working proposal — 2026-11-09](xPRIMEray_Commit_Roadmap_2026-11-09.md) — preserved proposal that introduced the expanded commit sequence.
- [Optical Transport Feature Map](OPTICAL_TRANSPORT_FEATURE_MAP.md)
- [Reference-Precision Null Geodesic Probe](reference_precision_null_geodesic_probe.md)
- [Live Acquisition Decision Tree](../architecture/live-acquisition-decision-tree.md)

**A roadmap is a promise to test, not a claim to have seen.**
