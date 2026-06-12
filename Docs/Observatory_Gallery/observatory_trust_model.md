# Observatory Trust Model v1.1

The Observatory Trust Model is the shared vocabulary for evidence strength. It keeps maturity, showcase readiness, citation tiers, archive status, closure, coverage, and verdict from being read as the same kind of claim.

## Master Evidence-Strength Axis

| score | stage | how to read it |
|---:|---|---|
| 0 | **Proposed** | A concept, expected panel, or fixture slot has been named, but repeatable artifact evidence is not established yet. |
| 1 | **Experimental** | An artifact or run exists, but the contract, inputs, or interpretation are still unstable or superseded. |
| 2 | **Observed** | The artifact was produced from real run data and can be inspected, but coverage, closure, or interpretation may be partial. |
| 3 | **Confirmed** | The artifact satisfies its local validation gate for at least one run or fixture. |
| 4 | **Characterized** | The artifact has repeatable structure, documented interpretation, tooling or schema support, and known caveats. |
| 5 | **Canonical** | The artifact is part of the stable Observatory language and can anchor other interpretations within a declared fixture contract. |

**Canonical does not mean physically true.** It means the artifact can anchor Observatory interpretation within its declared scene contract.

**Unlabeled ≠ Proposed.** Artifacts without a recorded score are unlabeled, not Proposed. Proposed means a concept, expected panel, or fixture slot has been named but the evidence pipeline is not established.

## Crosswalk

### Showcase Status

| status | trust stage | score | read as |
|---|---:|---:|---|
| Strong | Characterized | 4 | Complete enough to teach the fixture, but not automatically canonical. |
| Partial | Observed | 2 | Inspectable and useful, but incomplete. |
| Placeholder | Proposed | 0 | A concept, expected panel, or fixture slot has been named, but repeatable artifact evidence is not established yet. |
| Experimental | Experimental | 1 | Internal or exploratory; not visitor-ready. |

### Citation Tiers

Citation tiers are evidence-placement labels, not transport implementation tiers.

Citation placement tiers in this Trust Model are distinct from Xeno/Zeno investigation confidence tiers. The Trust Model's placement tiers rank evidence authority; the Xeno/Zeno Atlas tiers rank anomaly-investigation completeness.

| tier | trust stage | score | read as |
|---|---:|---:|---|
| Tier 0 | Canonical | 5 | Source-of-record evidence. |
| Tier 1 | Characterized | 4 | Strong standalone evidence. |
| Tier 2 | Confirmed | 3 | Supporting evidence for a local gate. |
| Tier 3 | Observed | 2 | Context or narrative evidence. |

### Xeno/Zeno Atlas Tiers

| atlas tier | trust stage | score | read as |
|---|---:|---:|---|
| Atlas Tier 0 Candidate | Observed | 2 | An anomaly candidate is inspectable, but investigation completeness is still early. |
| Atlas Tier 1 Confirmed | Confirmed | 3 | The anomaly has a local confirmation basis. |
| Atlas Tier 2 Characterized | Characterized | 4 | The anomaly has documented structure and caveats. |
| Atlas Tier 3 Explained | Canonical | 5 | The anomaly explanation can anchor future interpretation within its declared scope. |

### Archive Status

| archive status | trust stage | score | read as |
|---|---:|---:|---|
| Visual reference | Characterized | 4 | Curated for presentation, not a physical-correctness claim. |
| Validation candidate | Confirmed | 3 | Strong local evidence awaiting promotion or another reproducibility pass. |
| Test output | Observed | 2 | Useful run output exists. |
| Archived | Experimental | 1 | Historical or superseded evidence; revalidate before using for current claims. |

### Artifact Status

`PASS`, `PARTIAL`, and `MISSING` describe local fields such as coverage, closure, or verdict. They do not by themselves assign whole-artifact maturity.

| artifact status | trust stage | score | read as |
|---|---:|---:|---|
| PASS | Confirmed | 3 | The local gate passed for the stated run, fixture, and scene contract. |
| PARTIAL | Observed | 2 | Evidence exists, but the local gate or coverage is incomplete. |
| MISSING | Proposed | 0 | A concept, expected panel, or fixture slot has been named, but repeatable artifact evidence is not established yet. |

## Reading Rule

When labels disagree, keep the local label and add the Trust Model stage beside it. A fixture can be **Strong**, have `PASS` closure, and still be **Characterized** rather than **Canonical**.

The machine-readable source lives in:

```text
reports/observatory_trust_model.json
```

The report version lives in:

```text
reports/observatory_trust_model.md
```
