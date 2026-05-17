# Phase 2 Hermetic Closure Milestone

**Date:** 2026-05-17  
**Status:** PASSED — straight and GRIN hermetic observatory fixtures both validated  
**Runtime:** WSL2 / Ubuntu 24.04 / AMD Radeon D3D12 (hardware accelerated)

---

## 1. Summary

Phase 2 of the xPRIMEray v0.0-pre instrument validation now includes a hermetic
calibration chamber where both straight and GRIN optical transport close with zero
missed rays.

Both the straight transport case (field disabled) and the GRIN transport case (field
enabled, conservative parameters) achieved `missHits == 0` and
`traversalRowsCompleted == filmHeight` across a 640×360 full-pixel render under
GPU-backed WSL D3D12 runtime.

**Framing:** This documents coherent optical transport instrumentation. No exotic
physics claims are made. The GRIN field curves rays by a bounded, modest amount.
Closure is an instrumentation property — every ray must terminate on a classified
wall or fail loudly. The hermetic chamber is a calibration tool, not a physics
demonstration.

---

## 2. Why This Matters

The Phase 2 observatory fixture set distinguishes two separate validation concerns:

| Fixture type | What it validates |
|---|---|
| Presentation fixtures (`test-grin-basic-visual-offaxis-observe.tscn`, etc.) | Visual observability — output looks correct, camera framing is right, no obvious render artifacts |
| Hermetic fixtures (`test-*-hermetic-observatory-*.tscn`) | Transport closure — every ray terminates on a classified surface, zero escapes |

Presentation fixtures can pass visually even if a small fraction of rays escape
silently. The hermetic fixture is designed to fail loudly: a sealed 12-unit box with
no openings, no background source, no `fixture_source` nodes. Every ray that misses
a wall increments `missHits`. The gate is `missHits == 0`.

A secondary distinction was also confirmed during this milestone: `filmRowsRendered`
(which tracks presentation-band refresh progress) is not the same as
`traversalRowsCompleted` (which tracks how many rows the transport instrument has
fully traversed). These two metrics decouple once the renderer begins its second pass
while the first pass image is still resident in the film buffer. The validator now
gates on `traversalRowsCompleted` for transport completeness, while retaining
`filmRowsRendered` as a diagnostic reference for presentation state.

---

## 3. Exact Commands Run

