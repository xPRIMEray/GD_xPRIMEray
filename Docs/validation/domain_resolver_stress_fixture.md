# Domain Resolver Stress Fixture

Date: 2026-04-29

Purpose: find or create a deterministic, minimal fixture where
`EnableDomainAwareFirstHitResolver=1` produces nonzero resolver-change telemetry
without changing default render behavior, tuning visuals, or adding beauty-image
post-processing.

## Pass / Fail Table

| Check | Status | Evidence |
|---|---|---|
| OFF vs telemetry ON beauty identical | PASS | Final stress run OFF and telemetry ON beauty PNGs both hashed `bf3bfc703a1eeecd06211f3c007ddaaa5f0333b5d8c3e96fb290e8c27c088c33`. |
| Resolver ON has nonzero `changed_pixels` | PASS | Final stress run reported `changed_pixels=8`. |
| Changes localized near boundary/normal discontinuity | PASS | Beauty diffs were 8 pixels in bbox `(38,10)-(41,11)`; boundary at changed pixels was `0.698039`, normal discontinuity was nonzero, and selection flip was `1.0`. |
| Telemetry explains hit reason | PASS | Summary reports changed collider id pixels, hit-distance deltas, score delta, and score-component means. |
| No global blur signature | PASS | Only 8 beauty pixels changed between telemetry ON and resolver ON; no smoothing or blur pass is used by the fixture. |
| Default render behavior unchanged | PASS | New fixture is opt-in through `domain_resolver_stress`; resolver remains off unless explicitly enabled by CLI/env. |

## Files Changed For This Follow-Up

| File | Classification | Notes |
|---|---|---|
| `Fixtures/fixture_domain_resolver_stress.tscn` | validation/tooling | Adds the controlled two-surface competing-hit fixture. |
| `test-domain-resolver-stress.tscn` | validation/tooling | Adds the render-test harness scene for the fixture. |
| `RendererCore/Testing/RenderTestRunner.cs` | validation/tooling | Registers `domain_resolver_stress`, honors quick frame counts, and applies fixture-scoped acquisition settings needed to expose both close hits. |
| `RendererCore/Testing/LauncherAudit.cs` | validation/tooling | Maps the new fixture token and scene path for audit consistency. |
| `scripts/run_domain_audit_quick.sh` | validation/tooling | Adds `DOMAIN_AUDIT_QUICK_OUTPUT_ROOT` so stress runs can write under `output/domain_resolver_stress/`. |
| `Docs/validation/domain_resolver_stress_fixture.md` | validation docs | Records the stress search, commands, outputs, and remaining risks. |

## Candidate Search

Three existing runner-backed fixtures were tested first with quick OFF,
telemetry ON, and resolver ON passes:

| Candidate | Output | Result | Boundary / normal / flip signal | Resolver changes |
|---|---|---|---|---|
| `curved_minimal_backdrop` | `output/domain_resolver_stress/curved_minimal_backdrop/20260429T012741Z/` | PASS, but no hit changes | boundary max `0.7`, mean `0.063`; normal max `0`; selection flip max `1`, mean `0.09` | `changed_pixels=0`, `compared_pixels=324` |
| `blackhole_minimal` | `output/domain_resolver_stress/blackhole_minimal/20260429T012810Z/` | PASS effective; Godot Mono aborted during shutdown after harness success | boundary max `0`; normal max `0`; selection flip max `0` | `changed_pixels=0`, `compared_pixels=0` |
| `einstein_ring_minimal` | `output/domain_resolver_stress/einstein_ring_minimal/20260429T012832Z/` | FAIL | Fixture invariant failed before telemetry comparison | Not available; log reports `Einstein_Ring_Minimal invalid: curvature not engaged` |

No existing fixture produced nonzero `changed_pixels`, so a purpose-built
diagnostic fixture was added.

## Stress Fixture Design

The new scene contains a camera facing two close, competing hit surfaces:

- A broad background plate at `z=-4.055`.
- A narrow near source plate at `z=-4`, rotated 8 degrees around Y.
- Two small off-axis background markers to make broad blur or smoothing obvious.
- No beauty-image smoothing or post-processing.

