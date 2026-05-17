# GRIN Hermetic Observatory Baseline

## Purpose

This is a calibration chamber fixture pair, not a physics demonstration.

The hermetic observatory provides a sealed room where every ray cast by the transport
instrument must terminate on a classified wall surface. The validation pass criterion is
`missHits == 0`. Any ray that escapes the chamber (step-budget exhaustion, geometry gap,
or integration instability) is logged as a miss and fails the gate loudly.

**What this proves:** The transport instrumentation correctly classifies every pixel in a
sealed geometry. Straight transport closes. GRIN-bent transport closes.

**What this does not prove:** No exotic physics claims are made. The GRIN field curves
rays by a modest, bounded amount (ROuter=3.0, Amp=0.6). Closure is an instrumentation
property, not a claim about the physical significance of the field model.

---

## Pass Criteria

| Criterion | Required value | Meaning |
|-----------|---------------|---------|
| `missHits` | `== 0` | No ray escaped the sealed chamber |
| `filmRowsRendered` | `== filmHeight` | Full film coverage — no rows skipped |
| `tracedPixels` | `> 0` | Render actually ran |

Both scenes (straight and GRIN) must pass independently.

---

## Scene Inventory

| File | Role |
|------|------|
| `Fixtures/fixture_hermetic_observatory_straight.tscn` | 12-unit sealed box, field disabled |
| `Fixtures/fixture_hermetic_observatory_grin.tscn` | 12-unit sealed box, field enabled (ROuter=3.0, Amp=0.6) |
| `test-straight-hermetic-observatory-v0-pre.tscn` | Harness: GrinBasicVisualController + straight fixture |
| `test-grin-hermetic-observatory-v0-pre.tscn` | Harness: GrinBasicVisualController + GRIN fixture |
| `tools/hermetic_observatory_observe.py` | Validation tool: runs both scenes, checks pass criteria, writes report |
| `scripts/run_hermetic_observatory_full_pixel.sh` | Shell wrapper: activates GPU runtime, invokes Python tool |

---

## How to Run

```bash
bash scripts/run_hermetic_observatory_full_pixel.sh
```

Output: `output/v0.0-pre/HERMETIC_OBSERVATORY_VALIDATE.md`

Screenshots: `output/v0.0-pre/hermetic_straight.png`, `output/v0.0-pre/hermetic_grin.png`

Logs: `output/v0.0-pre/logs/hermetic_observatory/`

### Override min rows

```bash
HERMETIC_MIN_ROWS=180 bash scripts/run_hermetic_observatory_full_pixel.sh
```

### Specify Godot executable

```bash
GODOT_EXE=/path/to/godot bash scripts/run_hermetic_observatory_full_pixel.sh
```

---

## Geometry

Both fixtures use a 12-unit box (walls at ±6.0 from origin) with 0.2-unit wall
thickness. Camera is at the origin, no tilt, FOV=75°. RayBeamRenderer uses
StepsPerRay=900 to give sufficient path budget for GRIN-bent rays.

Wall groups on all six surfaces: `fixture_background`, `fixture_geometry`,
`hermetic_receiver`, `raytrace_geometry`. No `fixture_source` nodes — all wall hits
classify as `backgroundHits`.

---

## Interpretation

- **Straight baseline PASS**: confirms the sealed geometry correctly catches all rays
  with no field influence. Any miss here indicates a geometry gap or renderer bug.
- **GRIN baseline PASS**: confirms that transport curvature at the configured field
  parameters does not cause ray escape. If Amp is increased beyond the current
  conservative value, step-budget exhaustion may produce misses — this is expected
  behavior and the gate will fail loudly.

---

## Known Limitations

- Step-budget exhaustion at high Amp values will produce `missHits > 0` and fail the
  gate. This is intentional: the chamber must fail loudly, not silently degrade.
- The existing v0.0-pre demo scenes (`test-straight-basic-visual-offaxis-observe.tscn`,
  `test-grin-basic-visual-offaxis-observe.tscn`) are not replaced by this fixture pair.
  They serve different purposes: visual presentation vs. closure validation.
- Full-pixel capture at 640×360 with FilmResolutionScale=1.0 requires adequate GPU
  performance. On llvmpipe software rasterizer the render will be very slow.
  Use `scripts/use_gpu_runtime.sh` (activated by the run script) to ensure D3D12
  hardware acceleration.
