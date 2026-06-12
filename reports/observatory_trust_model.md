# Observatory Trust Model v1.1

The Observatory Trust Model is the shared vocabulary for evidence strength. It keeps maturity, showcase readiness, citation tiers, archive status, closure, coverage, and verdict from being read as the same kind of claim.

## Master Evidence-Strength Axis

| score | stage | meaning | minimum evidence |
|---:|---|---|---|
| 0 | **Proposed** | A concept, expected panel, or fixture slot has been named, but repeatable artifact evidence is not established yet. | Concept document, prompt, architecture note, or missing expected evidence. |
| 1 | **Experimental** | Artifact or run exists, but the contract, inputs, or interpretation are still unstable or superseded. | At least one generated artifact, archive record, or report with caveats. |
| 2 | **Observed** | Artifact has been produced from real run data and can be inspected, but coverage, closure, or interpretation may be partial. | Source path plus visible artifact, report, or inspectable run output. |
| 3 | **Confirmed** | Artifact satisfies its local validation gate for at least one run or fixture. | `PASS` closure/verdict or equivalent local acceptance gate. |
| 4 | **Characterized** | Artifact has repeatable structure, documented interpretation, tooling or schema support, and known caveats. | Documented method, schema/tooling, or repeated comparable outputs. |
| 5 | **Canonical** | Artifact is part of the stable Observatory language and can anchor other interpretations. | Promoted visitor-facing artifact, stable fixture contract, and clear non-ground-truth caveats. |

**Canonical does not mean physically true.** It means the artifact can anchor Observatory interpretation within its declared scene contract.

**Unlabeled ≠ Proposed.** Artifacts without a recorded score are unlabeled, not Proposed. Proposed means a concept, expected panel, or fixture slot has been named but the evidence pipeline is not established.

## Vocabulary Crosswalk

### Showcase Status

| showcase status | equivalent stage | score | reading rule |
|---|---:|---:|---|
| Strong | Characterized | 4 | Public showcase is complete enough to teach the fixture, but this does not automatically make it canonical. |
| Partial | Observed | 2 | Evidence is inspectable and useful, but panel inventory, closure, or interpretation is incomplete. |
| Placeholder | Proposed | 0 | A concept, expected panel, or fixture slot has been named, but repeatable artifact evidence is not established yet. |
| Experimental | Experimental | 1 | Internal or exploratory evidence exists, but it is not ready for visitor-facing claims. |

### Citation Tiers

Citation tiers are evidence-placement labels, not transport implementation tiers. Only documents that explicitly say "citation tier" should use this crosswalk.

Citation placement tiers in this Trust Model are distinct from Xeno/Zeno investigation confidence tiers. The Trust Model's placement tiers rank evidence authority; the Xeno/Zeno Atlas tiers rank anomaly-investigation completeness.

| citation tier | equivalent stage | score | reading rule |
|---|---:|---:|---|
| Tier 0 | Canonical | 5 | Source-of-record evidence that can anchor the Observatory language or fixture contract. |
| Tier 1 | Characterized | 4 | Strong standalone evidence with documented interpretation and caveats. |
| Tier 2 | Confirmed | 3 | Supporting evidence that passes or corroborates a local gate. |
| Tier 3 | Observed | 2 | Contextual or narrative evidence that is inspectable but not a validation anchor. |

### Xeno/Zeno Atlas Tiers

| atlas tier | equivalent stage | score | reading rule |
|---|---:|---:|---|
| Atlas Tier 0 Candidate | Observed | 2 | An anomaly candidate is inspectable, but investigation completeness is still early. |
| Atlas Tier 1 Confirmed | Confirmed | 3 | The anomaly has a local confirmation basis. |
| Atlas Tier 2 Characterized | Characterized | 4 | The anomaly has documented structure and caveats. |
| Atlas Tier 3 Explained | Canonical | 5 | The anomaly explanation can anchor future interpretation within its declared scope. |

### Archive Status

| archive status | equivalent stage | score | reading rule |
|---|---:|---:|---|
| Visual reference | Characterized | 4 | Curated for presentation, not a physical-correctness claim. |
| Validation candidate | Confirmed | 3 | Strong local evidence, usually needing one more reproducibility pass, crop, or promotion step. |
| Test output | Observed | 2 | Useful run output exists and can be inspected. |
| Archived | Experimental | 1 | Historical or superseded evidence remains auditable, but should not carry current claims without revalidation. |

### Artifact Status

`PASS`, `PARTIAL`, and `MISSING` describe a local field such as coverage, closure, or verdict. They do not by themselves assign whole-artifact maturity.

| artifact status | equivalent stage | score | reading rule |
|---|---:|---:|---|
| PASS | Confirmed | 3 | The local gate passed for the stated run, fixture, and scene contract. |
| PARTIAL | Observed | 2 | Evidence exists, but the local gate or coverage is incomplete. |
| MISSING | Proposed | 0 | A concept, expected panel, or fixture slot has been named, but repeatable artifact evidence is not established yet. |

## Conflict Rule

When labels disagree, keep the original local label and add the Trust Model stage beside it. Example: a fixture can have **Strong** showcase status, `PASS` closure, and **Characterized** evidence strength without claiming **Canonical** status or physical ground truth.

The Trust Model is the reader-facing crosswalk. The source maturity assignments remain available in `reports/observatory_maturity_ladder.json`.