The source/background plate spacing is within the resolver candidate window, and
the source plate normal differs from the background normal. This gives the
resolver a real set of acquired narrowphase hits to choose among and gives the
telemetry measurable boundary, selection-flip, collider-id, hit-distance, and
normal-discontinuity signals. The scene is diagnostic only; the geometry is not
tuned for appearance.

`RenderTestRunner` applies fixture-scoped acquisition settings only for
`domain_resolver_stress`: overlap broadphase, no pass-2 collision stride, and a
smaller `RayBeamRenderer` step. These settings are limited to the stress fixture
so the default renderer path is not changed.

## Commands Run

Existing candidate runs:

```bash
DOMAIN_AUDIT_QUICK_OUTPUT_ROOT="$PWD/output/domain_resolver_stress/curved_minimal_backdrop" \
DOMAIN_AUDIT_QUICK_SCENE='res://test-curved-minimal-backdrop.tscn' \
DOMAIN_AUDIT_QUICK_FIXTURE='curved_minimal_backdrop' \
DOMAIN_AUDIT_QUICK_FRAMES=8 DOMAIN_AUDIT_QUICK_WARMUP=1 \
timeout 300s scripts/run_domain_audit_quick.sh
```

```bash
DOMAIN_AUDIT_QUICK_OUTPUT_ROOT="$PWD/output/domain_resolver_stress/blackhole_minimal" \
DOMAIN_AUDIT_QUICK_SCENE='res://test-blackhole-minimal.tscn' \
DOMAIN_AUDIT_QUICK_FIXTURE='blackhole_minimal' \
DOMAIN_AUDIT_QUICK_FRAMES=8 DOMAIN_AUDIT_QUICK_WARMUP=1 \
timeout 300s scripts/run_domain_audit_quick.sh
```

```bash
DOMAIN_AUDIT_QUICK_OUTPUT_ROOT="$PWD/output/domain_resolver_stress/einstein_ring_minimal" \
DOMAIN_AUDIT_QUICK_SCENE='res://test-einstein-ring-minimal.tscn' \
DOMAIN_AUDIT_QUICK_FIXTURE='einstein_ring_minimal' \
DOMAIN_AUDIT_QUICK_FRAMES=8 DOMAIN_AUDIT_QUICK_WARMUP=1 \
timeout 300s scripts/run_domain_audit_quick.sh
```

Final stress run:

```bash
DOMAIN_AUDIT_QUICK_OUTPUT_ROOT="$PWD/output/domain_resolver_stress/domain_resolver_stress" \
DOMAIN_AUDIT_QUICK_SCENE='res://test-domain-resolver-stress.tscn' \
DOMAIN_AUDIT_QUICK_FIXTURE='domain_resolver_stress' \
DOMAIN_AUDIT_QUICK_FRAMES=16 DOMAIN_AUDIT_QUICK_WARMUP=1 \
timeout 360s scripts/run_domain_audit_quick.sh
```

Localization check:

```bash
python3 - <<'PY'
from pathlib import Path
from PIL import Image
root = Path('output/domain_resolver_stress/domain_resolver_stress/20260429T013524Z')
tel = next((root / 'telemetry_on').glob('*__runid-1.png'))
res = next((root / 'resolver_on').glob('*__runid-1.png'))
bound = next((root / 'resolver_on').glob('*.boundary_confidence.png'))
normal = next((root / 'resolver_on').glob('*.normal_discontinuity.png'))
sel = next((root / 'resolver_on').glob('*.selection_flip.png'))
imgs = {k: Image.open(p).convert('RGBA') for k, p in [
    ('tel', tel), ('res', res), ('boundary', bound), ('normal', normal), ('selection', sel)
]}
diff = [(x, y) for y in range(imgs['tel'].height) for x in range(imgs['tel'].width)
        if imgs['tel'].getpixel((x, y)) != imgs['res'].getpixel((x, y))]
print('diff_pixels', len(diff))
xs = [p[0] for p in diff]
ys = [p[1] for p in diff]
print('bbox', min(xs), min(ys), max(xs), max(ys))
def value(img, x, y):
    return img.getpixel((x, y))[0] / 255.0
print('boundary_at_diff_mean', sum(value(imgs['boundary'], x, y) for x, y in diff) / len(diff),
      'max', max(value(imgs['boundary'], x, y) for x, y in diff))
print('normal_at_diff_mean', sum(value(imgs['normal'], x, y) for x, y in diff) / len(diff),
      'max', max(value(imgs['normal'], x, y) for x, y in diff))
print('selection_at_diff_mean', sum(value(imgs['selection'], x, y) for x, y in diff) / len(diff),
      'max', max(value(imgs['selection'], x, y) for x, y in diff))
PY
```

