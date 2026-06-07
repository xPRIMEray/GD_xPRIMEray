# xPRIMEray Observatory — Canonical Fixture Taxonomy

> This document is the authoritative reference for what each Observatory fixture is, what it proves, and what it does not prove. It is intended for engineers, researchers, and reviewers who need to interpret Observatory diagnostic outputs without misreading partial coverage as complete validation.

**Guardrail (applies to all fixtures):** Observatory diagnostics validate transport completion within a known scene contract. They do not establish physical correctness of the rendering model.

Read [What the Observatory Measures](./what_the_observatory_measures.md) before comparing fixture cards. It explains why `9/9` or `7/9` panel coverage is evidence coverage, not a physical-correctness score.

---

## Fixture Overview

| Fixture | Coverage | Hit Rate | Acceptance Gate | Showcase Status |
|---|---:|---:|---|---|
| hermetic_curved_room | 9/9 | 100% (hermetic) | sealed closure | ✅ Strong |
| curved_minimal | 7/9 | ~1% (open target) | traversal diagnostics | ✓ Partial |
| object_island | 7/9 | ~49% (reference-integration diagnostic) | oracle diagnostics | ✓ Partial |
| corner_probe_reference [PLACEHOLDER] | 6/9 | none | probe comparison only | ⚠ Placeholder |
| oracle_closure [EXPERIMENTAL - internal only] | 3/9 | none | internal only | 🔬 Experimental |

**Minimum for GitHub Pages showcase: 7/9 panels + hit_diagnostics.csv + exit code 0.**

---

## hermetic_curved_room

### Purpose
The primary closure benchmark fixture. A sealed six-wall chamber — front, back, left, right, floor, ceiling — each wall a distinct receiver. A single camera at the center traces rays in all directions. An optional parametric curvature field deflects rays toward receiver surfaces.

### What it proves
- **Hermetic transport closure:** Every evaluated ray must hit exactly one receiver. Miss count = 0 is the acceptance gate.
- **Curvature FPS benchmark:** Performance and closure hold across a curvature ramp from 0% to 100% (field amplitude 0.0 → 1.15).
- **Curvature application:** Resolved amplitude confirmed via post-load snapshot from `HermeticCurvedRoomController`.
- **Traversal budget stress:** At 40×22 (smoke), 72.7% of rays exhaust the 700-step budget and resolve on the overrun step (step 701). The loop permits one extra overrun step by design; no pixel failed to find a hit. At 160×112 (mini), traversal is within budget (avg 271 steps, max 273).

### What it does NOT prove
- Physical accuracy of the transport model.
- Visual beauty-layer correctness — at mini scale, beauty capture is currently blank. Diagnostic overlays are valid and independent of beauty.
- FPS performance at production resolution (tiny-HD or SNES). Smoke/mini runs are telemetry and visual sanity only.

### Expected diagnostics (current coverage: 9/9)
All nine Observatory Story panels are present:
1. Raw visual — beauty placeholder (blank at current scale)
2. Scene geometry — sealed room bounds with field activation glyph
3. Curvature field — field volume and resolved amplitude
4. Transport ownership — all pixels assigned to one of six walls
5. Hit/miss map — all green (100% hit)
6. Traversal steps — heatmap; stress visible at smoke scale
7. Budget stress — 72.7% at smoke; well within budget at mini
8. Combined diagnostic overlay
9. Curvature Signature — traversal-step delta vs 0% baseline

### Acceptance gate
- `sealed_hit_validation_passed = true` (miss_count = 0)
- `curvature_application_passed = true` (resolved amps match requested)
- `godot_exit_code = 0`

### Risk of misinterpretation
- "Sealed hit validation passed" sounds absolute but depends on the 701st-step overrun at smoke scale. The closure is classified to completion within the scene contract; 72.7% of rays resolve on the extra step. The loop permits one extra overrun step by design; no pixel failed to find a hit. This is traversal budget stress, not a closure failure. Budget stress is reported separately.
- A blank beauty frame is not a renderer failure at smoke scale. Smoke is telemetry only. Run at mini (160×112) or larger for visual evidence.

