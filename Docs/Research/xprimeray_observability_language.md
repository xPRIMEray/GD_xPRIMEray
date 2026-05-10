# xPRIMEray Observability Language

This document defines the canonical visual observability language for xPRIMEray diagnostics, reports, overlays, step ladders, and future realtime diagnostic interfaces.

This is not branding guidance. It is renderer observability structure: a disciplined way to show what the renderer saw, what transport did, where topology formed, where quality constraints appeared, and how those facts changed over refinement.

Core principle:

```text
geometry -> transport -> topology -> quality constraints -> evolution
```

Diagnostic visuals must prioritize explanatory clarity over artistic appearance.

## Canonical Panel Order

Every diagnostic storyboard, contact sheet, and realtime observability mode should preserve this sequence unless a report explicitly explains why it deviates.

| Level | Panel | Purpose | Primary Question |
|---:|---|---|---|
| 0 | Rendered reference / observer frame | Show the final captured observer frame without interpretation. | What did the camera see? |
| 1 | Cartesian geometry projection | Establish a stable coordinate-space reference using projected object bounds, corners, edge midpoints, centroids, and labels. | Where does ordinary scene geometry project? |
| 2 | Transport vectors / hit normals | Show local transport direction, hit normals, continuity vectors, sampled paths, and cross-section slices. | How is local transport behaving? |
| 3 | Ownership topology / seams | Show connected ownership regions, collider/domain seams, graph nodes, graph edges, and topology changes. | What topology did hit acquisition produce? |
| 4 | Budget exhaustion / unresolved islands | Show traversal budget limits, unresolved pixels, oracle disagreement, risk nodes, and quality constraints. | Where is the result numerically constrained or unresolved? |
| 5 | Lineage / phase evolution | Show graph persistence, merge/split lineage, transport quality phase, and step-ladder evolution. | How does the topology evolve across precision levels? |

Layer 1 is foundational. The Cartesian wireframe projection gives the observer a stable anchor before transport complexity is introduced.

## Panel Purposes

### Level 0: Rendered Reference

Use the untouched beauty or capture PNG. It is the observer-frame reference, not a diagnostic conclusion.

Rules:

- Do not draw over the Level 0 source panel except for the panel label.
- Prefer a representative frame with visible geometry and visible hit support.
- If the beauty frame is visually empty, the storyboard may still include it, but selection metadata must mark it as low-information.

### Level 1: Cartesian Geometry Projection

Use ordinary projection of known scene geometry:

- object AABB edges
- corners
- edge midpoints
- centroids
- domain/portal/object ids where available

This panel is not a curved-transport truth claim. It is the coordinate reference layer.

### Level 2: Transport Vectors / Hit Normals

Show renderer-derived local transport facts:

- hit-normal glyphs
- continuity vectors
- sampled ray/path polylines
- camera cross-section minimaps
- centerline or targeted anatomy slices

If projected normal magnitude is degenerate, show a distinct dot/glyph and report the degeneracy count.

### Level 3: Ownership Topology / Seams

Show transport ownership as topology:

- connected same-signature regions
- graph nodes
- graph edges
- seam pixels
- ownership flips
- collider/domain transitions

This is where pixel-level facts become graph-level renderer evidence.

### Level 4: Budget Exhaustion / Unresolved Islands

Show quality constraints and unresolved structures:

- max-step/budget exhaustion heatmaps
- unresolved island overlays
- epsilon-stability maps
- oracle disagreement maps
- risk nodes and probe markers

Budget saturation means smaller step size may no longer improve transport quality under the current traversal limits. It is not automatically a geometry failure.

### Level 5: Lineage / Phase Evolution

Show how topology evolves:

- graph persistence
- appear/disappear events
- merge/split events
- transport quality phase classification
- plateau and budget saturation start steps
- recommended next action

Lineage panels should make refinement behavior legible at a glance.

## Color Semantics

Use symbolic colors consistently across post-process outputs and future realtime overlays.

| Meaning | Preferred Color | Notes |
|---|---|---|
| Cartesian projection | red / coral | Ordinary coordinate geometry. |
| Transport vectors / hit normals | green / cyan / blue | Local transport direction and surface orientation. |
| Ownership seams / graph edges | yellow / gold | Topological boundaries. |
| Risk / unresolved / ownership flips | magenta / hot pink | Local instability or disagreement. |
| Budget exhaustion | amber / orange / purple heat | Traversal limit, max-step, or no-hit budget pressure. |
| Stable / plateau | green | Candidate stable operating region. |
| Converging | yellow | Still changing; continue local ladder. |
| Underresolved | red | Needs smaller step or focused oracle/island microscope. |
| Budget saturated | purple | Needs increased traversal/step budget or adaptive budget scaling. |
| Labels | white with dark backing | Keep readable without hiding data. |

Do not use color alone. Important states should also be named in labels, metadata, legends, or summaries.

