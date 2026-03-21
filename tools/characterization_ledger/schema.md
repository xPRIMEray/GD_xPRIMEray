# Characterization Ledger Schema

Ledger file: `output/characterization_ledger/fixture_runs.csv`

One row is appended per fixture run. Empty values are allowed when a field is
missing or cannot be derived safely.

## Columns

| Column | Meaning |
| --- | --- |
| `timestamp` | Run timestamp, typically derived from the run directory name |
| `fixture_id` | Stable fixture identifier such as `fixture_001` |
| `commit_hash` | Current git commit hash from `git rev-parse HEAD` |
| `transport_model` | Effective transport model observed in renderer logs |
| `requested_stepLength` | Requested step length passed by the harness, if any |
| `requested_min_stepLength` | Requested minimum step length passed by the harness, if any |
| `steps_per_ray` | Requested steps-per-ray when provided, otherwise effective renderer value |
| `effective_stepLength` | Effective step length observed in renderer logs |
| `effective_min_stepLength` | Effective minimum step length observed in renderer logs |
| `status` | Run outcome such as `ok` or `failed` |
| `capture_succeeded` | Whether the capture artifact was produced successfully |
| `launch_audit_status` | Launch audit status from the run log |
| `guard_progress` | Count of `guard_progress` scheduler exits observed in `run.log` |
| `forcedAdvance` | Count of `forcedAdvance=1` events observed in `run.log` |
| `processed_rows` | Processed row count reported by capture logs |
| `traced_pixels` | Traced pixel count reported by capture logs |
| `runtime` | Total run duration in seconds |
| `source_hits` | Source hit count |
| `miss_hits` | Miss hit count |
| `backgroundHits` | Background hit count |
| `useful_hit_ratio` | `source_hits / traced_pixels`, blank when divide-by-zero would occur |
| `ssim_vs_baseline` | SSIM against the configured baseline capture, blank on failure |
| `mad_vs_baseline` | Mean absolute difference against the configured baseline capture, blank on failure |
| `image_width` | Capture width in pixels |
| `image_height` | Capture height in pixels |
| `visual_tag` | Optional manual classification placeholder |
| `decision_tag` | Optional manual classification placeholder |

## Notes

- The writer creates the CSV and header automatically when missing.
- Existing headers are preserved; new runs append data rows only.
- Image comparison failures do not fail the run. The SSIM and MAD fields remain blank instead.
