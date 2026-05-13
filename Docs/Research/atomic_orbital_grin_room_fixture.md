# Atomic Orbital GRIN Room Fixture

`atomic_orbital_grin_room` is a V1 sealed-room render-test fixture for a macro-scaled atomic-orbital GRIN analogue. It is not quantum chemistry; it is a deterministic transport stress fixture.

## V1 Scope

- A0: straight sealed-room baseline
- A1: no electron cloud reference, `electronCount=0`, `density=0`
- A2: static hydrogen-like 1s cloud
- A3: clocked hydrogen-like 1s cloud through repeated deterministic short runs

Helium-like, lithium-like, exaggerated hydrogen, and gold stress are deferred.

## Field Model

The V1 source is `AtomicEigenmodeFieldSource3D`, which fits the existing `FieldSource3D` packed field path without widening field snapshots.

Hydrogen-like density:

```text
density = clamp(exp(-2r / orbitalRadius) * temporalModulation, 0, 1)
```

A1 is a no-cloud reference. Proton-core curvature is separate and disabled by default.

## Gates

A0-A2 pass only when:

- `closure_rate >= 0.999`
- `miss_pixels == 0`
- `budget_exhausted_pixels == 0`

A3 reports the same values, but classified differences may be accepted by the report.

## Guardrail

The implementation must not change `hermetic_curved_room` outputs when the atomic fixture is not selected.
