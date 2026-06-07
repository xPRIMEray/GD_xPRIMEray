# Observatory Fixture Index

Square 3×3 Observatory Story sheets generated from existing xPRIMEray outputs. Missing panels are explicit red-bordered placeholders labeled MISSING / N.A. Renderer logic was not changed.

**Guardrail:** Observatory diagnostics validate transport completion within a known scene contract. They do not establish physical correctness.

**Minimum for GitHub Pages showcase:** 7/9 panels + `hit_diagnostics.csv` present + exit code 0. `corner_probe_reference` and `oracle_closure` do not currently meet this threshold and are excluded from public showcase.

See [docs/observatory/canonical_fixtures.md](../docs/observatory/canonical_fixtures.md) for the full fixture taxonomy: purpose, what each proves, what it does not prove, and risk of misinterpretation.

| fixture | category | what it proves | panels | hit rate | contact sheet | report |
|---|---|---|---:|---:|---|---|
| `hermetic_curved_room` | sealed fixture | Every evaluated ray hits a receiver under a curvature ramp (0%→100%). Hermetic transport closure confirmed. | 9/9 | 100% | [sheet](observatory_fixtures/hermetic_curved_room/diagnostic_contact_sheet.png) | [report](observatory_fixtures/hermetic_curved_room/fixture_observatory_report.md) |
| `curved_minimal` | open fixture | Field-sensitive traversal and transport diagnostics on a compact curved scene. ~1% hit rate is expected — sphere is a small deliberate target; remaining rays are background. | 7/9 ¹ | ~1% | [sheet](observatory_fixtures/curved_minimal/diagnostic_contact_sheet.png) | [report](observatory_fixtures/curved_minimal/fixture_observatory_report.md) |
| `object_island` | reference-integration diagnostic | Reference-integration convergence diagnostics in ambiguous transport regions. ~49% hit rate reflects measurable disagreement within this scene and field configuration, not a failure. | 7/9 ² | ~49% | [sheet](observatory_fixtures/object_island/diagnostic_contact_sheet.png) | [report](observatory_fixtures/object_island/fixture_observatory_report.md) |
| `corner_probe_reference` **[PLACEHOLDER]** | probe reference | First-pass traversal comparison probe data. No dedicated cathedral_probe fixture exists; this row uses corner probe data as a stand-in. Not a closure or scene test. | 6/9 ³ | none | [sheet](observatory_fixtures/cathedral_probe/diagnostic_contact_sheet.png) | [report](observatory_fixtures/cathedral_probe/fixture_observatory_report.md) |
| `oracle_closure` **[EXPERIMENTAL — internal only]** | reference-integration diagnostic | Passive ROI sweep diagnostics. Confirms the reference-integration pipeline runs without feeding decisions to renderer. Not a closure or visual-render confirmation test. | 3/9 ⁴ | none | [sheet](observatory_fixtures/oracle_closure/diagnostic_contact_sheet.png) | [report](observatory_fixtures/oracle_closure/fixture_observatory_report.md) |

**Missing panel notes:**

¹ `curved_minimal` — missing: `curvature_field_view`, `curvature_signature`. These panel types were not generated from `curved_field_validation_ladder` outputs. Pipeline gap, not a scene limitation.

² `object_island` — missing: `curvature_field_view`, `budget_stress`. Not generated from `reference_transport_oracle_unresolved_island` outputs. Budget stress is especially relevant here (max steps = budget 700) but the heatmap panel is absent.

³ `corner_probe_reference` — missing: `scene_geometry`, `transport_ownership`, `combined_diagnostic`. Not available from `first_pass_traversal_comparison` probe data. Exit code 134 (harness abort after success — not a fully clean exit).

⁴ `oracle_closure` — missing: `scene_geometry`, `curvature_field_view`, `transport_ownership`, `hit_miss_map`, `traversal_step_heatmap`, `budget_stress`. This study predates all panel types except raw visual, combined diagnostic, and curvature signature diff.
