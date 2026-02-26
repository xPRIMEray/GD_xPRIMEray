# C1.7 g.X - AutoCal weak-signal FieldHeavy delta-aware stopgap (Option A)
## Intent
- Prevent weak-signal `FieldHeavy` AutoCal recommendations from forcing a prune flip (`prune_enabled:false`) that is safe only on `baseline_prune_off` but causes large overhead when applied as a delta to common `prune_on_*` baselines.
- Make the smallest harness-only change with deterministic behavior and no RenderHealth gating changes.

## Codex Summary
Implemented Option A only: `RecommendationEngine` now emits no prune delta for weak-signal `FieldHeavy` instead of forcing `prune_off`.

What changed
- Weak-signal `FieldHeavy` branch no longer sets `preset.enable_tlas_prune`.
- Branch now returns a no-op prune delta with updated note:
  - `fieldheavy:weak_signal:no_prune_delta`

Why this is the smallest safe move
- It avoids cross-baseline prune flips entirely for the weak-signal case.
- It preserves probe/archetype detection and preset generation determinism.
- It avoids widening acceptance-policy scope or matrix aggregation changes in this patch.

## Diff Summary
- Files changed:
  - `RendererCore/Testing/RecommendationEngine.cs`
  - `Docs/CalibRoadmap/PatchLogs/C1_7_g_X.md`

- `RendererCore/Testing/RecommendationEngine.cs`
  - Weak-signal `FieldHeavy` preset branch now returns no prune delta and updates note text from `prune_off` to `no_prune_delta`. Anchors: `RendererCore/Testing/RecommendationEngine.cs:28`, `RendererCore/Testing/RecommendationEngine.cs:30`

## Behavior/Trust Impact
- Harness-only AutoCal preset recommendation behavior changes for one case:
  - `FieldHeavy` + weak signal no longer recommends a prune flip.
- No renderer hot-loop changes.
- No RenderHealth gating changes.

## Validation
- `dotnet build "Physical Light and Camera Units.sln" -c Debug` -> pass (warnings only)

## Recommendation (next step)
- Option B is the stronger long-term fix because it enforces delta-budget safety at acceptance time across the baseline matrix.
- Option C is a narrower harness workaround but risks masking cross-baseline fragility rather than preventing it.

## Update - Option B (implemented)
Implemented Option B with a minimal harness-only matrix aggregation path for prune-delta presets.

What changed
- Acceptance now aggregates shadow-eval pair outcomes across the matrix when the preset contains a prune delta (`enable_tlas_prune.HasValue`).
- Per-pair `accept` decisions are emitted as `defer` with `reason=matrix_pending` while aggregation is in progress.
- A final existing-format `AutoCalDecision` is emitted at matrix end using aggregate acceptance policy:
  - `accept_matrix`
  - `matrix_pair_reject`
  - `matrix_pair_defer`
  - `matrix_overhead_exceeds_max`

Notes
- No renderer hot-loop or RenderHealth gating changes.
- No new log format was added; this reuses existing `AutoCalDecision` lines and emits one final matrix-level decision when aggregation is active.
- Aggregation is enabled conservatively for any preset with a prune delta (not only weak-signal FieldHeavy).

## Diff Summary (Option B)
- Files changed:
  - `RendererCore/Testing/CalibrationAcceptancePolicy.cs`
  - `RendererCore/Testing/RenderTestRunner.cs`

- `RendererCore/Testing/CalibrationAcceptancePolicy.cs`
  - Added matrix-level shadow-eval input + finalization helpers (`CalibrationShadowEvalMatrixInput`, `DecideFromShadowEvalMatrix(...)`). Anchors: `RendererCore/Testing/CalibrationAcceptancePolicy.cs:47`, `RendererCore/Testing/CalibrationAcceptancePolicy.cs:149`
  - Added matrix outcome reasons (`matrix_pair_reject`, `matrix_pair_defer`, `accept_matrix`). Anchors: `RendererCore/Testing/CalibrationAcceptancePolicy.cs:179`, `RendererCore/Testing/CalibrationAcceptancePolicy.cs:186`, `RendererCore/Testing/CalibrationAcceptancePolicy.cs:204`

- `RendererCore/Testing/RenderTestRunner.cs`
  - Enable matrix aggregation only for prune-delta presets (`preset.enable_tlas_prune.HasValue`). Anchor: `RendererCore/Testing/RenderTestRunner.cs:690`
  - Accumulate per-pair shadow decisions and coerce interim pair accepts to `matrix_pending` (defer) while preserving existing per-pair logs. Anchors: `RendererCore/Testing/RenderTestRunner.cs:826`, `RendererCore/Testing/RenderTestRunner.cs:880`, `RendererCore/Testing/RenderTestRunner.cs:959`, `RendererCore/Testing/RenderTestRunner.cs:995`
  - Emit final matrix-level decision at matrix shutdown before state reset. Anchor: `RendererCore/Testing/RenderTestRunner.cs:1461`

## Validation (Option B)
- `dotnet build "Physical Light and Camera Units.sln" -c Debug` -> pass (warnings only)
