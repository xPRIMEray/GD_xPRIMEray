# Closure Diagnostics

Hermetic closure is the scene-contract invariant: every evaluated pixel must reach a terminal classification before integration declares completion. Closure passing does not validate the renderer's physics; it confirms that no pixel was silently abandoned within the declared scene.

## The Hermetic Closure Hero

**The canonical image**: Two renders of the identical curved-room scene.

- Left: low integration budget (e.g. 32 steps) — visually plausible.
- Right: high budget (e.g. 700 steps) — visually nearly identical.
- Instrument: closure percentage + budget-exhaustion heatmap.

At low budget the closure can be **0.0%** — every pixel is unresolved noise that happens to look like a render. At sufficient budget it reaches **100.0%**.

100.0% closure means every pixel was classified within the scene contract, not that the classification is physically accurate.

The red/green accent strips and the closure status map are the only reliable indicators. The eye cannot tell.

**Key Artifacts**

- `output/hermetic_hit_closure/` (multiple timestamped runs, including 3-panel and storyboard variants).
- `visuals/hermetic-closure-hero.png`, `hermetic-closure-hero-3panel.png`, `hermetic-closure-hero-web.png`.
- Hermetic fixture rule documentation.

**Falsifiable Claims**

- At the documented production step length and scene, closure at budget X must be ≥ Y%.
- Below the cliff, closure must collapse to near zero.
- Failures must spatially correlate with high-curvature zones (field boundaries, corners).

## Related Closure & Hermetic Diagnostics

- Hermetic Hit Closure Recovery and Storyboard sequences.
- Budget exhaustion heatmaps tied to specific fixtures (curved room, domain stress).
- Graceful shutdown probes (what happens to closure when the harness is interrupted).

**Exhibit Design Notes**

- Always present the plausible image first, then the instrumented closure measurement.
- Use side-by-side with the closure metric overlay.
- Include the exact scene + step + budget parameters so visitors can reproduce the falsification.

**Connection to Other Sections**

- The ladders in [Curvature Benchmark](./curvature_benchmark.md) explain *why* certain regions require high budgets.
- Canonical fixtures (especially Hermetic Curved Room and Boundary Shells) are the repeatable test beds for closure.
- Failures in closure often appear as banding or unresolved islands in the research fixtures.

This section is the heart of the scene-contract philosophy: without the closure contract, every beautiful image is an unverified story — there is no evidence that pixels reached terminal classification rather than stopping mid-journey.
