# Architecture Design Council Traversal Review

Date: 2026-05-03

This note uses the requested council figures as perspective lenses, not as authorities. Every conclusion below is limited to measured xPRIMEray renderer behavior. Terms such as null geodesic, Gordon metric, and coherence basin are renderer-design metaphors here unless a section explicitly cites a measured diagnostic.

## Executive Summary

The current evidence supports a two-part failure model:

1. A traversal/scheduler resonance can amplify local instability into row-global horizontal bands.
2. Local transport decisions around edge/corner-like regions remain genuinely high risk and often require the local reference step `0.003125`.

The scheduler/stride DOE is the strongest evidence. In OFF mode, stride `1` averaged `28.7889%` band pixels across the step sweep, stride `2` averaged `13.6917%`, stride `4` averaged `0.3722%`, and stride `8` averaged `0.2027%`. This is too large to explain as simple geometric convergence alone.

The corner and risk-region probes show a separate local issue. The focused corner probe found `89 / 89` sampled points requiring reference/fine precision or ownership changes, with `39` collider/ownership flips in the sampled edge ROI. The earlier risk-region analyzer found `41 / 41` regions classified as `UNSEALED_NONCONVERGENT`, all requiring `0.003125`, all with persistent mismatch at `0.00625`.

The first-pass traversal smoke shows that traversal order is live: column traversal changed `448` pixels versus row at `step_length=0.015`. However, the smoke does not prove suppression. Row band percent was `0.059%`; column was `0.1181%`. The corner ROI metrics were unchanged between row and column in the smoke (`required_precision=0.003125`, `ownership_change_samples=360` for both).

The immediate corrective experiment should be a narrow tile/checkerboard traversal comparison with probe contamination controlled, not another broad sweep.

## Evidence Table

| Source | Measured Result | Architectural Reading |
|---|---|---|
| `output/doe_scheduler_resonance/20260503T002804Z` | OFF stride `1` mean band `% = 28.7889`; stride `2 = 13.6917`; stride `4 = 0.3722`; stride `8 = 0.2027` | Raster traversal cadence strongly affects row-global artifacts. |
| Scheduler DOE at `step=0.015` | stride `1 = 32.625%`; stride `2 = 1.8472%`; stride `4 = 0.3403%`; stride `8 = 0.1944%` | Banding is not monotonic step-size error; stride decorrelation can nearly suppress it. |
| Scheduler DOE at `step=0.0125` | stride `1 = 33.0347%`; stride `2 = 18.0799%`; stride `4 = 0.2222%`; stride `8 = 0.1944%` | The worst visual regime is also strongly scheduler-sensitive. |
| Traversal smoke `20260503T134229Z` | column changed `448` pixels vs row at `0.015`; row band `% = 0.059`; column band `% = 0.1181` | First-pass order affects output, but smoke does not show improvement yet. |
| Traversal smoke corner metrics | row and column both `required_precision=0.003125`; both `ownership_change_samples=360` | Local corner ROI instability persisted across row/column in the smoke. |
| Corner probe `20260503T132655Z` | edge ROI: `89` samples, `required_precision=0.003125`, `39` collider/ownership flips, mean max risk `4.038819` | At least one edge transition is not merely a display artifact; hit ownership changes locally. |
| Risk regions `20260502T200137Z` | `41` regions, all `UNSEALED_NONCONVERGENT`, all `required_precision=0.003125` | Local high-risk regions persist at the sampled precision levels. |
| Coherence basin smoke `20260503T001944Z` | `8` basins, `0` unstable seams, mean coherence `0.999999`, entropy `0`, `33` centers skipped by budget | Sampled basin centers can be coherent; this smoke did not capture the unstable seam structure seen by risk-region analysis. |
| Latest full traversal run `20260503T162851Z` | Run started for row/column/tile/checkerboard, but no completed summary was present at review time | Tile/checkerboard conclusions are pending. |

## Claims Vs Evidence