### Public-facing explanation
*A sealed room with a camera at the center. Every ray the camera fires must eventually hit one of the six walls. This test proves the ray transport system can complete every ray journey — even when the geometry of space itself is curved by a field. It measures how efficiently rays travel, not what the room looks like.*

### Artifact coverage status
**Strong.** All 9 panels present. Beauty blank at current scale (open issue). Closure and curvature diagnostics are complete and trustworthy.

---

## curved_minimal

### Purpose
The canonical compact curved-field reference fixture. A small sphere at origin (actual radius ~2.16 units) with an optional detector backdrop at z = −18. The field is a parametric radial inversion field. Designed to verify that curved-field traversal and hit classification work correctly on a simple, geometrically unambiguous scene.

**Caveat:** Low hit rate is expected for this open-target fixture. It is not a hermetic closure test.

### What it proves
- **Field-sensitive traversal:** Traversal step counts and path shapes respond to curvature. Traversal step heatmaps show field deflection.
- **Transport diagnostics on a compact scene:** Normal overlays, transport continuity, and ownership diagnostics are all generated and legible.
- **CurvedMinimalFingerprint:** The canonical reference amplitude (1.15) is defined by this fixture.

### What it does NOT prove
- Hermetic closure — this fixture is open (most rays miss the sphere). The backdrop receiver captures background rays; the sphere is a small deliberate target.
- Curvature application (missing panel: curvature_field_view).
- Curvature Signature (missing panel).

### Expected diagnostics (current coverage: 7/9)
- Present: raw visual, scene geometry, transport ownership, hit/miss map, traversal steps, combined diagnostic overlay, (budget stress partially covered)
- **Missing: curvature_field_view, curvature_signature** — these panels were not generated from this study's output folder. This is a pipeline gap, not a scene limitation.

### Acceptance gate
- Traversal diagnostics are present and consistent with known geometry
- Hit rate is non-zero (sphere must be hit by some rays)
- Traversal step counts are within expected range for field amplitude

### Risk of misinterpretation
- **Hit rate ~1% looks like a test failure.** It is not. Low hit rate is expected for this open-target fixture. It is not a hermetic closure test; the sphere is a small deliberate target and the remaining rays are classified as backdrop/background or miss within that study's contract.
- The word "minimal" does not mean "simple benchmark" — it means "canonical minimal reference for the field fingerprint."

### Public-facing explanation
*A single sphere suspended in a curved field. Most rays will miss the sphere and hit the backdrop or background — that is expected. This test confirms that ray transport correctly classifies which rays hit the sphere, which hit the backdrop, and which pass through entirely. It is a reference fixture for the shape of the curvature field itself.*

### Artifact coverage status
**Partial.** Two diagnostic panels are missing due to a pipeline gap in the field-view and signature generation for this study. The existing 7 panels provide diagnostic agreement for this fixture, not a physical-correctness claim. Recommend regenerating with updated `observatory_fixture_report.py` when field view artifacts are available.

---

## object_island

### Purpose
A reference-integration diagnostic run targeting an "unresolved island" — a patch region in a 320×180 scene where transport is ambiguous or not converged. This is a ReferenceTransportOracle study, not a primary fixture. It tests reference integration's ability to surface convergence diagnostics without feeding those decisions back into the renderer.

### What it proves
- **Reference diagnostic availability:** Reference-integration hit classification, transport ownership, and convergence data are accessible as post-process artifacts.
- **Traversal budget stress:** max_traversal_steps = 700 (budget fully exhausted for some rays). This fixture is the only one currently confirming hard budget exhaustion without the overrun step.
- **Oracle guardrail:** ReferenceTransportOracle outputs are diagnostic only; they do not affect scheduling, hit selection, or renderer decisions.

