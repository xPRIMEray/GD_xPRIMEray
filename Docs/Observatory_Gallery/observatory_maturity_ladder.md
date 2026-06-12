# Observatory Maturity Ladder v1

**Maturity is evidence strength, not physical truth.**

The Observatory now tracks not only what exists, but how strongly each artifact should be trusted. A higher maturity score means the artifact has stronger evidence, clearer caveats, and a more stable interpretation inside its declared scene contract.

## How to read artifact maturity

| score | stage | how to read it |
|---:|---|---|
| 0 | **Proposed** | A concept has been named and scoped, but repeatable artifact evidence is not established yet. |
| 1 | **Experimental** | An artifact exists, but the contract, inputs, or interpretation are still unstable. |
| 2 | **Observed** | The artifact was produced from real run data and can be inspected, but coverage or closure may be partial. |
| 3 | **Confirmed** | The artifact satisfies its local validation gate for at least one run or fixture. |
| 4 | **Characterized** | The artifact has repeatable structure, documented interpretation, tooling or schema support, and known caveats. |
| 5 | **Canonical** | The artifact is part of the stable Observatory language and can anchor other interpretations within a declared fixture contract. |

**Canonical does not mean physically true.** It means the artifact can anchor Observatory interpretation within its declared scene contract.

## Current Anchors

| artifact | maturity | why |
|---|---:|---|
| **Hermetic Storyboard** | Canonical | Promoted Gallery anchor for the sealed-room fixture with closure, coverage, curvature signature, verdict, and explicit scene-contract caveats. |
| **Observer Storyboard Demo** | Characterized | Renderer-agnostic nine-panel framework with schema, rendering tool, demo artifact, and explicit `PASS` / `FAIL` / `PARTIAL` / `MISSING` vocabulary. |
| **Query Observatory** | Observed | A real storyboard artifact exists, but it has not yet been promoted into a stable catalog or canonical visitor-facing contract. |
| **Cost Basin** | Proposed | Concept architecture is documented; no dedicated Cost Basin artifact generation or validation gate exists yet. |

## Catalog Scores

The machine-readable source lives in:

```text
reports/observatory_maturity_ladder.json
```

The generated report lives in:

```text
reports/observatory_maturity_ladder.md
```

Current catalog scores are produced from `reports/observatory_catalog.json` plus the curated anchor assignments above. Cataloged artifacts are scored independently per run, so an early curvature signature ladder can remain **Observed** while a later full-coverage ladder becomes **Characterized**.

## Reading Rule

Use maturity as a trust label for Observatory evidence:

- **Low maturity** means useful, but easy to misread.
- **Middle maturity** means the artifact can support a local claim with caveats.
- **High maturity** means the artifact can teach the Observatory language itself.

Canonical still means only this: the artifact can anchor interpretation within its declared scene contract.
