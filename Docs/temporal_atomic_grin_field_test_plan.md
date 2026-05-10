# Temporal Atomic GRIN Field Test Plan

## Purpose

Prototype a minimal **Temporal Hydrogen Atom Eigenmode-Modulated GRIN Field** inside the xPRIMEray / FieldSource3D architecture.

The goal is not to simulate literal quantum mechanics. The goal is to create a deterministic, inspectable curved-ray transport field inspired by atomic orbital probability density, then test whether temporal modulation of that field produces coherent full-frame pixel transport behavior.

This becomes the baseline bridge between:

- atomic orbital harmonics
- GRIN field curvature
- temporal Hamiltonian path modulation
- macro-scaled quantum optical transport
- future GML / time crystal / Phase Prime Metric resonant cavity extensions

---

# 1. Core Concept

Create a new child or duplicate of the existing `FieldSource3D` concept:

## Proposed Type Name

`AtomicEigenmodeFieldSource3D`

Alternative names:

- `OrbitalGrinFieldSource3D`
- `TemporalAtomicFieldSource3D`
- `HydrogenEigenmodeFieldSource3D`
- `MacroOrbitalFieldSource3D`

Initial preferred implementation:

> `AtomicEigenmodeFieldSource3D`

because it remains expandable beyond hydrogen.

---

# 2. Baseline Hydrogen Field

Hydrogen should be the calm baseline case.

It should have:

- minimal curvature strength
- smooth spherical falloff
- stable full-frame pixel fill behavior
- clear debug visibility
- low likelihood of transport chaos

Use a macro-scaled hydrogen-like 1s orbital density:

```text
density(r) = exp(-2r / orbitalRadius)
```

Map this density into a GRIN perturbation:

```text
n(r, t) = 1.0 + curvatureStrength * density(r) * temporalModulation(t)
```

Where:

```text
temporalModulation(t) = 1.0 + modulationDepth * sin(phase)
phase = 2π * fieldClockHz * t
```

Recommended first values:

```text
orbitalRadius = 10.0 world units
curvatureStrength = 0.002
modulationDepth = 0.10
fieldClockHz = 0.25
updateIntervalSeconds = 1.0
```

This creates a very gentle breathing GRIN atom.

---

# 3. Temporal Clocking Model

The field should update curvature only every fixed interval, rather than every frame at first.

Purpose:

- reduce noise
- isolate field-state transitions
- make changes easier to inspect
- allow deterministic A/B testing between frozen and clocked fields

Initial modes:

## Mode A: Frozen Field

```text
timeEnabled = false
```

Field density remains static.

## Mode B: Discrete Clocked Field

```text
timeEnabled = true
updateIntervalSeconds = 1.0
```

Field phase advances once per interval.

## Mode C: Continuous Time Field

Future mode only.

Field phase evolves every frame or every ray step.

---

# 4. Full-Frame Pixel Fill Test

The first validation objective is not beauty. It is transport completeness.

For each test frame:

Track:

```text
totalPixels
hitPixels
missPixels
fieldSampledPixels
curvedTransportPixels
maxStepCountPixels
budgetExhaustedPixels
meanCurvatureMagnitude
maxCurvatureMagnitude
meanPhaseState
fieldClockTickIndex
```

Pass condition for enclosed fixture:

```text
hitPixels == totalPixels
budgetExhaustedPixels == 0, or explicitly classified
```

If curvature creates misses, classify them by:

- transport escape
- step budget exhaustion
- unresolved curvature domain
- missed geometry despite enclosed fixture
- numerical instability

---

# 5. Recommended Fixture

Use a hermetically sealed room or sphere fixture.

Camera should be fully enclosed by visible hit surfaces so every pixel is expected to resolve a hit.

Preferred fixture:

## `atomic_grin_enclosed_room_fixture`

Scene:

- camera at center or slightly offset
- closed cube or spherical chamber
- `AtomicEigenmodeFieldSource3D` at center
- debug planes or wall colors to inspect distortion
- optional reference grid on all walls

Render modes:

1. Straight reference
2. Static Hydrogen GRIN
3. Clocked Hydrogen GRIN
4. Strong Hydrogen artistic exaggeration
5. Gold relativistic curvature stress test

---

# 6. Hydrogen Test Ladder

## H0: Straight Baseline

```text
fieldEnabled = false
```

Expected:

- 100% pixel fill
- no curvature artifacts
- reference image stored

## H1: Static Weak Hydrogen

```text
fieldEnabled = true
curvatureStrength = 0.002
timeEnabled = false
```

Expected:

- 100% pixel fill
- subtle optical compression near field center
- low/no instability

## H2: Clocked Weak Hydrogen

```text
curvatureStrength = 0.002
modulationDepth = 0.10
updateIntervalSeconds = 1.0
```

Expected:

- 100% pixel fill across several frames
- visible but gentle breathing distortion
- stable field tick telemetry

## H3: Artistic Hydrogen Exaggeration

```text
curvatureStrength = 0.02
modulationDepth = 0.25
updateIntervalSeconds = 0.5
```

Expected:

- stronger caustic-like distortion
- possible early transport island formation
- useful visual debugging

---

# 7. Gold Relativistic Curvature Stress Test

Gold is the first heavy-atom artistic analogue.

This is not literal relativistic quantum chemistry. It is a macro-scaled transport stress field inspired by high-Z inner-electron relativistic compression.

Use:

```text
atomicNumber = 79
relativisticFactorApprox = atomicNumber / 137.0
```

For gold:

```text
relativisticFactorApprox ≈ 0.577
```

Use this factor to compress the inner curvature profile:

```text
innerCompression = 1.0 - clamp(relativisticFactorApprox^2, 0.0, 0.9)
effectiveOrbitalRadius = orbitalRadius * innerCompression
```