| Claim | Status | Evidence |
|---|---|---|
| Row/stride synchronization is a real amplifier. | Strongly supported | Scheduler DOE stride `1/2` versus `4/8` gap. |
| Traversal order can affect final beauty output. | Supported | Column smoke changed `448` pixels vs row. |
| Column traversal suppresses horizontal bands. | Not supported yet | Column increased band percent in smoke; full traversal run incomplete. |
| Tile/checkerboard reduce scheduler resonance. | Untested in completed data | No completed tile/checkerboard summary was available. |
| Corner/edge instability is only scheduler artifact. | Rejected | Corner ROI metrics stayed high under row and column; risk regions remain nonconvergent. |
| Hit/geodesic precision alone explains row-global bands. | Rejected | Stride `4/8` nearly suppress broad bands without changing physics interpretation. |
| Coherence basins explain current bands. | Not established | Basin smoke found stable basins and no seams; risk-region analyzer found nonconvergent regions elsewhere. |

## Direct Answers

1. Did traversal mode materially change banding or only move it?

In the completed smoke, traversal mode materially changed the image hash and `448` pixels, but it did not suppress banding. Column had higher band percent than row at `0.015`. Treat this as “traversal affects output” rather than “column fixes row bands.”

2. Did tile/checkerboard reduce scheduler resonance compared with row/column?

Not answered by completed outputs. The latest full traversal run was started, but no completed summary existed at review time. The scheduler DOE implies decorrelated/block-like traversal is promising because stride `4/8` suppress broad row bands, but tile/checkerboard must be measured directly.

3. Are corner/edge risk zones stable across traversal modes?

In the row-vs-column smoke, yes in the narrow sense that the corner ROI convergence metrics were unchanged. Both row and column required `0.003125` and had `360` ownership-change samples. That suggests local edge/corner instability is not eliminated by changing row to column.

4. Does banding correlate with risk nodes, seams, entropy, or row-mod-stride classes?

Banding correlates strongly with stride class. It does not yet correlate cleanly with coherence basin seams or entropy because the basin smoke found `0` unstable seams and entropy `0`, while risk-region analysis found nonconvergent regions. Current evidence says row-mod-stride is the strongest global-band predictor; risk nodes identify local instability but do not yet explain row-global propagation.

5. Dominant failure mode?

Overlap/interference. The current architecture appears to have local hit/transport ambiguity that can be amplified by scheduler/traversal synchronization into global bands.

6. Next corrective experiment?

Run a narrow tile/checkerboard traversal correction experiment with probes controlled, not a broad DOE. Compare row, column, tile, and checkerboard at `0.015` and `0.0125`, with fixed camera, fixed seed, stride `1`, diagnostics off for beauty timing, and a separate passive corner ROI pass afterward.

## Council Lenses

### 1. Einstein / Minkowski Lens

The renderer should not let coordinate/order artifacts masquerade as observer geometry. The stride DOE shows the observed image depends strongly on raster cadence. That is a coordinate-order artifact in renderer terms, not evidence of spacetime structure.

Design implication: separate observer-consistent transport results from traversal order. A stable render should not create row-global geometry because the film was visited row-wise.

### 2. Maxwell / Gordon Metric Lens

The field-medium interpretation is useful only if field effects separate from raster traversal effects. Current data shows they are entangled: local transport ambiguity exists, but raster stride determines whether it becomes a broad horizontal artifact.

Design implication: keep field transport math pure, then decorrelate scheduler traversal so medium-induced curvature is not confused with film-order resonance.

### 3. Hamilton / Variational Mechanics Lens

The local probes show path-decision instability near sampled extrema-like regions. Persistent mismatch at `0.00625` and required precision `0.003125` suggest local action-path neighborhoods are not numerically stable under the current step sizes.

Design implication: local precision diagnostics are valuable, but they should describe high-risk neighborhoods, not drive global smoothing.

### 4. Kajiya / Rendering Lineage Lens

