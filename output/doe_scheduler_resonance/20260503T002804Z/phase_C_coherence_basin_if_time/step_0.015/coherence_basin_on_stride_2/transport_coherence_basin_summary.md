# Transport Coherence Basin Summary

SceneTransportMemory is diagnostic-only and must not feed render scheduling, hit selection, shading, resolver decisions, or adaptive precision yet.

## Probe Budget

| metric | value |
|---|---:|
| `probe_sample_count` | 1156 |
| `probe_runtime_ms` | 6782 |
| `max_centers_used` | 8 |
| `centers_skipped_due_to_budget` | 33 |
| `rows_written` | 1156 |

## Basin Counts

- Basins: 8
- Unstable seams: 0
- Mean local coherence: 0.999999
- Mean transport entropy: 0

## Seam Classes

| class | count |
|---|---:|

## Guardrail

Analysis only. No beauty, hit, shading, resolver, scheduler, or precision-step feedback is permitted.
