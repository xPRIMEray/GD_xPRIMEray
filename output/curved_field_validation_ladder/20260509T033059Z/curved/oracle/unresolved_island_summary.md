# ReferenceTransportOracle Unresolved-Island Refinement

Diagnostic-only renderer validation. This is a best-known renderer-reference transport comparison, not physical ground truth.

- Samples: 64
- Comparisons: 768
- Oracle replay failures: 0
- Local continuity vectors in x=32..48, y=27..43: 0
- Local transport shape regions touching patch: 0
- Sealed at 0.00625: true
- Unresolved at 0.003125: 0

## Pixel Classes

- stable: 64

## First Stable Step

- 0.02: 64

## Step Comparisons

- Mean absolute decision-risk delta, 0.00625 vs 0.003125: 0.000090
- Max absolute decision-risk delta, 0.00625 vs 0.003125: 0.000090

## Guardrail

- This analyzer writes diagnostics only. It does not smooth, alter beauty output, or feed any render decision.

## Stopping Rule

- The island is sealed at 0.00625 for the sampled patch. Extra-fine rerun is not required by this packet.