### What it does NOT prove
- Scene closure — 49% hit rate means roughly half the evaluated region has unresolved transport. This is the point of the test, not a failure.
- Curvature field behavior (no curvature panel).
- Budget stress visualization (budget_stress panel missing).

### Expected diagnostics (current coverage: 7/9)
- Present: raw visual, scene geometry, transport ownership, hit/miss map, traversal steps, combined diagnostic, curvature signature
- **Missing: curvature_field_view, budget_stress** — not generated from this study's output.

### Acceptance gate
- Reference-integration diagnostic artifacts are present and internally consistent
- Hit rate and traversal data are non-trivial (neither 0% nor 100%)
- Reference-integration data does not appear in renderer execution path

### Risk of misinterpretation
- **"Unresolved island" sounds like the test broke.** It did not. An unresolved island is a deliberate diagnostic category — a region where reference integration flags convergence uncertainty. The 49% hit rate reflects measurable disagreement within this scene and field configuration.
- This fixture does not prove closure. Listing it next to hermetic_curved_room without clarification implies comparable validation depth.

### Public-facing explanation
*Some regions of a scene are genuinely ambiguous — rays can reach multiple possible destinations, or the computation hasn't fully converged. This fixture uses reference integration to map those ambiguous regions and confirm the diagnostic system can locate and report them. The unresolved pixels are the result, not evidence of a bug.*

### Artifact coverage status
**Partial.** Two panels missing (field view, budget stress). The seven present panels are valid. Budget stress is especially relevant for this fixture (max steps = budget) but the heatmap panel is absent. Recommend regenerating budget stress panel.

---

## corner_probe_reference [PLACEHOLDER]

**PLACEHOLDER WARNING:** This is not a cathedral fixture and should not be read as a cathedral transport result. It is first-pass corner/reference probe data standing in until a dedicated cathedral probe exists.

> **Note on naming:** This fixture is listed as `cathedral_probe` in the Observatory Fixture Index. No dedicated cathedral_probe fixture or output folder exists. The index maps this name to first-pass traversal comparison corner probe data. Until a real cathedral_probe fixture is implemented, this entry should be called `corner_probe_reference` in all public-facing documentation.

### Purpose
First-pass traversal comparison corner/reference probe data. A diagnostic comparison of traversal quality between different step sizes at a corner region, used to calibrate traversal step choice. This is probe instrumentation data, not a scene fixture.

### What it proves
- First-pass traversal comparison diagnostics are available.
- Step size calibration data exists for the corner probe scenario.

### What it does NOT prove
- Cathedral acoustics or cathedral geometry — no such fixture exists.
- Closure — no hit_diagnostics.csv was generated; hit rate is unknown.
- Any cathedral-specific transport behavior.

### Expected diagnostics (current coverage: 6/9)
- Present: raw visual, curvature field view, hit/miss map (if available), traversal steps, budget stress, curvature signature
- **Missing: scene geometry, transport ownership, combined diagnostic overlay**
- **Exit code: 134** (godot_shutdown_abort_after_harness_success — not a fully clean exit)

### Acceptance gate
None defined for public showcase. This entry does not meet the minimum criteria:
- No hit_diagnostics.csv
- Non-zero exit code (134)
- No dedicated fixture implementation

### Risk of misinterpretation
- **HIGH.** The name "cathedral_probe" implies a sophisticated acoustic or physical probe of a cathedral geometry. Nothing about the underlying data supports this. A reader will assume a cathedral-shaped scene was tested.
- If retained in the public index as `cathedral_probe`, it actively misleads about what xPRIMEray has been run against.

### Public-facing explanation
*Not yet available. This placeholder points to corner traversal comparison probe data while the cathedral probe fixture is under development. When the actual cathedral fixture is implemented, this entry will be replaced with geometry, acoustics, and transport probes specific to large reverberant spaces.*

### Artifact coverage status
**Placeholder.** Must not appear in GitHub Pages showcase as `cathedral_probe`. Rename to `corner_probe_reference` with explicit `[PLACEHOLDER]` label, or exclude from public index entirely until the real fixture exists.

