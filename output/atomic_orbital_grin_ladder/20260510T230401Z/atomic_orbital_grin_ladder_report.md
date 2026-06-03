# Atomic Orbital GRIN V1 Ladder Report

| cell | verdict | closure_rate | miss_pixels | budget_exhausted_pixels | mean_steps | max_steps | reason |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- |
| `A0_straight_baseline` | FAIL | 0.160000 | 12096 | 0 | 58.884 | 500 | strict_v1_gate_failed |
| `A1_no_cloud_reference` | FAIL | 0.157778 | 12128 | 0 | 58.016 | 500 | strict_v1_gate_failed |
| `A2_static_hydrogen` | FAIL | 0.142222 | 12352 | 0 | 51.096 | 500 | strict_v1_gate_failed |
| `A3_clocked_hydrogen_tick0` | REPORT | 0.140000 | 12384 | 0 | 50.436 | 500 | clocked_requires_classification |
| `A3_clocked_hydrogen_tick1` | REPORT | 0.144444 | 12320 | 0 | 51.842 | 500 | clocked_requires_classification |

V1 strict gates apply to A0-A2. A3 may report classified differences.
