# TriClock DOE Sandbox

Status: exploratory proposal and additive scaffold

## Intent

The TriClock DOE Sandbox is a speculative research and visualization framework for studying whether three configurable proxy clocks can be computed, swept, visualized, and optionally compared against existing xPRIMEray field and transport diagnostics.

This is not a claim of validated Orch OR physics, quantum gravity validation, or authoritative nuclear simulation. The proposed formulas are proxy inputs unless and until they are independently justified.

## Scientific Humility / Caveats

- Atomic-scale `E_G` effects may be negligible for many parameter choices.
- `tau_proxy = hbar / E_G_proxy` is included here as a structured exploratory proxy, not as a validated collapse result for this engine.
- Nuclear isotope, spin, and isomer terms are treated as configurable scalar metadata, not a full nuclear model.
- Relativistic orbital behavior is treated as a heuristic proxy driven by `Z`, orbital family, effective radius, and contraction/compression terms, not full QED.
- Any three-clock score such as `ratio_12`, `ratio_23`, `ratio_13`, `resonance_score`, `beat_score`, or `crossover_score` is an analytical heuristic for DOE ranking.

## Architecture Sketch

Recommended layering:

1. `RendererCore/Config/ResearchModeConfig.cs`
   Keep the top-level opt-in switch here so the sandbox remains inside the existing research configuration lane.

2. `RendererCore/Research/TriClock/`
   Place tri-clock proxy types, scalar evaluators, sample structs, and later sweep/report helpers here.

3. `GrinFilmCamera.cs`
   Reuse existing research-mode resolution, telemetry heatmap plumbing, and deterministic capture flow for V1 and V2.

4. `RenderTestRunner.cs`
   Reuse the current capture/export pattern for deterministic CLI runs, output directories, and JSON-sidecar artifacts.

5. `tools/`
   Add DOE sweep wrappers and report emitters here so parameter matrices stay reproducible and easy to rerun.

## Candidate Proxy Formulas

Clock 1: nuclear resonance proxy

- Example inputs: `Z`, `A`, spin, optional isomer metadata
- Example scalar form:
  - `clock1_proxy_hz ~ nuclear_clock_scale * f(Z, A, spin, isomer)`

Clock 2: relativistic orbital dynamics proxy

- Example inputs: `Z`, orbital family, effective radius, contraction scale, compression bias
- Example scalar form:
  - `clock2_proxy_hz ~ orbital_clock_scale * family_scale * contraction_proxy * Z^2 / radius_proxy`

Clock 3: collapse proxy

- Preserve the conceptual relation:
  - `tau_proxy = hbar / E_G_proxy`
  - `E_G_proxy ~ G * m_proxy^2 / max(delta_x_proxy, epsilon)`
  - `clock3_proxy_hz = 1 / tau_proxy`

## Phased Roadmap

### V1: scalar analysis only

- Add portable tri-clock config and proxy evaluator types.
- Compute scalar samples from explicit proxy inputs.
- Support deterministic grid sampling and DOE sweeps.
- Emit CSV and markdown summaries.
- Optionally export heatmaps using the existing film-space telemetry artifact pattern.
- Avoid transport mutation and avoid `RayBeamRenderer` changes where possible.

### V2: ray-coupled accumulation and visualization

- Attach tri-clock metrics to per-pixel or per-sample accumulation in `GrinFilmCamera`.
- Export tri-clock heatmaps alongside existing telemetry heatmaps.
- Reuse `DebugOverlayBus` or film overlay text for summary labels.
- Keep coupling read-only with respect to transport decisions.

### V3: optional experimental transport coupling

- Gate everything behind explicit research opt-in.
- Allow tri-clock scores to modulate experimental transport heuristics only in dedicated sandbox profiles.
- Keep baseline GRIN and metric modes unchanged by default.

## Minimal Artifact Chain

First artifact chain to produce:

1. Deterministic config snapshot
2. CSV rows of sampled tri-clock values
3. Markdown run summary
4. Optional PNG heatmap of a selected heuristic score
5. JSON metadata sidecar with formulas, bounds, and parameter provenance

Suggested output root:

- `output/triclock_doe/<timestamp>/`

Suggested files:

- `params.json`
- `triclock_samples.csv`
- `triclock_summary.json`
- `triclock_summary.md`
- `triclock_resonance_heatmap.png`

## Example Harness Usage

Illustrative future CLI pattern:

```bash
./scripts/godot_local.sh --path . --scene res://test-curved-minimal.tscn -- \
  --render-test \
  --render-test-fixture=curved_minimal \
  --render-test-capture=1 \
  --render-test-capture-dir=output/triclock_doe/manual_probe \
  --render-test-capture-mode=triclock-v1
```

Illustrative future Python DOE wrapper:

```bash
python tools/triclock_sweep.py \
  --scene test-curved-minimal.tscn \
  --z 79 82 92 \
  --delta-x 1e-12 1e-10 1e-9 \
  --orbital-family 6s 5f \
  --output-dir output/triclock_doe
```

## Open Questions

- Should the V1 scalar field be sampled directly in world space, film space, or both?
- Should tri-clock origin/anchor data come from `FieldSource3D`, dedicated sandbox nodes, or pure harness parameters?
- Which score should drive the first heatmap: resonance, beat, or crossover?
- Is there enough existing analysis-capture support to piggyback on current fixture post-processing without adding a new report parser?
- Should V2 couple to per-hit payloads, pass-1 segment telemetry, or pass-2 resolve counts first?

## TODOs

- Add a dedicated tri-clock config block under research mode.
- Add deterministic sweep tooling under `tools/`.
- Add a minimal fixture scene or fixture profile for tri-clock sampling.
- Add CSV/markdown exporters that mirror existing fixture output conventions.
- Add a clear disclaimer string anywhere the sandbox is surfaced in UI or logs.
