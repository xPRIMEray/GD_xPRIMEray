# xPRIMEray | Optical Reach & Probe Profiles Commit Roadmap

**Working proposal | 2026-11-09 | Not a statement that future milestones are implemented**  
**Project:** [xPRIMEray/GD_xPRIMEray](https://github.com/xPRIMEray/GD_xPRIMEray)  
**Research north star:** Develop a reproducible curved-ray instrument that discovers and resolves genuinely visible optical paths, from fine near-field curved geometry through distant weak-deflection targets to reflection, refraction, and bounded multi-interaction transport.

> **The instrument must know not just what it drew, but how it searched, what it measured, what remains unresolved, and which policy owned each conclusion.**

> **Historical working proposal:** This document is preserved as the 2026-11-09 design input. It is superseded for current milestone authority by the [canonical Optical Reach roadmap](optical_reach_commit_roadmap.md). Commit and publication statements below describe the dated review state; the research discussion remains part of the project record.

## 1. Evidence anchors and present state

- **Public remote main at review:** `742199a4400fa7c0effb00d68a6e1157a639a948`, provisional transport disposition map (Nov. 5 CST / Nov. 6 UTC). The next two qualified commits have **not** been pushed as of the user-provided post-commit report.
- **PQG-R1 locally committed:** `c13266e011cea1abfdc0ca778eec47326ad252b0`, decouples P (profile), Q (Formal acquisition / sealed plate), G (LIVE toggle).
- **PQG-R1.1A locally committed:** `4b864fe9d6e51b4021f500ded0a7c03ac40a379d`, request-owned Formal policy provenance, interactive Q/G authority, restored LIVE rows. Five-file exact-scope commit, parent `c13266e`, tree `fad9a86ae893af462978e7e5a2fd970b5e93dd92`.
- **Last qualified scientific regression:** eight policy/data/context identities unchanged, five raw Q arrays byte-identical, fifteen deterministic portal/disposition artifacts byte-identical. Workbench 160×90, Gallery 80×45. Recorder observation remains provisional and non-sealable.
- **Public documentation lag:** `Docs/index.md`, `Docs/architecture/live-acquisition-decision-tree.md`, and associated MkDocs pages still describe an older G=Formal SNAPSHOT / Q=read-only view model. This is **historical documentation**, not the current qualified local control model.
- **Existing local PUBLIC-R1 dirt:** `mkdocs.yml`, `Docs/index.md`, `Docs/current_research_trajectory_2026-11-01.md`, and a portal image are independently modified/uncommitted. Preserve and reconcile; never indiscriminately stage.
- **Inherited foundations:** PixelMemory and opt-in G4/G5 fair-garden scheduling, TLAS/OverlapOnly LIVE, canonical contact/effort/outcome plates, provisional Formal boundary recorder, object-seeded and reference-precision research, existing mirror/refraction boundary-layer implementation. Historical 'G4/G5' names are scheduler milestone labels, not the present keyboard G key.

### Why this is not simply a 'hit-rate improvement' project

Separate four metrics:

1. **Classification closure:** all pixels have honest terminal outcomes; zero unresolved exits in qualified hermetic reference fixtures. Not synonymous with a geometry hit.
2. **Reference-visible hit recall:** among pixels or object-region samples where an independent high-precision reference indicates reachable geometry, what fraction does the tested policy detect? `TP/(TP+FN)`, with reference disagreements and multi-solution paths tracked separately.
3. **False-positive / wrong-object rate:** a larger hit count must not be purchased by phantom contacts or incorrect collider/domain ownership.
4. **Cost to discover:** query counts, integrated steps, candidate scans, latency, and coverage over time, under fixed compute envelope and scene/observer conditions.

**Aspirational target:** ≥90% reference-visible hit recall on a declared, nontrivial near/far/multi-domain benchmark suite, at bounded cost and with accuracy constraints. It is **not** a claim that 90% of all image pixels should hit a surface or that 90% is currently achieved. In simple Euclidean control scenes aim at 100% agreement where mathematically determinable. Treat the finest numerical oracle as a tested reference, not absolute truth.

## 2. Commit sequence

Each milestone is a **separately reviewed, small-scope commit**, not a single mega-branch. Names below are proposed; avoid colliding with existing published tags and commit families.

| Proposed checkpoint | Purpose and boundaries | Acceptance / stop gate |
|---|---|---|
| **DOC-R0: Roadmap reconciliation** | Reconcile in-flight PUBLIC-R1 without overwriting existing work. Add canonical `Docs/Research/optical_reach_commit_roadmap.md`, a concise homepage roadmap panel, and MkDocs nav/README pointer. Docs-only. | Site builds, accurate local-vs-remote status and P/Q/G semantics, no duplicate contradictory 'canonical' page, precise changed-path inventory. No push by default. |
| **PQG-R1.1B: Authority regression expansion** | Tests only: complete 52-field post-Formal LIVE comparison; disabled Q/Shift+Q/Ctrl+Q; real programmatic capture while interactive input disabled; synthetic differing Formal-policy fixture and post-seal fail-closed refinement. | No production numerical changes. Existing eight identities/five arrays/15 artifacts preserved. Negative tests actually execute, not just `has_method`. |
| **PQG-R1.2: LIVE→Q acquisition handoff** | Allow Q during LIVE only by explicitly releasing transient LIVE authority, restoring Formal defaults, obtaining a new request-owned acquisition, then sealing. G remains LIVE toggle; Q owns Formal take and plate interrogation. | Deterministic transition matrix and no mixed buffers, stale ledger, inherited LIVE scheduler settings, or implicit generation replacements. Formal parity exact. |
| **PQG-R1.3: Effective-policy instrumentation and SmartScale isolation** | A runtime HUD showing selected profile, LIVE effective policy, current acquisition owner, sealed Formal policy/hash, row/collision budgets and disposition. Audit SmartScale before enabling any new policy. | SmartScale cannot mutate active Formal settings or invalidly label provenance; runtime display derives from effective config rather than guesses. Prefer shadow-only first. |
| **REACH-B0: Visibility benchmark atlas** | Establish reference fixtures at (a) uncurved human-eye baseline, (b) near intense-curvature/small-detail, (c) portal-mouth and opposite-mouth fields, (d) moderate curvature at long range, (e) tiny distant targets, (f) occlusion/empty-space negative controls. Pair viewport masks with independent per-pixel oracle/precision ladders. | Baseline per-fixture recall, precision, classification, wrong-object rate, cost, and uncertainty. Store baseline hashes. No optimizer allowed before this measures meaningful improvement. |
| **PROFILE-R0: Probe Profile taxonomy** | Preserve Cathedral and Motion. Design *Detail/Micro*, *Deep Field/Tele*, and optionally *Adaptive/Auto* as **LIVE execution policies** first. Define budget, sampling, step size, collision cadence, curvature risk, range, and fairness independently. | Profiles are named and versioned; policy manifests expose the full effective configuration. Existing Formal `cathedral.formal.v1` unchanged unless separately authorized and re-qualified. |
| **REACH-R0: Shadow ReachPlan-v0** | Derive candidate risk from world-space object/domain anchors, projected size, curvature bound, proximity, segment-query opportunity and prior *eligible* LIVE evidence. Generate a policy-only recommended per-region budget and search plan. Reuse PixelMemory and the object-seeded/reference-precision probes. | **Shadow only**; no effect on actual ray paths, geometry results or Formal. Compare recommendations with measured misses; account for unknown regions and finite exploration reserve. |
| **REACH-R1: Adaptive LIVE hit discovery** | Activate bounded ReachPlan on LIVE only: broad-phase candidate seeding, curvature/error-aware local step refinement, query cadence and subdivision, long-distance path coverage, one or more opportunities for difficult small targets. Maintain G5-style fairness/exploration budget. | On frozen near/far test suite, improve reference-visible recall per cost without raising false contacts, dropping classification closure or contaminating Formal. No neighbor inherits a measured result. |
| **OPTICS-R0: Material continuation authority audit** | Inventory and independently test existing `BoundaryLayerVolume` `PerfectMirrorReflection`, `DielectricRefraction`, Snell/TIR helpers, normals, IOR and material crossing behavior. Distinguish discrete interface events from continuous GRIN steering and wormhole chart transitions. | Single mirror and single interface analytical/control fixtures pass; event ordering, normal orientation, loop/re-entry avoidance and bounded termination documented. No mistaken claim that the older proxy validates recursive beauty paths. |
| **OPTICS-R1: Bounded multi-interaction continuation** | Follow a reflected or refracted outgoing ray through new curved segments, with explicit `interaction_index`, interface type, entry/exit medium, path state, and termination reason. Start with one continuation, then a small deterministic max-depth ladder. | Exact analytical planar-mirror and slab-lens cases, TIR, medium nesting, no infinite loops, reproducible branch/continuation ledger, clear energy/throughput semantics if modeled. Separate experimental Formal policy if transport authority changes. |
| **PORTAL-R1: Recursive mirror/GRIN flagship** | Build the existing proposed two-mirror + GRIN corridor benchmark, then combine with wormhole mouth/chart transitions as a distinct later fixture. Show recursive depth, caustic/risk maps and authoritative event traces. | Stable ordered events across bounces, finite resource caps, honest unknown/unresolved state, no banding-induced false confidence, scientific diagnostics alongside beauty PNG. |
| **OBS-R2: Research observatory publication** | Publish qualified benchmark Atlas and probe-profile comparisons to GitHub Pages. MisterY Labs may feature cinematic/artifact views, with backlinks to engineering evidence. | Every shipped capability links to implementation commit, fixture, results and known limitations; proposals remain marked Planned/Experimental. |

## 3. Scientific distinctions to preserve

**Human-eye vs microscope vs telescope are workload analogies, not new optical physics claims.** Near fine structures demand enough screen-space/angular resolution, collision opportunity and curvature precision to avoid missing small geometry. Far weakly deflected targets demand sufficient travel range, uncertainty-bounded step sizing and search opportunities in regions where local curvature alone may look unimportant. Increasing global `StepsPerRay` cannot substitute for a correct decision tree.

Suggested decision hierarchy: **observer / scene context → eligible geometry & domains → broad-phase risk → adaptive path error bound → candidate contact opportunity → collision/contact validation → accept / refine / continue / terminate**. Treat scheduler importance separately from ray-integration step length and separately from physical interaction count.

A *bounce* is a discrete reflection or refraction interface event followed by a new ray continuation; a portal transfer is a chart/topology mapping event; GRIN bending is continuous field propagation. These may share a transport event ledger, but must never be conflated numerically or linguistically. For dielectric boundaries, start with a single deterministic transmitted/reflected continuation policy and record its limitations; branching Fresnel paths and spectral transport are later opt-in experiments.

**Project Optical Closure** in the current site is a post-hit *interpretive* program constrained not to create new intersection authority before its own Stage 4. This roadmap's hit-recovery and multi-interaction work belongs to a distinct **Optical Reach / Transport Research** program, not an undocumented expansion of that project's Stage 1–3.

## 4. Publication plan: one source of truth, multiple windows

Canonical working spec: `Docs/Research/optical_reach_commit_roadmap.md`. The existing `Docs/architecture/live-acquisition-decision-tree.md` remains the canonical *acquisition control-flow architecture*; it should link to the roadmap and receive a clearly scoped P/Q/G terminology update, not become a second conflicting roadmap. `Docs/index.md` is the **visitor-facing summary**. `README.md` gets one short roadmap link. `mkdocs.yml` adds one nav entry, preferably under Research, near the Optical Transport Map.

Suggested homepage panel directly below the hero or Engine State:

> **Research Flight Plan · November 2026**  
> **Qualified locally:** PQG-R1 and PQG-R1.1A, pending intentional remote publication.  
> **Next:** authority regression tests → direct LIVE-to-Formal Q handoff → effective-policy HUD.  
> **Research frontier:** near-field fine-detail probe, distant Deep Field reach, bounded adaptive discovery, mirror/refraction continuations, recursive GRIN portal.  
> **Read the roadmap:** [Optical Reach & Probe Profiles](optical_reach_commit_roadmap.md).  
> *Qualification is not deployment. A listed proposal is not a measured feature.*

At the same time revise older homepage sentences that currently say `G measures Formal SNAPSHOT; Q only rereads` to reflect *committed local* PQG semantics **only once those commits are on the website branch**, or label the upcoming behavior as a preview. Disambiguate old scheduler milestone labels G4/G5 from keyboard G. Do not turn historical experimental numbers or stale Phase-0 readiness counts into current claims.

### Site implementation sequence

1. **Codex read-only inventory:** current `git status --porcelain=v1`, HEAD, index, staged/unstaged/untracked inventory; inspect local `Docs/index.md`, `mkdocs.yml`, uncommitted `Docs/current_research_trajectory_2026-11-01.md`, and the current Pages deployment workflow. Compare all to public `origin/main` and this proposal. Do not overwrite PUBLIC-R1.
2. **Claude architecture and claim review:** validate the distinction between proven, experimental, planned, locally committed, and remotely published; reconcile nomenclature and prior documents; enumerate contradictions and broken nav links.
3. **Codex docs-only implementation:** after the audit, use a separate exact-scope docs commit. Make the canonical roadmap, homepage summary, nav and README links with minimal changes. Prefer pre-existing PUBLIC-R1 edits preserved, merged by hand at the hunk level where necessary.
4. **Build and review:** `mkdocs build --strict` in the project environment; inspect generated homepage at desktop/mobile; link-check roadmap and nav; ensure only authored docs paths staged, no `site/`, `obj/`, `output/` or unrelated dirt.
5. **Publication gate:** explicit authorization for push/deploy after core commits are pushed and site copy accurately reflects remote capability. Verify actual GitHub Pages after deployment.

## 5. Standing commit audit protocol

For each feature checkpoint publish a one-page record: `Research question → effective policy ID/version → scope/files → baseline ref and source fingerprint → fixture matrix and expected metric → actual measurements → scientific parity (or new policy identity) → source-to-test chronology → independent Claude audit → GREEN/HOLD → commit SHA → publish status → next unresolved risk`.

No unchecked hit-count optimization, implicit Formal changes, non-attributed screenshots, staged generated build output, indiscriminate cleanup of an unrelated dirty tree, or pushes without deliberate authority.

## 6. Relevant living references

- [Live Acquisition Decision Tree](https://github.com/xPRIMEray/GD_xPRIMEray/blob/main/Docs/architecture/live-acquisition-decision-tree.md)
- [Reference-Precision Null Geodesic Probe](https://github.com/xPRIMEray/GD_xPRIMEray/blob/main/Docs/Research/reference_precision_null_geodesic_probe.md)
- [Optical Transport Feature Map](https://github.com/xPRIMEray/GD_xPRIMEray/blob/main/Docs/Research/OPTICAL_TRANSPORT_FEATURE_MAP.md)
- [RenderStep Gate Hierarchy](https://github.com/xPRIMEray/GD_xPRIMEray/blob/main/Docs/RenderStep_GateHierarchy.md)
- [Recursive Mirror Ghost Portal Phase 1](https://github.com/xPRIMEray/GD_xPRIMEray/blob/main/misterylabs_artifacts/cards/recursive-mirror-ghost-portal.md)
- [Project Optical Closure Roadmap](https://github.com/xPRIMEray/GD_xPRIMEray/blob/main/Docs/xPRIMEray/optical_closure_roadmap.md)
- [Current GitHub Pages](https://xprimeray.github.io/GD_xPRIMEray/)

**Publishing rule:** A roadmap is a promise to test, not a claim to have seen. Preserve visible, measurable distinctions between both.
