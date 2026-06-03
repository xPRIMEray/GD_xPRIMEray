# ReferenceTransportOracle Unresolved-Island Refinement

Diagnostic-only renderer validation. This is a best-known renderer-reference transport comparison, not physical ground truth.

- Samples: 8
- Comparisons: 24
- Oracle replay failures: 0
- Local continuity vectors in x=32..48, y=27..43: 0
- Local transport shape regions touching patch: 0
- Sealed at 0.00625: false
- Unresolved at 0.003125: 8

## Pixel Classes

- extra_fine_required: 8

## First Stable Step

- never: 8

## Step Comparisons

- Mean absolute decision-risk delta, 0.00625 vs 0.003125: 2.000000
- Max absolute decision-risk delta, 0.00625 vs 0.003125: 2.000000

## Guardrail

- This analyzer writes diagnostics only. It does not smooth, alter beauty output, or feed any render decision.

## Stopping Rule

- Some pixels remain unresolved at 0.003125. Use ORACLE_ISLAND_EXTRA_FINE=1 to rerun only those pixel centers with the extra-fine oracle step.