## Overlay Priority Rules

When multiple overlays compete for a panel, choose the highest-priority informative layer:

1. Cartesian wireframe for geometry-anchor panels.
2. Hit normals or continuity vectors for transport panels.
3. Ownership graph seams for topology panels.
4. Budget exhaustion heatmap for quality panels when budget exhaustion exists.
5. Unresolved island or oracle disagreement overlay when budget exhaustion is absent but unresolved support exists.
6. Graph lineage or transport quality phase plot for evolution panels.
7. Placeholder only when no relevant diagnostic input exists.

For quad panels, Panel D priority is:

1. budget exhaustion heatmap
2. ownership graph seams
3. transport/field overlay
4. field arrows placeholder

## Representative-Frame Selection Policy

Storyboard generators should prefer cells with:

- visible geometry
- visible hit samples
- coherent transport structure
- non-empty overlays
- informative ownership topology
- useful seams, unresolved support, or quality constraints

Avoid selecting:

- low-hit cells
- visually empty cells
- fully or mostly budget-exhausted cells
- unresolved cells with no observable geometry
- cells missing the Cartesian geometry anchor unless no alternative exists

Selection should be recorded in machine-readable metadata such as `storyboard_selection.json`, including score, selected step, reasons, overlay availability, and budget penalty.

## Typography Hierarchy

Keep text small, restrained, and functional.

| Text Type | Use |
|---|---|
| Storyboard title | Study and comparison scope. |
| Panel number | Canonical order cue. |
| Panel title | Layer identity. |
| Panel subtitle | One sentence explaining the diagnostic role. |
| Layer badge | Level name, e.g. `Level 1 Geometry`. |
| Legend | Only the symbols needed to read the current panel. |
| Footer guardrail | Passive/post-process warning when relevant. |

Avoid marketing copy inside diagnostic panels. A diagnostic image should teach the observer how to read the renderer, not sell the renderer.

## Label Conventions

Use consistent labels:

- `Level 0 Beauty` or `Level 0 Observer`
- `Level 1 Geometry`
- `Level 2 Transport`
- `Level 3 Topology`
- `Level 4 Quality`
- `Step Evolution`

Use renderer-grounded terms:

- `rendered reference`
- `Cartesian wireframe`
- `hit normals`
- `continuity vectors`
- `ownership graph seams`
- `budget exhaustion`
- `unresolved island`
- `graph lineage`
- `transport quality phase`

Avoid labels that imply unsupported physical truth:

- `true geodesic`
- `ground truth spacetime`
- `real curvature proof`
- `physics verified`

## Warning Conventions

Warnings should be explicit, short, and attached to the relevant panel or report.

Required warning language:

- `Post-process only. No render scheduling, hit selection, shading, resolver, or adaptive precision consumes these images.`
- `ReferenceTransportOracle is a best-known renderer-reference comparison, not physical ground truth.`
- `Do not describe visible band/support artifacts as caused by curvature unless comparison metrics support that claim.`
- `Use "associated with curved transport fixture under tested settings" unless stronger causal evidence is measured.`

If curved/control comparability differs, report:

- `comparability_status=warning`
- differing fields
- `control_comparison_type`
- whether the control is `matched_pose`, `scene_control`, or `unavailable`

If curvature evidence is missing in a curved fixture log, mark the run invalid rather than weak.

## Fallback Behaviors

Missing inputs should degrade gracefully.

| Missing Input | Behavior |
|---|---|
| Beauty PNG | Show a labeled missing panel and record missing input. |
| Cartesian primitives | Keep Level 1 panel with placeholder; do not silently skip geometry anchor. |
| Hit diagnostics | Disable transport ownership, continuity, budget, and normal overlays; record missing graph basis. |
| Continuity vectors | Show hit normals if available; otherwise placeholder. |
| Ownership graph | Show transport shape regions if available; otherwise placeholder. |
| Budget summary | Show unresolved/oracle overlay if available; otherwise placeholder. |
| Oracle outputs | Do not report epsilon-stability conclusions; mark oracle data unavailable. |
| Multi-step graph persistence | Do not show full-frame lineage claims; show sample-only closure if that is all that exists. |

Empty panels should be honest. A clear placeholder is better than decorative noise.

## Empty-Data Handling

Empty-data states are diagnostic facts.

Record:

- missing input filenames
- zero-hit counts
- low-hit selection penalties
- no graph basis
- oracle-sample-only limitations
- budget exhaustion with no hit
- projection-degenerate normals

Do not hide empty data with generated art, gradients, or decorative fills.

## Budget Saturation Semantics

Budget saturation means traversal limits are influencing diagnostic interpretation.

Use these terms:

- `max_steps_reached`
- `budget_exhausted_without_hit`
- `budget_exhausted_hit_count`
- `budget_exhausted_no_hit_count`
- `budget_exhaustion_percent`
- `step_quality_plateau_candidate`
- `budget_saturation_start_step`

