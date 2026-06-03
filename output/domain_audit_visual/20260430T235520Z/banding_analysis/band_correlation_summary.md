# Band Correlation Analysis

**Audit dir:** `output/domain_audit_visual/20260430T235520Z`  
**Image size:** 320×180  
**Band detector:** sensitivity=1.5, min_score=0.05

## Beauty Hash Comparison

| Pair | Result |
|------|--------|
| OFF vs telemetry ON | `different` |
| OFF vs resolver ON | `different` |
| telemetry ON vs resolver ON | `different` |

## Band Pixel Counts

| Label | Band pixels | Fraction |
|-------|-------------|----------|
| off | 9162 | 15.9% |
| telemetry_on | 7618 | 13.2% |
| resolver_on | 15268 | 26.5% |

OFF vs telemetry changed pixels: **3968**  
Resolver vs telemetry changed pixels: **6848**

## Overlap Metrics (OFF band vs telemetry maps)

| Map | Band px | High-map px | Intersection | Precision | Recall | IoU | Mean@band |
|-----|---------|-------------|--------------|-----------|--------|-----|-----------|
| off_band_vs_tel_boundary | 9162 | 24336 | 1061 | 11.6% | 4.4% | 3.3% | 0.0808 |
| off_band_vs_tel_normal_discontinuity | 9162 | 0 | 0 | 0.0% | 0.0% | 0.0% | 0.0000 |
| off_band_vs_tel_selection_flip | 9162 | 24336 | 1061 | 11.6% | 4.4% | 3.3% | 0.1158 |
| off_band_vs_tel_domain_instability | 9162 | 57600 | 9162 | 100.0% | 15.9% | 15.9% | 0.5979 |
| resolver_changed_vs_off_band | 6848 | 9154 | 1666 | 24.3% | 18.2% | 11.6% | 0.2238 |
| resolver_changed_vs_res_boundary | 6848 | 20672 | 384 | 5.6% | 1.9% | 1.4% | 0.0391 |

## Telemetry Maps Present

- `boundary_confidence`: yes
- `normal_discontinuity`: yes
- `selection_flip`: yes
- `domain_confidence`: yes
- `step_convergence_confidence`: **no** (step-convergence maps not yet generated)
- `step_sensitivity`: **no** (step-convergence maps not yet generated)
- `precision_required`: **no** (step-convergence maps not yet generated)

## Interpretation

- **Precision** (band→map): fraction of band pixels that are also high-instability in the telemetry map.
- **Recall** (map→band): fraction of high-instability map pixels that are also banding pixels.
- **IoU**: Jaccard index of band mask and high-instability region.
- High precision + recall → banding co-localises with domain instability.
- `off_vs_telemetry_on=different` is a pre-existing issue (telemetry probes alter physics query ordering).
- Step-convergence maps absent: run with `--step-convergence` to generate them.