A renderer should preserve integrator purity: traversal order may affect performance, but should not change the mathematical decision unless the algorithm is stateful or budget-coupled. The smoke showed traversal order changed pixels.

Design implication: isolate whether the change comes from pass budget timing, shared mutable state, first-hit acquisition order, or cached scheduler state. The next experiment should run diagnostics disabled first, then run probes separately.

### 5. Penrose Lens

Global causal/topological claims are not justified by these artifacts. Row-global bands can be generated by scheduler cadence. Local nonconvergent regions may indicate topology-like ambiguity in renderer state, but they are not evidence of physical topology.

Design implication: preserve real discontinuities, but do not promote row bands into geometry.

### 6. Numerical Methods / Kahan-Style Lens

The system has two numerical smells: step thresholds and aliasing. The local probes show precision thresholds; the scheduler DOE shows aliasing/resonance against traversal cadence. Epsilon discipline matters: the reference step is a local numerical reference, not truth.

Design implication: measure convergence per local region and decorrelate traversal cadence. Do not solve aliasing by blurring and do not solve local nonconvergence by pretending the finest tested step is exact.

### 7. Grant Sanderson / 3Blue1Brown Lens

The failure mode needs a picture that separates local trigger from global amplifier.

Recommended diagram: a 2x2 panel:

- Panel A: beauty image with horizontal band overlay.
- Panel B: row index vs band score heat strip, annotated by stride class.
- Panel C: corner/risk-node map showing local high-risk points.
- Panel D: arrows showing how a local unstable decision becomes row-global under row traversal but should stay local under tile traversal if the scheduler hypothesis is right.

The key visual question: “Does changing traversal change the propagation pattern while the same local risk points remain?”

### 8. Curt Jaimungal / Theories of Everything Lens

Separate hypothesis from evidence:

- Evidence: stride changes banding dramatically.
- Evidence: local corner/risk samples are nonconvergent.
- Evidence: row vs column changes pixels.
- Hypothesis: row traversal amplifies local instability into global bands.
- Open question: whether tile/checkerboard traversal localizes the artifact.

Design implication: keep the question tree explicit and avoid overclaiming.

### 9. Anirban-Inspired Complexity Lens

Coherence basins and unstable seams are useful design language, but current measured basin smoke found stable basins and no unstable seams. The risk-region outputs did find unsealed nonconvergent regions. These may be nested local structures, but that needs direct measurement across traversal modes.

Design implication: use basin memory as diagnostic scaffolding only until seam persistence is measured across row/column/tile/checkerboard.

### 10. Action Lab / Practical Demo Lens

The demo should be simple and repeatable:

1. Same camera.
2. Same step length.
3. Render row, column, tile, checkerboard.
4. Show contact sheet and difference maps.
5. Show corner ROI convergence table separately.

If tile/checkerboard localizes bands without blurring geometry, the demo is understandable in one minute.

### 11. Product Pressure Lens

The shortest path to a stable, demo-worthy wormhole render is not a global smoothing pass. It is a conservative traversal decorrelation layer plus local diagnostics.

Near-term product direction:

- Use tile/checkerboard traversal as an experimental scheduler mode.
- Keep fixed seed determinism.
- Preserve exact hit/shading logic.
- Use corner/risk diagnostics to choose future precision work, not to alter current beauty.

## Rejected Hypotheses

1. Pure geometry failure.

Rejected because stride `4/8` suppress broad bands by orders of magnitude without changing the scene geometry.

2. Pure row traversal artifact.

Rejected because corner/risk probes show persistent local hit ownership changes and required precision `0.003125`.

3. Telemetry/probes alone cause the artifact.

Rejected as a complete explanation because OFF scheduler DOE already shows strong stride dependence. Still, probes can contaminate timing and should be separated from beauty comparisons.

4. Global smoothing is the right fix.

Rejected because it would hide evidence, blur true discontinuities, and fail to distinguish local nonconvergence from scheduler amplification.