## Final Stress Run Results

Output root:
`output/domain_resolver_stress/domain_resolver_stress/20260429T013524Z/`

| Case | Result | Notes |
|---|---|---|
| OFF | PASS effective, exit 134 | Harness reported success; Godot Mono aborted during shutdown after artifacts were written. |
| Telemetry ON | PASS effective, exit 134 | Beauty render identical to OFF. |
| Resolver ON | PASS effective, exit 134 | Resolver emitted nonzero changed-hit telemetry. |

Beauty hashes:

| Case | SHA-256 |
|---|---|
| OFF | `bf3bfc703a1eeecd06211f3c007ddaaa5f0333b5d8c3e96fb290e8c27c088c33` |
| Telemetry ON | `bf3bfc703a1eeecd06211f3c007ddaaa5f0333b5d8c3e96fb290e8c27c088c33` |
| Resolver ON | `d48af3ba6936715d444f5c485f617d6c5490dd2f6a616b4785d4779dd1f491c6` |

Resolver telemetry from `domain_telemetry_summary.json`:

| Metric | Value |
|---|---:|
| `enabled` | `true` |
| `compared_pixels` | 1632 |
| `changed_pixels` | 8 |
| `changed_collider_id_pixels` | 8 |
| `changed_hit_distance_pixels` | 8 |
| `mean_hit_distance_delta` | 0.000232 |
| `max_hit_distance_delta` | 0.064747 |
| `score_available_pixels` | 1632 |
| `mean_score_delta` | 0.006618 |
| `mean_score_components.confidence` | 0.686 |
| `mean_score_components.neighbor_continuity` | 0.696212 |
| `mean_score_components.boundary_stability` | 0.105 |
| `mean_score_components.normal_discontinuity_penalty` | 0 |
| `mean_score_components.flip_penalty` | 0.016176 |

Map summaries from resolver ON:

| Map | Max | Mean |
|---|---:|---:|
| `boundary_confidence` | 0.7 | 0.317333 |
| `selection_flip` | 1 | 0.453333 |
| `normal_discontinuity` | 0.009732 | 0.000022 |

Localization result:

| Metric | Value |
|---|---|
| Beauty diff pixels, telemetry ON vs resolver ON | 8 |
| Diff bounding box | `(38,10)-(41,11)` |
| Boundary confidence at changed pixels | mean `0.698039`, max `0.698039` |
| Normal discontinuity at changed pixels | mean `0.007843`, max `0.007843` |
| Selection flip at changed pixels | mean `1.0`, max `1.0` |

## Remaining Risks

1. Godot Mono still aborts during shutdown after successful harness runs in this environment; the artifacts are valid, but the clean process exit remains unresolved.
2. The stress fixture proves the resolver counters and localized behavior on one controlled scene; it is not evidence that the heuristic resolver improves arbitrary production renders.
3. `normal_discontinuity` is small but nonzero in this scene. It is enough to prove export and localization, not broad normal-boundary coverage.
4. The fixture uses labels `fixture_source` and `fixture_background`, so domain IDs remain diagnostic classifications, not proof of metric-only domain ownership.
5. The resolver comparison is against the first accepted/nearest hit inside the current acquisition path, not a separately reconstructed legacy renderer.