Recommended artistic values:

```text
orbitalRadius = 10.0
curvatureStrength = 0.01 to 0.05
modulationDepth = 0.10 to 0.20
atomicNumber = 79
updateIntervalSeconds = 1.0
```

Expected behavior:

- stronger near-core curvature
- sharper central compression
- greater likelihood of transport instability
- useful stress case for adaptive stepping and topology-aware pixel recovery

---

# 8. Field Function Sketch

```csharp
public class AtomicEigenmodeFieldSource3D : FieldSource3D
{
    public enum AtomPreset
    {
        Hydrogen,
        Gold,
        Custom
    }

    public AtomPreset Preset = AtomPreset.Hydrogen;

    public float AtomicNumber = 1.0f;
    public float OrbitalRadius = 10.0f;
    public float CurvatureStrength = 0.002f;
    public float ModulationDepth = 0.10f;
    public float FieldClockHz = 0.25f;
    public float UpdateIntervalSeconds = 1.0f;
    public bool TimeEnabled = false;

    private float cachedPhase = 0.0f;
    private float lastUpdateTime = 0.0f;
    private int fieldClockTickIndex = 0;

    public override FieldSample SampleField(Vector3 worldPosition, float renderTime)
    {
        Vector3 local = ToLocal(worldPosition);
        float r = local.Length();

        if (TimeEnabled && renderTime - lastUpdateTime >= UpdateIntervalSeconds)
        {
            lastUpdateTime = renderTime;
            fieldClockTickIndex++;
            cachedPhase = Mathf.Tau * FieldClockHz * renderTime;
        }

        float temporal = 1.0f + ModulationDepth * Mathf.Sin(cachedPhase);

        float zFactor = AtomicNumber / 137.0f;
        float compression = 1.0f;

        if (Preset == AtomPreset.Gold || AtomicNumber > 20.0f)
        {
            compression = 1.0f - Mathf.Clamp(zFactor * zFactor, 0.0f, 0.9f);
        }

        float effectiveRadius = OrbitalRadius * compression;
        float density = Mathf.Exp(-2.0f * r / effectiveRadius);
        float n = 1.0f + CurvatureStrength * density * temporal;

        Vector3 grad = EstimateGradient(worldPosition, effectiveRadius, temporal);

        return new FieldSample
        {
            RefractiveIndex = n,
            Gradient = grad,
            Density = density,
            Phase = cachedPhase,
            TickIndex = fieldClockTickIndex
        };
    }
}
```

---

# 9. Telemetry Requirements

Add render output CSV columns:

```text
fixture_name
atom_preset
atomic_number
orbital_radius
curvature_strength
modulation_depth
field_clock_hz
update_interval_seconds
field_tick_index
phase
frame_index
total_pixels
hit_pixels
miss_pixels
budget_exhausted_pixels
mean_steps_per_pixel
max_steps_per_pixel
mean_curvature_magnitude
max_curvature_magnitude
mean_density_sampled
max_density_sampled
```

Optional image overlays:

- density map
- curvature magnitude map
- phase tick map
- missed pixel mask
- transport budget exhaustion mask
- straight-vs-curved delta map

---

# 10. Claude Implementation Prompt

```text
We want to add a minimal AtomicEigenmodeFieldSource3D prototype to the xPRIMEray engine as a child/duplicate of the existing FieldSource3D pattern.

Goal:
Create a macro-scaled hydrogen-like GRIN field using a simple 1s orbital-inspired density function:

density(r) = exp(-2r / orbitalRadius)

Map density into refractive index:

n(r,t) = 1.0 + curvatureStrength * density(r) * temporalModulation(t)

temporalModulation(t) = 1.0 + modulationDepth * sin(phase)
phase = 2π * fieldClockHz * t

Important:
Start with discrete clocked updates. Field phase should update only every updateIntervalSeconds so we can inspect transport stability at stable field ticks.

Add presets:
1. Hydrogen: atomicNumber=1, weak curvature, no relativistic compression.
2. Gold: atomicNumber=79, stronger near-core curvature using a simple artistic relativistic compression factor based on Z/137.

Gold compression sketch:
zFactor = atomicNumber / 137.0
compression = 1.0 - clamp(zFactor*zFactor, 0.0, 0.9)
effectiveOrbitalRadius = orbitalRadius * compression

This is an artistic / computational analogue only, not a literal quantum chemistry model.

Create or wire a simple enclosed-room fixture so every full-frame pixel should hit a surface. Render straight reference, static hydrogen GRIN, clocked hydrogen GRIN, exaggerated hydrogen GRIN, and gold stress test.

Telemetry must track:
- totalPixels
- hitPixels
- missPixels
- budgetExhaustedPixels
- mean/max steps per pixel
- mean/max curvature magnitude
- mean/max sampled density
- fieldClockTickIndex
- phase
- atom preset
- atomic number
- curvature settings

Pass condition:
For the sealed fixture, every pixel should resolve a hit or be explicitly classified. Primary goal is full-frame pixel fill stability under a temporally modulated GRIN field.

Please inspect the current FieldSource3D / GRIN / fixture architecture and implement this in the smallest coherent patch possible, preserving existing tests and adding a new markdown artifact documenting the test ladder.
```

---

# 11. Expected Research Value

This prototype tests whether xPRIMEray can support:

- dynamic field curvature
- clocked temporal transport fields
- orbital-inspired harmonic density sources
- full-frame hit recovery under evolving GRIN conditions
- high-Z artistic curvature stress cases
- future resonant cavity / GML / time crystal field evolution

Hydrogen provides the stable baseline.

Gold provides the curvature dragon.

Together they create the first atomic-to-macro transport bridge.