---

## oracle_closure [EXPERIMENTAL - internal only]

**INTERNAL / EXPERIMENTAL:** Oracle means reference integration, not ground truth. This fixture is a passive comparison study and is not a public closure gate.

### Purpose
ReferenceTransportOracle ROI sweep: a comparison study that runs reference integration at 94 sample points across a 320×180 scene at multiple step sizes, comparing reference and production transport behavior. This is a passive diagnostic tool, not a fixture that proves closure or visual correctness.

### What it proves
- The reference-integration pipeline runs without crashing.
- Reference-integration data does not feed renderer decisions (guardrail confirmed).
- Passive comparison between reference and production transport is available as a post-process artifact.

### What it does NOT prove
- Scene closure — no hit_diagnostics.csv, no miss_count, no closure contract.
- Visual-render confirmation — beauty frame status unknown.
- Transport physical accuracy — the reference-integration comparison is a diagnostic, not ground truth.
- Anything about the five Observatory concepts (visual render, sealed closure, curvature application, traversal stress, FPS).

### Expected diagnostics (current coverage: 3/9)
- Present: raw visual, combined diagnostic overlay, curvature signature
- **Missing: scene geometry, curvature field view, transport ownership, hit/miss map, traversal steps, budget stress** — 6 of 9 panels absent.

### Acceptance gate
None defined for public showcase. This entry does not meet the minimum criteria:
- No hit_diagnostics.csv
- 3/9 panel coverage (threshold is 7/9)
- No closure contract or acceptance gate defined

### Risk of misinterpretation
- **VERY HIGH for a non-expert.** Appearing alongside hermetic_curved_room in the same index implies comparable diagnostic depth. The gap is 9/9 vs 3/9, with zero closure metrics in oracle_closure.
- "oracle_closure" sounds like it proves closure using an oracle. It only shows the reference-integration pipeline ran for this study. These are different claims.

### Public-facing explanation
Not yet ready for public explanation. This fixture is internal tooling — a research instrument for comparing reference integration and production transport behavior. It is not a validation gate for scene closure or rendering quality.

### Artifact coverage status
**Experimental / internal only.** Must not appear in GitHub Pages showcase. Internal use only until coverage reaches 7/9 and a clear acceptance gate is defined.

---

## Recommended Changes to observatory_fixture_index.md

### Immediate (before any public-facing use)

1. **Rename `cathedral_probe` → `corner_probe_reference [PLACEHOLDER]`** in the fixture column. Add a note: "cathedral_probe fixture is not yet implemented; this row uses first-pass traversal comparison probe data as a stand-in."

2. **Add `[EXPERIMENTAL — internal only]` label to `oracle_closure`** in the fixture column.

3. **Add a minimum coverage note** below the table:
   ```
   Fixtures with ≥ 7/9 panels, a hit_diagnostics.csv, and exit code 0 are candidates for the GitHub Pages showcase.
   corner_probe_reference and oracle_closure do not currently meet this threshold.
   ```

4. **Add a "missing panels reason" note per fixture** (one sentence each):
   - curved_minimal: curvature_field_view and curvature_signature not generated from curved_field_validation_ladder outputs.
   - object_island: curvature_field_view and budget_stress not generated from reference_transport_oracle_unresolved_island outputs.
   - corner_probe_reference: scene geometry, transport ownership, and combined diagnostic not available from first_pass_traversal_comparison probe data.
   - oracle_closure: 6 of 9 panels not generated — this study predates all panel types except raw visual, combined diagnostic, and signature diff.

### Deferred (next fixture development cycle)

5. Add a `category` column distinguishing: `sealed fixture` / `open fixture` / `oracle diagnostic` / `probe reference` / `placeholder`. This makes it immediately clear why fixtures have different hit rates and acceptance gates.

6. Add a `visual_confirmation` column: `yes` / `no (telemetry)` / `pending (beauty blank)`.