Interpretation:

- Budget saturation may create a false plateau.
- Smaller step size is not automatically better when max traversal/step budgets dominate.
- Recommended next action is budget scaling or adaptive budget allocation, not necessarily smaller steps.

Canonical next-action language:

- `budget_saturated`: increase max traversal/step budget or use adaptive budget scaling
- `underresolved`: reduce step size or improve oracle/island focus
- `converging`: continue ladder around neighboring steps
- `plateau`: candidate operating window / diminishing returns region

## Topology Terminology

Use topology terms as renderer-validation terms.

| Term | Meaning |
|---|---|
| Transport ownership signature | Renderer-observed decision tuple, typically hit/miss, collider id, domain id, and event buckets. |
| Ownership region | Connected pixels with equivalent transport ownership signatures. |
| Ownership graph node | Connected component of equivalent renderer transport signatures. |
| Ownership graph edge | Observed adjacency/seam relation between ownership graph nodes. |
| Seam | Boundary pixels or adjacency relation where ownership signatures differ. |
| Unstable subgraph | Node/edge region annotated by unresolved, high-discontinuity, or precision-sensitive evidence. |
| Unresolved island | Local sampled region that has not yet reached epsilon-stable agreement against the tested reference. |
| Seam persistence | Boundary relation that remains present across step ladder levels. |
| Topology stability | Nodes and edges remain structurally consistent across refinement levels. |
| Scalar precision stability | Numeric deltas remain within epsilon: path length, hit distance, normal angle, decision risk. |
| Unresolved island closure | Previously unstable sampled pixels become stable by a tested production step. |

Hard rule:

Oracle ladder-only data may annotate unstable sampled pixels, but must not be reported as full-frame graph persistence.

## Oracle Terminology

Use `ReferenceTransportOracle`, not `GeodesicOracle`.

Preferred wording:

- `best-known renderer-reference transport path`
- `reference transport comparison`
- `oracle replay determinism`
- `production-vs-reference delta`
- `epsilon-stability classification`

Avoid:

- `true geodesic`
- `absolute truth`
- `physical GR validation`
- `ground truth spacetime`

ReferenceTransportOracle outputs are validation references inside xPRIMEray's current renderer model. They do not solve Einstein field equations and do not establish physical correctness.

## Guardrail Language

All diagnostic reports and major visual packets should preserve these guardrails:

- Diagnostics are passive.
- Diagnostic outputs do not alter beauty rendering.
- Diagnostic outputs do not change scheduler order, hit selection, shading, resolver scoring, or adaptive precision.
- Post-process overlays must never be consumed by render decisions.
- Do not smooth artifacts to hide failure.
- Preserve real geometric discontinuities.
- Keep claims tied to measured renderer outputs.
- Treat `null geodesic`, `Gordon metric`, `GRIN`, `coherence basin`, and `transport topology` as renderer-design/validation terms unless a report directly supports a stronger mathematical claim.

## Visual Tone

The visual tone is:

```text
Bell Labs observability + Grant Sanderson geometric readability + NASA telemetry restraint.
```

Practical meaning:

- Make the geometry readable first.
- Let the overlays explain one idea at a time.
- Use restrained color with strong semantic meaning.
- Prefer diagrams that teach the failure mode.
- Avoid clutter that makes the renderer look more understood than it is.
- Use cinematic readability only when it improves comprehension.

## Realtime UI Direction

These observability layers should eventually map into realtime engine overlays and in-game diagnostic modes.

Suggested realtime mapping:

| Diagnostic Layer | Future Realtime Mode |
|---|---|
| Level 0 rendered reference | normal observer view |
| Level 1 Cartesian projection | geometry reference overlay |
| Level 2 transport vectors / hit normals | transport vector mode |
| Level 3 ownership topology / seams | topology inspection mode |
| Level 4 budget / unresolved islands | quality budget warning mode |
| Level 5 lineage / phase evolution | time/step-ladder inspector |

Realtime overlays should remain opt-in and diagnostic. They should not become beauty effects.

## Non-Goals

Avoid:

- corporate dashboards
- crypto aesthetics
- chaotic hacker UI
- fake cinematic effects
- decorative sci-fi noise
- post-process smoothing that hides artifacts
- speculative physics claims
- treating oracle/reference output as physical ground truth
- implying curvature caused an artifact without comparison metrics

## Truthfulness Principle

Diagnostic visuals must prioritize explanatory clarity over artistic appearance.

If a panel is empty, show that it is empty.

If a metric is sample-only, say sample-only.

If the control is not matched, say it is not matched.

If curvature evidence is absent, mark the run invalid.

If budget saturation dominates, do not call the step ladder converged.

The renderer should teach the observer how transport topology evolves. The visual language exists to make that teaching honest, repeatable, and readable at a glance.