Build (no C# changes required; confirms clean compilation):

```bash
dotnet build "Physical Light and Camera Units.csproj"
```

Quick smoke gate (320×180 effective, min_rows=180):

```bash
bash scripts/run_hermetic_observatory_full_pixel.sh --quick --godot-exe ./scripts/godot_local.sh
```

Full release gate (640×360, min_rows=360):

```bash
bash scripts/run_hermetic_observatory_full_pixel.sh --godot-exe ./scripts/godot_local.sh
```

GPU state audit (optional verification):

```bash
bash scripts/check_gpu_runtime.sh
```

---

## 4. GPU Runtime Confirmation

The WSL2 Mesa installation defaults to the `llvmpipe` software rasterizer without
an explicit driver selection. All observatory scripts now activate D3D12 hardware
acceleration before spawning Godot via `scripts/use_gpu_runtime.sh`.

| Variable | Value |
|---|---|
| `GALLIUM_DRIVER` | `d3d12` |
| `MESA_D3D12_DEFAULT_ADAPTER_NAME` | `AMD` |
| `LIBGL_ALWAYS_SOFTWARE` | unset |

Confirmed renderer output from `glxinfo -B`:

```
Device:      D3D12 (AMD Radeon(TM) Graphics)
Accelerated: yes
```

This replaces the prior default `llvmpipe` software rendering path for all observatory
runtime invocations. The bootstrap is centralized in `scripts/use_gpu_runtime.sh` and
sourced by `scripts/run_hermetic_observatory_full_pixel.sh` and all other observatory
entry-point scripts.

Full GPU runtime context: [GRIN_OBSERVE_GPU_RUNTIME_REPORT.md](GRIN_OBSERVE_GPU_RUNTIME_REPORT.md)

---

## 5. Fixture Inventory

| File | Role |
|---|---|
| `Fixtures/fixture_hermetic_observatory_straight.tscn` | 12-unit sealed box, FieldSource3D disabled (Amp=0.0) |
| `Fixtures/fixture_hermetic_observatory_grin.tscn` | 12-unit sealed box, FieldSource3D enabled (ROuter=3.0, Amp=0.6, Gamma=1.5) |
| `test-straight-hermetic-observatory-v0-pre.tscn` | Full gate (640×360): GrinBasicVisualController + straight fixture |
| `test-grin-hermetic-observatory-v0-pre.tscn` | Full gate (640×360): GrinBasicVisualController + GRIN fixture |
| `test-straight-hermetic-observatory-quick.tscn` | Quick smoke (320×180, FilmResolutionScale=0.5): straight fixture |
| `test-grin-hermetic-observatory-quick.tscn` | Quick smoke (320×180, FilmResolutionScale=0.5): GRIN fixture |
| `tools/hermetic_observatory_observe.py` | Validation tool: runs cases, checks criteria, writes markdown report |
| `scripts/run_hermetic_observatory_full_pixel.sh` | Shell wrapper: activates GPU runtime, invokes Python tool |

All six wall surfaces in each fixture carry the groups:
`fixture_background`, `fixture_geometry`, `hermetic_receiver`, `raytrace_geometry`.
No `fixture_source` nodes are present — all wall hits classify as `backgroundHits`.

---

## 6. Validation Results

Source: `output/v0.0-pre/HERMETIC_OBSERVATORY_VALIDATE.md`  
Generated: `2026-05-17T20:08:33Z`  
Mode: full (640×360)  
Overall: **PASS**

| Case | Status | missHits | traversalRowsCompleted | filmHeight | filmRowsRendered | tracedPixels | backgroundHits |
|---|---|---|---|---|---|---|---|
| hermetic straight | PASS | 0 | 360 | 360 | 36 | 245,696 | 245,696 |
| hermetic GRIN | PASS | 0 | 360 | 360 | 44 | 472,288 | 472,288 |

Notes on values:

- `missHits = 0` for both cases confirms sealed-box closure. No ray escaped.
- `traversalRowsCompleted = 360 = filmHeight` for both cases confirms full transport
  traversal completion.
- `filmRowsRendered` (36 / 44) reflects the second-pass cursor position at capture
  time, not traversal status — see §7.
- `tracedPixels` and `backgroundHits` are equal for both cases, confirming that every
  traced pixel resolved to a classified wall hit with no unclassified remainder.
- Coverage log fields (`totalPixels`, `classifiedPixels`, `escapedNoHitPixels`,
  `budgetExhaustedPixels`) were not emitted in this run — the hermetic rule was
  satisfied via the `missHits == 0` path.
- Screenshot artifacts written: `output/v0.0-pre/hermetic_straight.png`,
  `output/v0.0-pre/hermetic_grin.png`

---

## 7. Semantic Correction: Traversal vs. Presentation Accounting

During Phase 2 development, a false-negative validation failure was diagnosed and
corrected. The root cause was a metric ambiguity in `GrinBasicVisualController`:

**Before the fix:**  
`filmRowsRendered` was computed from `filmDiagnostics.RowCursor`, which maps to
`GrinFilmCamera._rowCursor`. When the renderer completes a full traversal pass,
`_rowCursor` resets to 0 and a second pass begins immediately. At the moment of
capture (triggered by `--grin-basic-settle-frames=6`), the second pass had
advanced only ~28–44 rows. The validator read `filmRowsRendered = 28` and reported
`filmRowsRendered < filmHeight` as a failure — even though the image was fully
rendered and `missHits == 0`.

**After the fix:**  
`GrinBasicVisualController` now logs `traversalRowsCompleted` in the
`[GrinBasicVisual][CaptureArtifacts]` line. This value is sourced from
`writeDiagnostics.RowsCompleted`, which counts entries in `_fixtureRowsCompleted`
— a persistent per-row bitmask that is **not** cleared when `_rowCursor` resets.
It correctly reports 360 after a full traversal pass, independent of which pass the
renderer is currently on.

The Python validator now gates on `traversalRowsCompleted` as the primary
row-completion criterion. `filmRowsRendered` is retained in the report as a
diagnostic reference for presentation-band state.

**Metric definitions:**

| Metric | Source | What it measures |
|---|---|---|
| `filmRowsRendered` | `_rowCursor` at capture time | Presentation-band cursor — resets on pass boundary |
| `traversalRowsCompleted` | `CountMarkedRows(_fixtureRowsCompleted)` | Transport traversal rows completed — persists across pass resets |

---

## 8. Acceptance Criteria

All criteria confirmed met for both straight and GRIN cases:

| Criterion | Required | Straight | GRIN |
|---|---|---|---|
| `missHits` | `== 0` | 0 | 0 |
| `traversalRowsCompleted` | `== filmHeight` | 360 == 360 | 360 == 360 |
| `tracedPixels` | `> 0` | 245,696 | 472,288 |
| Screenshot written | present | confirmed | confirmed |
| Log written | present | confirmed | confirmed |
| Build compiles | 0 errors | confirmed | — |

Budget exhaustion data not available in this run (coverage log not emitted).
The `missHits == 0` result is sufficient: budget-exhausted rays that do not terminate
on a surface are counted as misses.

---

## 9. Known Limitations

- **Resolved-film presentation bounds:** `filmRowsRendered` reflects the current
  presentation cursor, not transport completion. The validator now uses
  `traversalRowsCompleted`, but downstream tools that consume `filmRowsRendered`
  directly will still see the partial second-pass value. This is a known diagnostic
  artifact, not a correctness issue.

- **Conservative GRIN field parameters:** The GRIN fixture uses `ROuter=3.0`,
  `Amp=0.6`, `CanonicalGamma=1.5`. This is a modest, bounded field that curves rays
  within the inner 3-unit radius of the 12-unit box. High-amplitude or extended-radius
  variants have not been validated and are not covered by this milestone. Increasing
  `Amp` beyond tested values may produce step-budget exhaustion and `missHits > 0`;
  the gate will fail loudly.

- **Field-offset variant not tested:** The current GRIN fixture places the field
  center at the origin (coincident with the camera). A field-offset variant — where
  the lens center is displaced from the camera — may be added in a future fixture
  iteration to test asymmetric transport paths.

- **Coverage log fields absent:** `totalPixels`, `classifiedPixels`,
  `escapedNoHitPixels`, `budgetExhaustedPixels` were not emitted by the current
  coverage log path. These fields supplement the `missHits` criterion; their absence
  does not invalidate the pass result, but a future run should confirm coverage
  emission.

- **Not a physics proof:** Hermetic closure at these field parameters does not
  constitute a claim about the physical significance of the GRIN field model.

- **OBS GPU-accelerated recording:** OBS installation is confirmed present but
  GPU-accelerated recording under WSL has not been tested. Observatory live capture
  is a separate task.

---

## 10. Next Steps

| Item | Status |
|---|---|
| Commit Phase 2 work (GPU runtime + hermetic observatory pair) | Pending |
| Run and verify OBS live capture pipeline | Pending |
| Add field-offset hermetic GRIN interaction variant | Future |
| Confirm coverage log emission (`escapedNoHitPixels`, etc.) | Future |
| Oracle Scheduler / adaptive tile refinement direction | Documented in [ORACLE_SCHEDULER_V02_DIRECTION.md](ORACLE_SCHEDULER_V02_DIRECTION.md) |
| Refine v0.0-pre release capsule | Pending |

---

## References

- Validation report: `output/v0.0-pre/HERMETIC_OBSERVATORY_VALIDATE.md`
- Baseline doc: [GRIN_HERMETIC_OBSERVATORY_BASELINE.md](GRIN_HERMETIC_OBSERVATORY_BASELINE.md)
- GPU runtime report: [GRIN_OBSERVE_GPU_RUNTIME_REPORT.md](GRIN_OBSERVE_GPU_RUNTIME_REPORT.md)
- Oracle scheduler direction: [ORACLE_SCHEDULER_V02_DIRECTION.md](ORACLE_SCHEDULER_V02_DIRECTION.md)
- Validator: `tools/hermetic_observatory_observe.py`
- Run script: `scripts/run_hermetic_observatory_full_pixel.sh`
