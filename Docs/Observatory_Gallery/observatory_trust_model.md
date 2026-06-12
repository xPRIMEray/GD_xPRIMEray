# Observatory Trust Model v1

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

## Crosswalk

### Showcase Status

| status | trust stage | score | read as |
|---|---:|---:|---|
| Strong | Characterized | 4 | Complete enough to teach the fixture, but not automatically canonical. |
| Partial | Observed | 2 | Inspectable and useful, but incomplete. |
| Placeholder | Proposed | 0 | Named slot; public evidence not established. |
| Experimental | Experimental | 1 | Internal or exploratory; not visitor-ready. |

### Citation Tiers

Citation tiers are evidence-placement labels, not transport implementation tiers.

| tier | trust stage | score | read as |
|---|---:|---:|---|
| Tier 0 | Canonical | 5 | Source-of-record evidence. |
| Tier 1 | Characterized | 4 | Strong standalone evidence. |
| Tier 2 | Confirmed | 3 | Supporting evidence for a local gate. |
| Tier 3 | Observed | 2 | Context or narrative evidence. |

### Archive Status

| archive status | trust stage | score | read as |
|---|---:|---:|---|
| Visual reference | Characterized | 4 | Curated reference, not a ground-truth claim. |
| Validation candidate | Confirmed | 3 | Strong local evidence awaiting promotion or another reproducibility pass. |
| Test output | Observed | 2 | Useful run output exists. |
| Archived | Experimental | 1 | Historical or superseded evidence; revalidate before using for current claims. |

### Artifact Status

`PASS`, `PARTIAL`, and `MISSING` describe local fields such as coverage, closure, or verdict. They do not by themselves assign whole-artifact maturity.

| artifact status | trust stage | score | read as |
|---|---:|---:|---|
| PASS | Confirmed | 3 | The local gate passed for the stated run, fixture, and scene contract. |
| PARTIAL | Observed | 2 | Evidence exists, but the local gate or coverage is incomplete. |
| MISSING | Proposed | 0 | Expected evidence is named, but no matching artifact or value is present. |

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