5. Coherence basin memory is ready to feed rendering.

Rejected. Current basin memory is diagnostic-only; the smoke found stable basins but did not validate seam prediction against traversal artifacts.

## Top 3 Next Experiments

1. Narrow tile/checkerboard traversal comparison.

Run row, column, tile, checkerboard at `0.015` and `0.0125`, stride `1`, fixed camera, fixed seed, diagnostics disabled for beauty capture. Then run corner ROI probes separately on the same camera. This directly answers whether tile traversal localizes the artifact.

2. Traversal-order propagation map.

For each traversal mode, compute band pixels by row, column, and tile block. Compare whether artifact support remains row-global, rotates under column traversal, or breaks into tile-local islands.

3. Local-risk-to-band overlay.

Overlay risk nodes/regions onto the traversal diff maps. Measure whether changed pixels cluster near local risk regions or propagate along full rows.

## Recommended Immediate Corrective Implementation

Promote `square_tile` and `checkerboard_tile` from pass1-only experiment into a complete render-test traversal mode that schedules both pass1 acquisition and pass2 commit by deterministic tile order, while preserving per-pixel hit/shading math.

Guardrails:

- Fixed seed.
- Every pixel rendered exactly once.
- No smoothing.
- No probe results feeding hit selection.
- No adaptive precision yet.
- Beauty comparison runs must disable diagnostic probes, then run probes in a second pass.

Why this is the next corrective step: stride DOE already showed that changing traversal cadence can nearly eliminate row-global banding. The current pass1 traversal hook proves traversal changes output, but pass2 remains row-oriented enough that the architecture question is not fully answered.

## Grant-Style Diagram Recommendation

Create `traversal_failure_mode_explainer.png` with four horizontal layers:

1. Local risk nodes: small red points around corner/edge regions.
2. Row traversal: red points smear horizontally into bands.
3. Tile traversal: red points remain in local tile neighborhoods.
4. Measurement strip: bar chart for band percent by traversal mode.

The caption should say: “If the scheduler is the amplifier, changing traversal changes propagation. If hit precision is dominant, the same local regions remain unstable across traversal modes.”

## Curt-Style Question Tree

```text
What creates the visible bands?
  Is there local decision instability?
    Yes: corner/risk probes require 0.003125 and show ownership flips.
  Does traversal amplify it globally?
    Yes: stride strongly changes band percent.
  Does row vs column suppress or rotate it?
    Partially measured: output changes, suppression not shown.
  Does tile/checkerboard localize it?
    Not yet answered by completed data.
  If tile localizes it:
    implement deterministic tile/domain scheduler.
  If tile does not localize it:
    focus on local first-hit precision and resolver stability.
  If local risk maps align with bands:
    build passive risk-aware scheduling diagnostics.
  If local risk maps do not align:
    inspect budget/caching/shared-state contamination.
```

## Demo-Ready Fixture Recommendation

Build a small fixed-camera traversal demo around the domain resolver stress scene:

- Resolution: `320x180`.
- Step lengths: `0.015` and `0.0125`.
- Modes: row, column, tile, checkerboard.
- Diagnostics off for beauty contact sheet.
- Separate corner ROI probe at `40,35;280,35;40,145;280,145`.
- Outputs: contact sheet, row-vs-tile diff, band percent table, corner required precision table.

Success criterion for a demo-worthy corrective path:

- Tile/checkerboard reduces horizontal band score versus row.
- Artifacts become local rather than row-global.
- Corner ROI instability remains visible in diagnostics instead of being hidden.
- Fixed seed reproduces the same hashes.

## Final Decision

Do not tune aesthetics and do not smooth. The next corrective implementation should be deterministic tile/checkerboard traversal across the render-test first-pass and pass2 commit path, followed by a narrow two-step comparison. If that localizes bands while corner ROI risk remains, the architecture pivot is validated: scheduler decorrelation first, local precision/coherence management second.
