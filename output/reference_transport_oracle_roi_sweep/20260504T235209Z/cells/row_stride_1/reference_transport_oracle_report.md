# ReferenceTransportOracle Report

Diagnostic-only renderer validation. This packet records best-known renderer-reference transport paths; it is not physical GR validation.

- Samples: 64
- Oracle runs: 128
- Comparisons: 64
- Family rows: 576
- Cost rows: 128
- Runtime ms: 10874
- Oracle replay failures: 0
- Mean decision risk: 0.000807
- Max decision risk: 0.001674

## Epsilon Stability Classes

- stable: 46
- unresolved: 18

## Secondary Pathology Tags


## Trajectory Family Classes

- stable_family: 576

## Precision Cost

- Samples with at least one stable production step: 46
- Minimum precision means minimum tested production step satisfying topology/epsilon stability, not smallest possible step.

## Guardrail

- Oracle outputs must not feed rendering, scheduling, hit selection, shading, resolver decisions, traversal, or adaptive precision.
